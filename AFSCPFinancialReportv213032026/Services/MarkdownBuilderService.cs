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
    /// </summary>
    public class MarkdownBuilderService
    {
        private static readonly string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        public string Build(
            FLRTFinancialReport report,
            List<(ReportCalculationEngine.DefinitionLink DefLink, List<FLRTReportLineItem> Items)> definitions,
            Dictionary<string, string> results)
        {
            var labels = DeriveLabels(report.FinancialMonth, report.CurrYear);
            var sb = new StringBuilder();

            string title = $"{labels.Cy} Financial Report";
            AppendHeader(sb, title, report.Organization, report.Branch, report.Ledger, labels);
            AppendInstructions(sb, labels);
            AppendDataHeader(sb);
            AppendTBLineItems(sb, definitions, results, labels, hasAnySection: false);
            AppendFooter(sb);

            return sb.ToString();
        }

        public string Build(
            FLRTPresentationGeneration presentation,
            List<(ReportCalculationEngine.DefinitionLink DefLink, List<FLRTReportLineItem> Items)> definitions,
            Dictionary<string, string> results,
            List<(FLRTGIDataSource DS, List<FLRTGIDataSourceColumn> Columns)> giDataSources = null)
        {
            var labels = DeriveLabels(presentation.FinancialMonth, presentation.CurrYear);
            var sb = new StringBuilder();

            string title = !string.IsNullOrWhiteSpace(presentation.PresentationTitle)
                ? presentation.PresentationTitle
                : $"{labels.Cy} Financial Report";

            AppendHeader(sb, title, presentation.Organization, presentation.Branch, presentation.Ledger, labels);
            AppendInstructions(sb, labels);
            AppendDataHeader(sb);

            bool hasTB = definitions != null && definitions.Any(d => d.Items.Any(l => l.IsVisible == true));
            bool hasGI = giDataSources != null && giDataSources.Any();

            if (hasTB)
            {
                sb.AppendLine("### Financial Report Lines");
                sb.AppendLine();
                sb.AppendLine($"Each metric below shows two fiscal-year periods for comparison: current (**{labels.Cy}**) and prior (**{labels.Py}**).");
                sb.AppendLine();
                AppendTBLineItems(sb, definitions, results, labels, hasAnySection: true);
            }

            if (hasGI)
            {
                sb.AppendLine("### Supplementary Data (Generic Inquiries)");
                sb.AppendLine();
                sb.AppendLine("Aggregated business-transaction figures derived from Acumatica Generic Inquiries. Use these to enrich narrative with operational context.");
                sb.AppendLine();
                AppendGIDataSources(sb, giDataSources, results);
            }

            AppendFooter(sb);

            return sb.ToString();
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────────

        private struct PeriodLabels
        {
            public string Cy;
            public string Py;
        }

        private static PeriodLabels DeriveLabels(string financialMonth, string currYear)
        {
            int month = int.TryParse(financialMonth, out int m) ? m : 12;
            if (month < 1 || month > 12) month = 12;
            int year = int.TryParse(currYear, out int y) ? y : DateTime.Now.Year;

            // FY model: FinancialMonth = FY start. FY end month = month - 1 (wraps Jan→Dec).
            int fyEndMonth = month == 1 ? 12 : month - 1;

            return new PeriodLabels
            {
                Cy = $"FY{year} ({MonthNames[fyEndMonth - 1]} {year})",
                Py = $"FY{year - 1} ({MonthNames[fyEndMonth - 1]} {year - 1})"
            };
        }

        private static void AppendHeader(StringBuilder sb, string title, string org, string branch, string ledger, PeriodLabels labels)
        {
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine("## Report Context");
            sb.AppendLine();
            sb.AppendLine($"- **Reporting Period:** {labels.Cy}");
            sb.AppendLine($"- **Organization:** {org ?? "N/A"}");
            sb.AppendLine($"- **Branch:** {branch ?? "N/A"}");
            sb.AppendLine($"- **Ledger:** {ledger ?? "N/A"}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        private static void AppendInstructions(StringBuilder sb, PeriodLabels labels)
        {
            sb.AppendLine("## Your Task");
            sb.AppendLine();
            sb.AppendLine($"Create a professional monthly financial report slide deck for **{labels.Cy}** using only the data provided in the **Data** section below.");
            sb.AppendLine();

            sb.AppendLine("### Audience");
            sb.AppendLine("Senior management and board members.");
            sb.AppendLine();

            sb.AppendLine("### Tone");
            sb.AppendLine("Executive, concise, insight-driven. Interpret the numbers — do not merely repeat them. Emphasize direction, materiality, and trend.");
            sb.AppendLine();

            sb.AppendLine("### Recommended Slide Flow");
            sb.AppendLine("Adapt to whichever data sections are populated below. Typical structure:");
            sb.AppendLine();
            sb.AppendLine("1. **Title slide** — report name, period, organization/branch/ledger");
            sb.AppendLine("2. **Executive summary** — 3-5 headline insights with supporting figures");
            sb.AppendLine("3. **Supplementary data highlights** — callouts from Generic Inquiry sections");
            sb.AppendLine("4. **Observations & risks** — 3-5 items worth flagging to leadership");
            sb.AppendLine("5. **Recommendations** — actionable next steps");
            sb.AppendLine("6. **Key takeaways** — 3-5 closing bullets");
            sb.AppendLine();

            sb.AppendLine("### Each Slide Should Include");
            sb.AppendLine("- **Clear, specific title**");
            sb.AppendLine("- **3-5 bullet insights** (narrative, not just raw figures)");
            sb.AppendLine("- **Key figures cited** (preserve the formatting shown in the data)");
            sb.AppendLine("- **Suggested chart type** (bar, line, waterfall, pie, table)");
            sb.AppendLine();

            sb.AppendLine("### Formatting Guidelines");
            sb.AppendLine("- Use tables for multi-metric comparisons");
            sb.AppendLine("- Use bullet lists for narrative insights");
            sb.AppendLine("- Bold the most material figures");
            sb.AppendLine("- Keep each slide visually balanced — no more than 7 bullets");
            sb.AppendLine("- Do not invent data not present below; if a figure is missing, note \"N/A\"");
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();
        }

        private static void AppendDataHeader(StringBuilder sb)
        {
            sb.AppendLine("## Data");
            sb.AppendLine();
        }

        private static void AppendFooter(StringBuilder sb)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("End of data. Generate the slide deck now.");
        }

        private static void AppendTBLineItems(
            StringBuilder sb,
            List<(ReportCalculationEngine.DefinitionLink DefLink, List<FLRTReportLineItem> Items)> definitions,
            Dictionary<string, string> results,
            PeriodLabels labels,
            bool hasAnySection)
        {
            if (definitions == null || !definitions.Any()) return;

            foreach (var (defLink, items) in definitions)
            {
                var visibleItems = items.Where(l => l.IsVisible == true).ToList();
                if (!visibleItems.Any()) continue;

                foreach (var line in visibleItems)
                {
                    string keyBase = $"{defLink.Prefix}_{line.LineCode}";
                    string cyVal = GetValue(results, keyBase + "_" + Constants.CurrentYearSuffix);
                    string pyVal = GetValue(results, keyBase + "_" + Constants.PreviousYearSuffix);

                    string label = !string.IsNullOrWhiteSpace(line.Description)
                        ? line.Description
                        : line.LineCode;

                    sb.AppendLine($"#### {label}");
                    sb.AppendLine();
                    sb.AppendLine("| Period | Value |");
                    sb.AppendLine("|--------|-------|");
                    sb.AppendLine($"| {labels.Cy} | {cyVal} |");
                    sb.AppendLine($"| {labels.Py} | {pyVal} |");
                    sb.AppendLine();
                }
            }
        }

        private static void AppendGIDataSources(
            StringBuilder sb,
            List<(FLRTGIDataSource DS, List<FLRTGIDataSourceColumn> Columns)> giDataSources,
            Dictionary<string, string> results)
        {
            foreach (var (ds, columns) in giDataSources)
            {
                string prefix = ds.Prefix ?? "";
                string dsLabel = !string.IsNullOrWhiteSpace(ds.Description) ? ds.Description : ds.DataSourceCD;
                sb.AppendLine($"#### {dsLabel}");
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
                        int limit = col.RowLimit ?? 10;

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

        private static string GetValue(Dictionary<string, string> results, string key)
        {
            if (results != null && results.TryGetValue(key, out string val) && !string.IsNullOrEmpty(val))
                return val;
            return "0";
        }
    }
}
