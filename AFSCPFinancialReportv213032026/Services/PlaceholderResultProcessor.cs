using Newtonsoft.Json.Linq;
using PX.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static FinancialReport.Services.FinancialDataService;

namespace FinancialReport.Services
{
    public class PlaceholderResultProcessor
    {
        public Dictionary<string, string> ProcessApiResults(List<PlaceholderRequest> requests, Dictionary<string, List<JToken>> apiResults)
        {
            var placeholderValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var request in requests)
            {
                if (!apiResults.TryGetValue(request.ApiCall, out var results) || results == null)
                {
                    // No data found - set all placeholders to "0"
                    foreach (var placeholder in request.Placeholders)
                    {
                        placeholderValues[placeholder] = "0";
                    }
                    continue;
                }

                // Process each placeholder for this API call
                foreach (var placeholder in request.Placeholders)
                {
                    string value = ExtractValueForPlaceholder(placeholder, results);
                    placeholderValues[placeholder] = value;
                }
            }

            return placeholderValues;
        }

        private string ExtractValueForPlaceholder(string placeholder, List<JToken> results)
        {
            string cleanKey = placeholder.Trim('{', '}');

            try
            {
                // Simple account: A39101_CY or 100-10_CY -> EndingBalance
                if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_(CY|PY)$"))
                {
                    decimal sum = results.Sum(r => r["EndingBalance"]?.ToObject<decimal>() ?? 0);
                    return sum.ToString("#,##0");
                }

                // Sum prefix: Sum3_B69_CY or Sum3_100_CY -> EndingBalance sum
                if (Regex.IsMatch(cleanKey, @"^Sum\d+_[A-Z0-9\-]*_(CY|PY)$"))
                {
                    decimal sum = results.Sum(r => r["EndingBalance"]?.ToObject<decimal>() ?? 0);
                    return sum.ToString("#,##0");
                }

                // Debit sum: DebitSum3_B53_CY or DebitSum3_100_CY -> Debit sum
                if (Regex.IsMatch(cleanKey, @"^DebitSum\d+_[A-Z0-9\-]*_(CY|PY)$"))
                {
                    decimal sum = results.Sum(r => r["Debit"]?.ToObject<decimal>() ?? 0);
                    return sum.ToString("#,##0");
                }

                // Credit sum: CreditSum3_B53_CY or CreditSum3_100_CY -> Credit sum
                if (Regex.IsMatch(cleanKey, @"^CreditSum\d+_[A-Z0-9\-]*_(CY|PY)$"))
                {
                    decimal sum = results.Sum(r => r["Credit"]?.ToObject<decimal>() ?? 0);
                    return sum.ToString("#,##0");
                }

                // Beginning sum: BegSum3_A11_CY or BegSum3_100_CY -> BeginningBalance sum
                if (Regex.IsMatch(cleanKey, @"^BegSum\d+_[A-Z0-9\-]*_(CY|PY)$"))
                {
                    decimal sum = results.Sum(r => r["BeginningBalance"]?.ToObject<decimal>() ?? 0);
                    return sum.ToString("#,##0");
                }

                // January balance: A21101_Jan1_CY or 100-10_Jan1_CY -> BeginningBalance
                if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_Jan1_(CY|PY)$"))
                {
                    decimal sum = results.Sum(r => r["BeginningBalance"]?.ToObject<decimal>() ?? 0);
                    return sum.ToString("#,##0");
                }

                // Specific balance - debit: A34101_debit_CY or 100-10_debit_CY -> Sum of all Debit
                if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_debit_(CY|PY)$"))
                {
                    decimal sum = results.Sum(r => r["Debit"]?.ToObject<decimal>() ?? 0);
                    return sum.ToString("#,##0");
                }

                // Specific balance - credit: A34101_credit_CY or 100-10_credit_CY -> Sum of all Credit
                if (Regex.IsMatch(cleanKey, @"^[A-Z0-9\-]+_credit_(CY|PY)$"))
                {
                    decimal sum = results.Sum(r => r["Credit"]?.ToObject<decimal>() ?? 0);
                    return sum.ToString("#,##0");
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
}