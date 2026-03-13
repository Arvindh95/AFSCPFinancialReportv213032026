using System;
using PX.Data;
using PX.Data.BQL.Fluent;
using System.Collections.Generic;
using System.IO;
using PX.SM;
using System.Linq;
using FinancialReport.Helper;
using FinancialReport.Services;
using PX.Data.WorkflowAPI;
using static FinancialReport.FLRTFinancialReport;

namespace FinancialReport
{
    public class FLRTFinancialReportMaint : PXGraph<FLRTFinancialReportMaint, FLRTFinancialReport>
    {
        #region DAC View
        public SelectFrom<FLRTFinancialReport>.View FinancialReport = null!; // Initialized by PXGraph framework

        /// <summary>
        /// Child grid: linked Report Definitions for the currently selected report.
        /// Each row ties one definition (with its prefix) to this report.
        /// Multiple definitions enable cross-definition formulas and a single unified output.
        /// </summary>
        public SelectFrom<FLRTReportDefinitionLink>
            .Where<FLRTReportDefinitionLink.reportID.IsEqual<FLRTFinancialReport.reportID.FromCurrent>>
            .OrderBy<FLRTReportDefinitionLink.displayOrder.Asc>
            .View DefinitionLinks;
        #endregion

        #region Events

        // ── FLRTReportDefinitionLink events ──────────────────────────────────

        /// <summary>
        /// Populates the read-only Prefix display column from the joined FLRTReportDefinition.
        /// </summary>
        protected void _(Events.FieldSelecting<FLRTReportDefinitionLink, FLRTReportDefinitionLink.definitionPrefix> e)
        {
            if (e.Row?.DefinitionID == null) return;
            var def = PXSelectorAttribute.Select<FLRTReportDefinitionLink.definitionID>(e.Cache, e.Row) as FLRTReportDefinition;
            if (def != null)
                e.ReturnValue = def.DefinitionPrefix;
        }

