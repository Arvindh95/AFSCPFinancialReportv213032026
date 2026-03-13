//using FinancialReport.Helper;
using FinancialReport.Helper;
using Newtonsoft.Json.Linq;
using PX.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FinancialReport.Services
{
    public class FinancialDataService
    {
        private readonly string _baseUrl;
        private readonly string _tenantName;
        private readonly AuthService _authService;
        private readonly GIColumnMapping _columnMapping;

        // Static HttpClient shared across all instances and methods
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            MaxConnectionsPerServer = 10
        })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        static FinancialDataService()
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public FinancialDataService(AuthService authService, string tenantName, GIColumnMapping columnMapping = null)
        {
            AcumaticaCredentials credentials = CredentialProvider.GetCredentials(tenantName);
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _tenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
            _baseUrl = credentials.BaseURL ?? throw new ArgumentNullException(nameof(credentials.BaseURL));
            _columnMapping = columnMapping ?? new GIColumnMapping();
        }

        // --------------------------------------------------------
        // 1) FetchAllApiData (with URL Fallback Logic)
        // --------------------------------------------------------

        // Synchronous version for backward compatibility
        public FinancialApiData FetchAllApiData(string branch, string organization, string ledger, string period, bool includeDetail = true)
        {
            return FetchAllApiDataAsync(branch, organization, ledger, period, includeDetail).Result;
        }

        // Async version
        public async Task<FinancialApiData> FetchAllApiDataAsync(string branch, string organization, string ledger, string period, bool includeDetail = true)
        {
            string accessToken = await _authService.AuthenticateAndGetTokenAsync();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string filter = $"FinancialPeriod eq '{period}' and {dimensionFilter}";

            var accountData = new Dictionary<string, FinancialPeriodData>();
            var detailRows  = new List<FinancialPeriodData>();

            // Token is passed per-request via HttpRequestMessage — no shared header mutation needed.
            List<JToken> results = await ExecuteFetchWithFallbackAsync(_httpClient, filter, ledger, accessToken);

            if (results == null)
            {
                throw new PXException(Messages.FailedToFetchOData);
            }

            // Diagnostic: log the JSON property names from the first OData row
            if (results.Count > 0 && results[0] is JObject firstObj)
            {
                var propNames = firstObj.Properties().Select(p => p.Name).ToList();
                PXTrace.WriteInformation($"OData columns ({propNames.Count}): {string.Join(", ", propNames)}");
            }

            foreach (var item in results)
            {
                string accountId = item[_columnMapping.AccountColumn]?.ToString();
                if (string.IsNullOrEmpty(accountId)) continue;

                string subaccount  = item[_columnMapping.SubaccountColumn]?.ToString()?.Trim()   ?? string.Empty;
                string branchId    = item[_columnMapping.BranchColumn]?.ToString()?.Trim()      ?? string.Empty;
                string orgId       = item[_columnMapping.OrganizationColumn]?.ToString()?.Trim() ?? string.Empty;
                string ledgerId    = item[_columnMapping.LedgerColumn]?.ToString()?.Trim()      ?? string.Empty;
                string accountType = item[_columnMapping.TypeColumn]?.ToString() ?? string.Empty;
                decimal begBal  = item[_columnMapping.BeginningBalCol]?.ToObject<decimal>() ?? 0;
                decimal endBal  = item[_columnMapping.EndingBalCol]?.ToObject<decimal>()    ?? 0;
                decimal debit   = item[_columnMapping.DebitColumn]?.ToObject<decimal>()     ?? 0;
                decimal credit  = item[_columnMapping.CreditColumn]?.ToObject<decimal>()    ?? 0;

                // Aggregated AccountData (existing behaviour — unchanged)
                if (!accountData.TryGetValue(accountId!, out var acctEntry))
                {
                    acctEntry = new FinancialPeriodData
                    {
                        Account     = accountId,
                        AccountType = accountType
                    };
                    accountData[accountId!] = acctEntry;
                }
                acctEntry.BeginningBalance += begBal;
                acctEntry.EndingBalance    += endBal;
                acctEntry.Debit            += debit;
                acctEntry.Credit           += credit;

                // Per-row detail — used only when a line item has dimension filters (SubaccountFilter, BranchFilter, etc.)
                // Skipped when includeDetail=false to avoid allocating large lists when not needed.
                if (includeDetail)
                {
                    detailRows.Add(new FinancialPeriodData
                    {
                        Account        = accountId,
                        Subaccount     = subaccount,
                        AccountType    = accountType,
                        BranchID       = branchId,
                        OrganizationID = orgId,
                        Ledger         = ledgerId,
                        BeginningBalance = begBal,
                        EndingBalance    = endBal,
                        Debit            = debit,
                        Credit           = credit
                    });
                }
            }

            // Diagnostic: log DetailRows summary for troubleshooting dimension filters
            PXTrace.WriteInformation($"FetchAllApiData: {accountData.Count} aggregated accounts, {detailRows.Count} detail rows.");
            if (detailRows.Count > 0)
            {
                var first = detailRows[0];
                PXTrace.WriteInformation($"  First detail row: Account={first.Account}, Sub={first.Subaccount}, Branch={first.BranchID}, Org={first.OrganizationID}, Ledger={first.Ledger}, EndBal={first.EndingBalance}");

                // Log distinct subaccount values for the first account
                var sampleAccount = detailRows[0].Account;
                var subs = detailRows.Where(r => r.Account == sampleAccount).Select(r => r.Subaccount).Distinct().ToList();
                PXTrace.WriteInformation($"  Subaccounts for {sampleAccount}: [{string.Join(", ", subs)}]");
            }

            return new FinancialApiData { AccountData = accountData, DetailRows = detailRows };
        }

        // --------------------------------------------------------
        // 2) FetchJanuaryBeginningBalance
        // --------------------------------------------------------
        public FinancialApiData FetchJanuaryBeginningBalance(string branch, string organization, string ledger, string prevYear)
        {
            string januaryPeriod = "01" + prevYear;
            string accessToken = _authService.AuthenticateAndGetToken();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string baseFilter = $"FinancialPeriod eq '{januaryPeriod}' and {dimensionFilter}";

            var apiData = new FinancialApiData();

            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger, accessToken);

            if (results == null)
            {
                throw new PXException(Messages.FailedToFetchOData);
            }

            foreach (var item in results)
            {
                string accountId = item[_columnMapping.AccountColumn]?.ToString();
                decimal beginningBalance = item[_columnMapping.BeginningBalCol]?.ToObject<decimal>() ?? 0;

                if (string.IsNullOrEmpty(accountId)) continue;

                if (!apiData.AccountData.TryGetValue(accountId!, out var janEntry))
                {
                    janEntry = new FinancialPeriodData();
                    apiData.AccountData[accountId!] = janEntry;
                }
                janEntry.BeginningBalance += beginningBalance;
                janEntry.EndingBalance += beginningBalance;
            }

            return apiData;
        }

        // --------------------------------------------------------
        // 3) FetchRangeApiData
        // --------------------------------------------------------
        public FinancialApiData FetchRangeApiData(string branch, string organization, string ledger, string fromPeriod, string toPeriod)
        {
            string accessToken = _authService.AuthenticateAndGetToken();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string baseFilter = $"FinancialPeriod ge '{fromPeriod}' and FinancialPeriod le '{toPeriod}' and {dimensionFilter}";

            var cumulativeDict = new Dictionary<string, FinancialPeriodData>();

            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger, accessToken);

            if (results == null)
            {
                throw new PXException(Messages.FailedToFetchOData);
            }

            foreach (var item in results)
            {
                string accountId = item[_columnMapping.AccountColumn]?.ToString();
                decimal debit = item[_columnMapping.DebitColumn]?.ToObject<decimal>() ?? 0;
                decimal credit = item[_columnMapping.CreditColumn]?.ToObject<decimal>() ?? 0;
                decimal endingBalance = item[_columnMapping.EndingBalCol]?.ToObject<decimal>() ?? 0;

                if (string.IsNullOrEmpty(accountId)) continue;

                if (!cumulativeDict.TryGetValue(accountId!, out var cumEntry))
                {
                    cumEntry = new FinancialPeriodData();
                    cumulativeDict[accountId!] = cumEntry;
                }

                cumEntry.Debit += debit;
                cumEntry.Credit += credit;
                cumEntry.EndingBalance += endingBalance;
            }

            var apiData = new FinancialApiData();
            foreach (var kvp in cumulativeDict)
            {
                apiData.AccountData[kvp.Key] = kvp.Value;
            }

            return apiData;
        }

        // --------------------------------------------------------
        // 4) FetchCompositeKeyData
        // --------------------------------------------------------
        public FinancialApiData FetchCompositeKeyData(string branch, string organization, string ledger, string period)
        {
            string accessToken = _authService.AuthenticateAndGetToken();
            string baseFilter = $"FinancialPeriod eq '{period}' and 1 eq 1";

            var compositeData = new Dictionary<string, FinancialPeriodData>();

            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger, accessToken);

            if (results == null)
            {
                throw new PXException(Messages.FailedToFetchOData);
            }

            foreach (var item in results)
            {
                string accountId = item[_columnMapping.AccountColumn]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(accountId)) continue;

                string subaccountId = item[_columnMapping.SubaccountColumn]?.ToString()?.Trim() ?? "N/A";
                string branchId = item[_columnMapping.BranchColumn]?.ToString()?.Trim() ?? branch;
                string orgId = item[_columnMapping.OrganizationColumn]?.ToString()?.Trim() ?? organization;
                string compositeKey = $"{accountId}-{subaccountId}-{branchId}-{orgId}-{period}-{ledger}";

                var data = new FinancialPeriodData
                {
                    Account = accountId,
                    Subaccount = subaccountId,
                    BeginningBalance = item[_columnMapping.BeginningBalCol]?.ToObject<decimal>() ?? 0,
                    EndingBalance = item[_columnMapping.EndingBalCol]?.ToObject<decimal>() ?? 0,
                    Debit = item[_columnMapping.DebitColumn]?.ToObject<decimal>() ?? 0,
                    Credit = item[_columnMapping.CreditColumn]?.ToObject<decimal>() ?? 0,
                };

                compositeData[compositeKey] = data;
            }

            var apiData = new FinancialApiData();
            foreach (var kvp in compositeData)
            {
                apiData.CompositeKeyData[kvp.Key] = kvp.Value;
            }

            return apiData;
        }

        // --------------------------------------------------------
        // 5) FetchEndingBalance
        // --------------------------------------------------------
        public decimal FetchEndingBalance(string period, string branch, string organization, string ledger, string account, string subaccount)
        {
            if (string.IsNullOrEmpty(period) || string.IsNullOrEmpty(branch) ||
                string.IsNullOrEmpty(organization) ||
                string.IsNullOrEmpty(account) || string.IsNullOrEmpty(subaccount))
            {
                PXTrace.WriteWarning("FetchEndingBalance called with missing filter parameters.");
                return 0m;
            }

            string accessToken = _authService.AuthenticateAndGetToken();
            string baseFilter = $"FinancialPeriod eq '{period}' and BranchID eq '{branch}' and OrganizationID eq '{organization}' and " +
                               $"Account eq '{account}' and Subaccount eq '{subaccount}'";

            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger, accessToken);

            if (results == null || results.Count == 0)
            {
                PXTrace.WriteWarning($"No rows found for precise match: {baseFilter}");
                return 0m;
            }

            return results.FirstOrDefault()?[_columnMapping.EndingBalCol]?.ToObject<decimal>() ?? 0m;
        }


        /// <summary>
        /// Executes a fetch operation with fallback logic for both URL format and Ledger filtering (synchronous).
        /// </summary>
        private List<JToken> ExecuteFetchWithFallback(HttpClient client, string baseFilter, string ledger, string accessToken)
        {
            return ExecuteFetchWithFallbackAsync(client, baseFilter, ledger, accessToken).Result;
        }

        /// <summary>
        /// Executes a fetch operation with fallback logic for both URL format and Ledger filtering (async).
        /// The access token is set per-request via HttpRequestMessage so parallel calls never race on shared headers.
        /// </summary>
        private async Task<List<JToken>> ExecuteFetchWithFallbackAsync(HttpClient client, string baseFilter, string ledger, string accessToken)
        {
            string giName = _columnMapping.GIName;
            string modernUrlBase = $"{_baseUrl}/odata/{_tenantName}/{giName}";
            string legacyUrlBase = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/{giName}";
            string selectColumns = _columnMapping.BuildSelectColumns();

            // Attempt 1: Modern URL with Ledger (normal path — no trace on success)
            string filterWithLedger = AppendLedgerFilter(baseFilter, ledger);
            var results = await PaginatedFetchAsync(client, modernUrlBase, filterWithLedger, selectColumns, accessToken);
            if (results != null) return results;

            // Attempt 2: Modern URL without Ledger (Attempt 1 with ledger filter failed)
            PXTrace.WriteWarning($"Fallback 2: Modern URL without Ledger. URL: {modernUrlBase}, Filter: {baseFilter}");
            results = await PaginatedFetchAsync(client, modernUrlBase, baseFilter, selectColumns, accessToken);
            if (results != null) return results;

            // Attempt 3: Legacy URL with Ledger
            PXTrace.WriteWarning($"Attempt 2 failed. Retrying with Legacy URL with Ledger. URL: {legacyUrlBase}, Filter: {filterWithLedger}");
            results = await PaginatedFetchAsync(client, legacyUrlBase, filterWithLedger, selectColumns, accessToken);
            if (results != null) return results;

            // Attempt 4: Legacy URL without Ledger
            PXTrace.WriteWarning($"Attempt 3 failed. Retrying with Legacy URL without Ledger. URL: {legacyUrlBase}, Filter: {baseFilter}");
            results = await PaginatedFetchAsync(client, legacyUrlBase, baseFilter, selectColumns, accessToken);
            if (results != null) return results;

            PXTrace.WriteError("All fetch attempts failed.");
            return null;
        }

        /// <summary>
        /// Private helper method to execute a paginated OData fetch operation against a specific URL (synchronous).
        /// </summary>
        private List<JToken> PaginatedFetch(HttpClient client, string baseUrl, string filter, string selectColumns, string accessToken)
        {
            return PaginatedFetchAsync(client, baseUrl, filter, selectColumns, accessToken).Result;
        }

        /// <summary>
        /// Private helper method to execute a paginated OData fetch operation against a specific URL (async).
        /// Uses per-request HttpRequestMessage so the Bearer token is set on each individual request —
        /// safe for parallel calls without any serialization or shared-state mutation.
        /// </summary>
        private async Task<List<JToken>> PaginatedFetchAsync(HttpClient client, string baseUrl, string filter, string selectColumns, string accessToken)
        {
            var allResults = new List<JToken>();
            int pageSize = 5000;
            int skip = 0;

            while (true)
            {
                // URL encode the filter and select parameters to handle special characters properly
                string encodedFilter = Uri.EscapeDataString(filter);
                string encodedSelect = Uri.EscapeDataString(selectColumns);
                string pagedUrl = $"{baseUrl}?$filter={encodedFilter}&$select={encodedSelect}&$top={pageSize}&$skip={skip}";

                HttpResponseMessage response;
                try
                {
                    // Set Authorization per-request — avoids mutating shared DefaultRequestHeaders
                    // which would require serialization when called from multiple parallel tasks.
                    using (var request = new HttpRequestMessage(HttpMethod.Get, pagedUrl))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        response = await client.SendAsync(request);
                    }
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"HTTP request to {pagedUrl} failed: {ex.Message}");
                    return null; // Return null to indicate failure
                }

                if (!response.IsSuccessStatusCode)
                {
                    // This is an expected failure for a fallback, so log as warning, not error.
                    PXTrace.WriteWarning($"Request to {pagedUrl} returned status {response.StatusCode}.");
                    return null; // Return null to indicate failure, triggering the next fallback.
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(jsonResponse) || !jsonResponse.TrimStart().StartsWith("{"))
                {
                    PXTrace.WriteError("API returned a non-JSON or empty response.");
                    return null;
                }

                JObject parsed = JObject.Parse(jsonResponse);
                var pageResults = (parsed["value"] as JArray)?.ToObject<List<JToken>>();

                if (pageResults == null || pageResults.Count == 0)
                {
                    break; // Exit loop when no more pages are returned
                }

                allResults.AddRange(pageResults);
                skip += pageSize;
            }

            PXTrace.WriteInformation($"Successfully fetched {allResults.Count} total records for base URL: {baseUrl}");
            return allResults;
        }

        
        // --------------------------------------------------------
        // HELPER: BuildDimensionFilter
        // --------------------------------------------------------
        private string BuildDimensionFilter(string branch, string organization)
        {
            // Handle the scenario where both or either is selected
            if (!string.IsNullOrEmpty(branch) && !string.IsNullOrEmpty(organization))
            {
                // Return a filter that requires BOTH match
                return $"BranchID eq '{branch}' and OrganizationID eq '{organization}'";
            }
            else if (!string.IsNullOrEmpty(branch))
            {
                return $"BranchID eq '{branch}'";
            }
            else if (!string.IsNullOrEmpty(organization))
            {
                return $"OrganizationID eq '{organization}'";
            }
            else
            {
                // Nothing selected => user must pick one
                return $"1 eq 1";
            }
        }

        private string AppendLedgerFilter(string baseFilter, string ledger)
        {
            return !string.IsNullOrEmpty(ledger)
                ? $"{baseFilter} and LedgerID eq '{ledger}'"
                : baseFilter;
        }

        /// <summary>
        /// Fetches column names from a GI by retrieving a single row and inspecting JSON properties.
        /// Used by the "Detect Columns" action on the Report Definition screen.
        /// </summary>
        public List<string> FetchGIColumns(string giName)
        {
            string accessToken = _authService.AuthenticateAndGetToken();

            string modernUrl = $"{_baseUrl}/odata/{_tenantName}/{giName}?$top=1";
            string legacyUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/{giName}?$top=1";

            var columns = TryFetchColumnsFromUrl(modernUrl, accessToken);
            if (columns != null && columns.Count > 0) return columns;

            columns = TryFetchColumnsFromUrl(legacyUrl, accessToken);
            if (columns != null && columns.Count > 0) return columns;

            return new List<string>();
        }

        private List<string> TryFetchColumnsFromUrl(string url, string accessToken)
        {
            try
            {
                HttpResponseMessage response;
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    response = _httpClient.SendAsync(request).Result;
                }
                if (!response.IsSuccessStatusCode) return null;

                string json = response.Content.ReadAsStringAsync().Result;
                if (string.IsNullOrWhiteSpace(json)) return null;

                JObject parsed = JObject.Parse(json);
                var values = parsed["value"] as JArray;
                if (values == null || values.Count == 0) return null;

                var firstRecord = values[0] as JObject;
                if (firstRecord == null) return null;

                return firstRecord.Properties()
                    .Select(p => p.Name)
                    .Where(n => !n.StartsWith("@") && !n.Contains("odata"))
                    .ToList();
            }
            catch (Exception ex)
            {
                PXTrace.WriteWarning($"Failed to fetch GI columns from {url}: {ex.Message}");
                return null;
            }
        }

        public Dictionary<string, string> BuildPlaceholderMapFromKeys(List<string> placeholderKeys, Dictionary<string, FinancialPeriodData> cyData, Dictionary<string, FinancialPeriodData> pyData)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in placeholderKeys)
            {
                if (string.IsNullOrWhiteSpace(key) || !key.Contains("_"))
                {
                    continue;
                }

                var parts = key.Split('_');
                if (parts.Length != 2)
                {
                    continue;
                }

                string accountCode = parts[0];
                string period = parts[1].ToUpper();

                var source = period == "CY" ? cyData : period == "PY" ? pyData : null;

                if (source != null && source.TryGetValue(accountCode, out var data))
                {
                    string value = data.EndingBalance.ToString("N2");
                    result[key] = value;
                }
                else
                {
                    result[key] = "0";
                }
            }
            return result;
        }

        public Dictionary<string, string> BuildSmartPlaceholderMapFromKeys(
        List<string> keys,
        FinancialApiData cyData,
        FinancialApiData pyData,
        FinancialApiData janCY,
        FinancialApiData janPY,
        FinancialApiData rangeCY,
        FinancialApiData rangePY)
        {
            var dict = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Parallel.ForEach(keys, key =>
            {
                try
                {
                    string cleanKey = key.Trim('{', '}');

                    // Match: CreditSum3_B1234_CY
                    var sumMatch = Regex.Match(cleanKey, @"^(CreditSum|DebitSum|BegSum|Sum)(\d)_(.+)_(CY|PY)$", RegexOptions.IgnoreCase);
                    if (sumMatch.Success)
                    {
                        var type = sumMatch.Groups[1].Value.ToLower();
                        int level = int.Parse(sumMatch.Groups[2].Value);
                        string prefix = sumMatch.Groups[3].Value;
                        string yearType = sumMatch.Groups[4].Value.ToUpper();

                        var source = yearType == "CY"
                            ? (type == "begsum" ? janCY : (type == "debitsum" || type == "creditsum" ? rangeCY : cyData))
                            : (type == "begsum" ? janPY : (type == "debitsum" || type == "creditsum" ? rangePY : pyData));

                        decimal sum = 0;
                        foreach (var kvp in source.AccountData)
                        {
                            if (kvp.Key.Length >= level && kvp.Key.Substring(0, level) == prefix)
                            {
                                var data = kvp.Value;
                                switch (type)
                                {
                                    case "debitsum":
                                        sum += data.Debit;
                                        break;
                                    case "creditsum":
                                        sum += data.Credit;
                                        break;
                                    case "begsum":
                                        sum += data.BeginningBalance;
                                        break;
                                    default:
                                        sum += data.EndingBalance;
                                        break;
                                }
                            }
                        }

                        dict[key] = sum.ToString("#,##0");
                        return;
                    }

                    // Match: A81101_Jan1_PY
                    var janMatch = Regex.Match(cleanKey, @"^(.+)_Jan1_(CY|PY)$", RegexOptions.IgnoreCase);
                    if (janMatch.Success)
                    {
                        var acct = janMatch.Groups[1].Value;
                        var yearType = janMatch.Groups[2].Value.ToUpper();
                        var source = yearType == "CY" ? janCY : janPY;

                        if (source.AccountData.TryGetValue(acct, out var data))
                        {
                            dict[key] = data.BeginningBalance.ToString("#,##0");
                        }

                        return;
                    }

                    // Match: B1234_credit_CY
                    var partMatch = Regex.Match(cleanKey, @"^(.+?)_(credit|debit|ending)_(CY|PY)$", RegexOptions.IgnoreCase);
                    if (partMatch.Success)
                    {
                        var acct = partMatch.Groups[1].Value;
                        var part = partMatch.Groups[2].Value.ToLower();
                        var yearType = partMatch.Groups[3].Value.ToUpper();
                        var source = yearType == "CY" ? rangeCY : rangePY;

                        if (source.AccountData.TryGetValue(acct, out var data))
                        {
                            switch (part)
                            {
                                case "credit":
                                    dict[key] = data.Credit.ToString("#,##0");
                                    break;
                                case "debit":
                                    dict[key] = data.Debit.ToString("#,##0");
                                    break;
                                default:
                                    dict[key] = data.EndingBalance.ToString("#,##0");
                                    break;
                            }
                        }

                        return;
                    }

                    // Match: A123456_CY / A123456_PY
                    var basicMatch = Regex.Match(cleanKey, @"^(.+)_(CY|PY)$", RegexOptions.IgnoreCase);
                    if (basicMatch.Success)
                    {
                        var acct = basicMatch.Groups[1].Value;
                        var yearType = basicMatch.Groups[2].Value.ToUpper();
                        var source = yearType == "CY" ? cyData : pyData;

                        if (source.AccountData.TryGetValue(acct, out var data))
                        {
                            dict[key] = data.EndingBalance.ToString("#,##0");
                        }

                        return;
                    }

                    // Fallback
                    dict[key] = "0";
                }
                catch (Exception ex)
                {
                    PXTrace.WriteWarning($"[Parallel Placeholder] Failed to process key '{key}': {ex.Message}");
                    dict[key] = "0";
                }
            });

            return new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
        }


        public Dictionary<string, string> BuildAllFinancialDataMap(
        FinancialApiData currYearData,
        FinancialApiData prevYearData,
        FinancialApiData januaryBeginningDataCY,
        FinancialApiData januaryBeginningDataPY,
        FinancialApiData cumulativeCYData,
        FinancialApiData cumulativePYData)
        {
            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void AddAccountData(FinancialApiData data, string suffix)
            {
                foreach (var kvp in data.AccountData)
                {
                    string key = kvp.Key;
                    var val = kvp.Value;
                    placeholders[$"{key}_Ending_{suffix}"] = val.EndingBalance.ToString("#,##0.##");
                    placeholders[$"{key}_Debit_{suffix}"] = val.Debit.ToString("#,##0.##");
                    placeholders[$"{key}_Credit_{suffix}"] = val.Credit.ToString("#,##0.##");
                    placeholders[$"{key}_Beg_{suffix}"] = val.BeginningBalance.ToString("#,##0.##");
                }
            }

            AddAccountData(currYearData, "CY");
            AddAccountData(prevYearData, "PY");
            AddAccountData(januaryBeginningDataCY, "JanBegCY");
            AddAccountData(januaryBeginningDataPY, "JanBegPY");
            AddAccountData(cumulativeCYData, "RangeCY");
            AddAccountData(cumulativePYData, "RangePY");

            return placeholders;
        }

        #region Range Fetch Methods
        /// <summary>
        /// Checks if a placeholder follows the account range pattern
        /// </summary>
        public bool HasAccountRangePattern(string placeholder)
        {
            string cleanKey = placeholder.Trim('{', '}');
            // Pattern: A74101:A75101_e_CY or 100-10:200-20_e_CY
            return Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+:[A-Z0-9\-]+_(e|b|c|d)_(CY|PY)$", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Processes account range placeholders and returns their calculated values
        /// </summary>
        public Dictionary<string, string> ProcessAccountRangePlaceholders(
            List<string> rangePlaceholders,
            FinancialApiData currYearData,
            FinancialApiData prevYearData,
            FinancialApiData januaryBeginningDataCY,
            FinancialApiData januaryBeginningDataPY,
            FinancialApiData cumulativeCYData,
            FinancialApiData cumulativePYData)
        {
            var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            PXTrace.WriteInformation($"Processing {rangePlaceholders.Count} account range placeholders...");

            foreach (var placeholder in rangePlaceholders)
            {
                try
                {
                    string cleanKey = placeholder.Trim('{', '}');

                    // Parse the range placeholder: A74101:A75101_e_CY or 100-10:200-20_e_CY
                    var match = Regex.Match(cleanKey, @"^([A-Z0-9\-]+):([A-Z0-9\-]+)_(e|b|c|d)_(CY|PY)$", RegexOptions.IgnoreCase);

                    if (!match.Success)
                    {
                        PXTrace.WriteWarning($"Invalid account range format: {placeholder}");
                        results[placeholder] = "0";
                        continue;
                    }

                    string startAccount = match.Groups[1].Value;
                    string endAccount = match.Groups[2].Value;
                    string balanceTypeCode = match.Groups[3].Value.ToLower();
                    string yearType = match.Groups[4].Value.ToUpper();

                    // Convert single letter to full balance type
                    string balanceType = ConvertBalanceTypeCode(balanceTypeCode);

                    // Get the appropriate data source
                    FinancialApiData dataSource = GetDataSourceForRangeCalculation(balanceType, yearType,
                        currYearData, prevYearData, januaryBeginningDataCY, januaryBeginningDataPY,
                        cumulativeCYData, cumulativePYData);

                    // Calculate the sum for accounts in range
                    decimal total = CalculateAccountRangeSum(startAccount, endAccount, balanceType, dataSource);

                    results[placeholder] = total.ToString("#,##0");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Failed to process range placeholder '{placeholder}': {ex.Message}");
                    results[placeholder] = "0";
                }
            }

            return results;
        }

        /// <summary>
        /// Converts single letter balance type codes to full names
        /// </summary>
        private string ConvertBalanceTypeCode(string code)
        {
            switch (code.ToLower())
            {
                case "e":
                    return "ending";
                case "b":
                    return "beginning";
                case "c":
                    return "credit";
                case "d":
                    return "debit";
                default:
                    PXTrace.WriteWarning($"Unknown balance type code: {code}, defaulting to 'ending'");
                    return "ending";
            }
        }

        /// <summary>
        /// Gets the appropriate data source based on balance type and year
        /// </summary>
        private FinancialApiData GetDataSourceForRangeCalculation(
            string balanceType,
            string yearType,
            FinancialApiData currYearData,
            FinancialApiData prevYearData,
            FinancialApiData januaryBeginningDataCY,
            FinancialApiData januaryBeginningDataPY,
            FinancialApiData cumulativeCYData,
            FinancialApiData cumulativePYData)
        {
            switch (balanceType.ToLower())
            {
                case "beginning":
                    return yearType == "CY" ? januaryBeginningDataCY : januaryBeginningDataPY;

                case "credit":
                case "debit":
                    return yearType == "CY" ? cumulativeCYData : cumulativePYData;

                case "ending":
                default:
                    return yearType == "CY" ? currYearData : prevYearData;
            }
        }

        /// <summary>
        /// Calculates the sum for all accounts within the specified range
        /// </summary>
        private decimal CalculateAccountRangeSum(string startAccount, string endAccount, string balanceType, FinancialApiData dataSource)
        {
            decimal total = 0;
            int processedCount = 0;

            foreach (var kvp in dataSource.AccountData)
            {
                string account = kvp.Key;

                // Check if account is within range (inclusive)
                if (IsAccountInRange(account, startAccount, endAccount))
                {
                    var data = kvp.Value;

                    switch (balanceType.ToLower())
                    {
                        case "ending":
                            total += data.EndingBalance;
                            break;
                        case "beginning":
                            total += data.BeginningBalance;
                            break;
                        case "credit":
                            total += data.Credit;
                            break;
                        case "debit":
                            total += data.Debit;
                            break;
                    }

                    processedCount++;
                }
            }

            return total;
        }

        /// <summary>
        /// Determines if an account is within the specified range (inclusive)
        /// Uses smart comparison that handles both alphabetic and numeric portions correctly
        /// </summary>
        private bool IsAccountInRange(string account, string startAccount, string endAccount)
        {
            // Use smart alphanumeric comparison instead of simple lexicographic comparison
            // This correctly handles cases like A100 < A1000 (numeric) vs A100 > A1000 (lexicographic)
            return CompareAccountCodes(account, startAccount) >= 0 &&
                   CompareAccountCodes(account, endAccount) <= 0;
        }

        /// <summary>
        /// Compares two account codes intelligently, treating numeric portions as numbers
        /// and handling segmented accounts properly.
        /// Examples:
        ///   A100 < A200 (correct)
        ///   A100 < A1000 (correct - treats 100 and 1000 as numbers)
        ///   A2 < A10 (correct - treats 2 and 10 as numbers)
        ///   ABC100DEF200 < ABC100DEF1000 (correct - handles multiple numeric portions)
        ///   100-09 < 100-10 (correct - handles segmented accounts)
        ///   100-10 < 100-20 (correct)
        ///   100-10 < 200-05 (correct - first segment takes precedence)
        /// </summary>
        private int CompareAccountCodes(string account1, string account2)
        {
            if (string.IsNullOrEmpty(account1) && string.IsNullOrEmpty(account2)) return 0;
            if (string.IsNullOrEmpty(account1)) return -1;
            if (string.IsNullOrEmpty(account2)) return 1;

            // Check if both accounts are segmented (contain hyphens)
            bool isSegmented1 = account1.Contains('-');
            bool isSegmented2 = account2.Contains('-');

            // If both are segmented, compare segment by segment
            if (isSegmented1 && isSegmented2)
            {
                var segments1 = account1.Split('-');
                var segments2 = account2.Split('-');

                // Compare each segment pair
                int minSegments = Math.Min(segments1.Length, segments2.Length);
                for (int s = 0; s < minSegments; s++)
                {
                    // Try to parse segments as numbers
                    bool isNum1 = int.TryParse(segments1[s], out int num1);
                    bool isNum2 = int.TryParse(segments2[s], out int num2);

                    if (isNum1 && isNum2)
                    {
                        // Both are numeric - compare as numbers
                        if (num1 != num2)
                            return num1.CompareTo(num2);
                    }
                    else
                    {
                        // At least one is not numeric - compare as strings
                        int cmp = string.Compare(segments1[s], segments2[s], StringComparison.OrdinalIgnoreCase);
                        if (cmp != 0)
                            return cmp;
                    }
                }

                // All compared segments are equal, the one with fewer segments comes first
                return segments1.Length.CompareTo(segments2.Length);
            }

            // Fall back to character-by-character comparison for non-segmented or mixed cases
            int i = 0, j = 0;

            while (i < account1.Length && j < account2.Length)
            {
                // Check if both current characters are digits
                if (char.IsDigit(account1[i]) && char.IsDigit(account2[j]))
                {
                    // Extract full numeric portions
                    long num1 = 0;
                    while (i < account1.Length && char.IsDigit(account1[i]))
                    {
                        num1 = num1 * 10 + (account1[i] - '0');
                        i++;
                    }

                    long num2 = 0;
                    while (j < account2.Length && char.IsDigit(account2[j]))
                    {
                        num2 = num2 * 10 + (account2[j] - '0');
                        j++;
                    }

                    // Compare numeric portions
                    if (num1 != num2)
                        return num1.CompareTo(num2);
                }
                else
                {
                    // Compare characters (case-insensitive)
                    int charComparison = char.ToUpperInvariant(account1[i])
                        .CompareTo(char.ToUpperInvariant(account2[j]));

                    if (charComparison != 0)
                        return charComparison;

                    i++;
                    j++;
                }
            }

            // If we've exhausted one string, the shorter one comes first
            return account1.Length.CompareTo(account2.Length);
        }

        /// <summary>
        /// Enhanced method to separate range placeholders from regular ones
        /// </summary>
        public (List<string> rangePlaceholders, List<string> regularPlaceholders) SeparateRangePlaceholders(List<string> allPlaceholders)
        {
            var rangePlaceholders = new List<string>();
            var regularPlaceholders = new List<string>();

            foreach (var placeholder in allPlaceholders)
            {
                if (HasAccountRangePattern(placeholder))
                {
                    rangePlaceholders.Add(placeholder);
                }
                else
                {
                    regularPlaceholders.Add(placeholder);
                }
            }

            PXTrace.WriteInformation($"Separated placeholders: {rangePlaceholders.Count} range, {regularPlaceholders.Count} regular");
            return (rangePlaceholders, regularPlaceholders);
        }
        #endregion

        #region Wildcard Placeholder Methods
        /// <summary>
        /// Checks if a placeholder follows the wildcard range pattern
        /// </summary>
        public bool HasWildcardRangePattern(string placeholder)
        {
            string cleanKey = placeholder.Trim('{', '}');
            // Pattern: A?????:B?????_e_CY or A3????:A4????_c_PY or 10?-??:20?-??_e_CY
            return Regex.IsMatch(cleanKey, @"^[A-Z0-9\-?]+:[A-Z0-9\-?]+_(e|b|c|d)_(CY|PY)$", RegexOptions.IgnoreCase) &&
                   (cleanKey.Contains('?')); // Must contain at least one wildcard
        }

        /// <summary>
        /// Processes wildcard range placeholders and returns their calculated values
        /// </summary>
        public Dictionary<string, string> ProcessWildcardRangePlaceholders(
            List<string> wildcardRangePlaceholders,
            FinancialApiData currYearData,
            FinancialApiData prevYearData,
            FinancialApiData januaryBeginningDataCY,
            FinancialApiData januaryBeginningDataPY,
            FinancialApiData cumulativeCYData,
            FinancialApiData cumulativePYData)
        {
            var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            PXTrace.WriteInformation($"Processing {wildcardRangePlaceholders.Count} wildcard range placeholders...");

            foreach (var placeholder in wildcardRangePlaceholders)
            {
                try
                {
                    string cleanKey = placeholder.Trim('{', '}');

                    // Parse the wildcard range placeholder: A3????:A4????_e_CY or 10?-??:20?-??_e_CY
                    var match = Regex.Match(cleanKey, @"^([A-Z0-9\-?]+):([A-Z0-9\-?]+)_(e|b|c|d)_(CY|PY)$", RegexOptions.IgnoreCase);

                    if (!match.Success)
                    {
                        PXTrace.WriteWarning($"Invalid wildcard range format: {placeholder}");
                        results[placeholder] = "0";
                        continue;
                    }

                    string startPattern = match.Groups[1].Value;
                    string endPattern = match.Groups[2].Value;
                    string balanceTypeCode = match.Groups[3].Value.ToLower();
                    string yearType = match.Groups[4].Value.ToUpper();

                    // Validate that patterns contain wildcards
                    if (!startPattern.Contains('?') && !endPattern.Contains('?'))
                    {
                        PXTrace.WriteWarning($"No wildcards found in pattern: {placeholder}");
                        results[placeholder] = "0";
                        continue;
                    }

                    // Convert single letter to full balance type
                    string balanceType = ConvertBalanceTypeCode(balanceTypeCode);

                    // Get the appropriate data source
                    FinancialApiData dataSource = GetDataSourceForRangeCalculation(balanceType, yearType,
                        currYearData, prevYearData, januaryBeginningDataCY, januaryBeginningDataPY,
                        cumulativeCYData, cumulativePYData);

                    // Calculate the sum for accounts in wildcard range
                    decimal total = CalculateWildcardRangeSum(startPattern, endPattern, balanceType, dataSource);

                    results[placeholder] = total.ToString("#,##0");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Failed to process wildcard range placeholder '{placeholder}': {ex.Message}");
                    results[placeholder] = "0";
                }
            }

            return results;
        }

        /// <summary>
        /// Expands a wildcard pattern to its min and max values
        /// </summary>
        private (string minValue, string maxValue) ExpandWildcardPattern(string pattern)
        {
            if (!pattern.Contains('?'))
            {
                // No wildcards, return the pattern as both min and max
                return (pattern, pattern);
            }

            // Replace ? with 0 for minimum value and 9 for maximum value
            string minValue = pattern.Replace('?', '0');
            string maxValue = pattern.Replace('?', '9');

            PXTrace.WriteInformation($"Expanded pattern '{pattern}' to range: {minValue} - {maxValue}");

            return (minValue, maxValue);
        }

        /// <summary>
        /// Calculates the sum for all accounts within the specified wildcard range
        /// </summary>
        private decimal CalculateWildcardRangeSum(string startPattern, string endPattern, string balanceType, FinancialApiData dataSource)
        {
            // Expand wildcard patterns to actual ranges
            var (startMin, startMax) = ExpandWildcardPattern(startPattern);
            var (endMin, endMax) = ExpandWildcardPattern(endPattern);

            // Determine the overall range bounds
            string overallMin = string.Compare(startMin, endMin, StringComparison.OrdinalIgnoreCase) <= 0 ? startMin : endMin;
            string overallMax = string.Compare(startMax, endMax, StringComparison.OrdinalIgnoreCase) >= 0 ? startMax : endMax;

            decimal total = 0;
            int processedCount = 0;

            foreach (var kvp in dataSource.AccountData)
            {
                string account = kvp.Key;

                // Check if account is within the overall wildcard range
                if (IsAccountInWildcardRange(account, startPattern, endPattern, overallMin, overallMax))
                {
                    var data = kvp.Value;

                    switch (balanceType.ToLower())
                    {
                        case "ending":
                            total += data.EndingBalance;
                            break;
                        case "beginning":
                            total += data.BeginningBalance;
                            break;
                        case "credit":
                            total += data.Credit;
                            break;
                        case "debit":
                            total += data.Debit;
                            break;
                    }

                    processedCount++;
                }
            }

            return total;
        }

        /// <summary>
        /// Determines if an account is within the specified wildcard range
        /// </summary>
        private bool IsAccountInWildcardRange(string account, string startPattern, string endPattern, string overallMin, string overallMax)
        {
            // First check: Is the account within the overall expanded range?
            bool inOverallRange = string.Compare(account, overallMin, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                 string.Compare(account, overallMax, StringComparison.OrdinalIgnoreCase) <= 0;

            if (!inOverallRange)
            {
                return false;
            }

            // Second check: Does the account match either the start or end pattern ranges?
            bool matchesStartPattern = DoesAccountMatchWildcardPattern(account, startPattern);
            bool matchesEndPattern = DoesAccountMatchWildcardPattern(account, endPattern);

            // Third check: Is the account between the pattern ranges?
            var (startMin, startMax) = ExpandWildcardPattern(startPattern);
            var (endMin, endMax) = ExpandWildcardPattern(endPattern);

            bool betweenPatterns = string.Compare(account, startMax, StringComparison.OrdinalIgnoreCase) > 0 &&
                                  string.Compare(account, endMin, StringComparison.OrdinalIgnoreCase) < 0;

            return matchesStartPattern || matchesEndPattern || betweenPatterns;
        }

        /// <summary>
        /// Checks if an account matches a specific wildcard pattern
        /// </summary>
        private bool DoesAccountMatchWildcardPattern(string account, string pattern)
        {
            if (account.Length != pattern.Length)
            {
                return false;
            }

            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] != '?' && pattern[i] != account[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Enhanced method to separate all placeholder types
        /// </summary>
        public (List<string> wildcardRangePlaceholders, List<string> exactRangePlaceholders, List<string> regularPlaceholders)
            SeparateAllPlaceholderTypes(List<string> allPlaceholders)
        {
            var wildcardRangePlaceholders = new List<string>();
            var exactRangePlaceholders = new List<string>();
            var regularPlaceholders = new List<string>();

            foreach (var placeholder in allPlaceholders)
            {
                if (HasWildcardRangePattern(placeholder))
                {
                    wildcardRangePlaceholders.Add(placeholder);
                }
                else if (HasAccountRangePattern(placeholder))
                {
                    exactRangePlaceholders.Add(placeholder);
                }
                else
                {
                    regularPlaceholders.Add(placeholder);
                }
            }

            PXTrace.WriteInformation($"Separated placeholders: {wildcardRangePlaceholders.Count} wildcard range, " +
                                   $"{exactRangePlaceholders.Count} exact range, {regularPlaceholders.Count} regular");

            return (wildcardRangePlaceholders, exactRangePlaceholders, regularPlaceholders);
        }
        #endregion

        // Optional: Static cleanup method
        public static void Cleanup()
        {
            try
            {
                _httpClient?.Dispose();
                PXTrace.WriteInformation("FinancialDataService: Static resources cleaned up");
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Error during cleanup: {ex.Message}");
            }
        }

        // Add this method to FinancialDataService class
        // Replace the existing FetchBalanceForPrefixPlaceholder method with this enhanced version
        private decimal FetchBalanceForPrefixPlaceholder(PrefixPlaceholderInfo info)
        {
            try
            {
                // Build OData filter
                var filterParts = new List<string>();

                // Always filter by period and account
                filterParts.Add($"FinancialPeriod eq '{info.Period}'");
                filterParts.Add($"Account eq '{info.Account}'");

                // Add dimensional filters only if not "1" (which means no filter)
                if (!string.IsNullOrEmpty(info.Branch) && info.Branch != "1")
                    filterParts.Add($"BranchID eq '{info.Branch}'");

                if (!string.IsNullOrEmpty(info.Organization) && info.Organization != "1")
                    filterParts.Add($"OrganizationID eq '{info.Organization}'");

                if (!string.IsNullOrEmpty(info.Ledger) && info.Ledger != "1")
                    filterParts.Add($"LedgerID eq '{info.Ledger}'");

                if (!string.IsNullOrEmpty(info.Subaccount) && info.Subaccount != "1")
                    filterParts.Add($"Subaccount eq '{info.Subaccount}'");

                string filter = string.Join(" and ", filterParts);

                // Select columns based on balance type
                string selectColumns = GetSelectColumnsForBalanceType(info.BalanceType);

                PXTrace.WriteInformation($"Executing prefix OData query (BalanceType: {info.BalanceType}): {filter}");

                // Get access token and execute query
                string accessToken = _authService.AuthenticateAndGetToken();

                // Use existing fallback logic
                var results = ExecutePrefixFetchWithFallback(filter, selectColumns, accessToken);

                // Sum all matching records based on balance type
                decimal totalBalance = 0m;
                foreach (var result in results)
                {
                    decimal value = GetBalanceValueFromResult(result, info.BalanceType);
                    totalBalance += value;
                }

                PXTrace.WriteInformation($"Prefix query returned {results.Count} records, total {info.BalanceType} balance: {totalBalance}");

                return totalBalance;
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Failed to fetch balance for prefix placeholder {info.OriginalPlaceholder}: {ex.Message}");
                return 0m;
            }
        }

        // Helper method to get appropriate select columns based on balance type
        private string GetSelectColumnsForBalanceType(string balanceType)
        {
            switch (balanceType.ToLower())
            {
                case "credit":
                    return "Credit";
                case "debit":
                    return "Debit";
                case "beginning":
                    return "BeginningBalance";
                case "ending":
                default:
                    return "EndingBalance";
            }
        }

        // Helper method to extract the correct balance value from the result
        private decimal GetBalanceValueFromResult(JToken result, string balanceType)
        {
            switch (balanceType.ToLower())
            {
                case "credit":
                    return result["Credit"]?.ToObject<decimal>() ?? 0m;
                case "debit":
                    return result["Debit"]?.ToObject<decimal>() ?? 0m;
                case "beginning":
                    return result["BeginningBalance"]?.ToObject<decimal>() ?? 0m;
                case "ending":
                default:
                    return result["EndingBalance"]?.ToObject<decimal>() ?? 0m;
            }
        }

        // Helper method for prefix placeholder OData execution
        private List<JToken> ExecutePrefixFetchWithFallback(string filter, string selectColumns, string accessToken)
        {
            string modernUrlBase = $"{_baseUrl}/odata/{_tenantName}/TrialBalance";
            string legacyUrlBase = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance";

            // Try modern URL first
            var results = PaginatedFetch(_httpClient, modernUrlBase, filter, selectColumns, accessToken);
            if (results != null) return results;

            // Try legacy URL if modern fails
            results = PaginatedFetch(_httpClient, legacyUrlBase, filter, selectColumns, accessToken);
            if (results != null) return results;

            // Return empty list if both fail
            PXTrace.WriteWarning($"Both modern and legacy URLs failed for filter: {filter}");
            return new List<JToken>();
        }

        // Add this method to FinancialDataService class
        // Replace the existing ParsePrefixPlaceholder method with this enhanced version
        private PrefixPlaceholderInfo ParsePrefixPlaceholder(string placeholder, string currentYearPeriod, string previousYearPeriod, UserSettings userSettings)
        {
            try
            {
                string cleanKey = placeholder.Trim('{', '}');

                var parts = cleanKey.Split('_');

                if (parts.Length < 2)
                {
                    PXTrace.WriteWarning($"Invalid prefix placeholder format: {placeholder}");
                    return null;
                }

                // First part should be account
                string account = parts[0];

                // Last part should be CY or PY
                string yearType = parts[parts.Length - 1].ToUpper();
                if (yearType != "CY" && yearType != "PY")
                {
                    PXTrace.WriteWarning($"Invalid year type in placeholder: {placeholder}");
                    return null;
                }

                // Create placeholder info with defaults from user settings
                var info = new PrefixPlaceholderInfo
                {
                    OriginalPlaceholder = placeholder,
                    Account = account,
                    YearType = yearType,
                    Period = yearType == "CY" ? currentYearPeriod : previousYearPeriod,
                    BalanceType = "ending", // Default balance type

                    // Set defaults from user settings or "1"
                    Branch = !string.IsNullOrEmpty(userSettings?.Branch) ? userSettings!.Branch : "1",
                    Organization = !string.IsNullOrEmpty(userSettings?.Organization) ? userSettings!.Organization : "1",
                    Ledger = !string.IsNullOrEmpty(userSettings?.Ledger) ? userSettings!.Ledger : "1",
                    Subaccount = "1" // Always default to "1"
                };

                // Parse middle parts for prefix components
                var middleParts = parts.Skip(1).Take(parts.Length - 2);

                foreach (var part in middleParts)
                {
                    if (part.StartsWith("sb", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Subaccount = part.Substring(2); // Remove "sb" prefix
                    }
                    else if (part.StartsWith("br", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Branch = part.Substring(2); // Remove "br" prefix
                    }
                    else if (part.StartsWith("ld", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Ledger = part.Substring(2); // Remove "ld" prefix
                    }
                    else if (part.StartsWith("or", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Organization = part.Substring(2); // Remove "or" prefix
                    }
                    else if (part.StartsWith("bt", StringComparison.OrdinalIgnoreCase))
                    {
                        string balanceType = part.Substring(2).ToLower(); // Remove "bt" prefix

                        // Validate balance type
                        if (balanceType == "credit" || balanceType == "debit" ||
                            balanceType == "beginning" || balanceType == "ending")
                        {
                            info.BalanceType = balanceType;
                        }
                        else
                        {
                            PXTrace.WriteWarning($"Invalid balance type '{balanceType}' in placeholder {placeholder}. Using default 'ending'.");
                            info.BalanceType = "ending";
                        }
                    }
                }


                return info;
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Failed to parse prefix placeholder {placeholder}: {ex.Message}");
                return null;
            }
        }

        // Add this main method to FinancialDataService class
        public Dictionary<string, string> ProcessPrefixPlaceholders(List<string> prefixPlaceholders, string currentYearPeriod, string previousYearPeriod, UserSettings userSettings)
        {
            var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // No artificial call limit — process all prefix placeholders.
            // Process ALL prefix placeholders without any artificial limits
            var keysToProcess = prefixPlaceholders; // Process everything

            PXTrace.WriteInformation($"Processing {keysToProcess.Count} prefix placeholders (no limits applied)...");

            // Process each prefix placeholder
            foreach (var placeholder in keysToProcess)
            {
                try
                {
                    // Parse the placeholder
                    var info = ParsePrefixPlaceholder(placeholder, currentYearPeriod, previousYearPeriod, userSettings);
                    if (info == null)
                    {
                        results[placeholder] = "0";
                        continue;
                    }

                    // Build and execute OData query
                    decimal balance = FetchBalanceForPrefixPlaceholder(info);
                    results[placeholder] = balance.ToString("#,##0");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Failed to process prefix placeholder {placeholder}: {ex.Message}");
                    results[placeholder] = "0";
                }
            }

            return results;
        }

        // Add this method to FinancialDataService class
        public bool HasPrefixPattern(string placeholder)
        {
            string cleanKey = placeholder.Trim('{', '}');
            return cleanKey.Contains("_sb") || cleanKey.Contains("_ld") ||
                   cleanKey.Contains("_or") || cleanKey.Contains("_br") ||
                   cleanKey.Contains("_bt"); // NEW: Add bt detection
        }

        public List<PlaceholderRequest> AnalyzePlaceholders(List<string> placeholderKeys, string currentYearPeriod, string previousYearPeriod, UserSettings userSettings)
        {
            var requests = new List<PlaceholderRequest>();

            // Group placeholders by type and period
            var simpleAccountsCY = new List<string>();
            var simpleAccountsPY = new List<string>();
            var sumPrefixesCY = new List<string>();
            var sumPrefixesPY = new List<string>();
            var otherPlaceholders = new List<string>();
            var debitCreditSumsCY = new List<string>();
            var debitCreditSumsPY = new List<string>();
            var begSumsCY = new List<string>();
            var begSumsPY = new List<string>();
            var janBalancesCY = new List<string>();
            var janBalancesPY = new List<string>();
            var specificBalancesCY = new List<string>();
            var specificBalancesPY = new List<string>();

            foreach (var placeholder in placeholderKeys)
            {
                string cleanKey = placeholder.Trim('{', '}');

                // Prefix placeholder: A12345_sb000123_br001_btcredit_CY
                if (HasPrefixPattern(placeholder))
                {
                    otherPlaceholders.Add(placeholder);
                }
                // Simple account: A39101_CY or 100-10_CY
                else if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_(CY|PY)$"))
                {
                    if (cleanKey.EndsWith("_CY"))
                        simpleAccountsCY.Add(placeholder);
                    else
                        simpleAccountsPY.Add(placeholder);
                }
                // Sum prefix: Sum3_B69_CY, Sum1_B_CY, Sum3_100_CY
                else if (Regex.IsMatch(cleanKey, @"^Sum\d+_[A-Z0-9\-]*_(CY|PY)$"))
                {
                    if (cleanKey.EndsWith("_CY"))
                        sumPrefixesCY.Add(placeholder);
                    else
                        sumPrefixesPY.Add(placeholder);
                }
                // Debit/Credit Sum: DebitSum3_B53_CY, CreditSum3_B53_CY, DebitSum3_100_CY
                else if (Regex.IsMatch(cleanKey, @"^(Debit|Credit)Sum\d+_[A-Z0-9\-]*_(CY|PY)$"))
                {
                    if (cleanKey.EndsWith("_CY"))
                        debitCreditSumsCY.Add(placeholder);
                    else
                        debitCreditSumsPY.Add(placeholder);
                }
                // Beginning Sum: BegSum3_A11_CY or BegSum3_100_CY
                else if (Regex.IsMatch(cleanKey, @"^BegSum\d+_[A-Z0-9\-]*_(CY|PY)$"))
                {
                    if (cleanKey.EndsWith("_CY"))
                        begSumsCY.Add(placeholder);
                    else
                        begSumsPY.Add(placeholder);
                }
                // January balance: A34101_Jan1_PY or 100-10_Jan1_PY
                else if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_Jan1_(CY|PY)$"))
                {
                    if (cleanKey.EndsWith("_CY"))
                        janBalancesCY.Add(placeholder);
                    else
                        janBalancesPY.Add(placeholder);
                }
                // Specific balance type: A34101_debit_CY, A34101_credit_PY, 100-10_debit_CY
                else if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_(debit|credit)_(CY|PY)$"))
                {
                    if (cleanKey.EndsWith("_CY"))
                        specificBalancesCY.Add(placeholder);
                    else
                        specificBalancesPY.Add(placeholder);
                }
                else
                {
                    otherPlaceholders.Add(placeholder);
                }
            }

            // Dictionary to group placeholders by API call
            var apiCallGroups = new Dictionary<string, PlaceholderRequest>(StringComparer.OrdinalIgnoreCase);

            void AddToGroup(string apiCall, string placeholder, string period)
            {
                if (!apiCallGroups.TryGetValue(apiCall, out var group))
                {
                    group = new PlaceholderRequest
                    {
                        ApiCall = apiCall,
                        Placeholders = new List<string>(),
                        Period = period
                    };
                    apiCallGroups[apiCall] = group;
                }
                group.Placeholders.Add(placeholder);
            }

            // Build API requests for simple accounts
            foreach (var placeholder in simpleAccountsCY)
            {
                string account = placeholder.Trim('{', '}').Replace("_CY", "");
                string apiCall = $"Account eq '{account}' and FinancialPeriod eq '{currentYearPeriod}'";
                AddToGroup(apiCall, placeholder, currentYearPeriod);
            }

            foreach (var placeholder in simpleAccountsPY)
            {
                string account = placeholder.Trim('{', '}').Replace("_PY", "");
                string apiCall = $"Account eq '{account}' and FinancialPeriod eq '{previousYearPeriod}'";
                AddToGroup(apiCall, placeholder, previousYearPeriod);
            }

            // Build API requests for sum prefixes
            foreach (var placeholder in sumPrefixesCY)
            {
                string cleanKey = placeholder.Trim('{', '}');

                var match = Regex.Match(cleanKey, @"^Sum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                if (match.Success)
                {
                    string prefix = match.Groups[2].Value;
                    string upperBound = IncrementPrefix(prefix);
                    string apiCall = $"Account ge '{prefix}' and Account lt '{upperBound}' and FinancialPeriod eq '{currentYearPeriod}'";
                    AddToGroup(apiCall, placeholder, currentYearPeriod);
                }
            }

            foreach (var placeholder in sumPrefixesPY)
            {
                string cleanKey = placeholder.Trim('{', '}');

                var match = Regex.Match(cleanKey, @"^Sum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                if (match.Success)
                {
                    string prefix = match.Groups[2].Value;
                    string upperBound = IncrementPrefix(prefix);
                    string apiCall = $"Account ge '{prefix}' and Account lt '{upperBound}' and FinancialPeriod eq '{previousYearPeriod}'";
                    AddToGroup(apiCall, placeholder, previousYearPeriod);
                }
            }

            // Build API requests for Debit/Credit Sums
            foreach (var placeholder in debitCreditSumsCY)
            {
                string cleanKey = placeholder.Trim('{', '}');

                var match = Regex.Match(cleanKey, @"^(Debit|Credit)Sum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                if (match.Success)
                {
                    string prefix = match.Groups[3].Value;
                    string upperBound = IncrementPrefix(prefix);
                    string apiCall = $"Account ge '{prefix}' and Account lt '{upperBound}' and FinancialPeriod eq '{currentYearPeriod}'";
                    AddToGroup(apiCall, placeholder, currentYearPeriod);
                }
            }

            foreach (var placeholder in debitCreditSumsPY)
            {
                string cleanKey = placeholder.Trim('{', '}');

                var match = Regex.Match(cleanKey, @"^(Debit|Credit)Sum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                if (match.Success)
                {
                    string prefix = match.Groups[3].Value;
                    string upperBound = IncrementPrefix(prefix);
                    string apiCall = $"Account ge '{prefix}' and Account lt '{upperBound}' and FinancialPeriod eq '{previousYearPeriod}'";
                    AddToGroup(apiCall, placeholder, previousYearPeriod);
                }
            }

            // Build API requests for Beginning Sums
            foreach (var placeholder in begSumsCY)
            {
                string cleanKey = placeholder.Trim('{', '}');

                var match = Regex.Match(cleanKey, @"^BegSum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                if (match.Success)
                {
                    string prefix = match.Groups[2].Value;
                    string upperBound = IncrementPrefix(prefix);
                    string apiCall = $"Account ge '{prefix}' and Account lt '{upperBound}' and FinancialPeriod eq '01{currentYearPeriod.Substring(2)}'";
                    AddToGroup(apiCall, placeholder, $"01{currentYearPeriod.Substring(2)}");
                }
            }

            foreach (var placeholder in begSumsPY)
            {
                string cleanKey = placeholder.Trim('{', '}');

                var match = Regex.Match(cleanKey, @"^BegSum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                if (match.Success)
                {
                    string prefix = match.Groups[2].Value;
                    string upperBound = IncrementPrefix(prefix);
                    string apiCall = $"Account ge '{prefix}' and Account lt '{upperBound}' and FinancialPeriod eq '01{previousYearPeriod.Substring(2)}'";
                    AddToGroup(apiCall, placeholder, $"01{previousYearPeriod.Substring(2)}");
                }
            }

            // Build API requests for January balances
            foreach (var placeholder in janBalancesCY)
            {
                string account = placeholder.Trim('{', '}').Replace("_Jan1_CY", "");
                string apiCall = $"Account eq '{account}' and FinancialPeriod eq '01{currentYearPeriod.Substring(2)}'";
                AddToGroup(apiCall, placeholder, $"01{currentYearPeriod.Substring(2)}");
            }

            foreach (var placeholder in janBalancesPY)
            {
                string account = placeholder.Trim('{', '}').Replace("_Jan1_PY", "");
                string apiCall = $"Account eq '{account}' and FinancialPeriod eq '01{previousYearPeriod.Substring(2)}'";
                AddToGroup(apiCall, placeholder, $"01{previousYearPeriod.Substring(2)}");
            }

            // Build API requests for Specific balances
            foreach (var placeholder in specificBalancesCY)
            {
                string cleanKey = placeholder.Trim('{', '}');

                var match = Regex.Match(cleanKey, @"^([A-Z0-9\-]+)_(debit|credit)_(CY|PY)$");
                if (match.Success)
                {
                    string account = match.Groups[1].Value;
                    string apiCall = $"Account eq '{account}' and FinancialPeriod ge '01{currentYearPeriod.Substring(2)}' and FinancialPeriod le '{currentYearPeriod}'";
                    AddToGroup(apiCall, placeholder, currentYearPeriod);
                }
            }

            foreach (var placeholder in specificBalancesPY)
            {
                string cleanKey = placeholder.Trim('{', '}');

                var match = Regex.Match(cleanKey, @"^([A-Z0-9\-]+)_(debit|credit)_(CY|PY)$");
                if (match.Success)
                {
                    string account = match.Groups[1].Value;
                    string apiCall = $"Account eq '{account}' and FinancialPeriod ge '01{previousYearPeriod.Substring(2)}' and FinancialPeriod le '{previousYearPeriod}'";
                    AddToGroup(apiCall, placeholder, previousYearPeriod);
                }
            }

            // Convert grouped calls to final request list
            requests.AddRange(apiCallGroups.Values);

            return requests;


        }


        private string IncrementPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return "A"; // fallback

            // Check if this is a segmented account (contains hyphen)
            // Examples: "100-10" → "100-11", "100-99" → "100-100", "100" → "101"
            if (prefix.Contains('-'))
            {
                // Split by hyphen and increment the last segment
                var segments = prefix.Split('-');
                var lastSegment = segments[segments.Length - 1];

                // Check if last segment is numeric
                if (int.TryParse(lastSegment, out int lastNum))
                {
                    // Increment the last segment
                    segments[segments.Length - 1] = (lastNum + 1).ToString();
                    return string.Join("-", segments);
                }
                else
                {
                    // Last segment is not numeric, treat as whole string
                    // Fall through to regular logic
                }
            }

            // For single character: B → C, H → I, Z → AA
            if (prefix.Length == 1)
            {
                char c = prefix[0];
                if (c == 'Z' || c == 'z')
                    return char.IsUpper(c) ? "AA" : "aa"; // edge case
                return ((char)(c + 1)).ToString();
            }

            // For multi-character: B69 → B70, B539 → B540, B999 → B1000, 100 → 101
            char lastChar = prefix[prefix.Length - 1];

            if (char.IsDigit(lastChar))
            {
                // Extract the number part and increment it
                string numberPart = "";
                int i = prefix.Length - 1;
                while (i >= 0 && char.IsDigit(prefix[i]))
                {
                    numberPart = prefix[i] + numberPart;
                    i--;
                }

                string letterPart = prefix.Substring(0, i + 1);
                int number = int.Parse(numberPart);
                return $"{letterPart}{number + 1}";
            }
            else
            {
                // Last character is a letter
                string basePrefix = prefix.Substring(0, prefix.Length - 1);
                if (lastChar == 'Z' || lastChar == 'z')
                    return basePrefix + (char.IsUpper(lastChar) ? "AA" : "aa"); // B → BAA
                return basePrefix + ((char)(lastChar + 1)).ToString();
            }
        }

        /// <summary>
        /// Executes all optimized API requests and returns placeholder values
        /// </summary>
        public Dictionary<string, string> ExecuteOptimizedApiRequests(List<PlaceholderRequest> requests, UserSettings userSettings)
        {
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var apiResults = new Dictionary<string, List<JToken>>(StringComparer.OrdinalIgnoreCase);
            var processor = new PlaceholderResultProcessor();

            PXTrace.WriteInformation($"🚀 Executing {requests.Count} optimized API requests...");

            // Build dimension filter once
            string dimensionFilter = BuildDimensionFilter(userSettings.Branch, userSettings.Organization);

            // Execute API calls in parallel batches
            var semaphore = new System.Threading.SemaphoreSlim(5, 5); // Limit to 5 concurrent calls
            var tasks = requests.Select(async request =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    // Get access token
                    string accessToken = _authService.AuthenticateAndGetToken();

                    // Build full filter with dimensions and ledger
                    string fullFilter = $"{request.ApiCall.Replace("FinancialPeriod", "FinancialPeriod")}";
                    if (!string.IsNullOrEmpty(dimensionFilter) && dimensionFilter != "1 eq 1")
                    {
                        fullFilter += $" and {dimensionFilter}";
                    }

                    // Execute the API call
                    var results = ExecuteFetchWithFallback(_httpClient, fullFilter, userSettings.Ledger, accessToken);

                    sw.Stop();
                    PXTrace.WriteInformation($"📡 API call completed in {sw.ElapsedMilliseconds}ms: {fullFilter} → {results?.Count ?? 0} records");

                    lock (apiResults)
                    {
                        apiResults[request.ApiCall] = results ?? new List<JToken>();
                    }
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"[API] Call failed for filter '{request.ApiCall}': {ex.Message}");
                    lock (apiResults)
                    {
                        apiResults[request.ApiCall] = new List<JToken>();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Wait for all API calls to complete
            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            totalStopwatch.Stop();
            PXTrace.WriteInformation($"[API] All API calls completed in {totalStopwatch.ElapsedMilliseconds}ms");

            // Process results and extract placeholder values
            PXTrace.WriteInformation($"[API] Processing results for {requests.Sum(r => r.Placeholders.Count)} placeholders...");
            var placeholderValues = processor.ProcessApiResults(requests, apiResults);

            PXTrace.WriteInformation($"[API] Execution complete: {placeholderValues.Count} placeholder values extracted");

            return placeholderValues;
        }

        /// <summary>
        /// Process all placeholders using pre-fetched data (no additional API calls)
        /// </summary>
        public Dictionary<string, string> ProcessPlaceholdersFromFetchedData(
            List<PlaceholderRequest> placeholderRequests,
            FinancialApiData currYearData,
            FinancialApiData prevYearData,
            FinancialApiData januaryBeginningDataCY,
            FinancialApiData januaryBeginningDataPY,
            FinancialApiData cumulativeCYData,
            FinancialApiData cumulativePYData)
        {
            var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            PXTrace.WriteInformation($"[API] Processing {placeholderRequests.Sum(r => r.Placeholders.Count)} placeholders from fetched data...");

            // Process each placeholder request
            foreach (var request in placeholderRequests)
            {
                foreach (var placeholder in request.Placeholders)
                {
                    string value = ProcessSinglePlaceholder(placeholder, currYearData, prevYearData,
                        januaryBeginningDataCY, januaryBeginningDataPY, cumulativeCYData, cumulativePYData);
                    results[placeholder] = value;
                }
            }

            sw.Stop();
            PXTrace.WriteInformation($"[API] Placeholder processing completed in {sw.ElapsedMilliseconds}ms");

            return results;
        }

        private string ProcessSinglePlaceholder(string placeholder,
            FinancialApiData currYearData, FinancialApiData prevYearData,
            FinancialApiData januaryBeginningDataCY, FinancialApiData januaryBeginningDataPY,
            FinancialApiData cumulativeCYData, FinancialApiData cumulativePYData)
        {
            string cleanKey = placeholder.Trim('{', '}');

            try
            {
                // Simple account: A39101_CY or 100-10_CY -> EndingBalance
                if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_(CY|PY)$"))
                {
                    var parts = cleanKey.Split('_');
                    string account = parts[0];
                    string year = parts[1];

                    var source = year == "CY" ? currYearData : prevYearData;
                    if (source.AccountData.TryGetValue(account, out var data))
                        return data.EndingBalance.ToString("#,##0");
                    return "0";
                }

                // Sum prefix: Sum3_B69_CY or Sum3_100_CY -> EndingBalance sum
                if (Regex.IsMatch(cleanKey, @"^Sum\d+_([A-Z0-9\-]*)_(CY|PY)$"))
                {
                    var match = Regex.Match(cleanKey, @"^Sum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                    int level = int.Parse(match.Groups[1].Value);
                    string prefix = match.Groups[2].Value;
                    string year = match.Groups[3].Value;

                    var source = year == "CY" ? currYearData : prevYearData;
                    decimal sum = 0;
                    foreach (var kvp in source.AccountData)
                    {
                        if (kvp.Key.Length >= level && kvp.Key.Substring(0, level) == prefix)
                            sum += kvp.Value.EndingBalance;
                    }
                    return sum.ToString("#,##0");
                }

                // Debit sum: DebitSum3_B53_CY or DebitSum3_100_CY -> Debit sum from range data
                if (Regex.IsMatch(cleanKey, @"^DebitSum\d+_([A-Z0-9\-]*)_(CY|PY)$"))
                {
                    var match = Regex.Match(cleanKey, @"^DebitSum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                    int level = int.Parse(match.Groups[1].Value);
                    string prefix = match.Groups[2].Value;
                    string year = match.Groups[3].Value;

                    var source = year == "CY" ? cumulativeCYData : cumulativePYData;
                    decimal sum = 0;
                    foreach (var kvp in source.AccountData)
                    {
                        if (kvp.Key.Length >= level && kvp.Key.Substring(0, level) == prefix)
                            sum += kvp.Value.Debit;
                    }
                    return sum.ToString("#,##0");
                }

                // Credit sum: CreditSum3_B53_CY or CreditSum3_100_CY -> Credit sum from range data
                if (Regex.IsMatch(cleanKey, @"^CreditSum\d+_([A-Z0-9\-]*)_(CY|PY)$"))
                {
                    var match = Regex.Match(cleanKey, @"^CreditSum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                    int level = int.Parse(match.Groups[1].Value);
                    string prefix = match.Groups[2].Value;
                    string year = match.Groups[3].Value;

                    var source = year == "CY" ? cumulativeCYData : cumulativePYData;
                    decimal sum = 0;
                    foreach (var kvp in source.AccountData)
                    {
                        if (kvp.Key.Length >= level && kvp.Key.Substring(0, level) == prefix)
                            sum += kvp.Value.Credit;
                    }
                    return sum.ToString("#,##0");
                }

                // Beginning sum: BegSum3_A11_CY or BegSum3_100_CY -> BeginningBalance sum from January data
                if (Regex.IsMatch(cleanKey, @"^BegSum\d+_([A-Z0-9\-]*)_(CY|PY)$"))
                {
                    var match = Regex.Match(cleanKey, @"^BegSum(\d+)_([A-Z0-9\-]*)_(CY|PY)$");
                    int level = int.Parse(match.Groups[1].Value);
                    string prefix = match.Groups[2].Value;
                    string year = match.Groups[3].Value;

                    var source = year == "CY" ? januaryBeginningDataCY : januaryBeginningDataPY;
                    decimal sum = 0;
                    foreach (var kvp in source.AccountData)
                    {
                        if (kvp.Key.Length >= level && kvp.Key.Substring(0, level) == prefix)
                            sum += kvp.Value.BeginningBalance;
                    }
                    return sum.ToString("#,##0");
                }

                // January balance: A21101_Jan1_CY or 100-10_Jan1_CY -> BeginningBalance
                if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_Jan1_(CY|PY)$"))
                {
                    var parts = cleanKey.Split('_');
                    string account = parts[0];
                    string year = parts[2];

                    var source = year == "CY" ? januaryBeginningDataCY : januaryBeginningDataPY;
                    if (source.AccountData.TryGetValue(account, out var data))
                        return data.BeginningBalance.ToString("#,##0");
                    return "0";
                }

                // Specific balance - debit: A34101_debit_CY or 100-10_debit_CY -> Sum of all Debit from range
                if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_debit_(CY|PY)$"))
                {
                    var parts = cleanKey.Split('_');
                    string account = parts[0];
                    string year = parts[2];

                    var source = year == "CY" ? cumulativeCYData : cumulativePYData;
                    if (source.AccountData.TryGetValue(account, out var data))
                        return data.Debit.ToString("#,##0");
                    return "0";
                }

                // Specific balance - credit: A34101_credit_CY or 100-10_credit_CY -> Sum of all Credit from range
                if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_credit_(CY|PY)$"))
                {
                    var parts = cleanKey.Split('_');
                    string account = parts[0];
                    string year = parts[2];

                    var source = year == "CY" ? cumulativeCYData : cumulativePYData;
                    if (source.AccountData.TryGetValue(account, out var data))
                        return data.Credit.ToString("#,##0");
                    return "0";
                }

                PXTrace.WriteWarning($"Unknown placeholder pattern: {placeholder}");
                return "0";
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Error processing placeholder {placeholder}: {ex.Message}");
                return "0";
            }
        }

    }

    public class PlaceholderRequest
    {
        public string ApiCall { get; set; } = string.Empty;
        public List<string> Placeholders { get; set; } = new List<string>();
        public string Period { get; set; } = string.Empty;
    }

    public class UserSettings
    {
        public string Branch { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public string Ledger { get; set; } = string.Empty;
    }



    // Classes for convenience
    public class FinancialPeriodData
    {
        public string Account { get; set; }
        public string Subaccount { get; set; }
        /// <summary>
        /// Account type from the TrialBalance GI "Type" column.
        /// Values: Asset, Liability, Expense, Income, Equity
        /// Used by ReportCalculationEngine for sign normalization.
        /// </summary>
        public string AccountType { get; set; }
        /// <summary>BranchID from the GI row. Populated in DetailRows; empty in aggregated AccountData.</summary>
        public string BranchID { get; set; }
        /// <summary>OrganizationID from the GI row. Populated in DetailRows; empty in aggregated AccountData.</summary>
        public string OrganizationID { get; set; }
        /// <summary>Ledger from the GI row. Populated in DetailRows; empty in aggregated AccountData.</summary>
        public string Ledger { get; set; }
        public decimal BeginningBalance { get; set; }
        public decimal EndingBalance { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class FinancialApiData
    {
        public Dictionary<string, FinancialPeriodData> AccountData { get; set; } = new Dictionary<string, FinancialPeriodData>();

        // Composite key-level data (used for FetchCompositeKeyData)
        public Dictionary<string, FinancialPeriodData> CompositeKeyData { get; set; } = new Dictionary<string, FinancialPeriodData>();

        /// <summary>
        /// Raw per-row detail data (one entry per GI row) including Subaccount, BranchID, OrganizationID.
        /// Used by ReportCalculationEngine when a line item has per-line dimension filters set.
        /// </summary>
        public List<FinancialPeriodData> DetailRows { get; set; } = new List<FinancialPeriodData>();
    }

    public class PrefixPlaceholderInfo
    {
        public string OriginalPlaceholder { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
        public string Subaccount { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Ledger { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public string YearType { get; set; } = string.Empty; // "CY" or "PY"
        public string Period { get; set; } = string.Empty;   // "122024" or "122023"
        public string BalanceType { get; set; } = "ending";
    }


}
