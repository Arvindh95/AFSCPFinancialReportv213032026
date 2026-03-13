using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using FinancialReport.Helper;
using FinancialReport.Services;

namespace FinancialReport
{
    /// <summary>
    /// Maintenance screen for Report Definitions and their Line Items.
    /// This is where accountants configure the financial statement structure —
    /// which accounts map to which report lines, sign rules, and calculated fields.
    /// No code deployment needed when account structure changes.
    /// </summary>
    public class FLRTReportDefinitionMaint : PXGraph<FLRTReportDefinitionMaint, FLRTReportDefinition>
    {
        #region Views

        public SelectFrom<FLRTReportDefinition>.View ReportDefinition;

        public SelectFrom<FLRTReportLineItem>
            .Where<FLRTReportLineItem.definitionID.IsEqual<FLRTReportDefinition.definitionID.FromCurrent>>
            .OrderBy<FLRTReportLineItem.sortOrder.Asc>
            .View LineItems;

        #endregion

        #region Definition Events

        protected void _(Events.RowSelected<FLRTReportDefinition> e)
        {
            if (e.Row == null) return;
            bool isNewRecord = e.Cache.GetStatus(e.Row) == PXEntryStatus.Inserted;
            PXUIFieldAttribute.SetEnabled<FLRTReportDefinition.definitionCD>(e.Cache, e.Row, isNewRecord);
            // Prefix is also locked once saved to prevent breaking existing Word templates
            PXUIFieldAttribute.SetEnabled<FLRTReportDefinition.definitionPrefix>(e.Cache, e.Row, isNewRecord);
            Actions["detectColumns"]?.SetEnabled(!string.IsNullOrWhiteSpace(e.Row.GIName));
        }

        protected void _(Events.RowPersisting<FLRTReportDefinition> e)
        {
            if (e.Row == null || e.Operation == PXDBOperation.Delete) return;

            if (string.IsNullOrWhiteSpace(e.Row.DefinitionCD))
            {
                e.Cache.RaiseExceptionHandling<FLRTReportDefinition.definitionCD>(
                    e.Row, e.Row.DefinitionCD,
                    new PXSetPropertyException(Messages.DefinitionCodeRequired, PXErrorLevel.Error, e.Row));
            }

            // Validate prefix is provided
            if (string.IsNullOrWhiteSpace(e.Row.DefinitionPrefix))
            {
                e.Cache.RaiseExceptionHandling<FLRTReportDefinition.definitionPrefix>(
                    e.Row, e.Row.DefinitionPrefix,
                    new PXSetPropertyException(Messages.DefinitionPrefixRequired, PXErrorLevel.Error, e.Row));
                return;
            }

            // Validate prefix is alphanumeric only (no underscores, spaces, or special chars)
            if (!System.Text.RegularExpressions.Regex.IsMatch(e.Row.DefinitionPrefix, @"^[A-Za-z0-9]+$"))
            {
                e.Cache.RaiseExceptionHandling<FLRTReportDefinition.definitionPrefix>(
                    e.Row, e.Row.DefinitionPrefix,
                    new PXSetPropertyException(Messages.DefinitionPrefixMustBeAlphanumeric, PXErrorLevel.Error, e.Row));
                return;
            }

            // Validate prefix uniqueness across all definitions
            FLRTReportDefinition duplicatePrefix = SelectFrom<FLRTReportDefinition>
                .Where<FLRTReportDefinition.definitionPrefix.IsEqual<@P.AsString>
                    .And<FLRTReportDefinition.definitionID.IsNotEqual<@P.AsInt>>>
                .View.Select(this, e.Row.DefinitionPrefix, e.Row.DefinitionID ?? -1);

            if (duplicatePrefix != null)
            {
                e.Cache.RaiseExceptionHandling<FLRTReportDefinition.definitionPrefix>(
                    e.Row, e.Row.DefinitionPrefix,
                    new PXSetPropertyException(Messages.DefinitionPrefixMustBeUnique, PXErrorLevel.Error, e.Row));
            }

            // Validate DefinitionCD uniqueness
            FLRTReportDefinition duplicate = SelectFrom<FLRTReportDefinition>
                .Where<FLRTReportDefinition.definitionCD.IsEqual<@P.AsString>
                    .And<FLRTReportDefinition.definitionID.IsNotEqual<@P.AsInt>>>
                .View.Select(this, e.Row.DefinitionCD, e.Row.DefinitionID ?? -1);

            if (duplicate != null)
            {
                e.Cache.RaiseExceptionHandling<FLRTReportDefinition.definitionCD>(
                    e.Row, e.Row.DefinitionCD,
                    new PXSetPropertyException(Messages.DefinitionCodeMustBeUnique, PXErrorLevel.Error, e.Row));
            }
        }