        /// <summary>
        /// Validates that linked definitions have unique prefixes within this report.
        /// </summary>
        protected void _(Events.RowPersisting<FLRTReportDefinitionLink> e)
        {
            if (e.Row == null || e.Operation == PXDBOperation.Delete) return;

            // Ensure a definition is selected
            if (e.Row.DefinitionID == null)
            {
                e.Cache.RaiseExceptionHandling<FLRTReportDefinitionLink.definitionID>(
                    e.Row, e.Row.DefinitionID,
                    new PXSetPropertyException(Messages.DefinitionRequired, PXErrorLevel.Error, e.Row));
                return;
            }

            // Load the definition to get its prefix
            var def = PXSelectorAttribute.Select<FLRTReportDefinitionLink.definitionID>(e.Cache, e.Row) as FLRTReportDefinition;
            if (def == null || string.IsNullOrWhiteSpace(def.DefinitionPrefix)) return;

            // Check for duplicate prefix among other links for the same report
            foreach (FLRTReportDefinitionLink other in DefinitionLinks.Cache.Cached)
            {
                if (other.LinkID == e.Row.LinkID) continue;
                if (other.DefinitionID == null) continue;

                var otherDef = PXSelectorAttribute.Select<FLRTReportDefinitionLink.definitionID>(
                    DefinitionLinks.Cache, other) as FLRTReportDefinition;

                if (otherDef != null
                    && string.Equals(otherDef.DefinitionPrefix, def.DefinitionPrefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    e.Cache.RaiseExceptionHandling<FLRTReportDefinitionLink.definitionID>(
                        e.Row, e.Row.DefinitionID,
                        new PXSetPropertyException(Messages.DuplicatePrefixInReport, PXErrorLevel.Error, e.Row, def.DefinitionPrefix));
                    return;
                }
            }
        }

        // ── FLRTFinancialReport events ────────────────────────────────────────

        protected void FLRTFinancialReport_RowSelected(PXCache cache, PXRowSelectedEventArgs e)
        {
            var row = (FLRTFinancialReport)e.Row;
            if (row == null) return;

            bool isInProgress = row.Status == ReportStatus.InProgress;

            PXUIFieldAttribute.SetEnabled(cache, row, !isInProgress);
            GenerateReport.SetEnabled(!isInProgress);
            DownloadReport.SetEnabled(!isInProgress);
            DownloadPresentation.SetEnabled(row.SlideGeneratedFileID != null);
        }

        protected void FLRTFinancialReport_RowPersisting(PXCache cache, PXRowPersistingEventArgs e)
        {
            var row = (FLRTFinancialReport)e.Row;
            if (row == null) return;

            // Update file ID fields before persisting if needed
            if (row.Noteid != null)
            {
                using (new PXConnectionScope())
                {
                    var fileCmd = new PXSelectJoin<UploadFile,
                                        InnerJoin<NoteDoc, On<NoteDoc.fileID, Equal<UploadFile.fileID>>>,
                                    Where<NoteDoc.noteID, Equal<Required<NoteDoc.noteID>>>,
                                    OrderBy<Desc<UploadFile.createdDateTime>>>(this);

                    var file = fileCmd.SelectSingle(row.Noteid);

                    if (file?.Name?.Contains(Constants.TemplateFileFilter) == true)
                    {
                        string fileIdString = file.FileID.ToString();
                        if (row.UploadedFileIDDisplay != fileIdString)
                        {
                            cache.SetValue<FLRTFinancialReport.uploadedFileIDDisplay>(row, fileIdString);
                            cache.SetValueExt<FLRTFinancialReport.uploadedFileID>(row, file.FileID);
                        }
                    }
                }
            }
        }

#endregion

        #region Actions
        public new PXSave<FLRTFinancialReport> Save = null!; // Initialized by PXGraph framework
        public new PXCancel<FLRTFinancialReport> Cancel = null!; // Initialized by PXGraph framework
        public PXAction<FLRTFinancialReport> GenerateReport = null!; // Initialized by PXGraph framework
        public PXAction<FLRTFinancialReport> DownloadReport = null!; // Initialized by PXGraph framework
        public PXAction<FLRTFinancialReport> ResetStatus = null!; // Initialized by PXGraph framework
        public PXAction<FLRTFinancialReport> DownloadPresentation = null!; // Initialized by PXGraph framework
        public PXAction<FLRTFinancialReport> PreviewMarkdown = null!; // Initialized by PXGraph framework
        public PXAction<FLRTFinancialReport> GenerateGamma = null!; // Initialized by PXGraph framework

        [PXButton(CommitChanges = false, ImageKey = "RecordAdd", ImageSet = "main", Tooltip = "Generate Report", Connotation = ActionConnotation.Success)]
        [PXUIField(DisplayName = "")]
        protected virtual System.Collections.IEnumerable generateReport(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Current;

            // Perform initial validations on the UI thread
            if (selectedRecord == null) throw new PXException(Messages.PleaseSelectTemplate);
            if (selectedRecord.ReportID == null) throw new PXException(Messages.NoReportSelected);
            if (selectedRecord.Status == ReportStatus.InProgress) throw new PXException(Messages.FileGenerationInProgress);
            if (selectedRecord.Noteid == null) throw new PXException(Messages.TemplateHasNoFiles);

            int? companyID = GetCompanyIDFromDB(selectedRecord.ReportID);
            string tenantName = MapCompanyIDToTenantName(companyID);
            AcumaticaCredentials tenantCredentials = CredentialProvider.GetCredentials(tenantName);

            var authService = new AuthService(
                tenantCredentials.BaseURL,
                tenantCredentials.ClientId,
                tenantCredentials.ClientSecret,
                tenantCredentials.Username,
                tenantCredentials.Password
            );

            // Pre-authenticate to ensure credentials are valid before starting the long operation
            //authService.AuthenticateAndGetToken();
            //PXTrace.WriteInformation($"Successfully authenticated for {tenantName}.");

            // Mark record as In Progress and save before starting background task
            selectedRecord.Status = ReportStatus.InProgress;
            FinancialReport.Update(selectedRecord);
            Actions.PressSave();

            int? reportID = selectedRecord.ReportID;

            PXLongOperation.StartOperation(this, () =>
            {
                var reportGraph = PXGraph.CreateInstance<FLRTFinancialReportMaint>();
                FLRTFinancialReport dbRecord = null;

                try
                {
                    dbRecord = PXSelect<FLRTFinancialReport, Where<FLRTFinancialReport.reportID, Equal<Required<FLRTFinancialReport.reportID>>>>
                        .SelectSingleBound(reportGraph, null, reportID);

                    // Timeout protection: 15 minutes maximum for report generation
                    var timeoutCancellation = new System.Threading.CancellationTokenSource();
                    const int timeoutMinutes = 15;
                    timeoutCancellation.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

                    var timeoutTask = System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            if (dbRecord == null) throw new PXException(Messages.FailedToRetrieveFile);
                            if (dbRecord.Noteid == null) throw new PXException(Messages.TemplateHasNoFiles);

                            // Check for cancellation before starting
                            timeoutCancellation.Token.ThrowIfCancellationRequested();

                        // Pre-authenticate INSIDE the long operation where async/sync issues are less problematic
                        PXTrace.WriteInformation("Authenticating...");
                        authService.AuthenticateAndGetToken();
                        PXTrace.WriteInformation($"Successfully authenticated for {tenantName}.");

                        // Check for cancellation before generation
                        timeoutCancellation.Token.ThrowIfCancellationRequested();

                        // Instantiate and execute the new service
                        var generationService = new ReportGenerationService(reportGraph, dbRecord, authService);
                        Guid generatedFileID = generationService.Execute();

                        // Check for cancellation before saving
                        timeoutCancellation.Token.ThrowIfCancellationRequested();

                        // Update the record with the successful result
                        dbRecord.GeneratedFileID = generatedFileID;
                        dbRecord.Status = ReportStatus.Completed;
                        reportGraph.FinancialReport.Update(dbRecord);
                        reportGraph.Actions.PressSave();
                        PXTrace.WriteInformation("Report generation completed successfully.");
                    }
                    catch (System.OperationCanceledException)
                    {
                        PXTrace.WriteError($"Report generation timed out after {timeoutMinutes} minutes");
                        if (dbRecord != null)
                        {
                            dbRecord.Status = ReportStatus.Failed;
                            reportGraph.FinancialReport.Update(dbRecord);
                            reportGraph.Actions.PressSave();
                        }
                        throw new PXException(Messages.ReportGenerationTimeout, timeoutMinutes);
                    }
                    catch (Exception ex)
                    {
                        PXTrace.WriteError($"Report generation failed: {ex.ToString()}");
                        if (dbRecord != null)
                        {
                            dbRecord.Status = ReportStatus.Failed;
                            reportGraph.FinancialReport.Update(dbRecord);
                            reportGraph.Actions.PressSave();
                        }
                        throw;
                    }
                    finally
                    {
                        if (authService?.IsAuthenticated == true) authService.Logout();
                        timeoutCancellation?.Dispose();
                    }
                }, timeoutCancellation.Token);

                    try
                    {
                        timeoutTask.Wait();
                    }
                    catch (System.AggregateException aex)
                    {
                        // Unwrap AggregateException and throw the actual exception
                        throw aex.InnerException ?? aex;
                    }
                }
                catch (Exception)
                {
                    // If ANY unhandled exception occurs (including user cancellation),
                    // ensure status is set to Failed
                    if (dbRecord != null && dbRecord.Status == ReportStatus.InProgress)
                    {
                        dbRecord.Status = ReportStatus.Failed;
                        reportGraph.FinancialReport.Update(dbRecord);
                        try
                        {
                            reportGraph.Actions.PressSave();
                        }
                        catch
                        {
                            // Ignore save errors in cleanup
                        }
                    }
                    throw; // Re-throw to show user the error
                }
            });

