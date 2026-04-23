using System;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using FinancialReport.Helper;

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
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.subaccountFilter>(e.Cache, e.Row, isAccount);
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.branchFilter>(e.Cache, e.Row, isAccount);
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.organizationFilter>(e.Cache, e.Row, isAccount);
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.ledgerFilter>(e.Cache, e.Row, isAccount);

            // Formula — only for CALCULATED type
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.formula>(e.Cache, e.Row, isCalculated);

            // ParentLineCode — for ACCOUNT, SUBTOTAL, and CALCULATED (not HEADING)
            PXUIFieldAttribute.SetEnabled<FLRTReportLineItem.parentLineCode>(e.Cache, e.Row, isAccount || isSubtotal || isCalculated);

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
                    e.Cache.SetValue<FLRTReportLineItem.subaccountFilter>(e.Row, null);
                    e.Cache.SetValue<FLRTReportLineItem.branchFilter>(e.Row, null);
                    e.Cache.SetValue<FLRTReportLineItem.organizationFilter>(e.Row, null);
                    e.Cache.SetValue<FLRTReportLineItem.ledgerFilter>(e.Row, null);
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

        #endregion
    }
}