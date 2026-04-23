//using FinancialReport.Helper;
using FinancialReport.Helper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PX.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
        public FinancialApiData FetchAllApiData(string branch, string organization, string ledger, string period, bool includeDetail = true, CancellationToken cancellationToken = default)
        {
            return FetchAllApiDataAsync(branch, organization, ledger, period, includeDetail, cancellationToken).Result;
        }

        // Async version — uses streaming aggregation to keep peak memory at one page of JTokens
        public async Task<FinancialApiData> FetchAllApiDataAsync(string branch, string organization, string ledger, string period, bool includeDetail = true, CancellationToken cancellationToken = default)
        {
            string accessToken = await _authService.AuthenticateAndGetTokenAsync();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string filter = $"FinancialPeriod eq '{period}' and {dimensionFilter}";

            var accountData = new Dictionary<string, FinancialPeriodData>();
            var detailRows  = new List<FinancialPeriodData>();
            bool firstRowLogged = false;

            Action<JToken> rowConsumer = (item) =>
            {
                // Diagnostic: log OData columns from the first row
                if (!firstRowLogged && item is JObject firstObj)
                {
                    var propNames = firstObj.Properties().Select(p => p.Name).ToList();
                    PXTrace.WriteInformation($"OData columns ({propNames.Count}): {string.Join(", ", propNames)}");
                    firstRowLogged = true;
                }

                string accountId = item[_columnMapping.AccountColumn]?.ToString();
                if (string.IsNullOrEmpty(accountId)) return;

                string subaccount  = item[_columnMapping.SubaccountColumn]?.ToString()?.Trim()   ?? string.Empty;
                string branchId    = item[_columnMapping.BranchColumn]?.ToString()?.Trim()      ?? string.Empty;
                string orgId       = item[_columnMapping.OrganizationColumn]?.ToString()?.Trim() ?? string.Empty;
                string ledgerId    = item[_columnMapping.LedgerColumn]?.ToString()?.Trim()      ?? string.Empty;
                string accountType = item[_columnMapping.TypeColumn]?.ToString() ?? string.Empty;
                decimal begBal  = item[_columnMapping.BeginningBalCol]?.ToObject<decimal>() ?? 0;
                decimal endBal  = item[_columnMapping.EndingBalCol]?.ToObject<decimal>()    ?? 0;
                decimal debit   = item[_columnMapping.DebitColumn]?.ToObject<decimal>()     ?? 0;
                decimal credit  = item[_columnMapping.CreditColumn]?.ToObject<decimal>()    ?? 0;

                if (!accountData.TryGetValue(accountId!, out var acctEntry))
                {
                    acctEntry = new FinancialPeriodData { Account = accountId, AccountType = accountType };
                    accountData[accountId!] = acctEntry;
                }
                acctEntry.BeginningBalance += begBal;
                acctEntry.EndingBalance    += endBal;
                acctEntry.Debit            += debit;
                acctEntry.Credit           += credit;

                if (includeDetail)
                {
                    detailRows.Add(new FinancialPeriodData
                    {
                        Account = accountId, Subaccount = subaccount, AccountType = accountType,
                        BranchID = branchId, OrganizationID = orgId, Ledger = ledgerId,
                        BeginningBalance = begBal, EndingBalance = endBal, Debit = debit, Credit = credit
                    });
                }
            };

            Action resetConsumer = () =>
            {
                accountData.Clear();
                detailRows.Clear();
                firstRowLogged = false;
            };

            int totalRows = await ExecuteFetchStreamWithFallbackAsync(_httpClient, filter, ledger, accessToken, rowConsumer, resetConsumer, cancellationToken);

            if (totalRows < 0)
                throw new PXException(Messages.FailedToFetchOData);

            PXTrace.WriteInformation($"FetchAllApiData: {accountData.Count} aggregated accounts, {detailRows.Count} detail rows.");
            if (detailRows.Count > 0)
            {
                var first = detailRows[0];
                PXTrace.WriteInformation($"  First detail row: Account={first.Account}, Sub={first.Subaccount}, Branch={first.BranchID}, Org={first.OrganizationID}, Ledger={first.Ledger}, EndBal={first.EndingBalance}");
                var sampleAccount = detailRows[0].Account;
                var subs = detailRows.Where(r => r.Account == sampleAccount).Select(r => r.Subaccount).Distinct().ToList();
                PXTrace.WriteInformation($"  Subaccounts for {sampleAccount}: [{string.Join(", ", subs)}]");
            }

            return new FinancialApiData { AccountData = accountData, DetailRows = detailRows };
        }

        // --------------------------------------------------------
        // 2) FetchRangeApiData
        // --------------------------------------------------------
        public FinancialApiData FetchRangeApiData(string branch, string organization, string ledger, string fromPeriod, string toPeriod, CancellationToken cancellationToken = default)
        {
            string accessToken = _authService.AuthenticateAndGetToken();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string baseFilter = $"FinancialPeriod ge '{fromPeriod}' and FinancialPeriod le '{toPeriod}' and {dimensionFilter}";

            var cumulativeDict = new Dictionary<string, FinancialPeriodData>();

            Action<JToken> rowConsumer = (item) =>
            {
                string accountId = item[_columnMapping.AccountColumn]?.ToString();
                decimal debit = item[_columnMapping.DebitColumn]?.ToObject<decimal>() ?? 0;
                decimal credit = item[_columnMapping.CreditColumn]?.ToObject<decimal>() ?? 0;
                decimal endingBalance = item[_columnMapping.EndingBalCol]?.ToObject<decimal>() ?? 0;
                if (string.IsNullOrEmpty(accountId)) return;

                if (!cumulativeDict.TryGetValue(accountId!, out var cumEntry))
                {
                    cumEntry = new FinancialPeriodData();
                    cumulativeDict[accountId!] = cumEntry;
                }
                cumEntry.Debit += debit;
                cumEntry.Credit += credit;
                cumEntry.EndingBalance += endingBalance;
            };

            Action resetConsumer = () => { cumulativeDict.Clear(); };

            int count = ExecuteFetchStreamWithFallbackAsync(_httpClient, baseFilter, ledger, accessToken, rowConsumer, resetConsumer, cancellationToken).Result;

            if (count < 0)
                throw new PXException(Messages.FailedToFetchOData);

            var apiData = new FinancialApiData();
            foreach (var kvp in cumulativeDict)
                apiData.AccountData[kvp.Key] = kvp.Value;

            return apiData;
        }

        // --------------------------------------------------------
        // 3) FetchCompositeKeyData
        // --------------------------------------------------------
        public FinancialApiData FetchCompositeKeyData(string branch, string organization, string ledger, string period, CancellationToken cancellationToken = default)
        {
            string accessToken = _authService.AuthenticateAndGetToken();
            string baseFilter = $"FinancialPeriod eq '{period}' and 1 eq 1";

            var compositeData = new Dictionary<string, FinancialPeriodData>();

            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger, accessToken, cancellationToken);

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
        // 4) FetchEndingBalance
        // --------------------------------------------------------
        public decimal FetchEndingBalance(string period, string branch, string organization, string ledger, string account, string subaccount, CancellationToken cancellationToken = default)
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

            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger, accessToken, cancellationToken);

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
        private List<JToken> ExecuteFetchWithFallback(HttpClient client, string baseFilter, string ledger, string accessToken, CancellationToken cancellationToken = default)
        {
            return ExecuteFetchWithFallbackAsync(client, baseFilter, ledger, accessToken, cancellationToken).Result;
        }

        /// <summary>
        /// Executes a fetch operation with fallback logic for both URL format and Ledger filtering (async).
        /// The access token is set per-request via HttpRequestMessage so parallel calls never race on shared headers.
        /// </summary>
        private async Task<List<JToken>> ExecuteFetchWithFallbackAsync(HttpClient client, string baseFilter, string ledger, string accessToken, CancellationToken cancellationToken = default)
        {
            string giName = _columnMapping.GIName;
            string modernUrlBase = $"{_baseUrl}/odata/{_tenantName}/{giName}";
            string legacyUrlBase = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/{giName}";
            string selectColumns = _columnMapping.BuildSelectColumns();

            // Attempt 1: Modern URL with Ledger (normal path — no trace on success)
            string filterWithLedger = AppendLedgerFilter(baseFilter, ledger);
            var results = await PaginatedFetchAsync(client, modernUrlBase, filterWithLedger, selectColumns, accessToken, cancellationToken);
            if (results != null) return results;

            // Attempt 2: Modern URL without Ledger (Attempt 1 with ledger filter failed)
            PXTrace.WriteWarning($"Fallback 2: Modern URL without Ledger. URL: {modernUrlBase}, Filter: {baseFilter}");
            results = await PaginatedFetchAsync(client, modernUrlBase, baseFilter, selectColumns, accessToken, cancellationToken);
            if (results != null) return results;

            // Attempt 3: Legacy URL with Ledger
            PXTrace.WriteWarning($"Attempt 2 failed. Retrying with Legacy URL with Ledger. URL: {legacyUrlBase}, Filter: {filterWithLedger}");
            results = await PaginatedFetchAsync(client, legacyUrlBase, filterWithLedger, selectColumns, accessToken, cancellationToken);
            if (results != null) return results;

            // Attempt 4: Legacy URL without Ledger
            PXTrace.WriteWarning($"Attempt 3 failed. Retrying with Legacy URL without Ledger. URL: {legacyUrlBase}, Filter: {baseFilter}");
            results = await PaginatedFetchAsync(client, legacyUrlBase, baseFilter, selectColumns, accessToken, cancellationToken);
            if (results != null) return results;

            PXTrace.WriteError("All fetch attempts failed.");
            return null;
        }

        /// <summary>
        /// Private helper method to execute a paginated OData fetch operation against a specific URL (synchronous).
        /// </summary>
        private List<JToken> PaginatedFetch(HttpClient client, string baseUrl, string filter, string selectColumns, string accessToken, CancellationToken cancellationToken = default, int maxRows = 100_000)
        {
            return PaginatedFetchAsync(client, baseUrl, filter, selectColumns, accessToken, cancellationToken, maxRows).Result;
        }

        /// <summary>
        /// Private helper method to execute a paginated OData fetch operation against a specific URL (async).
        /// Uses per-request HttpRequestMessage so the Bearer token is set on each individual request —
        /// safe for parallel calls without any serialization or shared-state mutation.
        /// </summary>
        private async Task<List<JToken>> PaginatedFetchAsync(HttpClient client, string baseUrl, string filter, string selectColumns, string accessToken, CancellationToken cancellationToken = default, int maxRows = 100_000)
        {
            var allResults = new List<JToken>();
            int pageSize = 10_000;
            int skip = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                        response = await client.SendAsync(request, cancellationToken);
                    }
                }
                catch (OperationCanceledException) { throw; }
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

                if (allResults.Count >= maxRows)
                {
                    PXTrace.WriteWarning($"[FinancialDataService] Row cap reached: fetched {allResults.Count} rows (limit={maxRows}) from {baseUrl}. Results may be incomplete.");
                    break;
                }
            }

            PXTrace.WriteInformation($"Successfully fetched {allResults.Count} total records for base URL: {baseUrl}");
            return allResults;
        }

        /// <summary>
        /// Streaming variant of PaginatedFetchAsync. Instead of accumulating all rows into a list,
        /// each row is passed to the <paramref name="rowConsumer"/> callback and then discarded.
        /// This keeps peak memory at one page of JTokens rather than the entire result set.
        /// Returns the total number of rows processed, or -1 if all fetch attempts failed.
        /// </summary>
        private async Task<int> PaginatedFetchStreamAsync(
            HttpClient client, string baseUrl, string filter, string selectColumns,
            string accessToken, Action<JToken> rowConsumer,
            CancellationToken cancellationToken = default, int maxRows = 100_000)
        {
            int totalRows = 0;
            int pageSize = 10_000;
            int skip = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string encodedFilter = Uri.EscapeDataString(filter);
                string encodedSelect = Uri.EscapeDataString(selectColumns);
                string pagedUrl = $"{baseUrl}?$filter={encodedFilter}&$select={encodedSelect}&$top={pageSize}&$skip={skip}";

                HttpResponseMessage response;
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, pagedUrl))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        response = await client.SendAsync(request, cancellationToken);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"HTTP request to {pagedUrl} failed: {ex.Message}");
                    return -1;
                }

                if (!response.IsSuccessStatusCode)
                {
                    PXTrace.WriteWarning($"Request to {pagedUrl} returned status {response.StatusCode}.");
                    return -1;
                }

                // Stream JSON directly from the HTTP response — avoids holding the full string in memory
                // AND the full JObject tree. Only one row's JObject exists at a time.
                int pageRowCount = 0;
                try
                {
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var sr = new System.IO.StreamReader(stream))
                    using (var reader = new JsonTextReader(sr))
                    {
                        // Navigate to the "value" array
                        bool foundValue = false;
                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonToken.PropertyName && (string)reader.Value == "value")
                            {
                                reader.Read(); // Move to StartArray
                                if (reader.TokenType == JsonToken.StartArray)
                                {
                                    foundValue = true;
                                    break;
                                }
                            }
                        }

                        if (!foundValue)
                        {
                            // No "value" array — treat as empty page
                        }
                        else
                        {
                            // Read each object in the array
                            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                            {
                                if (reader.TokenType == JsonToken.StartObject)
                                {
                                    var row = JObject.Load(reader);
                                    rowConsumer(row);
                                    pageRowCount++;
                                }
                            }
                        }
                    }
                }
                catch (JsonException jex)
                {
                    PXTrace.WriteError($"Failed to parse JSON stream from {pagedUrl}: {jex.Message}");
                    return -1;
                }

                if (pageRowCount == 0)
                    break;

                totalRows += pageRowCount;
                skip += pageSize;

                if (totalRows >= maxRows)
                {
                    PXTrace.WriteWarning($"[FinancialDataService] Row cap reached: {totalRows} rows (limit={maxRows}) from {baseUrl}.");
                    break;
                }
            }

            PXTrace.WriteInformation($"Successfully streamed {totalRows} total records for base URL: {baseUrl}");
            return totalRows;
        }

        /// <summary>
        /// Streaming variant of ExecuteFetchWithFallbackAsync. Tries all 4 URL/Ledger combinations
        /// and streams rows to the callback. Returns total row count, or -1 if all attempts fail.
        /// IMPORTANT: The caller must reset its aggregation state before each retry. This method
        /// calls <paramref name="resetConsumer"/> before each fallback attempt.
        /// </summary>
        private async Task<int> ExecuteFetchStreamWithFallbackAsync(
            HttpClient client, string baseFilter, string ledger, string accessToken,
            Action<JToken> rowConsumer, Action resetConsumer,
            CancellationToken cancellationToken = default)
        {
            string giName = _columnMapping.GIName;
            string modernUrlBase = $"{_baseUrl}/odata/{_tenantName}/{giName}";
            string legacyUrlBase = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/{giName}";
            string selectColumns = _columnMapping.BuildSelectColumns();

            string filterWithLedger = AppendLedgerFilter(baseFilter, ledger);

            // Attempt 1: Modern URL with Ledger
            resetConsumer();
            int count = await PaginatedFetchStreamAsync(client, modernUrlBase, filterWithLedger, selectColumns, accessToken, rowConsumer, cancellationToken);
            if (count >= 0) return count;

            // Attempt 2: Modern URL without Ledger
            PXTrace.WriteWarning($"Fallback 2: Modern URL without Ledger. URL: {modernUrlBase}, Filter: {baseFilter}");
            resetConsumer();
            count = await PaginatedFetchStreamAsync(client, modernUrlBase, baseFilter, selectColumns, accessToken, rowConsumer, cancellationToken);
            if (count >= 0) return count;

            // Attempt 3: Legacy URL with Ledger
            PXTrace.WriteWarning($"Attempt 2 failed. Retrying with Legacy URL with Ledger. URL: {legacyUrlBase}, Filter: {filterWithLedger}");
            resetConsumer();
            count = await PaginatedFetchStreamAsync(client, legacyUrlBase, filterWithLedger, selectColumns, accessToken, rowConsumer, cancellationToken);
            if (count >= 0) return count;

            // Attempt 4: Legacy URL without Ledger
            PXTrace.WriteWarning($"Attempt 3 failed. Retrying with Legacy URL without Ledger. URL: {legacyUrlBase}, Filter: {baseFilter}");
            resetConsumer();
            count = await PaginatedFetchStreamAsync(client, legacyUrlBase, baseFilter, selectColumns, accessToken, rowConsumer, cancellationToken);
            if (count >= 0) return count;

            PXTrace.WriteError("All streaming fetch attempts failed.");
            return -1;
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
}
