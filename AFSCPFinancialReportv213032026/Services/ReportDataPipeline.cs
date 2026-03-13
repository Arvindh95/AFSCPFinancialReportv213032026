using System;
using System.Collections.Generic;
using System.Linq;
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
            public string SelectedPeriod       { get; set; }
            public string PrevYearPeriod       { get; set; }
            public string PrevYearPriorPeriod  { get; set; }
            public string PrevMonthPeriod      { get; set; }
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
            string currYear      = record.CurrYear ?? DateTime.Now.ToString("yyyy");
            string selectedMonth = record.FinancialMonth ?? "12";
            int currYearInt      = int.TryParse(currYear, out int y) ? y : DateTime.Now.Year;
            int selectedMonthInt = int.TryParse(selectedMonth, out int m) ? m : 12;

            string prevYear           = (currYearInt - 1).ToString();
            string prevYearPrior      = (currYearInt - 2).ToString();
            string selectedPeriod     = $"{selectedMonth}{currYear}";
            string prevYearPeriod     = $"{selectedMonth}{prevYear}";
            string prevYearPriorPeriod = $"{selectedMonth}{prevYearPrior}";

            int prevMonthInt  = selectedMonthInt == 1 ? 12 : selectedMonthInt - 1;
            int prevMonthYear = selectedMonthInt == 1 ? currYearInt - 1 : currYearInt;
            string prevMonthPeriod = $"{prevMonthInt:D2}{prevMonthYear}";

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
                PrevMonthPeriod      = prevMonthPeriod
            };
        }

        /// <summary>
        /// Full pipeline for slide/markdown generation: builds context, fetches GL data, runs the engine.
        /// Always fetches CY, PY, Prior, PM, JanCY, JanPY (no conditional optimisation needed for slides).
        /// Used by SlideGenerationService only.
        /// </summary>
        public static Dictionary<string, string> FetchAndCalculate(
            Context ctx,
            PXGraph graph,
            FLRTFinancialReport record,
            AuthService authService,
            string tenantName)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var dataService = new FinancialDataService(authService, tenantName, ctx.ColumnMapping);

            var taskCY    = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.SelectedPeriod,      false));
            var taskPY    = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.PrevYearPeriod,      false));
            var taskPrior = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.PrevYearPriorPeriod, false));
            var taskPM    = Task.Run(() => dataService.FetchAllApiData(record.Branch, record.Organization, record.Ledger, ctx.PrevMonthPeriod,     false));
            var taskJanCY = Task.Run(() => dataService.FetchJanuaryBeginningBalance(record.Branch, record.Organization, record.Ledger, ctx.CurrYear));
            var taskJanPY = Task.Run(() => dataService.FetchJanuaryBeginningBalance(record.Branch, record.Organization, record.Ledger, ctx.PrevYear));

            Task.WhenAll(taskCY, taskPY, taskPrior, taskPM, taskJanCY, taskJanPY).Wait();

            var engine = new ReportCalculationEngine(graph);
            return engine.CalculateAll(
                ctx.DefinitionLinks,
                taskCY.Result,
                taskPY.Result,
                cyOpeningData:    taskPY.Result,
                pyOpeningData:    taskPrior.Result,
                cyJanOpeningData: taskJanCY.Result,
                pyJanOpeningData: taskJanPY.Result,
                pmData:           taskPM.Result);
        }
    }
}