        #endregion

        #region Line Item Events

        protected void _(Events.RowSelected<FLRTReportLineItem> e)
        {
            if (e.Row == null) return;

            bool isAccount    = e.Row.LineType == FLRTReportLineItem.LineItemType.Account;
            bool isSubtotal   = e.Row.LineType == FLRTReportLineItem.LineItemType.Subtotal;
            bool isCalculated = e.Row.LineType == FLRTReportLineItem.LineItemType.Calculated;
            bool isHeading    = e.Row.LineType == FLRTReportLineItem.LineItemType.Heading;

            // Account range fields — only relevant for ACCOUNT type
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.accountFrom>(e.Cache, e.Row, isAccount);
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.accountTo>(e.Cache, e.Row, isAccount);
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.accountTypeFilter>(e.Cache, e.Row, isAccount);
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.signRule>(e.Cache, e.Row, isAccount);
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.balanceType>(e.Cache, e.Row, isAccount);

            // Formula — only for CALCULATED type
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.formula>(e.Cache, e.Row, isCalculated);

            // ParentLineCode — for ACCOUNT and SUBTOTAL (not CALCULATED or HEADING)
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.parentLineCode>(e.Cache, e.Row, isAccount || isSubtotal);

            // Heading lines have no value — hide irrelevant fields
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.isVisible>(e.Cache, e.Row, !isHeading);
        }

        protected void _(Events.FieldUpdated<FLRTReportLineItem, FLRTReportLineItem.lineType> e)
        {
            if (e.Row == null) return;

            // Auto-clear fields that don't apply to the new type
            switch (e.Row.LineType)
            {
                case FLRTReportLineItem.LineItemType.Subtotal:
                case FLRTReportLineItem.LineItemType.Calculated:
                case FLRTReportLineItem.LineItemType.Heading:
                    e.Cache.SetValue<FLRTReportLineItem.accountFrom>(e.Row, null);
                    e.Cache.SetValue<FLRTReportLineItem.accountTo>(e.Row, null);
                    e.Cache.SetValue<FLRTReportLineItem.accountTypeFilter>(e.Row, null);
                    e.Cache.SetValue<FLRTReportLineItem.signRule>(e.Row, FLRTReportLineItem.SignRuleValue.AsIs);
                    e.Cache.SetValue<FLRTReportLineItem.balanceType>(e.Row, FLRTReportLineItem.BalanceTypeValue.Ending);
                    break;
            }

            if (e.Row.LineType != FLRTReportLineItem.LineItemType.Calculated)
                e.Cache.SetValue<FLRTReportLineItem.formula>(e.Row, null);

            if (e.Row.LineType == FLRTReportLineItem.LineItemType.Heading)
            {
                e.Cache.SetValue<FLRTReportLineItem.parentLineCode>(e.Row, null);
                e.Cache.SetValue<FLRTReportLineItem.isVisible>(e.Row, false);
            }
        }

        protected void _(Events.RowPersisting<FLRTReportLineItem> e)
        {
            if (e.Row == null || e.Operation == PXDBOperation.Delete) return;

            if (string.IsNullOrWhiteSpace(e.Row.LineCode))
            {
                e.Cache.RaiseExceptionHandling<FLRTReportLineItem.lineCode>(
                    e.Row, e.Row.LineCode,
                    new PXSetPropertyException(Messages.LineCodeRequired, PXErrorLevel.Error, e.Row));
            }

            // Validate ACCOUNT lines have a range
            if (e.Row.LineType == FLRTReportLineItem.LineItemType.Account)
            {
                if (string.IsNullOrWhiteSpace(e.Row.AccountFrom))
                {
                    e.Cache.RaiseExceptionHandling<FLRTReportLineItem.accountFrom>(
                        e.Row, e.Row.AccountFrom,
                        new PXSetPropertyException(Messages.AccountFromRequired, PXErrorLevel.Error, e.Row));
                }
                if (string.IsNullOrWhiteSpace(e.Row.AccountTo))
                {
                    e.Cache.RaiseExceptionHandling<FLRTReportLineItem.accountTo>(
                        e.Row, e.Row.AccountTo,
                        new PXSetPropertyException(Messages.AccountToRequired, PXErrorLevel.Error, e.Row));
                }
            }

            // Validate CALCULATED lines have a formula
            if (e.Row.LineType == FLRTReportLineItem.LineItemType.Calculated
                && string.IsNullOrWhiteSpace(e.Row.Formula))
            {
                e.Cache.RaiseExceptionHandling<FLRTReportLineItem.formula>(
                    e.Row, e.Row.Formula,
                    new PXSetPropertyException(Messages.FormulaRequired, PXErrorLevel.Error, e.Row));
            }

            // LineCode uniqueness within the same definition
            FLRTReportLineItem duplicate = SelectFrom<FLRTReportLineItem>
                .Where<FLRTReportLineItem.definitionID.IsEqual<@P.AsInt>
                    .And<FLRTReportLineItem.lineCode.IsEqual<@P.AsString>>
                    .And<FLRTReportLineItem.lineID.IsNotEqual<@P.AsInt>>>
                .View.Select(this, e.Row.DefinitionID, e.Row.LineCode, e.Row.LineID ?? -1);

            if (duplicate != null)
            {
                e.Cache.RaiseExceptionHandling<FLRTReportLineItem.lineCode>(
                    e.Row, e.Row.LineCode,
                    new PXSetPropertyException(Messages.LineCodeMustBeUnique, PXErrorLevel.Error, e.Row));
            }
        }

