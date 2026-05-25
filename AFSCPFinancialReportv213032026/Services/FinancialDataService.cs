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

        // OData escapes a literal ' inside a single-quoted string by doubling it.
        // Without this, any user-entered value containing ' (e.g. branch "O'Brien")
        // breaks the entire filter with a 500 syntax error.
        private static string OEsc(string s) => s == null ? "" : s.Replace("'", "''");

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
            string filter = $"{_columnMapping.PeriodColumn} eq '{OEsc(period)}' and {dimensionFilter}";

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
        public FinancialApiData FetchRangeApiData(string branch, string organization, string ledger, string fromPeriod, string toPeriod, bool includeDetail = false, CancellationToken cancellationToken = default)
        {
            string accessToken = _authService.AuthenticateAndGetToken();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string baseFilter = $"{_columnMapping.PeriodColumn} ge '{OEsc(fromPeriod)}' and {_columnMapping.PeriodColumn} le '{OEsc(toPeriod)}' and {dimensionFilter}";

            var cumulativeDict = new Dictionary<string, FinancialPeriodData>();
            var detailRows = new List<FinancialPeriodData>();

            Action<JToken> rowConsumer = (item) =>
            {
                string accountId = item[_columnMapping.AccountColumn]?.ToString();
                if (string.IsNullOrEmpty(accountId)) return;

                string subaccount  = item[_columnMapping.SubaccountColumn]?.ToString()?.Trim()    ?? string.Empty;
                string branchId    = item[_columnMapping.BranchColumn]?.ToString()?.Trim()        ?? string.Empty;
                string orgId       = item[_columnMapping.OrganizationColumn]?.ToString()?.Trim()  ?? string.Empty;
                string ledgerId    = item[_columnMapping.LedgerColumn]?.ToString()?.Trim()        ?? string.Empty;
                string accountType = item[_columnMapping.TypeColumn]?.ToString() ?? string.Empty;
                decimal debit         = item[_columnMapping.DebitColumn]?.ToObject<decimal>()    ?? 0;
                decimal credit        = item[_columnMapping.CreditColumn]?.ToObject<decimal>()   ?? 0;
                decimal endingBalance = item[_columnMapping.EndingBalCol]?.ToObject<decimal>()   ?? 0;

                if (!cumulativeDict.TryGetValue(accountId!, out var cumEntry))
                {
                    // AccountType captured here so sign-flip in ApplyAccountTypeSign works
                    // for Liability/Income lines using YTD Debit/Credit/Movement.
                    cumEntry = new FinancialPeriodData { Account = accountId, AccountType = accountType };
                    cumulativeDict[accountId!] = cumEntry;
                }
                cumEntry.Debit         += debit;
                cumEntry.Credit        += credit;
                cumEntry.EndingBalance += endingBalance;

                if (includeDetail)
                {
                    // Each row is one (account, sub, branch, org, ledger, period) tuple;
                    // engine aggregates these client-side when a line has dimension filters.
                    detailRows.Add(new FinancialPeriodData
                    {
                        Account = accountId, Subaccount = subaccount, AccountType = accountType,
                        BranchID = branchId, OrganizationID = orgId, Ledger = ledgerId,
                        Debit = debit, Credit = credit, EndingBalance = endingBalance
                    });
                }
            };

            Action resetConsumer = () => { cumulativeDict.Clear(); detailRows.Clear(); };

            int count = ExecuteFetchStreamWithFallbackAsync(_httpClient, baseFilter, ledger, accessToken, rowConsumer, resetConsumer, cancellationToken).Result;

            if (count < 0)
                throw new PXException(Messages.FailedToFetchOData);

            var apiData = new FinancialApiData { DetailRows = detailRows };
            foreach (var kvp in cumulativeDict)
                apiData.AccountData[kvp.Key] = kvp.Value;

            return apiData;
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

            // See ExecuteFetchWithFallbackAsync above — ledger filter is preserved across
            // retries to avoid silently blending ledgers on transient failures.
            string filterWithLedger = AppendLedgerFilter(baseFilter, ledger);

            // Attempt 1: Modern URL with Ledger
            resetConsumer();
            int count = await PaginatedFetchStreamAsync(client, modernUrlBase, filterWithLedger, selectColumns, accessToken, rowConsumer, cancellationToken);
            if (count >= 0) return count;

            // Attempt 2: Legacy URL with Ledger (URL-format fallback only)
            PXTrace.WriteWarning($"Modern URL failed, retrying Legacy URL. Filter: {filterWithLedger}");
            resetConsumer();
            count = await PaginatedFetchStreamAsync(client, legacyUrlBase, filterWithLedger, selectColumns, accessToken, rowConsumer, cancellationToken);
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
                return $"{_columnMapping.BranchColumn} eq '{OEsc(branch)}' and {_columnMapping.OrganizationColumn} eq '{OEsc(organization)}'";
            }
            else if (!string.IsNullOrEmpty(branch))
            {
                return $"{_columnMapping.BranchColumn} eq '{OEsc(branch)}'";
            }
            else if (!string.IsNullOrEmpty(organization))
            {
                return $"{_columnMapping.OrganizationColumn} eq '{OEsc(organization)}'";
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
                ? $"{baseFilter} and {_columnMapping.LedgerColumn} eq '{OEsc(ledger)}'"
                : baseFilter;
        }

        /// <summary>
        /// Probes the configured GI to confirm it exists and is reachable before the
        /// parallel fetch tasks fan out. Throws a clear configuration error early instead
        /// of letting every fetch task fail with the generic FailedToFetchOData.
        /// </summary>
        public void ValidateGIExists(CancellationToken cancellationToken = default)
        {
            string giName = _columnMapping.GIName;
            string accessToken = _authService.AuthenticateAndGetToken();
            string modernUrl = $"{_baseUrl}/odata/{_tenantName}/{giName}?$top=1&$select={_columnMapping.AccountColumn}";
            string legacyUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/{giName}?$top=1&$select={_columnMapping.AccountColumn}";

            foreach (var url in new[] { modernUrl, legacyUrl })
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        var response = _httpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
                        if (response.IsSuccessStatusCode) return; // GI reachable on this URL form
                        if ((int)response.StatusCode != 404)
                        {
                            // Not a "missing GI" — surface as generic OData failure
                            PXTrace.WriteWarning($"GI probe {url} returned {(int)response.StatusCode}.");
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    PXTrace.WriteWarning($"GI probe {url} threw: {ex.Message}");
                }
            }

            throw new PXException(Messages.GIDataSourceNotFound, giName, _tenantName);
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
        /// Values: Asset, Liability, Expense, Income
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

        /// <summary>
        /// Raw per-row detail data (one entry per GI row) including Subaccount, BranchID, OrganizationID.
        /// Used by ReportCalculationEngine when a line item has per-line dimension filters set.
        /// </summary>
        public List<FinancialPeriodData> DetailRows { get; set; } = new List<FinancialPeriodData>();
    }
}
