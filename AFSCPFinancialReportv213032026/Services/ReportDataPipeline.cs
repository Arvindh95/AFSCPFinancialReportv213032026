using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FinancialReport.Helper;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;

namespace FinancialReport.Services
{
    /// <summary>
    /// Shared pipeline for loading report definitions, line items, and computing periods.
    /// Used by both ReportGenerationService and SlideGenerationService to eliminate duplication.
    ///
    /// ReportGenerationService: calls BuildContext() then does its own conditional GL fetch + engine run.
    /// SlideGenerationService:  calls FetchAndCalculate() which wraps BuildContext + simple GL fetch + engine run.
    /// </summary>
    public class ReportDataPipeline
    {
        /// <summary>
        /// Shared context produced by BuildContext — contains definitions, line items, column mapping, and period strings.
        /// </summary>
        public class Context
        {
            public List<ReportCalculationEngine.DefinitionLink> DefinitionLinks { get; set; }
            public List<(ReportCalculationEngine.DefinitionLink DefLink, List<FLRTReportLineItem> Items)> DefinitionsWithItems { get; set; }
            public GIColumnMapping ColumnMapping { get; set; }

            // Period strings computed from the report record
            public string CurrYear             { get; set; }
            public string PrevYear             { get; set; }
            // Point-in-time periods = FY END month of each fiscal year (used for Ending/Beginning).
            public string SelectedPeriod       { get; set; } // FY end of CurrYear (e.g. Jul 2025 for Aug-start FY2025)
            public string PrevYearPeriod       { get; set; } // FY end of CurrYear-1
            public string PrevYearPriorPeriod  { get; set; } // FY end of CurrYear-2
            // FY start periods = first month of FY (used for YTD range start).
            public string CyFyStartPeriod      { get; set; } // FY start of CurrYear (e.g. Aug 2024 for Aug-start FY2025)
            public string PyFyStartPeriod      { get; set; } // FY start of CurrYear-1
        }

