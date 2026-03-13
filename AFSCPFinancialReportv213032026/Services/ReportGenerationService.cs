using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using FinancialReport.Helper;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;

namespace FinancialReport.Services
{
    /// <summary>
    /// This service encapsulates the entire business process of generating a single financial report.
    /// It is responsible for orchestrating data fetching, placeholder mapping, and file creation.
    /// </summary>
    public class ReportGenerationService
    {
        private readonly FLRTFinancialReportMaint _graph;
        private readonly FLRTFinancialReport _currentRecord;
        private readonly AuthService _authService;
        private readonly FileService _fileService;
        private readonly WordTemplateService _wordTemplateService;

        public ReportGenerationService(FLRTFinancialReportMaint graph, FLRTFinancialReport record, AuthService authService)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _currentRecord = record ?? throw new ArgumentNullException(nameof(record));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            // Instantiate dependent services
            _fileService = new FileService(_graph);
            _wordTemplateService = new WordTemplateService();
        }

        /// <summary>
        /// Executes the end-to-end report generation process with performance optimizations.
        /// </summary>
        /// <returns>The GUID of the newly generated and saved file.</returns>
        public Guid Execute()
        {
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

            string templatePath = null;
            string outputPath = null;

            try
            {
                // 1. Get Tenant Name for the data service
                int? companyID = _graph.GetCompanyIDFromDB(_currentRecord.ReportID);
                string tenantName = _graph.MapCompanyIDToTenantName(companyID);

                // 1b. Load definitions, line items, and period strings via shared pipeline
                var pipelineCtx = ReportDataPipeline.BuildContext(_graph, _currentRecord);
                var definitionLinks = pipelineCtx.DefinitionLinks;

                var localDataService = new FinancialDataService(_authService, tenantName, pipelineCtx.ColumnMapping);

                // 2. Get Template File
                var (templateFileContent, originalFileName) = _fileService.GetFileContentAndName(_currentRecord.Noteid, _currentRecord);
                if (templateFileContent == null || templateFileContent.Length == 0)
                    throw new PXException(Messages.TemplateFileIsEmpty);

                // Validate originalFileName is not null before using it
                if (string.IsNullOrWhiteSpace(originalFileName))
                {
                    PXTrace.WriteError("Original file name is null or empty");
                    throw new PXException(Messages.TemplateFileIsEmpty);
                }

                string extension = Path.GetExtension(originalFileName) ?? ".docx";
                templatePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);
                File.WriteAllBytes(templatePath, templateFileContent);

                // 3. Extract Placeholders
                List<string> extractedKeys = _wordTemplateService.ExtractPlaceholderKeys(templatePath);

                // 3b. Determine which optional API fetches are actually needed.
                // PM: check template for any _PM placeholder (e.g. {{BS_REVENUE_PM}})
                bool needsPM = extractedKeys.Any(k =>
                    k.EndsWith("_" + Constants.PreviousMonthSuffix, StringComparison.OrdinalIgnoreCase));

                // Detail rows + Cumulative: scan line items from all linked definitions.
                // - needsDetail     → at least one line has a dimension filter (Sub/Branch/Org/Ledger)
                // - needsCumulative → at least one line uses Debit/Credit/Movement (YTD) balance type
                bool needsDetail     = false;
                bool needsCumulative = false;
                foreach (var defLink in definitionLinks)
                {
                    var lineItems = SelectFrom<FLRTReportLineItem>
                        .Where<FLRTReportLineItem.definitionID.IsEqual<@P.AsInt>>
                        .View.Select(_graph, defLink.DefinitionID)
                        .RowCast<FLRTReportLineItem>();

                    foreach (var li in lineItems)
                    {
                        if (!needsDetail && (
                            !string.IsNullOrWhiteSpace(li.SubaccountFilter) ||
                            !string.IsNullOrWhiteSpace(li.BranchFilter)     ||
                            !string.IsNullOrWhiteSpace(li.OrganizationFilter) ||
                            !string.IsNullOrWhiteSpace(li.LedgerFilter)))
                            needsDetail = true;

                        if (!needsCumulative && (
                            string.Equals(li.BalanceType, FLRTReportLineItem.BalanceTypeValue.Debit,     StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(li.BalanceType, FLRTReportLineItem.BalanceTypeValue.Credit,    StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(li.BalanceType, FLRTReportLineItem.BalanceTypeValue.Movement,  StringComparison.OrdinalIgnoreCase)))
                            needsCumulative = true;

                        if (needsDetail && needsCumulative) break;
                    }
                    if (needsDetail && needsCumulative) break;
                }

                PXTrace.WriteInformation($"API fetch flags — needsDetail={needsDetail}, needsCumulative={needsCumulative}, needsPM={needsPM}");

                // 4. Separate ALL placeholder types (wildcard, exact range, regular)
                var (wildcardRangePlaceholders, exactRangePlaceholders, regularPlaceholders) =
                    localDataService.SeparateAllPlaceholderTypes(extractedKeys);

                PXTrace.WriteInformation($"[Step 4] Extracted {extractedKeys.Count} total placeholders:");
                PXTrace.WriteInformation($"   wildcard range: {wildcardRangePlaceholders.Count} (A????:B????_e_CY)");
                PXTrace.WriteInformation($"   exact range:    {exactRangePlaceholders.Count} (A74101:A75101_e_CY)");
                PXTrace.WriteInformation($"   regular:        {regularPlaceholders.Count} (A74101_CY)");

                // 5. Set up Parameters — reuse period strings from pipeline context
                string currYear            = pipelineCtx.CurrYear;
                string prevYear            = pipelineCtx.PrevYear;
                string selectedPeriod      = pipelineCtx.SelectedPeriod;
                string prevYearPeriod      = pipelineCtx.PrevYearPeriod;
                string prevYearPriorPeriod = pipelineCtx.PrevYearPriorPeriod;
                string prevMonthPeriod     = pipelineCtx.PrevMonthPeriod;

                string selectedMonth = _currentRecord.FinancialMonth ?? "12";
                int currYearInt      = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
                int selectedMonthInt = int.TryParse(selectedMonth, out int parsedMonth) ? parsedMonth : 12;
                string prevYearPrior = (currYearInt - 2).ToString();

                // Compute the fiscal year start period for cumulative (Debit/Credit/Movement) fetches.
                // The fiscal year starts on the month immediately after the financial year-end month.
                //   e.g. year-end = April (04) → fiscal start = May (05)
                //   e.g. year-end = December (12) → fiscal start = January (01) of same calendar year
                int fiscalStartMonthInt = (selectedMonthInt % 12) + 1;
                string fiscalStartMonth = fiscalStartMonthInt.ToString("D2");
                // When the fiscal start month is AFTER the year-end month numerically, the fiscal year
                // crosses a calendar year boundary and the start falls in the PRIOR calendar year.
                int cyCumulativeStartYearInt = fiscalStartMonthInt > selectedMonthInt ? currYearInt - 1 : currYearInt;
                string cyCumulativeStart = $"{fiscalStartMonth}{cyCumulativeStartYearInt}";
                string pyCumulativeStart = $"{fiscalStartMonth}{cyCumulativeStartYearInt - 1}";
                string pyCumulativeEnd   = prevYearPeriod; // $"{selectedMonth}{prevYear}"

                PXTrace.WriteInformation($"Fiscal year: {cyCumulativeStart} → {selectedPeriod} (CY), {pyCumulativeStart} → {pyCumulativeEnd} (PY)");

                // 6. Fetch all required data from the API in parallel.
                // Optional fetches (cumulative, PM) are skipped via Task.FromResult(null) when not needed,
                // so the engine receives null and gracefully returns 0 for those balance types.
                var taskCY      = Task.Run(() => localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, selectedPeriod,      needsDetail));
                var taskPY      = Task.Run(() => localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPeriod,      needsDetail));
                var taskJanPY   = Task.Run(() => localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYear));
                var taskJanCY   = Task.Run(() => localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, currYear));
                // Cumulative (Debit/Credit/Movement YTD): skip if no line uses those balance types
                var taskRangeCY = needsCumulative
                    ? Task.Run(() => localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, cyCumulativeStart, selectedPeriod))
                    : Task.FromResult<FinancialApiData>(null);
                var taskRangePY = needsCumulative
                    ? Task.Run(() => localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, pyCumulativeStart, pyCumulativeEnd))
                    : Task.FromResult<FinancialApiData>(null);
                // PY opening (EndingBalance of 2 years ago → PY fiscal-year opening for Beginning balance type)
                var taskPrior   = Task.Run(() => localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPriorPeriod, needsDetail));
                // Previous month: skip if no _PM placeholder in template
                var taskPM      = needsPM
                    ? Task.Run(() => localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevMonthPeriod, needsDetail))
                    : Task.FromResult<FinancialApiData>(null);

                Task.WhenAll(taskCY, taskPY, taskJanPY, taskJanCY, taskRangeCY, taskRangePY, taskPrior, taskPM).Wait();

                var currYearData          = taskCY.Result;
                var prevYearData          = taskPY.Result;
                var januaryBeginningDataPY = taskJanPY.Result;
                var januaryBeginningDataCY = taskJanCY.Result;
                var cumulativeCYData      = taskRangeCY.Result; // null when needsCumulative=false
                var cumulativePYData      = taskRangePY.Result; // null when needsCumulative=false
                var prevYearPriorData     = taskPrior.Result;
                var prevMonthData         = taskPM.Result;      // null when needsPM=false

                int fetchCount = 6 + (needsCumulative ? 2 : 0) + (needsPM ? 1 : 0);
                PXTrace.WriteInformation($"[Step 7] {fetchCount} API calls completed (skipped: cumulative={!needsCumulative}, PM={!needsPM}) — prevMonth: {prevMonthPeriod}");

                // 7. Set up user settings for analysis
                var userSettings = new UserSettings
                {
                    Branch = _currentRecord.Branch,
                    Organization = _currentRecord.Organization,
                    Ledger = _currentRecord.Ledger
                };

                // 8. Process regular placeholders using existing logic
                var regularPlaceholderRequests = localDataService.AnalyzePlaceholders(regularPlaceholders, selectedPeriod, prevYearPeriod, userSettings);
                var regularPlaceholderValues = localDataService.ProcessPlaceholdersFromFetchedData(
                    regularPlaceholderRequests, currYearData, prevYearData, januaryBeginningDataCY,
                    januaryBeginningDataPY, cumulativeCYData, cumulativePYData);

                // 9. Process exact range placeholders
                var exactRangePlaceholderValues = localDataService.ProcessAccountRangePlaceholders(
                    exactRangePlaceholders, currYearData, prevYearData, januaryBeginningDataCY,
                    januaryBeginningDataPY, cumulativeCYData, cumulativePYData);

                // 10. Process wildcard range placeholders
                var wildcardRangePlaceholderValues = localDataService.ProcessWildcardRangePlaceholders(
                    wildcardRangePlaceholders, currYearData, prevYearData, januaryBeginningDataCY,
                    januaryBeginningDataPY, cumulativeCYData, cumulativePYData);

                // 11. Combine all placeholder values
                var finalPlaceholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // ── Run ReportCalculationEngine for all linked definitions.
                // Produces PREFIX_LINECODE_CY / PREFIX_LINECODE_PY placeholders.
                // Cross-definition formulas are resolved via topological sort.
                // Engine values take priority over raw account placeholders.
                if (definitionLinks.Any())
                {
                    PXTrace.WriteInformation($"Running ReportCalculationEngine for {definitionLinks.Count} definition(s).");
                    var engine = new ReportCalculationEngine(_graph);
                    var enginePlaceholders = engine.CalculateAll(
                        definitionLinks,
                        currYearData,
                        prevYearData,
                        cyOpeningData:    prevYearData,           // BalanceType=Beginning: EndingBalance of prior year-end → CY fiscal opening
                        pyOpeningData:    prevYearPriorData,      // BalanceType=Beginning: EndingBalance of 2-years-ago year-end → PY fiscal opening
                        cyJanOpeningData: januaryBeginningDataCY, // BalanceType=JanuaryBeginning: BeginningBalance of 01-{currYear}
                        pyJanOpeningData: januaryBeginningDataPY, // BalanceType=JanuaryBeginning: BeginningBalance of 01-{prevYear}
                        cyCumulativeData: cumulativeCYData,       // BalanceType=Debit/Credit/Movement: full-year Jan–Dec CY totals
                        pyCumulativeData: cumulativePYData,       // BalanceType=Debit/Credit/Movement: full-year Jan–Dec PY totals
                        pmData:           prevMonthData);         // _PM placeholders: single period of previous month

                    foreach (var kvp in enginePlaceholders)
                        finalPlaceholders[kvp.Key] = kvp.Value;

                    PXTrace.WriteInformation($"ReportCalculationEngine produced {enginePlaceholders.Count} placeholders.");
                }

                // ── LEGACY: Raw account-code placeholders for anything not covered by the definition.
                // Preserves backward compatibility with existing templates that use {{A10100_CY}} syntax.
                foreach (var kvp in regularPlaceholderValues)
                {
                    if (!finalPlaceholders.ContainsKey(kvp.Key))
                        finalPlaceholders.Add(kvp.Key, kvp.Value);
                }

                foreach (var kvp in exactRangePlaceholderValues)
                {
                    if (!finalPlaceholders.ContainsKey(kvp.Key))
                        finalPlaceholders.Add(kvp.Key, kvp.Value);
                }

                foreach (var kvp in wildcardRangePlaceholderValues)
                {
                    if (!finalPlaceholders.ContainsKey(kvp.Key))
                        finalPlaceholders.Add(kvp.Key, kvp.Value);
                }

                // Add year constants
                finalPlaceholders[Constants.CurrentYearSuffix] = currYear;
                finalPlaceholders[Constants.PreviousYearSuffix] = prevYear;

                PXTrace.WriteInformation($"[Step 12] Final placeholder count: {finalPlaceholders.Count}");
                PXTrace.WriteInformation($"   regular:        {regularPlaceholderValues.Count}");
                PXTrace.WriteInformation($"   exact range:    {exactRangePlaceholderValues.Count}");
                PXTrace.WriteInformation($"   wildcard range: {wildcardRangePlaceholderValues.Count}");

                // 12. Populate Word Template
                string outputFileName = $"{_currentRecord.ReportCD}_Generated_{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
                outputPath = Path.Combine(Path.GetTempPath(), outputFileName);
                _wordTemplateService.PopulateTemplate(templatePath, outputPath, finalPlaceholders);

                // 12. Save Generated File and return its ID
                byte[] generatedFileContent = File.ReadAllBytes(outputPath);
                var fileId = _fileService.SaveGeneratedDocument(outputFileName, generatedFileContent, _currentRecord);

                totalStopwatch.Stop();
                PXTrace.WriteInformation($"Total report generation completed in {totalStopwatch.ElapsedMilliseconds} ms");

                return fileId;
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                PXTrace.WriteError($"Report generation failed after {totalStopwatch.ElapsedMilliseconds} ms for Report '{_currentRecord.ReportCD}' (ID: {_currentRecord.ReportID}): {ex.Message}");
                throw;
            }
            finally
            {
                // Cleanup temporary files
                if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                {
                    try
                    {
                        File.Delete(templatePath);
                        PXTrace.WriteInformation($"Cleaned up template file: {Path.GetFileName(templatePath)}");
                    }
                    catch (Exception ex)
                    {
                        PXTrace.WriteWarning($"Failed to cleanup template file '{Path.GetFileName(templatePath)}': {ex.Message}");
                    }
                }

                if (!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                        PXTrace.WriteInformation($"Cleaned up output file: {Path.GetFileName(outputPath)}");
                    }
                    catch (Exception ex)
                    {
                        PXTrace.WriteWarning($"Failed to cleanup output file '{Path.GetFileName(outputPath)}': {ex.Message}");
                    }
                }

                // Clear credential cache after report generation completes
                CredentialProvider.ClearCache();
            }
        }

    }
}
