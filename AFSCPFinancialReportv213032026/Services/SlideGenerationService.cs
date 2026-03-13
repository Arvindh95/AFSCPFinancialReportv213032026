using System;
using System.Linq;
using System.Text;
using System.Threading;
using FinancialReport.Helper;
using PX.Data;

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
        private readonly FLRTFinancialReportMaint _graph;
        private readonly FLRTFinancialReport _currentRecord;
        private readonly AuthService _authService;
        private readonly string _tenantName;
        private readonly FileService _fileService;

        public SlideGenerationService(
            FLRTFinancialReportMaint graph,
            FLRTFinancialReport record,
            AuthService authService,
            string tenantName)
        {
            _graph         = graph       ?? throw new ArgumentNullException(nameof(graph));
            _currentRecord = record      ?? throw new ArgumentNullException(nameof(record));
            _authService   = authService ?? throw new ArgumentNullException(nameof(authService));
            _tenantName    = tenantName  ?? throw new ArgumentNullException(nameof(tenantName));
            _fileService   = new FileService(_graph);
        }

        /// <summary>
        /// Set after BuildMarkdownPreview() completes. Callers can read this to
        /// persist the markdown text to the record's PresentationMarkdown field.
        /// </summary>
        public string LastGeneratedMarkdown { get; private set; }

        public Guid BuildMarkdownPreview(CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ── 1. Load definitions, items, periods ───────────────────────────
                var ctx = ReportDataPipeline.BuildContext(_graph, _currentRecord);

                if (!ctx.DefinitionLinks.Any())
                    throw new PXException(Messages.NoDefinitionsLinked);

                // ── 2. Validate descriptions on all visible items ─────────────────
                var missingDesc = ctx.DefinitionsWithItems
                    .SelectMany(d => d.Items)
                    .Where(li => li.IsVisible == true && string.IsNullOrWhiteSpace(li.Description))
                    .Select(li => li.LineCode)
                    .ToList();

                if (missingDesc.Any())
                    throw new PXException(Messages.VisibleLineItemsMissingDescriptions, string.Join(", ", missingDesc));

                PXTrace.WriteInformation($"[Slide] Periods — CY:{ctx.SelectedPeriod}, PY:{ctx.PrevYearPeriod}, PM:{ctx.PrevMonthPeriod}");

                // ── 3. Fetch GL data + run engine ─────────────────────────────────
                var results = ReportDataPipeline.FetchAndCalculate(ctx, _graph, _currentRecord, _authService, _tenantName);

                cancellationToken.ThrowIfCancellationRequested();
                PXTrace.WriteInformation($"[Slide] GL fetch + engine: {results.Count} values in {stopwatch.ElapsedMilliseconds}ms");

                // ── 4. Build markdown ─────────────────────────────────────────────
                var markdownBuilder = new MarkdownBuilderService();
                string markdown = markdownBuilder.Build(_currentRecord, ctx.DefinitionsWithItems, results);
                LastGeneratedMarkdown = markdown;

                PXTrace.WriteInformation($"[Slide] Markdown built — {markdown.Length} chars");

                // ── 5. Save as .txt attachment ────────────────────────────────────
                byte[] txtBytes = Encoding.UTF8.GetBytes(markdown);
                string fileName = $"{_currentRecord.ReportCD}_MarkdownPreview_{DateTime.Now:yyyyMMdd_HHmm}.txt";
                Guid fileID = _fileService.SaveGeneratedDocument(fileName, txtBytes, _currentRecord);

                stopwatch.Stop();
                PXTrace.WriteInformation($"[Slide] Markdown preview saved with FileID {fileID} — total {stopwatch.ElapsedMilliseconds}ms");

                return fileID;
            }
            finally
            {
                stopwatch.Stop();
            }
        }
    }
}
