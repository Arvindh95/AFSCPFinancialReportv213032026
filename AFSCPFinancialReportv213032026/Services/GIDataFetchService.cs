using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PX.Data;

namespace FinancialReport.Services
{
    /// <summary>
    /// Fetches data from any Generic Inquiry based on FLRTGIDataSource configuration,
    /// aggregates rows per column definition, resolves calculated formulas,
    /// and returns a dictionary of placeholder values: {{PREFIX_ALIAS}} → formatted value.
    /// </summary>
    public class GIDataFetchService
    {
        private readonly AuthService _authService;
        private readonly string _baseUrl;
        private readonly string _tenantName;

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            MaxConnectionsPerServer = 10
        })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        static GIDataFetchService()
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public GIDataFetchService(AuthService authService, string baseUrl, string tenantName)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
            _tenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
        }

        /// <summary>
        /// Main entry point: fetches GI data and produces placeholder dictionary.
        /// </summary>
        /// <param name="ds">The GI Data Source configuration header.</param>
        /// <param name="columns">Column definitions (VALUE / CALCULATED / HEADING).</param>
        /// <param name="year">Current year for period template substitution (e.g. "2026").</param>
        /// <param name="month">Current month for period template substitution (e.g. "03").</param>
        /// <param name="branch">Branch filter value (optional).</param>
        /// <param name="organization">Organization filter value (optional).</param>
        /// <param name="ledger">Ledger filter value (optional).</param>
        /// <returns>Dictionary of PREFIX_ALIAS → formatted value.</returns>
        public Dictionary<string, string> FetchAndAggregate(
            FLRTGIDataSource ds,
            List<FLRTGIDataSourceColumn> columns,
            string year, string month,
            string branch = null, string organization = null, string ledger = null)
        {
            if (ds == null) throw new PXException("Data source is null.");
            if (string.IsNullOrWhiteSpace(ds.GIName))
                throw new PXException("GI Name is required on the data source.");

            // Authenticate on main thread (PXTrace works here)
            PXTrace.WriteInformation($"[GIDataFetch] Starting fetch for GI={ds.GIName}, Year={year}, Month={month}");
            string accessToken = _authService.AuthenticateAndGetToken();
            PXTrace.WriteInformation("[GIDataFetch] Authenticated successfully.");

            // Build filter on main thread so we can trace it
            string filter = BuildHeaderFilter(ds, year, month, branch, organization, ledger);
            PXTrace.WriteInformation($"[GIDataFetch] Filter={filter}");

            // Build $select — skip entirely if any MultiRow column exists (needs all columns)
            bool hasMultiRow = columns.Any(c => c.LineType == FLRTGIDataSourceColumn.ColumnLineType.MultiRow);
            var valueColumns = columns
                .Where(c => c.LineType == FLRTGIDataSourceColumn.ColumnLineType.Value
                         && !string.IsNullOrWhiteSpace(c.GIColumn))
                .ToList();
            string selectColumns = "";
            if (!hasMultiRow)
            {
                var selectSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var vc in valueColumns)
                    selectSet.Add(vc.GIColumn);
                if (!string.IsNullOrWhiteSpace(ds.KeyColumn))
                    selectSet.Add(ds.KeyColumn);
                selectColumns = string.Join(",", selectSet);
            }
            PXTrace.WriteInformation(hasMultiRow
                ? "[GIDataFetch] $select skipped (MultiRow column present — fetching all columns)"
                : $"[GIDataFetch] $select={selectColumns}");

            string modernUrl = $"{_baseUrl}/odata/{_tenantName}/{ds.GIName}";
            string legacyUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/{ds.GIName}";
            PXTrace.WriteInformation($"[GIDataFetch] Modern URL={modernUrl}");

            // Fetch on background thread to avoid async deadlock.
            // Capture errors as strings so we can log them on the main thread.
            List<JToken> rows = null;
            var fetchErrors = new List<string>();
            try
            {
                var task = Task.Run(async () =>
                {
                    var errors = new List<string>();

                    // Attempt 1: Modern URL + filter + $select (optimal)
                    var result = await PaginatedFetchAsync(modernUrl, filter, selectColumns, accessToken, errors);
                    if (result != null) return (result, errors);

                    // Attempt 2: Modern URL + filter, NO $select (column names may differ)
                    result = await PaginatedFetchAsync(modernUrl, filter, null, accessToken, errors);
                    if (result != null) return (result, errors);

                    // Attempt 3: Modern URL, no filter, no $select (last resort)
                    result = await PaginatedFetchAsync(modernUrl, null, null, accessToken, errors);
                    if (result != null) return (result, errors);

                    // Attempt 4: Legacy URL, no filter, no $select
                    result = await PaginatedFetchAsync(legacyUrl, null, null, accessToken, errors);
                    return (result, errors);
                });
                task.Wait();
                rows = task.Result.result;
                fetchErrors = task.Result.errors;
            }
            catch (AggregateException ae)
            {
                var inner = ae.Flatten().InnerException;
                PXTrace.WriteError($"[GIDataFetch] HTTP Error: {inner?.Message}");
                throw new PXException($"GI Data Fetch failed: {inner?.Message}");
            }

            // Log fetch errors on main thread where PXTrace works
            foreach (var err in fetchErrors)
                PXTrace.WriteWarning($"[GIDataFetch] {err}");

            if (rows == null)
            {
                string errorDetail = fetchErrors.Count > 0
                    ? string.Join(" | ", fetchErrors)
                    : "No error details captured.";
                throw new PXException($"Failed to fetch data from GI '{ds.GIName}'. Errors: {errorDetail}");
            }

            PXTrace.WriteInformation($"[GIDataFetch] Fetched {rows.Count} rows from '{ds.GIName}'.");

            // Log actual OData property names from the first row so user can map columns correctly
            if (rows.Count > 0 && rows[0] is JObject firstRow)
            {
                var propNames = firstRow.Properties().Select(p => p.Name).Where(n => !n.StartsWith("odata")).ToList();
                PXTrace.WriteInformation($"[GIDataFetch] Available OData columns: {string.Join(", ", propNames)}");
            }

            // Aggregate each VALUE column
            var results = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var col in valueColumns)
            {
                var filteredRows = FilterRowsByKey(rows, ds.KeyColumn, col.KeyFrom, col.KeyTo);
                filteredRows = ApplyRowFilter(filteredRows, col.RowFilter);
                object value = AggregateColumn(filteredRows, col);
                results[col.ColumnAlias] = value;
            }

            // Resolve CALCULATED columns
            var calculatedColumns = columns
                .Where(c => c.LineType == FLRTGIDataSourceColumn.ColumnLineType.Calculated
                         && !string.IsNullOrWhiteSpace(c.Formula))
                .OrderBy(c => c.SortOrder)
                .ToList();

            foreach (var calc in calculatedColumns)
            {
                decimal formulaResult = EvaluateFormula(calc.Formula, results);
                results[calc.ColumnAlias] = formulaResult;
            }

            // Expand MULTIROW columns — each row becomes PREFIX_ALIAS_N_PROPNAME
            var multiRowColumns = columns
                .Where(c => c.LineType == FLRTGIDataSourceColumn.ColumnLineType.MultiRow)
                .ToList();

            foreach (var col in multiRowColumns)
            {
                var filteredRows = FilterRowsByKey(rows, ds.KeyColumn, col.KeyFrom, col.KeyTo);
                filteredRows = ApplyRowFilter(filteredRows, col.RowFilter);

                // Sort
                if (!string.IsNullOrWhiteSpace(col.OrderByColumn))
                {
                    bool desc = (col.OrderByDirection ?? FLRTGIDataSourceColumn.OrderByDirectionType.Desc)
                                == FLRTGIDataSourceColumn.OrderByDirectionType.Desc;
                    filteredRows = desc
                        ? filteredRows.OrderByDescending(r => GetSortValue(r, col.OrderByColumn)).ToList()
                        : filteredRows.OrderBy(r => GetSortValue(r, col.OrderByColumn)).ToList();
                }

                // Take top N
                int limit = col.RowLimit ?? 10;
                filteredRows = filteredRows.Take(limit).ToList();

                PXTrace.WriteInformation($"[GIDataFetch] MultiRow '{col.ColumnAlias}': {filteredRows.Count} rows (limit={limit}).");

                for (int i = 0; i < filteredRows.Count; i++)
                {
                    int rank = i + 1;
                    if (!(filteredRows[i] is JObject rowObj)) continue;
                    foreach (var prop in rowObj.Properties())
                    {
                        if (prop.Name.StartsWith("odata", StringComparison.OrdinalIgnoreCase)) continue;
                        string key = $"{col.ColumnAlias}_{rank}_{prop.Name}";
                        results[key] = prop.Value?.ToString() ?? "";
                    }
                }
            }

            // Format and build output
            var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string prefix = ds.Prefix ?? "";

            foreach (var col in columns)
            {
                if (col.LineType == FLRTGIDataSourceColumn.ColumnLineType.Heading) continue;
                if (col.IsVisible != true) continue;

                if (col.LineType == FLRTGIDataSourceColumn.ColumnLineType.MultiRow)
                {
                    // Emit all expanded keys for this alias: ALIAS_N_PROPNAME
                    string aliasPrefix = col.ColumnAlias + "_";
                    foreach (var kvp in results.Where(r =>
                        r.Key.StartsWith(aliasPrefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        string outKey = string.IsNullOrWhiteSpace(prefix)
                            ? kvp.Key
                            : $"{prefix}_{kvp.Key}";
                        output[outKey] = kvp.Value?.ToString() ?? "";
                    }
                    continue;
                }

                if (!results.TryGetValue(col.ColumnAlias, out object rawValue)) continue;

                string formatted = FormatValue(rawValue, col.ColumnType, col.FormatString);
                string key = string.IsNullOrWhiteSpace(prefix)
                    ? col.ColumnAlias
                    : $"{prefix}_{col.ColumnAlias}";
                output[key] = formatted;
            }

            PXTrace.WriteInformation($"[GIDataFetch] Produced {output.Count} placeholders.");
            return output;
        }

        // FetchAndAggregateAsync removed — sync FetchAndAggregate handles everything
        // to avoid sync-over-async deadlock and PXTrace context issues.

        #region MultiRow Helpers

        /// <summary>
        /// Returns a comparable sort key for a GI row property.
        /// Numeric strings sort as decimals; date strings sort as DateTime; others sort as string.
        /// </summary>
        private IComparable GetSortValue(JToken row, string column)
        {
            string raw = row[column]?.ToString()?.Trim() ?? "";
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d))
                return d;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                return dt;
            return raw;
        }

        #endregion

        #region Filter Building

        private string BuildHeaderFilter(FLRTGIDataSource ds, string year, string month,
            string branch, string organization, string ledger)
        {
            var parts = new List<string>();

            // Period filter
            if (!string.IsNullOrWhiteSpace(ds.PeriodFilterColumn) && !string.IsNullOrWhiteSpace(ds.PeriodFilterTemplate))
            {
                string periodValue = ds.PeriodFilterTemplate
                    .Replace("{YEAR}", year ?? "")
                    .Replace("{MONTH}", (month ?? "").PadLeft(2, '0'));

                string scope = ds.PeriodScope ?? FLRTGIDataSource.PeriodScopeType.Exact;

                // Date columns with Monthly/Yearly scope use range filters (ge/lt)
                if (ds.PeriodFilterType == FLRTGIDataSource.GIColumnType.Date
                    && scope != FLRTGIDataSource.PeriodScopeType.Exact)
                {
                    string dateRange = BuildDateRangeFilter(ds.PeriodFilterColumn, year, month, scope);
                    if (!string.IsNullOrWhiteSpace(dateRange))
                        parts.Add(dateRange);
                }
                else
                {
                    // Exact match (String/Integer period columns like "012025")
                    string periodFilter = BuildTypedFilter(ds.PeriodFilterColumn, ds.PeriodFilterType, periodValue);
                    if (!string.IsNullOrWhiteSpace(periodFilter))
                        parts.Add(periodFilter);
                }
            }

            // Branch filter
            if (!string.IsNullOrWhiteSpace(ds.BranchFilterColumn) && !string.IsNullOrWhiteSpace(branch))
            {
                string branchFilter = BuildTypedFilter(ds.BranchFilterColumn, ds.BranchFilterType, branch);
                if (!string.IsNullOrWhiteSpace(branchFilter))
                    parts.Add(branchFilter);
            }

            // Organization filter
            if (!string.IsNullOrWhiteSpace(ds.OrgFilterColumn) && !string.IsNullOrWhiteSpace(organization))
            {
                string orgFilter = BuildTypedFilter(ds.OrgFilterColumn, ds.OrgFilterType, organization);
                if (!string.IsNullOrWhiteSpace(orgFilter))
                    parts.Add(orgFilter);
            }

            // Ledger filter
            if (!string.IsNullOrWhiteSpace(ds.LedgerFilterColumn) && !string.IsNullOrWhiteSpace(ledger))
            {
                string ledgerFilter = BuildTypedFilter(ds.LedgerFilterColumn, ds.LedgerFilterType, ledger);
                if (!string.IsNullOrWhiteSpace(ledgerFilter))
                    parts.Add(ledgerFilter);
            }

            return parts.Count > 0 ? string.Join(" and ", parts) : "1 eq 1";
        }

        /// <summary>
        /// Builds a typed OData eq filter for a column based on its configured type.
        /// </summary>
        private string BuildTypedFilter(string column, string type, string value)
        {
            if (string.IsNullOrWhiteSpace(column) || string.IsNullOrWhiteSpace(value))
                return null;

            switch (type)
            {
                case FLRTGIDataSource.GIColumnType.Date:
                    // OData v3 datetime format — append T00:00:00 if no time component
                    string dtValue = value.Contains("T") ? value : value + "T00:00:00";
                    return $"{column} eq datetime'{dtValue}'";

                case FLRTGIDataSource.GIColumnType.Integer:
                    return $"{column} eq {value}";

                case FLRTGIDataSource.GIColumnType.Decimal:
                    return $"{column} eq {value}m";

                case FLRTGIDataSource.GIColumnType.Boolean:
                    return $"{column} eq {value.ToLower()}";

                case FLRTGIDataSource.GIColumnType.String:
                default:
                    return $"{column} eq '{value}'";
            }
        }

        /// <summary>
        /// Builds a date range OData filter for Monthly or Yearly scope.
        /// Monthly: Date ge '2026-01-01' and Date lt '2026-02-01'
        /// Yearly:  Date ge '2026-01-01' and Date lt '2027-01-01'
        /// </summary>
        private string BuildDateRangeFilter(string column, string year, string month, string scope)
        {
            if (!int.TryParse(year, out int y)) return null;
            int m = 1;
            if (!string.IsNullOrWhiteSpace(month))
                int.TryParse(month, out m);
            if (m < 1) m = 1;
            if (m > 12) m = 12;

            DateTime rangeStart;
            DateTime rangeEnd;

            if (scope == FLRTGIDataSource.PeriodScopeType.Yearly)
            {
                rangeStart = new DateTime(y, 1, 1);
                rangeEnd = new DateTime(y + 1, 1, 1);
            }
            else // Monthly
            {
                rangeStart = new DateTime(y, m, 1);
                rangeEnd = m == 12 ? new DateTime(y + 1, 1, 1) : new DateTime(y, m + 1, 1);
            }

            string start = rangeStart.ToString("yyyy-MM-ddT00:00:00");
            string end = rangeEnd.ToString("yyyy-MM-ddT00:00:00");

            return $"{column} ge datetime'{start}' and {column} lt datetime'{end}'";
        }

        #endregion

        #region Row Filtering

        private List<JToken> FilterRowsByKey(List<JToken> rows, string keyColumn, string keyFrom, string keyTo)
        {
            if (string.IsNullOrWhiteSpace(keyColumn)) return rows;
            if (string.IsNullOrWhiteSpace(keyFrom) && string.IsNullOrWhiteSpace(keyTo)) return rows;

            return rows.Where(row =>
            {
                string keyValue = row[keyColumn]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(keyFrom) && string.Compare(keyValue, keyFrom.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
                if (!string.IsNullOrWhiteSpace(keyTo) && string.Compare(keyValue, keyTo.Trim(), StringComparison.OrdinalIgnoreCase) > 0)
                    return false;
                return true;
            }).ToList();
        }

        /// <summary>
        /// Applies client-side row filters. Supports multiple conditions joined by "and".
        /// Each condition: "Column eq 'Value'" or "Column ne 'Value'".
        /// Example: "Status eq 'Open' and Branch eq 'HEADOFFICE'"
        /// </summary>
        private List<JToken> ApplyRowFilter(List<JToken> rows, string rowFilter)
        {
            if (string.IsNullOrWhiteSpace(rowFilter)) return rows;

            // Split on " and " (case-insensitive)
            var conditions = Regex.Split(rowFilter.Trim(), @"\s+and\s+", RegexOptions.IgnoreCase);
            var parsedConditions = new List<(string column, string op, string value)>();

            foreach (var condition in conditions)
            {
                // Support eq and ne operators
                var match = Regex.Match(condition.Trim(), @"^(\S+)\s+(eq|ne)\s+'?([^']*)'?$", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    PXTrace.WriteWarning($"[GIDataFetch] RowFilter condition '{condition}' not parseable — skipping.");
                    continue;
                }
                parsedConditions.Add((match.Groups[1].Value, match.Groups[2].Value.ToLower(), match.Groups[3].Value));
            }

            if (parsedConditions.Count == 0) return rows;

            return rows.Where(row =>
            {
                foreach (var (column, op, value) in parsedConditions)
                {
                    string actual = row[column]?.ToString()?.Trim() ?? "";
                    bool equals = string.Equals(actual, value, StringComparison.OrdinalIgnoreCase);
                    if (op == "eq" && !equals) return false;
                    if (op == "ne" && equals) return false;
                }
                return true;
            }).ToList();
        }

        #endregion

        #region Aggregation

        private object AggregateColumn(List<JToken> rows, FLRTGIDataSourceColumn col)
        {
            string colType = col.ColumnType ?? FLRTGIDataSource.GIColumnType.Decimal;
            string aggFunc = col.AggregateFunction ?? FLRTGIDataSourceColumn.AggregateFunctionType.Sum;
            string giColumn = col.GIColumn;

            if (rows == null || rows.Count == 0)
            {
                return GetDefaultValue(colType);
            }

            // COUNT doesn't care about column type
            if (aggFunc == FLRTGIDataSourceColumn.AggregateFunctionType.Count)
            {
                return rows.Count;
            }

            switch (colType)
            {
                case FLRTGIDataSource.GIColumnType.Decimal:
                case FLRTGIDataSource.GIColumnType.Integer:
                    return AggregateNumeric(rows, giColumn, aggFunc);

                case FLRTGIDataSource.GIColumnType.Boolean:
                    return AggregateBoolean(rows, giColumn, aggFunc);

                case FLRTGIDataSource.GIColumnType.Date:
                    return AggregateDate(rows, giColumn, aggFunc);

                case FLRTGIDataSource.GIColumnType.String:
                default:
                    return AggregateString(rows, giColumn, aggFunc);
            }
        }

        private decimal AggregateNumeric(List<JToken> rows, string column, string aggFunc)
        {
            var values = rows
                .Select(r => r[column]?.ToObject<decimal?>() ?? 0m)
                .ToList();

            switch (aggFunc)
            {
                case FLRTGIDataSourceColumn.AggregateFunctionType.Sum:
                    return values.Sum();
                case FLRTGIDataSourceColumn.AggregateFunctionType.Max:
                    return values.Max();
                case FLRTGIDataSourceColumn.AggregateFunctionType.Min:
                    return values.Min();
                case FLRTGIDataSourceColumn.AggregateFunctionType.First:
                    return values.FirstOrDefault();
                default:
                    return values.Sum();
            }
        }

        private object AggregateBoolean(List<JToken> rows, string column, string aggFunc)
        {
            var values = rows
                .Select(r =>
                {
                    var token = r[column];
                    if (token == null) return false;
                    if (token.Type == JTokenType.Boolean) return token.ToObject<bool>();
                    string s = token.ToString().Trim();
                    return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            switch (aggFunc)
            {
                case FLRTGIDataSourceColumn.AggregateFunctionType.Sum:
                    return values.Count(v => v);  // count of true values
                case FLRTGIDataSourceColumn.AggregateFunctionType.First:
                    return values.FirstOrDefault() ? 1 : 0;
                default:
                    return values.Count(v => v);
            }
        }

        private object AggregateDate(List<JToken> rows, string column, string aggFunc)
        {
            var dates = new List<DateTime>();
            foreach (var row in rows)
            {
                var token = row[column];
                if (token == null) continue;
                if (DateTime.TryParse(token.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                    dates.Add(dt);
            }

            if (dates.Count == 0) return (DateTime?)null;

            switch (aggFunc)
            {
                case FLRTGIDataSourceColumn.AggregateFunctionType.Max:
                    return dates.Max();
                case FLRTGIDataSourceColumn.AggregateFunctionType.Min:
                    return dates.Min();
                case FLRTGIDataSourceColumn.AggregateFunctionType.First:
                default:
                    return dates.First();
            }
        }

        private string AggregateString(List<JToken> rows, string column, string aggFunc)
        {
            // String only supports First
            var first = rows.FirstOrDefault();
            return first?[column]?.ToString() ?? "";
        }

        private object GetDefaultValue(string colType)
        {
            switch (colType)
            {
                case FLRTGIDataSource.GIColumnType.Decimal:
                case FLRTGIDataSource.GIColumnType.Integer:
                    return 0m;
                case FLRTGIDataSource.GIColumnType.Boolean:
                    return 0;
                case FLRTGIDataSource.GIColumnType.Date:
                    return (DateTime?)null;
                case FLRTGIDataSource.GIColumnType.String:
                default:
                    return "";
            }
        }

        #endregion

        #region Formula Evaluation

        /// <summary>
        /// Evaluates a simple arithmetic formula referencing other column aliases.
        /// Supports: +, -, *, /, parentheses, and numeric literals.
        /// Example: "REVENUE - TOTAL_COST" or "(SALES + SERVICES) / ORDER_COUNT"
        /// </summary>
        private decimal EvaluateFormula(string formula, Dictionary<string, object> values)
        {
            if (string.IsNullOrWhiteSpace(formula)) return 0m;

            // Replace alias references with their numeric values
            string expression = Regex.Replace(formula, @"[A-Za-z_][A-Za-z0-9_]*", match =>
            {
                string alias = match.Value;
                if (values.TryGetValue(alias, out object val))
                {
                    decimal numVal = ConvertToDecimal(val);
                    // Wrap negative numbers in parentheses to avoid expression errors
                    return numVal < 0 ? $"({numVal.ToString(CultureInfo.InvariantCulture)})" : numVal.ToString(CultureInfo.InvariantCulture);
                }
                PXTrace.WriteWarning($"[GIDataFetch] Formula reference '{alias}' not found in computed values. Using 0.");
                return "0";
            });

            try
            {
                return EvaluateArithmeticExpression(expression);
            }
            catch (Exception ex)
            {
                PXTrace.WriteWarning($"[GIDataFetch] Formula evaluation failed for '{formula}': {ex.Message}");
                return 0m;
            }
        }

        private decimal ConvertToDecimal(object value)
        {
            if (value == null) return 0m;
            if (value is decimal d) return d;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is double dbl) return (decimal)dbl;
            if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
                return parsed;
            return 0m;
        }

        /// <summary>
        /// Simple recursive-descent arithmetic evaluator for +, -, *, /, parentheses.
        /// </summary>
        private decimal EvaluateArithmeticExpression(string expr)
        {
            expr = expr.Trim();
            int pos = 0;
            decimal result = ParseExpression(expr, ref pos);
            return result;
        }

        private decimal ParseExpression(string expr, ref int pos)
        {
            decimal left = ParseTerm(expr, ref pos);
            while (pos < expr.Length)
            {
                SkipSpaces(expr, ref pos);
                if (pos >= expr.Length) break;
                char op = expr[pos];
                if (op != '+' && op != '-') break;
                pos++;
                decimal right = ParseTerm(expr, ref pos);
                left = op == '+' ? left + right : left - right;
            }
            return left;
        }

        private decimal ParseTerm(string expr, ref int pos)
        {
            decimal left = ParseFactor(expr, ref pos);
            while (pos < expr.Length)
            {
                SkipSpaces(expr, ref pos);
                if (pos >= expr.Length) break;
                char op = expr[pos];
                if (op != '*' && op != '/') break;
                pos++;
                decimal right = ParseFactor(expr, ref pos);
                left = op == '*' ? left * right : (right != 0 ? left / right : 0m);
            }
            return left;
        }

        private decimal ParseFactor(string expr, ref int pos)
        {
            SkipSpaces(expr, ref pos);
            if (pos >= expr.Length) return 0m;

            bool negative = false;
            if (expr[pos] == '-')
            {
                negative = true;
                pos++;
                SkipSpaces(expr, ref pos);
            }
            else if (expr[pos] == '+')
            {
                pos++;
                SkipSpaces(expr, ref pos);
            }

            decimal val;
            if (expr[pos] == '(')
            {
                pos++; // skip '('
                val = ParseExpression(expr, ref pos);
                SkipSpaces(expr, ref pos);
                if (pos < expr.Length && expr[pos] == ')')
                    pos++; // skip ')'
            }
            else
            {
                int start = pos;
                while (pos < expr.Length && (char.IsDigit(expr[pos]) || expr[pos] == '.'))
                    pos++;
                string numStr = expr.Substring(start, pos - start);
                decimal.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
            }

            return negative ? -val : val;
        }

        private void SkipSpaces(string expr, ref int pos)
        {
            while (pos < expr.Length && expr[pos] == ' ') pos++;
        }

        #endregion

        #region Value Formatting

        private string FormatValue(object value, string colType, string formatString)
        {
            if (value == null) return "";

            if (value is decimal dec)
            {
                return !string.IsNullOrWhiteSpace(formatString)
                    ? dec.ToString(formatString, CultureInfo.InvariantCulture)
                    : dec.ToString("N2", CultureInfo.InvariantCulture);
            }

            if (value is int intVal)
            {
                return !string.IsNullOrWhiteSpace(formatString)
                    ? intVal.ToString(formatString, CultureInfo.InvariantCulture)
                    : intVal.ToString();
            }

            if (value is DateTime dt)
            {
                return !string.IsNullOrWhiteSpace(formatString)
                    ? dt.ToString(formatString, CultureInfo.InvariantCulture)
                    : dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        #endregion

        #region OData Fetch

        // FetchWithFallbackAsync removed — fallback logic is now inline in FetchAndAggregate.

        private async Task<List<JToken>> PaginatedFetchAsync(string baseUrl, string filter, string selectColumns, string accessToken, List<string> errors)
        {
            var allResults = new List<JToken>();
            int pageSize = 5000;
            int skip = 0;

            while (true)
            {
                // Build query string from available parts
                var queryParts = new List<string>();
                if (!string.IsNullOrWhiteSpace(filter))
                    queryParts.Add($"$filter={Uri.EscapeDataString(filter)}");
                if (!string.IsNullOrWhiteSpace(selectColumns))
                    queryParts.Add($"$select={Uri.EscapeDataString(selectColumns)}");
                queryParts.Add($"$top={pageSize}");
                queryParts.Add($"$skip={skip}");
                string pagedUrl = $"{baseUrl}?{string.Join("&", queryParts)}";

                try
                {
                    HttpResponseMessage response;
                    using (var request = new HttpRequestMessage(HttpMethod.Get, pagedUrl))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                        response = await _httpClient.SendAsync(request);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        string snippet = body?.Length > 500 ? body.Substring(0, 500) : body;
                        errors.Add($"HTTP {(int)response.StatusCode} {response.StatusCode} from {baseUrl} — {snippet}");
                        return null;
                    }

                    string json = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                    {
                        string preview = json?.Length > 200 ? json.Substring(0, 200) : json;
                        errors.Add($"Non-JSON or empty response from {baseUrl}: {preview}");
                        return null;
                    }

                    JObject parsed = JObject.Parse(json);
                    var pageResults = (parsed["value"] as JArray)?.ToObject<List<JToken>>();

                    if (pageResults == null || pageResults.Count == 0) break;

                    allResults.AddRange(pageResults);
                    skip += pageSize;
                }
                catch (Exception ex)
                {
                    errors.Add($"Exception fetching {baseUrl}: {ex.Message}");
                    return null;
                }
            }

            return allResults;
        }

        #endregion
    }
}
