using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using FinancialReport.Helper;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;

namespace FinancialReport.Services
{
    /// <summary>
    /// Fetches GL data, runs the calculation engine, and builds a markdown string
    /// that can be passed to a presentation generation API (e.g. Gamma).
    ///
    /// BuildMarkdownPreview() runs the full data pipeline via ReportDataPipeline
    /// and saves the markdown as a .txt file attachment for user review.
    /// </summary>
    public class SlideGenerationService
    {
        private readonly FLRTFinancialPresentationMaint _graph;
        private readonly FLRTPresentationGeneration _currentRecord;
        private readonly AuthService _authService;
        private readonly string _tenantName;

        public SlideGenerationService(
            FLRTFinancialPresentationMaint graph,
            FLRTPresentationGeneration record,
            AuthService authService,
            string tenantName)
        {
            _graph         = graph       ?? throw new ArgumentNullException(nameof(graph));
            _currentRecord = record      ?? throw new ArgumentNullException(nameof(record));
            _authService   = authService ?? throw new ArgumentNullException(nameof(authService));
            _tenantName    = tenantName  ?? throw new ArgumentNullException(nameof(tenantName));
        }

        /// <summary>
        /// Set after BuildMarkdownPreview() completes. Callers can read this to
        /// persist the markdown text to the record's PresentationMarkdown field.
        /// </summary>
        public string LastGeneratedMarkdown { get; private set; }

        public void BuildMarkdownPreview(CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ── 1. Load definitions, items, periods ───────────────────────────
                var ctx = ReportDataPipeline.BuildContext(_graph, _currentRecord);

                bool hasDefinitions = ctx.DefinitionLinks.Any();
                bool hasDataSources = false;

                // ── 2. Load GI Data Source links ──────────────────────────────────
                var dataSourceLinks = PXSelect<FLRTPresentationDataSourceLink,
                    Where<FLRTPresentationDataSourceLink.presentationID, Equal<Required<FLRTPresentationDataSourceLink.presentationID>>>,
                    OrderBy<Asc<FLRTPresentationDataSourceLink.displayOrder>>>
                    .Select(_graph, _currentRecord.PresentationID)
                    .RowCast<FLRTPresentationDataSourceLink>()
                    .ToList();

                hasDataSources = dataSourceLinks.Any();

                if (!hasDefinitions && !hasDataSources)
                    throw new PXException("No Report Definitions or GI Data Sources are linked to this presentation.");

                Dictionary<string, string> results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // ── 3. Fetch GL data if definitions exist ─────────────────────────
                if (hasDefinitions)
                {
                    var missingDesc = ctx.DefinitionsWithItems
                        .SelectMany(d => d.Items)
                        .Where(li => li.IsVisible == true && string.IsNullOrWhiteSpace(li.Description))
                        .Select(li => li.LineCode)
                        .ToList();

                    if (missingDesc.Any())
                        throw new PXException(Messages.VisibleLineItemsMissingDescriptions, string.Join(", ", missingDesc));

                    PXTrace.WriteInformation($"[Slide] FY periods — CY:{ctx.CyFyStartPeriod}→{ctx.SelectedPeriod}, PY:{ctx.PyFyStartPeriod}→{ctx.PrevYearPeriod}");

                    results = ReportDataPipeline.FetchAndCalculate(ctx, _graph, _currentRecord, _authService, _tenantName, cancellationToken);
                    PXTrace.WriteInformation($"[Slide] GL fetch + engine: {results.Count} values in {stopwatch.ElapsedMilliseconds}ms");
                }

                cancellationToken.ThrowIfCancellationRequested();

                // ── 4. Fetch GI Data Sources ──────────────────────────────────────
                Dictionary<string, string> giPlaceholders = null;
                var giDataSources = new List<(FLRTGIDataSource DS, List<FLRTGIDataSourceColumn> Columns)>();

                if (hasDataSources)
                {
                    var creds = CredentialProvider.GetCredentials(_tenantName);
                    var giService = new GIDataFetchService(_authService, creds.BaseURL, _tenantName);

                    string year = _currentRecord.CurrYear ?? DateTime.Now.ToString("yyyy");
                    string month = (_currentRecord.FinancialMonth ?? "12").PadLeft(2, '0');

                    foreach (var link in dataSourceLinks)
                    {
                        var ds = PXSelect<FLRTGIDataSource,
                            Where<FLRTGIDataSource.dataSourceID, Equal<Required<FLRTGIDataSource.dataSourceID>>>>
                            .Select(_graph, link.DataSourceID)
                            .TopFirst;

                        if (ds == null || ds.IsActive != true) continue;

                        var columns = PXSelect<FLRTGIDataSourceColumn,
                            Where<FLRTGIDataSourceColumn.dataSourceID, Equal<Required<FLRTGIDataSourceColumn.dataSourceID>>>,
                            OrderBy<Asc<FLRTGIDataSourceColumn.sortOrder>>>
                            .Select(_graph, ds.DataSourceID)
                            .RowCast<FLRTGIDataSourceColumn>()
                            .ToList();

                        giDataSources.Add((ds, columns));

                        PXTrace.WriteInformation($"[Slide] Fetching GI Data Source '{ds.DataSourceCD}' (prefix: {ds.Prefix})...");

                        var dsResults = giService.FetchAndAggregate(
                            ds, columns, year, month,
                            _currentRecord.Branch,
                            _currentRecord.Organization,
                            _currentRecord.Ledger,
                            cancellationToken);

                        foreach (var kv in dsResults)
                        {
                            results[kv.Key] = kv.Value;
                        }

                        PXTrace.WriteInformation($"[Slide] GI '{ds.DataSourceCD}' produced {dsResults.Count} placeholders.");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // ── 5. Build markdown ─────────────────────────────────────────────
                var markdownBuilder = new MarkdownBuilderService();
                string markdown = markdownBuilder.Build(_currentRecord, ctx.DefinitionsWithItems, results, giDataSources);
                LastGeneratedMarkdown = markdown;

                stopwatch.Stop();
                PXTrace.WriteInformation($"[Slide] Markdown built — {markdown.Length} chars, total {stopwatch.ElapsedMilliseconds}ms");
            }
            finally
            {
                stopwatch.Stop();
            }
        }
    }
}