        #endregion

        #region Actions

        public new PXSave<FLRTReportDefinition> Save;
        public new PXCancel<FLRTReportDefinition> Cancel;
        public new PXInsert<FLRTReportDefinition> Insert;
        public new PXDelete<FLRTReportDefinition> Delete;
        public new PXFirst<FLRTReportDefinition> First;
        public new PXPrevious<FLRTReportDefinition> Previous;
        public new PXNext<FLRTReportDefinition> Next;
        public new PXLast<FLRTReportDefinition> Last;

        /// <summary>
        /// Copies an existing definition (header + all line items) as a new definition.
        /// Useful for creating a variant of an existing report (e.g. BS → BS_NOTES).
        /// </summary>
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Copy Definition")]
        public virtual IEnumerable copyDefinition(PXAdapter adapter)
        {
            var source = ReportDefinition.Current;
            if (source == null) return adapter.Get();

            if (ReportDefinition.Ask(Messages.ConfirmCopyDefinition, MessageButtons.YesNo) != WebDialogResult.Yes)
                return adapter.Get();

            // Build a unique copy prefix (truncate source prefix to 7 chars + "CP" suffix to stay within 10 chars)
            string copyPrefix = string.IsNullOrWhiteSpace(source.DefinitionPrefix)
                ? "COPY"
                : (source.DefinitionPrefix.Length > 7
                    ? source.DefinitionPrefix.Substring(0, 7) + "CP"
                    : source.DefinitionPrefix + "CP");

            var newDef = new FLRTReportDefinition
            {
                DefinitionCD       = source.DefinitionCD + "_COPY",
                DefinitionPrefix   = copyPrefix,
                Description        = source.Description + " (Copy)",
                ReportType         = source.ReportType,
                IsActive           = true,
                GIName             = source.GIName,
                AccountColumn      = source.AccountColumn,
                TypeColumn         = source.TypeColumn,
                BeginningBalColumn = source.BeginningBalColumn,
                EndingBalColumn    = source.EndingBalColumn,
                DebitColumn        = source.DebitColumn,
                CreditColumn       = source.CreditColumn,
                RoundingLevel      = source.RoundingLevel,
                DecimalPlaces      = source.DecimalPlaces
            };
            newDef = ReportDefinition.Insert(newDef);

            // Copy all line items
            var sourceLines = SelectFrom<FLRTReportLineItem>
                .Where<FLRTReportLineItem.definitionID.IsEqual<@P.AsInt>>
                .OrderBy<FLRTReportLineItem.sortOrder.Asc>
                .View.Select(this, source.DefinitionID);

            foreach (FLRTReportLineItem sourceLine in sourceLines)
            {
                var newLine = LineItems.Insert(new FLRTReportLineItem
                {
                    DefinitionID      = newDef.DefinitionID,
                    SortOrder         = sourceLine.SortOrder,
                    LineCode          = sourceLine.LineCode,
                    Description       = sourceLine.Description,
                    LineType          = sourceLine.LineType,
                    AccountFrom       = sourceLine.AccountFrom,
                    AccountTo         = sourceLine.AccountTo,
                    AccountTypeFilter = sourceLine.AccountTypeFilter,
                    SignRule          = sourceLine.SignRule,
                    BalanceType       = sourceLine.BalanceType,
                    ParentLineCode    = sourceLine.ParentLineCode,
                    Formula           = sourceLine.Formula,
                    IsVisible         = sourceLine.IsVisible
                });
            }

            Actions.PressSave();
            ReportDefinition.Current = newDef;
            return adapter.Get();
        }

