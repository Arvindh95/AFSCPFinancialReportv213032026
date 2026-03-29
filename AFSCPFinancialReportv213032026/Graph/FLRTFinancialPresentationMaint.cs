using System;
using PX.Data;
using PX.Data.BQL.Fluent;
using System.Collections.Generic;
using PX.SM;
using System.Linq;
using FinancialReport.Helper;
using FinancialReport.Services;
using PX.Data.WorkflowAPI;

namespace FinancialReport
{
    public class FLRTFinancialPresentationMaint : PXGraph<FLRTFinancialPresentationMaint, FLRTPresentationGeneration>
    {
        #region DAC Views
        public SelectFrom<FLRTPresentationGeneration>.View PresentationRecord = null!;

        public SelectFrom<FLRTPresentationDefinitionLink>
            .Where<FLRTPresentationDefinitionLink.presentationID.IsEqual<FLRTPresentationGeneration.presentationID.FromCurrent>>
            .OrderBy<FLRTPresentationDefinitionLink.displayOrder.Asc>
            .View DefinitionLinks;

        public SelectFrom<FLRTPresentationDataSourceLink>
            .Where<FLRTPresentationDataSourceLink.presentationID.IsEqual<FLRTPresentationGeneration.presentationID.FromCurrent>>
            .OrderBy<FLRTPresentationDataSourceLink.displayOrder.Asc>
            .View DataSourceLinks;
        #endregion

        #region Events

        protected void _(Events.FieldSelecting<FLRTPresentationDataSourceLink, FLRTPresentationDataSourceLink.dataSourcePrefix> e)
        {
            if (e.Row?.DataSourceID == null) return;
            var ds = PXSelectorAttribute.Select<FLRTPresentationDataSourceLink.dataSourceID>(e.Cache, e.Row) as FLRTGIDataSource;
            if (ds != null)
                e.ReturnValue = ds.Prefix;
        }

        protected void _(Events.FieldSelecting<FLRTPresentationDefinitionLink, FLRTPresentationDefinitionLink.definitionPrefix> e)
        {
            if (e.Row?.DefinitionID == null) return;
            var def = PXSelectorAttribute.Select<FLRTPresentationDefinitionLink.definitionID>(e.Cache, e.Row) as FLRTReportDefinition;
            if (def != null)
                e.ReturnValue = def.DefinitionPrefix;
        }