        /// <summary>
        /// Loads definition links and their line items, and computes period strings.
        /// Shared by both ReportGenerationService and SlideGenerationService.
        /// </summary>
        public static Context BuildContext(PXGraph graph, FLRTFinancialReport record)
        {
            if (graph == null)   throw new ArgumentNullException(nameof(graph));
            if (record == null)  throw new ArgumentNullException(nameof(record));

            // ── 1. Load definition links ──────────────────────────────────────────
            GIColumnMapping columnMapping = null;
            var definitionLinks = new List<ReportCalculationEngine.DefinitionLink>();

            var linkedDefs = SelectFrom<FLRTReportDefinitionLink>
                .InnerJoin<FLRTReportDefinition>
                    .On<FLRTReportDefinition.definitionID.IsEqual<FLRTReportDefinitionLink.definitionID>>
                .Where<FLRTReportDefinitionLink.reportID.IsEqual<@P.AsInt>>
                .OrderBy<FLRTReportDefinitionLink.displayOrder.Asc>
                .View.Select(graph, record.ReportID)
                .Cast<PXResult<FLRTReportDefinitionLink, FLRTReportDefinition>>()
                .ToList();

            if (linkedDefs.Any())
            {
                var firstDef = linkedDefs.First().GetItem<FLRTReportDefinition>();
                columnMapping = GIColumnMapping.FromDefinition(firstDef);

                foreach (var result in linkedDefs)
                {
                    var def = result.GetItem<FLRTReportDefinition>();
                    definitionLinks.Add(new ReportCalculationEngine.DefinitionLink
                    {
                        DefinitionID = def.DefinitionID.Value,
                        Prefix       = def.DefinitionPrefix,
                        Rounding     = RoundingSettings.FromDefinition(def)
                    });
                }

                PXTrace.WriteInformation($"[Pipeline] {definitionLinks.Count} definition(s) linked — prefixes: [{string.Join(", ", definitionLinks.Select(d => d.Prefix))}]");
            }
            else if (record.DefinitionID != null)
            {
                var reportDef = SelectFrom<FLRTReportDefinition>
                    .Where<FLRTReportDefinition.definitionID.IsEqual<@P.AsInt>>
                    .View.Select(graph, record.DefinitionID)
                    .TopFirst;

                if (reportDef != null)
                {
                    columnMapping = GIColumnMapping.FromDefinition(reportDef);
                    definitionLinks.Add(new ReportCalculationEngine.DefinitionLink
                    {
                        DefinitionID = reportDef.DefinitionID.Value,
                        Prefix       = reportDef.DefinitionPrefix,
                        Rounding     = RoundingSettings.FromDefinition(reportDef)
                    });
                    PXTrace.WriteInformation($"[Pipeline] Legacy single definition '{reportDef.DefinitionCD}' (prefix: {reportDef.DefinitionPrefix}).");
                }
            }

            // ── 2. Collect line items per definition ──────────────────────────────
            var definitionsWithItems = new List<(ReportCalculationEngine.DefinitionLink, List<FLRTReportLineItem>)>();
            foreach (var defLink in definitionLinks)
            {
                var items = SelectFrom<FLRTReportLineItem>
                    .Where<FLRTReportLineItem.definitionID.IsEqual<@P.AsInt>>
                    .OrderBy<FLRTReportLineItem.sortOrder.Asc>
                    .View.Select(graph, defLink.DefinitionID)
                    .RowCast<FLRTReportLineItem>()
                    .ToList();

                definitionsWithItems.Add((defLink, items));
            }

            // ── 3. Compute period strings ─────────────────────────────────────────
            // FY model: FinancialMonth = FY start month. CurrYear = FY end year.
            // FY end month = FinancialMonth - 1 (wraps Jan → Dec).
            // e.g. FinMonth=Aug, CurrYear=2025 → FY2025 = Aug 2024 → Jul 2025.
            //      FinMonth=Jan, CurrYear=2025 → FY2025 = Jan 2025 → Dec 2025 (calendar).
            string currYear      = record.CurrYear ?? DateTime.Now.ToString("yyyy");
            string selectedMonth = record.FinancialMonth ?? "12";
            int currYearInt      = int.TryParse(currYear, out int y) ? y : DateTime.Now.Year;
            int selectedMonthInt = int.TryParse(selectedMonth, out int m) ? m : 12;

            int fyEndMonthInt    = selectedMonthInt == 1 ? 12 : selectedMonthInt - 1;
            string fyEndMonth    = fyEndMonthInt.ToString("D2");
            int cyFyStartYear    = selectedMonthInt == 1 ? currYearInt : currYearInt - 1;

            string prevYear            = (currYearInt - 1).ToString();
            string selectedPeriod      = $"{fyEndMonth}{currYearInt}";
            string prevYearPeriod      = $"{fyEndMonth}{currYearInt - 1}";
            string prevYearPriorPeriod = $"{fyEndMonth}{currYearInt - 2}";
            string cyFyStartPeriod     = $"{selectedMonth}{cyFyStartYear}";
            string pyFyStartPeriod     = $"{selectedMonth}{cyFyStartYear - 1}";

            return new Context
            {
                DefinitionLinks      = definitionLinks,
                DefinitionsWithItems = definitionsWithItems,
                ColumnMapping        = columnMapping,
                CurrYear             = currYear,
                PrevYear             = prevYear,
                SelectedPeriod       = selectedPeriod,
                PrevYearPeriod       = prevYearPeriod,
                PrevYearPriorPeriod  = prevYearPriorPeriod,
                CyFyStartPeriod      = cyFyStartPeriod,
                PyFyStartPeriod      = pyFyStartPeriod
            };
        }

