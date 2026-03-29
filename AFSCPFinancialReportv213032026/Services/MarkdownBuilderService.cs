using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FinancialReport.Helper;

namespace FinancialReport.Services
{
    /// <summary>
    /// Builds a structured markdown string from calculated financial report data.
    /// The markdown is used as the prompt payload for Gamma's AI presentation generation.
    ///
    /// Output structure:
    ///   - CFO-level instruction prompt (role, requirements, slide sections, analysis guidance)
    ///   - Raw financial figures per visible line item (CY, PM, PY)
    ///   Gamma's AI calculates percentage changes and generates the narrative.
    /// </summary>
    public class MarkdownBuilderService
    {
        private static readonly string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        /// <summary>
        /// Builds the complete markdown prompt from the report header, line items, and calculated results.
        /// </summary>
        /// <param name="report">The FLRTFinancialReport record (provides period, org, branch, ledger, title, description).</param>
        /// <param name="definitions">Each definition paired with its ordered line items.</param>
        /// <param name="results">Dictionary from ReportCalculationEngine.CalculateAll() — keyed PREFIX_LINECODE_CY/PM/PY.</param>
        public string Build(
            FLRTFinancialReport report,
            List<(ReportCalculationEngine.DefinitionLink DefLink, List<FLRTReportLineItem> Items)> definitions,
            Dictionary<string, string> results)
        {
            // ── Derive period labels ───────────────────────────────────────────────
            int month = int.TryParse(report.FinancialMonth, out int m) ? m : 12;
            if (month < 1 || month > 12) month = 12;
            int year  = int.TryParse(report.CurrYear,       out int y) ? y : DateTime.Now.Year;

            int prevMonth     = month == 1 ? 12 : month - 1;
            int prevMonthYear = month == 1 ? year - 1 : year;

            string cyLabel = $"{MonthNames[month - 1]} {year}";
            string pmLabel = $"{MonthNames[prevMonth - 1]} {prevMonthYear}";
            string pyLabel = $"{MonthNames[month - 1]} {year - 1}";

            var sb = new StringBuilder();

            // ── Report context ─────────────────────────────────────────────────────
            string title = $"{cyLabel} Financial Report";

            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine($"**Organization:** {report.Organization ?? "N/A"} | **Branch:** {report.Branch ?? "N/A"} | **Ledger:** {report.Ledger ?? "N/A"}");
            sb.AppendLine($"**Reporting Period:** {cyLabel}");
            sb.AppendLine($"**Prior Month:** {pmLabel}");
            sb.AppendLine($"**Prior Year (same month):** {pyLabel}");
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();

            // ── Default CFO prompt ────────────────────────────────────────────────
            {
                // Default CFO prompt ───────────────────────────────────────────────
                sb.AppendLine("You are a **Chief Financial Officer (CFO)** preparing a professional financial analysis presentation for senior management and board members.");
                sb.AppendLine();
                sb.AppendLine("Your task is to analyze the financial data provided below and produce a professional **slide deck**.");
                sb.AppendLine();
                sb.AppendLine("## Presentation Requirements");
                sb.AppendLine();
                sb.AppendLine("The presentation should:");
                sb.AppendLine("- Be written in a professional CFO-level tone");
                sb.AppendLine("- Provide insights, not just repeat numbers");
                sb.AppendLine("- Highlight key trends, risks, and opportunities");
                sb.AppendLine("- Include recommendations where appropriate");
                sb.AppendLine("- Suggest charts where useful (bar chart, trend chart, waterfall, etc.)");
                sb.AppendLine("- **Calculate percentage changes yourself** (MoM %, YoY %) from the raw figures provided. Round to 1 decimal place.");
                sb.AppendLine();
                sb.AppendLine("## Slide Structure");
                sb.AppendLine();
                sb.AppendLine("Create a structured slide deck with the following sections:");
                sb.AppendLine();
                sb.AppendLine("1. Executive Summary");
                sb.AppendLine("2. Financial Position Overview");
                sb.AppendLine("3. Asset Analysis");
                sb.AppendLine("4. Income Performance");
                sb.AppendLine("5. Expense Analysis");
                sb.AppendLine("6. Equity and Capital Structure");
                sb.AppendLine("7. Liability Analysis");
                sb.AppendLine("8. Month-over-Month Key Movements");
                sb.AppendLine("9. Financial Health Assessment");
                sb.AppendLine("10. Risks and Observations");
                sb.AppendLine("11. Strategic Recommendations");
                sb.AppendLine("12. Key Takeaways");
                sb.AppendLine();
                sb.AppendLine("Each slide should include:");
                sb.AppendLine("- **Slide Title**");
                sb.AppendLine("- **Key bullet insights**");
                sb.AppendLine("- **Important figures referenced**");
                sb.AppendLine("- **Suggested chart type**");
                sb.AppendLine();
                sb.AppendLine("## Analysis Guidance");
                sb.AppendLine();
                sb.AppendLine("Use management-level analysis such as:");
                sb.AppendLine("- Operational performance");
                sb.AppendLine("- Balance sheet movement");
                sb.AppendLine("- Cost control effectiveness");
                sb.AppendLine("- Sustainability of revenue growth");
                sb.AppendLine("- Financial stability indicators");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Financial Data");
            sb.AppendLine();

            // ── Line items (raw figures only — let Gamma calculate % changes) ──────
            foreach (var (defLink, items) in definitions)
            {
                var visibleItems = items.Where(l => l.IsVisible == true).ToList();
                if (!visibleItems.Any()) continue;

                foreach (var line in visibleItems)
                {
                    string keyBase = $"{defLink.Prefix}_{line.LineCode}";
                    string cyVal   = GetValue(results, keyBase + "_" + Constants.CurrentYearSuffix);
                    string pmVal   = GetValue(results, keyBase + "_" + Constants.PreviousMonthSuffix);
                    string pyVal   = GetValue(results, keyBase + "_" + Constants.PreviousYearSuffix);

                    string label = !string.IsNullOrWhiteSpace(line.Description)
                        ? line.Description
                        : line.LineCode;

                    sb.AppendLine($"### {label}");
                    sb.AppendLine($"- {cyLabel}: {cyVal}");
                    sb.AppendLine($"- {pmLabel}: {pmVal}");
                    sb.AppendLine($"- {pyLabel}: {pyVal}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds the markdown prompt from a FLRTPresentationGeneration record.
        /// Overload with GI data sources for mixed or purely GI-driven presentations.
        /// </summary>
        public string Build(
            FLRTPresentationGeneration presentation,
            List<(ReportCalculationEngine.DefinitionLink DefLink, List<FLRTReportLineItem> Items)> definitions,
            Dictionary<string, string> results,
            List<(FLRTGIDataSource DS, List<FLRTGIDataSourceColumn> Columns)> giDataSources = null)
        {
            int month = int.TryParse(presentation.FinancialMonth, out int m) ? m : 12;
            if (month < 1 || month > 12) month = 12;
            int year  = int.TryParse(presentation.CurrYear, out int y) ? y : DateTime.Now.Year;

            int prevMonth     = month == 1 ? 12 : month - 1;
            int prevMonthYear = month == 1 ? year - 1 : year;

            string cyLabel = $"{MonthNames[month - 1]} {year}";
            string pmLabel = $"{MonthNames[prevMonth - 1]} {prevMonthYear}";
            string pyLabel = $"{MonthNames[month - 1]} {year - 1}";

            var sb = new StringBuilder();

            string title = !string.IsNullOrWhiteSpace(presentation.PresentationTitle)
                ? presentation.PresentationTitle
                : $"{cyLabel} Financial Report";

            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine($"**Organization:** {presentation.Organization ?? "N/A"} | **Branch:** {presentation.Branch ?? "N/A"} | **Ledger:** {presentation.Ledger ?? "N/A"}");
            sb.AppendLine($"**Reporting Period:** {cyLabel}");
            sb.AppendLine($"**Prior Month:** {pmLabel}");
            sb.AppendLine($"**Prior Year (same month):** {pyLabel}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(presentation.PresentationDescription))
            {
                sb.AppendLine(presentation.PresentationDescription.Trim());
            }
            else
            {
                sb.AppendLine("You are a **Chief Financial Officer (CFO)** preparing a professional financial analysis presentation for senior management and board members.");
                sb.AppendLine();
                sb.AppendLine("Your task is to analyze the financial data provided below and produce a professional **slide deck**.");
                sb.AppendLine();
                sb.AppendLine("## Presentation Requirements");
                sb.AppendLine();
                sb.AppendLine("The presentation should:");
                sb.AppendLine("- Be written in a professional CFO-level tone");
                sb.AppendLine("- Provide insights, not just repeat numbers");
                sb.AppendLine("- Highlight key trends, risks, and opportunities");
                sb.AppendLine("- Include recommendations where appropriate");
                sb.AppendLine("- Suggest charts where useful (bar chart, trend chart, waterfall, etc.)");
                sb.AppendLine("- **Calculate percentage changes yourself** (MoM %, YoY %) from the raw figures provided. Round to 1 decimal place.");
                sb.AppendLine();
                sb.AppendLine("## Slide Structure");
                sb.AppendLine();
                sb.AppendLine("Create a structured slide deck with the following sections:");
                sb.AppendLine();
                sb.AppendLine("1. Executive Summary");
                sb.AppendLine("2. Financial Position Overview");
                sb.AppendLine("3. Asset Analysis");
                sb.AppendLine("4. Income Performance");
                sb.AppendLine("5. Expense Analysis");
                sb.AppendLine("6. Equity and Capital Structure");
                sb.AppendLine("7. Liability Analysis");
                sb.AppendLine("8. Month-over-Month Key Movements");
                sb.AppendLine("9. Financial Health Assessment");
                sb.AppendLine("10. Risks and Observations");
                sb.AppendLine("11. Strategic Recommendations");
                sb.AppendLine("12. Key Takeaways");
                sb.AppendLine();
                sb.AppendLine("Each slide should include:");
                sb.AppendLine("- **Slide Title**");
                sb.AppendLine("- **Key bullet insights**");
                sb.AppendLine("- **Important figures referenced**");
                sb.AppendLine("- **Suggested chart type**");
                sb.AppendLine();
                sb.AppendLine("## Analysis Guidance");
                sb.AppendLine();
                sb.AppendLine("Use management-level analysis such as:");
                sb.AppendLine("- Operational performance");
                sb.AppendLine("- Balance sheet movement");
                sb.AppendLine("- Cost control effectiveness");
                sb.AppendLine("- Sustainability of revenue growth");
                sb.AppendLine("- Financial stability indicators");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Financial Data");
            sb.AppendLine();

            // ── TB Definition line items ──────────────────────────────────────
            if (definitions != null && definitions.Any())
            {
                foreach (var (defLink, items) in definitions)
                {
                    var visibleItems = items.Where(l => l.IsVisible == true).ToList();
                    if (!visibleItems.Any()) continue;

                    foreach (var line in visibleItems)
                    {
                        string keyBase = $"{defLink.Prefix}_{line.LineCode}";
                        string cyVal   = GetValue(results, keyBase + "_" + Constants.CurrentYearSuffix);
                        string pmVal   = GetValue(results, keyBase + "_" + Constants.PreviousMonthSuffix);
                        string pyVal   = GetValue(results, keyBase + "_" + Constants.PreviousYearSuffix);

                        string label = !string.IsNullOrWhiteSpace(line.Description) ? line.Description : line.LineCode;

                        sb.AppendLine($"### {label}");
                        sb.AppendLine($"- {cyLabel}: {cyVal}");
                        sb.AppendLine($"- {pmLabel}: {pmVal}");
                        sb.AppendLine($"- {pyLabel}: {pyVal}");
                        sb.AppendLine();
                    }
                }
            }

            // ── GI Data Source placeholders ────────────────────────────────────
            if (giDataSources != null && giDataSources.Any())
            {
                sb.AppendLine("## Additional Data (Generic Inquiries)");
                sb.AppendLine();

                foreach (var (ds, columns) in giDataSources)
                {
                    string prefix = ds.Prefix ?? "";
                    string dsLabel = !string.IsNullOrWhiteSpace(ds.Description) ? ds.Description : ds.DataSourceCD;
                    sb.AppendLine($"### {dsLabel}");
                    sb.AppendLine();

                    var visibleColumns = columns
                        .Where(c => c.IsVisible == true && c.LineType != FLRTGIDataSourceColumn.ColumnLineType.Heading)
                        .ToList();

                    foreach (var col in visibleColumns)
                    {
                        string colLabel = !string.IsNullOrWhiteSpace(col.Description) ? col.Description : col.ColumnAlias;
                        string baseKey = string.IsNullOrWhiteSpace(prefix)
                            ? col.ColumnAlias
                            : $"{prefix}_{col.ColumnAlias}";

                        if (col.LineType == FLRTGIDataSourceColumn.ColumnLineType.MultiRow)
                        {
                            // Find all expanded keys: PREFIX_ALIAS_RANK_PropName
                            int limit = col.RowLimit ?? 10;

                            // If DisplayColumns is set, filter which properties appear in markdown
                            HashSet<string> displayCols = null;
                            if (!string.IsNullOrWhiteSpace(col.DisplayColumns))
                            {
                                displayCols = new HashSet<string>(
                                    col.DisplayColumns.Split(',').Select(c => c.Trim()).Where(c => c.Length > 0),
                                    StringComparer.OrdinalIgnoreCase);
                            }

                            sb.AppendLine($"- **{colLabel}:**");
                            for (int rank = 1; rank <= limit; rank++)
                            {
                                string rankPrefix = $"{baseKey}_{rank}_";
                                var rankKeys = results != null
                                    ? results.Keys.Where(k => k.StartsWith(rankPrefix, StringComparison.OrdinalIgnoreCase)).OrderBy(k => k).ToList()
                                    : new List<string>();
                                if (!rankKeys.Any()) break;

                                // Apply display column filter if configured
                                if (displayCols != null)
                                    rankKeys = rankKeys.Where(k => displayCols.Contains(k.Substring(rankPrefix.Length))).ToList();

                                var parts = rankKeys.Select(k => $"{k.Substring(rankPrefix.Length)}: {results[k]}");
                                sb.AppendLine($"  - Row {rank}: {string.Join(", ", parts)}");
                            }
                        }
                        else
                        {
                            string value = GetValue(results, baseKey);
                            sb.AppendLine($"- **{colLabel}:** {value}");
                        }
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private string GetValue(Dictionary<string, string> results, string key)
        {
            if (results != null && results.TryGetValue(key, out string val) && !string.IsNullOrEmpty(val))
                return val;
            return "0";
        }
    }
}