        protected void _(Events.RowPersisting<FLRTPresentationDefinitionLink> e)
        {
            if (e.Row == null || e.Operation == PXDBOperation.Delete) return;

            if (e.Row.DefinitionID == null)
            {
                e.Cache.RaiseExceptionHandling<FLRTPresentationDefinitionLink.definitionID>(
                    e.Row, e.Row.DefinitionID,
                    new PXSetPropertyException(Messages.DefinitionRequired, PXErrorLevel.Error, e.Row));
                return;
            }

            var def = PXSelectorAttribute.Select<FLRTPresentationDefinitionLink.definitionID>(e.Cache, e.Row) as FLRTReportDefinition;
            if (def == null || string.IsNullOrWhiteSpace(def.DefinitionPrefix)) return;

            foreach (FLRTPresentationDefinitionLink other in DefinitionLinks.Cache.Cached)
            {
                if (other.LinkID == e.Row.LinkID) continue;
                if (other.DefinitionID == null) continue;

                var otherDef = PXSelectorAttribute.Select<FLRTPresentationDefinitionLink.definitionID>(
                    DefinitionLinks.Cache, other) as FLRTReportDefinition;

                if (otherDef != null
                    && string.Equals(otherDef.DefinitionPrefix, def.DefinitionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    e.Cache.RaiseExceptionHandling<FLRTPresentationDefinitionLink.definitionID>(
                        e.Row, e.Row.DefinitionID,
                        new PXSetPropertyException(Messages.DuplicatePrefixInReport, PXErrorLevel.Error, e.Row, def.DefinitionPrefix));
                    return;
                }
            }
        }

        protected void FLRTPresentationGeneration_RowSelected(PXCache cache, PXRowSelectedEventArgs e)
        {
            var row = (FLRTPresentationGeneration)e.Row;
            if (row == null) return;

            bool isInProgress = row.SlideStatus == ReportStatus.InProgress;

            PXUIFieldAttribute.SetEnabled(cache, row, !isInProgress);
            GenerateGamma.SetEnabled(!isInProgress);
            DownloadPresentation.SetEnabled(row.SlideGeneratedFileID != null);
        }

        #endregion

        #region Actions
        public new PXSave<FLRTPresentationGeneration> Save = null!;
        public new PXCancel<FLRTPresentationGeneration> Cancel = null!;
        public PXAction<FLRTPresentationGeneration> PreviewMarkdown = null!;
        public PXAction<FLRTPresentationGeneration> GenerateGamma = null!;
        public PXAction<FLRTPresentationGeneration> DownloadPresentation = null!;
        public PXAction<FLRTPresentationGeneration> ResetStatus = null!;

        [PXButton(CommitChanges = true, ImageKey = "RecordEdit", ImageSet = "main", Tooltip = "Preview Markdown", Connotation = ActionConnotation.Info)]
        [PXUIField(DisplayName = "Preview Markdown", MapEnableRights = PXCacheRights.Update, Visible = true)]
        protected virtual System.Collections.IEnumerable previewMarkdown(PXAdapter adapter)
        {
            var selectedRecord = PresentationRecord.Current;

            if (selectedRecord == null) throw new PXException(Messages.PleaseSelectTemplate);
            if (selectedRecord.PresentationID == null) throw new PXException(Messages.NoReportSelected);

            int? companyID = GetCompanyIDFromDB(selectedRecord.PresentationID);
            string tenantName = MapCompanyIDToTenantName(companyID);
            AcumaticaCredentials tenantCredentials = CredentialProvider.GetCredentials(tenantName);

            var authService = new AuthService(
                tenantCredentials.BaseURL,
                tenantCredentials.ClientId,
                tenantCredentials.ClientSecret,
                tenantCredentials.Username,
                tenantCredentials.Password
            );

            int? presentationID = selectedRecord.PresentationID;

            PXLongOperation.StartOperation(this, () =>
            {
                var presentationGraph = PXGraph.CreateInstance<FLRTFinancialPresentationMaint>();
                FLRTPresentationGeneration dbRecord = null;

                try
                {
                    dbRecord = PXSelect<FLRTPresentationGeneration,
                        Where<FLRTPresentationGeneration.presentationID, Equal<Required<FLRTPresentationGeneration.presentationID>>>>
                        .SelectSingleBound(presentationGraph, null, presentationID);

                    if (dbRecord == null) throw new PXException(Messages.FailedToRetrieveFile);

                    authService.AuthenticateAndGetToken();

                    var slideService = new SlideGenerationService(presentationGraph, dbRecord, authService, tenantName);
                    Guid fileID = slideService.BuildMarkdownPreview();

                    dbRecord.PresentationMarkdown = slideService.LastGeneratedMarkdown;

                    presentationGraph.PresentationRecord.Update(dbRecord);
                    presentationGraph.Actions.PressSave();

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

        [PXButton(CommitChanges = true, ImageKey = "DataEntry", ImageSet = "main", Tooltip = "Generate Presentation", Connotation = ActionConnotation.Success)]
        [PXUIField(DisplayName = "Generate Presentation", MapEnableRights = PXCacheRights.Update, Visible = true)]
        protected virtual System.Collections.IEnumerable generateGamma(PXAdapter adapter)
        {
            var selectedRecord = PresentationRecord.Current;

            if (selectedRecord == null) throw new PXException(Messages.PleaseSelectTemplate);
            if (selectedRecord.PresentationID == null) throw new PXException(Messages.NoReportSelected);
            if (selectedRecord.SlideStatus == ReportStatus.InProgress) throw new PXException(Messages.SlideGenerationInProgress);
            if (string.IsNullOrWhiteSpace(selectedRecord.PresentationTitle)) throw new PXException(Messages.PresentationTitleRequired);

            int? companyID = GetCompanyIDFromDB(selectedRecord.PresentationID);
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

            selectedRecord.SlideStatus = ReportStatus.InProgress;
            PresentationRecord.Update(selectedRecord);
            Actions.PressSave();

            int? presentationID = selectedRecord.PresentationID;

            PXLongOperation.StartOperation(this, () =>
            {
                var presentationGraph = PXGraph.CreateInstance<FLRTFinancialPresentationMaint>();
                FLRTPresentationGeneration dbRecord = null;

                try
                {
                    dbRecord = PXSelect<FLRTPresentationGeneration,
                        Where<FLRTPresentationGeneration.presentationID, Equal<Required<FLRTPresentationGeneration.presentationID>>>>
                        .SelectSingleBound(presentationGraph, null, presentationID);

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
                        authService.AuthenticateAndGetToken();
                        PXTrace.WriteInformation($"[Gamma] Authenticated for {tenantName}.");
                        var slideService = new SlideGenerationService(presentationGraph, dbRecord, authService, tenantName);
                        slideService.BuildMarkdownPreview();
                        markdown = slideService.LastGeneratedMarkdown;

                        dbRecord.PresentationMarkdown = markdown;
                    }

                    string slideTitle = !string.IsNullOrWhiteSpace(dbRecord.PresentationTitle)
                        ? dbRecord.PresentationTitle
                        : $"{dbRecord.PresentationCD} Presentation";

                    var gammaService = new GammaApiService(gammaApiKey);
                    byte[] pptBytes;

                    if (!string.IsNullOrWhiteSpace(dbRecord.GammaTemplateId))
                    {
                        PXTrace.WriteInformation($"[Gamma] Using template ID: {dbRecord.GammaTemplateId}.");
                        pptBytes = gammaService.GeneratePresentationFromTemplate(markdown, dbRecord.GammaTemplateId);
                    }
                    else
                    {
                        PXTrace.WriteInformation($"[Gamma] Submitting generation. Title: {slideTitle}");
                        pptBytes = gammaService.GeneratePresentation(markdown, slideTitle);
                    }

                    PXTrace.WriteInformation($"[Gamma] Downloaded {pptBytes.Length} bytes.");

                    string fileName = $"{dbRecord.PresentationCD}_Presentation_{DateTime.Now:yyyyMMdd_HHmm}.pptx";
                    var fileService = new FileService(presentationGraph);
                    fileID = fileService.SaveGeneratedDocument(fileName, pptBytes, dbRecord);

                    dbRecord.SlideGeneratedFileID = fileID;
                    dbRecord.SlideStatus          = ReportStatus.Completed;
                    presentationGraph.PresentationRecord.Update(dbRecord);
                    presentationGraph.Actions.PressSave();

                    PXTrace.WriteInformation($"[Gamma] Done. FileID: {fileID}");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"[Gamma] Generation failed: {ex}");
                    if (dbRecord != null)
                    {
                        dbRecord.SlideStatus = ReportStatus.Failed;
                        presentationGraph.PresentationRecord.Update(dbRecord);
                        try { presentationGraph.Actions.PressSave(); } catch { }
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
        [PXUIField(DisplayName = "Download Presentation", MapEnableRights = PXCacheRights.Select, Visible = true)]
        protected virtual System.Collections.IEnumerable downloadPresentation(PXAdapter adapter)
        {
            var selectedRecord = PresentationRecord.Current;

            if (selectedRecord == null)
                throw new PXException(Messages.NoRecordIsSelected);

            if (selectedRecord.SlideGeneratedFileID == null)
                throw new PXException(Messages.NoGeneratedPresentation);

            throw new PXRedirectToFileException(selectedRecord.SlideGeneratedFileID.Value, true);
        }

        [PXButton(CommitChanges = true, ImageKey = "Refresh", ImageSet = "main", Tooltip = "Reset Status", Connotation = ActionConnotation.Danger)]
        [PXUIField(DisplayName = "Reset Status", MapEnableRights = PXCacheRights.Update, Visible = true)]
        protected virtual System.Collections.IEnumerable resetStatus(PXAdapter adapter)
        {
            var selectedRecord = PresentationRecord.Current;

            if (selectedRecord == null)
                throw new PXException(Messages.UnselectedResetStatus);

            string currentStatus = selectedRecord.SlideStatus ?? "Unknown";
            string statusName = currentStatus == ReportStatus.InProgress ? "In Progress" :
                               currentStatus == ReportStatus.Failed ? "Failed" :
                               currentStatus == ReportStatus.Completed ? "Completed" : "Pending";

            if (PresentationRecord.Ask("Confirm Reset",
                $"Reset this presentation from '{statusName}' to 'Pending'? This will allow regeneration.",
                MessageButtons.YesNo) == WebDialogResult.Yes)
            {
                selectedRecord.SlideStatus = ReportStatus.Pending;
                selectedRecord.SlideGeneratedFileID = null;
                PresentationRecord.Update(selectedRecord);
                Actions.PressSave();

                PXTrace.WriteInformation($"Presentation {selectedRecord.PresentationCD} status reset from {statusName} to Pending.");
            }

            return adapter.Get();
        }

        #endregion

        #region Helper Methods
        public int? GetCompanyIDFromDB(int? presentationID)
        {
            if (presentationID == null)
                throw new PXException(Messages.ReportIDNull);

            using (new PXConnectionScope())
            {
                var result = PXDatabase.SelectSingle<FLRTPresentationGeneration>(
                    new PXDataField("CompanyID"),
                    new PXDataFieldValue(nameof(FLRTPresentationGeneration.PresentationID), presentationID)
                );

                if (result != null) return result.GetInt32(0);
            }
            throw new PXException(Messages.NoCompanyIDFound, presentationID);
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