        /// <summary>
        /// Full pipeline for slide/markdown generation: builds context, fetches GL data, runs the engine.
        /// Fetches CY, PY, Prior point-in-time (FY-end) balances plus CY/PY YTD ranges.
        /// Used by SlideGenerationService only.
        /// </summary>
        public static Dictionary<string, string> FetchAndCalculate(
            Context ctx,
            PXGraph graph,
            FLRTFinancialReport record,
            AuthService authService,
            string tenantName,
            CancellationToken cancellationToken = default)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var dataService = new FinancialDataService(authService, tenantName, ctx.ColumnMapping);

            var taskCY       = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.SelectedPeriod,      false, cancellationToken), cancellationToken);
            var taskPY       = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.PrevYearPeriod,      false, cancellationToken), cancellationToken);
            var taskPrior    = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.PrevYearPriorPeriod, false, cancellationToken), cancellationToken);
            var taskRangeCY  = Task.Run(() => dataService.FetchRangeApiData(record.Branch, record.Organization, record.Ledger, ctx.CyFyStartPeriod, ctx.SelectedPeriod, cancellationToken), cancellationToken);
            var taskRangePY  = Task.Run(() => dataService.FetchRangeApiData(record.Branch, record.Organization, record.Ledger, ctx.PyFyStartPeriod, ctx.PrevYearPeriod, cancellationToken), cancellationToken);

            Task.WhenAll(taskCY, taskPY, taskPrior, taskRangeCY, taskRangePY).Wait(cancellationToken);

            var engine = new ReportCalculationEngine(graph);
            return engine.CalculateAll(
                ctx.DefinitionLinks,
                taskCY.Result,
                taskPY.Result,
                cyOpeningData:    taskPY.Result,
                pyOpeningData:    taskPrior.Result,
                cyCumulativeData: taskRangeCY.Result,
                pyCumulativeData: taskRangePY.Result);
        }

        // ── Presentation Generation overloads ─────────────────────────────────────

        /// <summary>
        /// Builds a pipeline context from a FLRTPresentationGeneration record.
        /// Queries FLRTPresentationDefinitionLink instead of FLRTReportDefinitionLink.
        /// </summary>
        public static Context BuildContext(PXGraph graph, FLRTPresentationGeneration record)
        {
            if (graph == null)  throw new ArgumentNullException(nameof(graph));
            if (record == null) throw new ArgumentNullException(nameof(record));

            GIColumnMapping columnMapping = null;
            var definitionLinks = new List<ReportCalculationEngine.DefinitionLink>();

            var linkedDefs = SelectFrom<FLRTPresentationDefinitionLink>
                .InnerJoin<FLRTReportDefinition>
                    .On<FLRTReportDefinition.definitionID.IsEqual<FLRTPresentationDefinitionLink.definitionID>>
                .Where<FLRTPresentationDefinitionLink.presentationID.IsEqual<@P.AsInt>>
                .OrderBy<FLRTPresentationDefinitionLink.displayOrder.Asc>
                .View.Select(graph, record.PresentationID)
                .Cast<PXResult<FLRTPresentationDefinitionLink, FLRTReportDefinition>>()
                .ToList();

            if (linkedDefs.Any())
            {
                var firstDef = linkedDefs.First().GetItem<FLRTReportDefinition>();
                columnMapping = GIColumnMapping.FromDefinition(firstDef);

                foreach (var result in linkedDefs)
                {
                    var def = result.GetItem<FLRTReportDefinition>();
                    definitionLinks.Add(new ReportCalculationEngine.DefinitionLink
                    {
                        DefinitionID = def.DefinitionID.Value,
                        Prefix       = def.DefinitionPrefix,
                        Rounding     = RoundingSettings.FromDefinition(def)
                    });
                }

                PXTrace.WriteInformation($"[Pipeline/Presentation] {definitionLinks.Count} definition(s) — prefixes: [{string.Join(", ", definitionLinks.Select(d => d.Prefix))}]");
            }

            var definitionsWithItems = new List<(ReportCalculationEngine.DefinitionLink, List<FLRTReportLineItem>)>();
            foreach (var defLink in definitionLinks)
            {
                var items = SelectFrom<FLRTReportLineItem>
                    .Where<FLRTReportLineItem.definitionID.IsEqual<@P.AsInt>>
                    .OrderBy<FLRTReportLineItem.sortOrder.Asc>
                    .View.Select(graph, defLink.DefinitionID)
                    .RowCast<FLRTReportLineItem>()
                    .ToList();

                definitionsWithItems.Add((defLink, items));
            }

            // FY model: see FLRTFinancialReport BuildContext overload above.
            string currYear      = record.CurrYear ?? DateTime.Now.ToString("yyyy");
            string selectedMonth = record.FinancialMonth ?? "12";
            int currYearInt      = int.TryParse(currYear, out int y) ? y : DateTime.Now.Year;
            int selectedMonthInt = int.TryParse(selectedMonth, out int m) ? m : 12;

            int fyEndMonthInt    = selectedMonthInt == 1 ? 12 : selectedMonthInt - 1;
            string fyEndMonth    = fyEndMonthInt.ToString("D2");
            int cyFyStartYear    = selectedMonthInt == 1 ? currYearInt : currYearInt - 1;

            string prevYear            = (currYearInt - 1).ToString();
            string selectedPeriod      = $"{fyEndMonth}{currYearInt}";
            string prevYearPeriod      = $"{fyEndMonth}{currYearInt - 1}";
            string prevYearPriorPeriod = $"{fyEndMonth}{currYearInt - 2}";
            string cyFyStartPeriod     = $"{selectedMonth}{cyFyStartYear}";
            string pyFyStartPeriod     = $"{selectedMonth}{cyFyStartYear - 1}";

            return new Context
            {
                DefinitionLinks      = definitionLinks,
                DefinitionsWithItems = definitionsWithItems,
                ColumnMapping        = columnMapping,
                CurrYear             = currYear,
                PrevYear             = prevYear,
                SelectedPeriod       = selectedPeriod,
                PrevYearPeriod       = prevYearPeriod,
                PrevYearPriorPeriod  = prevYearPriorPeriod,
                CyFyStartPeriod      = cyFyStartPeriod,
                PyFyStartPeriod      = pyFyStartPeriod
            };
        }

        /// <summary>
        /// Full pipeline for slide/markdown generation using a FLRTPresentationGeneration record.
        /// </summary>
        public static Dictionary<string, string> FetchAndCalculate(
            Context ctx,
            PXGraph graph,
            FLRTPresentationGeneration record,
            AuthService authService,
            string tenantName,
            CancellationToken cancellationToken = default)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var dataService = new FinancialDataService(authService, tenantName, ctx.ColumnMapping);

            var taskCY       = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.SelectedPeriod,      false, cancellationToken), cancellationToken);
            var taskPY       = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.PrevYearPeriod,      false, cancellationToken), cancellationToken);
            var taskPrior    = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.PrevYearPriorPeriod, false, cancellationToken), cancellationToken);
            var taskRangeCY  = Task.Run(() => dataService.FetchRangeApiData(record.Branch, record.Organization, record.Ledger, ctx.CyFyStartPeriod, ctx.SelectedPeriod, cancellationToken), cancellationToken);
            var taskRangePY  = Task.Run(() => dataService.FetchRangeApiData(record.Branch, record.Organization, record.Ledger, ctx.PyFyStartPeriod, ctx.PrevYearPeriod, cancellationToken), cancellationToken);

            Task.WhenAll(taskCY, taskPY, taskPrior, taskRangeCY, taskRangePY).Wait(cancellationToken);

            var engine = new ReportCalculationEngine(graph);
            return engine.CalculateAll(
                ctx.DefinitionLinks,
                taskCY.Result,
                taskPY.Result,
                cyOpeningData:    taskPY.Result,
                pyOpeningData:    taskPrior.Result,
                cyCumulativeData: taskRangeCY.Result,
                pyCumulativeData: taskRangePY.Result);
        }
    }
}