            return adapter.Get();
        }

        [PXButton(ImageKey = "DataEntryF", ImageSet = "main", Tooltip = "Download Report", Connotation = ActionConnotation.Success)]
        [PXUIField(DisplayName = "", MapEnableRights = PXCacheRights.Select, Visible = true)]
        protected virtual System.Collections.IEnumerable downloadReport(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Current;

            if (selectedRecord == null)
                throw new PXException(Messages.NoRecordIsSelected);

            if (selectedRecord.GeneratedFileID == null)
                throw new PXException(Messages.NoGeneratedFile);

            throw new PXRedirectToFileException(selectedRecord.GeneratedFileID.Value, true);
        }

        [PXButton(CommitChanges = true, ImageKey = "Refresh", ImageSet = "main", Tooltip = "Reset Status", Connotation = ActionConnotation.Warning)]
        [PXUIField(DisplayName = "", MapEnableRights = PXCacheRights.Update, Visible = true)]
        protected virtual System.Collections.IEnumerable resetStatus(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Current;

            if (selectedRecord == null)
            {
                throw new PXException(Messages.UnselectedResetStatus);
            }

            string currentStatus = selectedRecord.Status ?? "Unknown";
            string statusName = currentStatus == ReportStatus.InProgress ? "In Progress" :
                               currentStatus == ReportStatus.Failed ? "Failed" :
                               currentStatus == ReportStatus.Completed ? "Completed" : "Pending";

            // Ask for confirmation before resetting
            if (FinancialReport.Ask("Confirm Reset",
                $"Reset this report from '{statusName}' to 'Pending'? This will allow regeneration.",
                MessageButtons.YesNo) == WebDialogResult.Yes)
            {
                selectedRecord.Status = ReportStatus.Pending;
                selectedRecord.GeneratedFileID = null; // Clear the generated file so it doesn't show old data
                FinancialReport.Update(selectedRecord);
                Actions.PressSave();

                PXTrace.WriteInformation($"Report {selectedRecord.ReportCD} status reset from {statusName} to Pending.");
            }

            return adapter.Get();
        }

        [PXButton(CommitChanges = true, ImageKey = "RecordEdit", ImageSet = "main", Tooltip = "Preview Markdown", Connotation = ActionConnotation.Info)]
        [PXUIField(DisplayName = "", MapEnableRights = PXCacheRights.Update, Visible = true)]
        protected virtual System.Collections.IEnumerable previewMarkdown(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Current;

            if (selectedRecord == null) throw new PXException(Messages.PleaseSelectTemplate);
            if (selectedRecord.ReportID == null) throw new PXException(Messages.NoReportSelected);

            int? companyID = GetCompanyIDFromDB(selectedRecord.ReportID);
            string tenantName = MapCompanyIDToTenantName(companyID);
            AcumaticaCredentials tenantCredentials = CredentialProvider.GetCredentials(tenantName);

            var authService = new AuthService(
                tenantCredentials.BaseURL,
                tenantCredentials.ClientId,
                tenantCredentials.ClientSecret,
                tenantCredentials.Username,
                tenantCredentials.Password
            );

            int? reportID = selectedRecord.ReportID;

            PXLongOperation.StartOperation(this, () =>
            {
                var reportGraph = PXGraph.CreateInstance<FLRTFinancialReportMaint>();
                FLRTFinancialReport dbRecord = null;

                try
                {
                    dbRecord = PXSelect<FLRTFinancialReport,
                        Where<FLRTFinancialReport.reportID, Equal<Required<FLRTFinancialReport.reportID>>>>
                        .SelectSingleBound(reportGraph, null, reportID);

                    if (dbRecord == null) throw new PXException(Messages.FailedToRetrieveFile);

                    authService.AuthenticateAndGetToken();

                    var slideService = new SlideGenerationService(reportGraph, dbRecord, authService, tenantName);
                    Guid fileID = slideService.BuildMarkdownPreview();

                    // Save markdown text into the record field so user can edit before generating
                    dbRecord.PresentationMarkdown = slideService.LastGeneratedMarkdown;

                    // Touch the record so PressSave flushes the NoteDoc link and saves the markdown field
                    reportGraph.FinancialReport.Update(dbRecord);
                    reportGraph.Actions.PressSave();

                    PXTrace.WriteInformation($"[Slide] Markdown preview saved. FileID={fileID}");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"[Slide] Markdown preview failed: {ex}");
                    throw;
                }
                finally
                {
                    if (authService?.IsAuthenticated == true) authService.Logout();
                }
            });

            return adapter.Get();
        }

        [PXButton(CommitChanges = true, ImageKey = "DataEntry", ImageSet = "main", Tooltip = "Generate Presentation", Connotation = ActionConnotation.Info)]
        [PXUIField(DisplayName = "", MapEnableRights = PXCacheRights.Update, Visible = true)]
        protected virtual System.Collections.IEnumerable generateGamma(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Current;

            if (selectedRecord == null) throw new PXException(Messages.PleaseSelectTemplate);
            if (selectedRecord.ReportID == null) throw new PXException(Messages.NoReportSelected);
            if (selectedRecord.SlideStatus == ReportStatus.InProgress) throw new PXException(Messages.SlideGenerationInProgress);
            if (string.IsNullOrWhiteSpace(selectedRecord.PresentationTitle)) throw new PXException(Messages.PresentationTitleRequired);

            int? companyID = GetCompanyIDFromDB(selectedRecord.ReportID);
            string tenantName = MapCompanyIDToTenantName(companyID);
            AcumaticaCredentials tenantCredentials = CredentialProvider.GetCredentials(tenantName);

            FLRTTenantCredentials tenantCreds = PXSelect<FLRTTenantCredentials,
                Where<FLRTTenantCredentials.companyNum, Equal<Required<FLRTTenantCredentials.companyNum>>>>
                .Select(this, companyID);

            if (tenantCreds == null || string.IsNullOrWhiteSpace(tenantCreds.GammaApiKey))
                throw new PXException(Messages.GammaApiKeyNotConfigured);

            string gammaApiKey = tenantCreds.GammaApiKey;

            var authService = new AuthService(
                tenantCredentials.BaseURL,
                tenantCredentials.ClientId,
                tenantCredentials.ClientSecret,
                tenantCredentials.Username,
                tenantCredentials.Password
            );

            // Mark slide as In Progress and save
            selectedRecord.SlideStatus = ReportStatus.InProgress;
            FinancialReport.Update(selectedRecord);
            Actions.PressSave();

            int? reportID = selectedRecord.ReportID;

            PXLongOperation.StartOperation(this, () =>
            {
                var reportGraph = PXGraph.CreateInstance<FLRTFinancialReportMaint>();
                FLRTFinancialReport dbRecord = null;

                try
                {
                    dbRecord = PXSelect<FLRTFinancialReport,
                        Where<FLRTFinancialReport.reportID, Equal<Required<FLRTFinancialReport.reportID>>>>
                        .SelectSingleBound(reportGraph, null, reportID);

                    if (dbRecord == null) throw new PXException(Messages.FailedToRetrieveFile);

                    Guid fileID;
                    string markdown;

                    if (!string.IsNullOrWhiteSpace(dbRecord.PresentationMarkdown))
                    {
                        PXTrace.WriteInformation($"[Gamma] Using stored markdown ({dbRecord.PresentationMarkdown.Length} chars).");
                        markdown = dbRecord.PresentationMarkdown;
                    }
                    else
                    {
                        // Generate markdown from GL data
                        authService.AuthenticateAndGetToken();
                        PXTrace.WriteInformation($"[Gamma] Authenticated for {tenantName}.");
                        var slideService = new SlideGenerationService(reportGraph, dbRecord, authService, tenantName);
                        slideService.BuildMarkdownPreview();
                        markdown = slideService.LastGeneratedMarkdown;

                        // Persist generated markdown
                        dbRecord.PresentationMarkdown = markdown;
                    }

                    string slideTitle = !string.IsNullOrWhiteSpace(dbRecord.PresentationTitle)
                        ? dbRecord.PresentationTitle
                        : $"{dbRecord.ReportCD} Presentation";

                    var gammaService = new GammaApiService(gammaApiKey);
                    byte[] pptBytes;

                    if (!string.IsNullOrWhiteSpace(dbRecord.GammaTemplateId))
                    {
                        PXTrace.WriteInformation($"[Gamma] Using template ID: {dbRecord.GammaTemplateId} (markdown: {markdown.Length} chars).");
                        pptBytes = gammaService.GeneratePresentationFromTemplate(markdown, dbRecord.GammaTemplateId);
                    }
                    else
                    {
                        PXTrace.WriteInformation($"[Gamma] Submitting generation (markdown: {markdown.Length} chars). Title: {slideTitle}");
                        pptBytes = gammaService.GeneratePresentation(markdown, slideTitle);
                    }

                    PXTrace.WriteInformation($"[Gamma] Downloaded {pptBytes.Length} bytes.");

                    string fileName = $"{dbRecord.ReportCD}_Presentation_{DateTime.Now:yyyyMMdd_HHmm}.pptx";
                    var fileService = new FileService(reportGraph);
                    fileID = fileService.SaveGeneratedDocument(fileName, pptBytes, dbRecord);

                    dbRecord.SlideGeneratedFileID = fileID;
                    dbRecord.SlideStatus          = ReportStatus.Completed;
                    reportGraph.FinancialReport.Update(dbRecord);
                    reportGraph.Actions.PressSave();

                    PXTrace.WriteInformation($"[Gamma] Done. FileID: {fileID}");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"[Gamma] Generation failed: {ex}");
                    if (dbRecord != null)
                    {
                        dbRecord.SlideStatus = ReportStatus.Failed;
                        reportGraph.FinancialReport.Update(dbRecord);
                        try { reportGraph.Actions.PressSave(); } catch { }
                    }
                    throw;
                }
                finally
                {
                    if (authService?.IsAuthenticated == true) authService.Logout();
                }
            });

            return adapter.Get();
        }

        [PXButton(ImageKey = "Copy", ImageSet = "main", Tooltip = "Download Presentation", Connotation = ActionConnotation.Info)]
        [PXUIField(DisplayName = "", MapEnableRights = PXCacheRights.Select, Visible = true)]
        protected virtual System.Collections.IEnumerable downloadPresentation(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Current;

            if (selectedRecord == null)
                throw new PXException(Messages.NoRecordIsSelected);

            if (selectedRecord.SlideGeneratedFileID == null)
                throw new PXException(Messages.NoGeneratedPresentation);

            throw new PXRedirectToFileException(selectedRecord.SlideGeneratedFileID.Value, true);
        }

        #endregion

        #region Public Helper Methods
        // These methods are made public so they can be called by the ReportGenerationService
        public int? GetCompanyIDFromDB(int? reportID)
        {
            if (reportID == null)
                throw new PXException(Messages.ReportIDNull);

            using (new PXConnectionScope())
            {
                var result = PXDatabase.SelectSingle<FLRTFinancialReport>(
                    new PXDataField("CompanyID"),
                    new PXDataFieldValue(nameof(FLRTFinancialReport.ReportID), reportID)
                );

                if (result != null) return result.GetInt32(0);
            }
            throw new PXException(Messages.NoCompanyIDFound, reportID);
        }

        public string MapCompanyIDToTenantName(int? companyID)
        {
            if (companyID == null)
                throw new PXException(Messages.CompanyNumRequired);

            FLRTTenantCredentials tenantCreds = PXSelect<FLRTTenantCredentials,
                Where<FLRTTenantCredentials.companyNum, Equal<Required<FLRTTenantCredentials.companyNum>>>>
                .Select(this, companyID);

            if (tenantCreds == null || string.IsNullOrEmpty(tenantCreds.TenantName))
            {
                PXTrace.WriteError($"No tenant found in FLRTTenantCredentials for CompanyID: {companyID}");
                throw new PXException(Messages.NoTenantMapping);
            }

            return tenantCreds.TenantName;
        }
        #endregion
    }
}