        /// <summary>
        /// Detects available columns from the specified GI by fetching a single row
        /// and auto-maps them to the column mapping fields.
        /// </summary>
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Detect Columns")]
        public virtual IEnumerable detectColumns(PXAdapter adapter)
        {
            var def = ReportDefinition.Current;
            if (def == null) return adapter.Get();

            string giName = def.GIName;
            if (string.IsNullOrWhiteSpace(giName))
            {
                throw new PXException(Messages.GINameRequired);
            }

            var credential = SelectFrom<FLRTTenantCredentials>.View.SelectSingleBound(this, null);
            if (credential == null)
            {
                throw new PXException(Messages.NoAPIFound);
            }

            FLRTTenantCredentials cred = (FLRTTenantCredentials)credential;
            string tenantName = cred.TenantName;

            try
            {
                var authService = new AuthService(cred.BaseURL, cred.ClientIDNew, cred.ClientSecretNew, cred.UsernameNew, cred.PasswordNew);
                var dataService = new FinancialDataService(authService, tenantName);
                List<string> columns = dataService.FetchGIColumns(giName);

                if (columns == null || columns.Count == 0)
                {
                    throw new PXException(Messages.NoColumnsDetected);
                }

                PXTrace.WriteInformation($"Detected {columns.Count} columns from GI '{giName}': {string.Join(", ", columns)}");

                AutoMapColumns(def, columns);

                ReportDefinition.Update(def);
                ReportDefinition.View.RequestRefresh();
            }
            catch (PXException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PXException(Messages.FailedToDetectColumns, giName, ex.Message);
            }

            return adapter.Get();
        }

        /// <summary>
        /// Auto-maps GI column names to definition column mapping fields
        /// using case-insensitive name matching.
        /// </summary>
        private void AutoMapColumns(FLRTReportDefinition def, List<string> columns)
        {
            string acctCol = columns.FirstOrDefault(c => string.Equals(c, "Account", StringComparison.OrdinalIgnoreCase))
                          ?? columns.FirstOrDefault(c => c.IndexOf("Account", StringComparison.OrdinalIgnoreCase) >= 0
                                                      && c.IndexOf("Sub", StringComparison.OrdinalIgnoreCase) < 0);
            if (acctCol != null) def.AccountColumn = acctCol;

            string typeCol = columns.FirstOrDefault(c => string.Equals(c, "Type", StringComparison.OrdinalIgnoreCase))
                          ?? columns.FirstOrDefault(c => c.IndexOf("AccountType", StringComparison.OrdinalIgnoreCase) >= 0)
                          ?? columns.FirstOrDefault(c => c.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0);
            if (typeCol != null) def.TypeColumn = typeCol;

            string begCol = columns.FirstOrDefault(c => c.IndexOf("BeginningBalance", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? columns.FirstOrDefault(c => c.IndexOf("Beginning", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? columns.FirstOrDefault(c => c.IndexOf("BegBal", StringComparison.OrdinalIgnoreCase) >= 0);
            if (begCol != null) def.BeginningBalColumn = begCol;

            string endCol = columns.FirstOrDefault(c => c.IndexOf("EndingBalance", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? columns.FirstOrDefault(c => c.IndexOf("Ending", StringComparison.OrdinalIgnoreCase) >= 0)
                         ?? columns.FirstOrDefault(c => c.IndexOf("YtdBalance", StringComparison.OrdinalIgnoreCase) >= 0);
            if (endCol != null) def.EndingBalColumn = endCol;

            string debitCol = columns.FirstOrDefault(c => string.Equals(c, "Debit", StringComparison.OrdinalIgnoreCase))
                           ?? columns.FirstOrDefault(c => c.IndexOf("Debit", StringComparison.OrdinalIgnoreCase) >= 0);
            if (debitCol != null) def.DebitColumn = debitCol;

            string creditCol = columns.FirstOrDefault(c => string.Equals(c, "Credit", StringComparison.OrdinalIgnoreCase))
                            ?? columns.FirstOrDefault(c => c.IndexOf("Credit", StringComparison.OrdinalIgnoreCase) >= 0);
            if (creditCol != null) def.CreditColumn = creditCol;
        }

        #endregion
    }
}