using System;
using PX.Data;
using PX.Data.BQL.Fluent;

namespace FinancialReport
{
    public class FLRTTenantCredentialsMaint : PXGraph<FLRTTenantCredentialsMaint>
    {
        public PXSave<FLRTTenantCredentials> Save = null!; // Initialized by PXGraph framework
        public PXCancel<FLRTTenantCredentials> Cancel = null!; // Initialized by PXGraph framework

        public SelectFrom<FLRTTenantCredentials>.View TenantCredentials = null!; // Initialized by PXGraph framework

        public FLRTTenantCredentialsMaint()
        {
            PXTrace.WriteInformation("Graph initialized.");
        }

        #region Event Handlers
        // RowPersisting for validation and logging
        protected void FLRTTenantCredentials_RowPersisting(PXCache cache, PXRowPersistingEventArgs e)
        {
            var row = (FLRTTenantCredentials)e.Row;
            if (row == null) return;

            // Validate required fields
            if (row.CompanyNum == null)
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.companyNum>(row, null, new PXSetPropertyException<FLRTTenantCredentials.companyNum>(Messages.CompanyNumRequired, PXErrorLevel.Error));
                throw new PXException(Messages.CompanyNumRequired);
            }
            if (string.IsNullOrEmpty(row.TenantName))
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.tenantName>(row, null, new PXSetPropertyException<FLRTTenantCredentials.tenantName>(Messages.TenantNameRequired, PXErrorLevel.Error));
                throw new PXException(Messages.TenantNameRequired);
            }

            // Check for duplicate TenantName (excluding the current row)
            FLRTTenantCredentials existing = PXSelect<FLRTTenantCredentials,
                Where<FLRTTenantCredentials.tenantName, Equal<Required<FLRTTenantCredentials.tenantName>>,
                    And<FLRTTenantCredentials.companyNum, NotEqual<Required<FLRTTenantCredentials.companyNum>>>>>
                .Select(this, row.TenantName, row.CompanyNum);

            if (existing != null)
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.tenantName>(row, row.TenantName, new PXSetPropertyException<FLRTTenantCredentials.tenantName>(Messages.TenantNameMustBeUnique, PXErrorLevel.Error));
                throw new PXException(Messages.TenantNameMustBeUnique);
            }
        }

        // RowPersisted to confirm success or failure
        protected void FLRTTenantCredentials_RowPersisted(PXCache cache, PXRowPersistedEventArgs e)
        {
            var row = (FLRTTenantCredentials)e.Row;
            if (row == null) return;

            if (e.TranStatus == PXTranStatus.Completed)
            {
                PXTrace.WriteInformation($"Row persisted successfully: CompanyNum={row.CompanyNum}, TenantName={row.TenantName}, Operation={e.Operation}");
            }
            else if (e.TranStatus == PXTranStatus.Aborted)
            {
                PXTrace.WriteError($"Row persistence failed: CompanyNum={row.CompanyNum}, TenantName={row.TenantName}, Operation={e.Operation}");
                if (e.Exception != null)
                {
                    PXTrace.WriteError($"Exception: {e.Exception.Message}\nStack Trace: {e.Exception.StackTrace}");
                }
            }
        }
        #endregion

        #region Actions
        // Override the existing Save action
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Save")]
        protected virtual void save()
        {
            try
            {
                PXTrace.WriteInformation("Save action triggered.");

                // Persist the cache
                TenantCredentials.Cache.Persist(PXDBOperation.Insert | PXDBOperation.Update);

                // Log success
                PXTrace.WriteInformation("Cache persisted successfully.");

                // Call the default save to commit the transaction
                Actions.PressSave();
                PXTrace.WriteInformation("Save action completed.");
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Save failed: {ex.Message}\nStack Trace: {ex.StackTrace}");
                throw new PXException(Messages.FailedToSaveMessage, ex);
            }
        }

        #endregion
    }
}