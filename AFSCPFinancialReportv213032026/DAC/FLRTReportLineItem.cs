using System;
using PX.Data;
using PX.Data.BQL.Fluent;
using PX.Objects.GL;
using PX.Objects.GL.DAC;

namespace FinancialReport
{
    [Serializable]
    [PXCacheName("FLRT Report Line Item")]
    public class FLRTReportLineItem : PXBqlTable, IBqlTable
    {
        #region LineID
        [PXDBIdentity(IsKey = true)]
        [PXUIField(DisplayName = "Line ID", Visible = false)]
        public virtual int? LineID { get; set; }
        public abstract class lineID : PX.Data.BQL.BqlInt.Field<lineID> { }
        #endregion

        #region DefinitionID
        [PXDBInt]
        [PXDBDefault(typeof(FLRTReportDefinition.definitionID))]
        [PXParent(typeof(SelectFrom<FLRTReportDefinition>
            .Where<FLRTReportDefinition.definitionID.IsEqual<definitionID.FromCurrent>>))]
        [PXUIField(DisplayName = "Definition ID", Visible = false)]
        public virtual int? DefinitionID { get; set; }
        public abstract class definitionID : PX.Data.BQL.BqlInt.Field<definitionID> { }
        #endregion

        #region SortOrder
        [PXDBInt]
        [PXDefault(0)]
        [PXUIField(DisplayName = "Sort Order")]
        public virtual int? SortOrder { get; set; }
        public abstract class sortOrder : PX.Data.BQL.BqlInt.Field<sortOrder> { }
        #endregion

        #region LineCode
        /// <summary>
        /// Unique code for this line within the definition. Used in Word template as {{LINECODE_CY}}.
        /// e.g. CASH, TOTAL_ASSETS, NET_INCOME
        /// </summary>
        [PXDBString(100, IsUnicode = true)]
        [PXDefault]
        [PXUIField(DisplayName = "Line Code")]
        public virtual string LineCode { get; set; }
        public abstract class lineCode : PX.Data.BQL.BqlString.Field<lineCode> { }
        #endregion

        #region Description
        [PXDBString(255, IsUnicode = true)]
        [PXUIField(DisplayName = "Description")]
        public virtual string Description { get; set; }
        public abstract class description : PX.Data.BQL.BqlString.Field<description> { }
        #endregion

        #region LineType
        /// <summary>
        /// ACCOUNT   = sum all GL accounts in the AccountFrom:AccountTo range
        /// SUBTOTAL  = sum all lines that have this LineCode as their ParentLineCode
        /// CALCULATED = evaluate the Formula expression referencing other LineCodes
        /// HEADING   = display only, no value (section headers)
        /// </summary>
        [PXDBString(20, IsUnicode = true)]
        [PXDefault(LineItemType.Account)]
        [PXUIField(DisplayName = "Line Type")]
        [PXStringList(
            new string[] { LineItemType.Account, LineItemType.Subtotal, LineItemType.Calculated, LineItemType.Heading },
            new string[] { "Account Range", "Subtotal", "Calculated", "Heading" }
        )]
        public virtual string LineType { get; set; }
        public abstract class lineType : PX.Data.BQL.BqlString.Field<lineType> { }
        #endregion

        #region AccountFrom
        /// <summary>
        /// Start of the account range to sum. Inclusive. e.g. A10100
        /// Only used when LineType = ACCOUNT.
        /// </summary>
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "Account From")]
        public virtual string AccountFrom { get; set; }
        public abstract class accountFrom : PX.Data.BQL.BqlString.Field<accountFrom> { }
        #endregion

        #region AccountTo
        /// <summary>
        /// End of the account range to sum. Inclusive. e.g. A10199
        /// Only used when LineType = ACCOUNT.
        /// </summary>
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "Account To")]
        public virtual string AccountTo { get; set; }
        public abstract class accountTo : PX.Data.BQL.BqlString.Field<accountTo> { }
        #endregion

        #region AccountTypeFilter
        /// <summary>
        /// Optional. Restricts this line to only accounts of this type from the GI.
        /// A = Asset, L = Liability, E = Expense, I = Income, Q = Equity
        /// Leave blank to include all types in the range.
        /// </summary>
        [PXDBString(5, IsUnicode = true)]
        [PXUIField(DisplayName = "Account Type Filter")]
        [PXStringList(
            new string[] { "", AccountTypeValue.Asset, AccountTypeValue.Liability, AccountTypeValue.Expense, AccountTypeValue.Income, AccountTypeValue.Equity },
            new string[] { "All Types", "Asset (A)", "Liability (L)", "Expense (E)", "Income (I)", "Equity (Q)" }
        )]
        public virtual string AccountTypeFilter { get; set; }
        public abstract class accountTypeFilter : PX.Data.BQL.BqlString.Field<accountTypeFilter> { }
        #endregion

        #region SignRule
        /// <summary>
        /// ASIS = keep the raw GL sign (Asset, Expense accounts)
        /// FLIP = multiply by -1 for presentation (Liability, Income, Equity accounts)
        /// </summary>
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(SignRuleValue.AsIs)]
        [PXUIField(DisplayName = "Sign Rule")]
        [PXStringList(
            new string[] { SignRuleValue.AsIs, SignRuleValue.Flip },
            new string[] { "As-Is", "Flip Sign" }
        )]
        public virtual string SignRule { get; set; }
        public abstract class signRule : PX.Data.BQL.BqlString.Field<signRule> { }
        #endregion

        #region BalanceType
        /// <summary>
        /// Which balance field to use from the GI.
        /// ENDING    = EndingBalance (default, most common)
        /// BEGINNING = BeginningBalance
        /// DEBIT     = Debit movement
        /// CREDIT    = Credit movement
        /// MOVEMENT  = Movement (net debit/credit)
        /// </summary>
        [PXDBString(15, IsUnicode = true)]
        [PXDefault(BalanceTypeValue.Ending)]
        [PXUIField(DisplayName = "Balance Type")]
        [PXStringList(
            new string[] { BalanceTypeValue.Ending, BalanceTypeValue.Beginning, BalanceTypeValue.JanuaryBeginning, BalanceTypeValue.Debit, BalanceTypeValue.Credit, BalanceTypeValue.Movement, BalanceTypeValue.PeriodDebit, BalanceTypeValue.PeriodCredit, BalanceTypeValue.PeriodMovement },
            new string[] { "Ending Balance", "Beginning Balance", "January Beginning Balance", "Debit (YTD)", "Credit (YTD)", "Movement (YTD)", "Period Debit", "Period Credit", "Period Movement" }
        )]
        public virtual string BalanceType { get; set; }
        public abstract class balanceType : PX.Data.BQL.BqlString.Field<balanceType> { }
        #endregion

        #region ParentLineCode
        /// <summary>
        /// When LineType = SUBTOTAL, the engine sums all lines whose ParentLineCode = this LineCode.
        /// e.g. CASH, RECEIVABLES, INVENTORY all have ParentLineCode = "CURRENT_ASSETS"
        /// so CURRENT_ASSETS (SUBTOTAL) = CASH + RECEIVABLES + INVENTORY
        /// </summary>
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Group / Parent Line")]
        public virtual string ParentLineCode { get; set; }
        public abstract class parentLineCode : PX.Data.BQL.BqlString.Field<parentLineCode> { }
        #endregion

        #region Formula
        /// <summary>
        /// Only used when LineType = CALCULATED.
        /// Simple expression referencing other LineCodes with +, -, *, / operators.
        /// Examples:
        ///   REVENUE - TOTAL_EXPENSES
        ///   TOTAL_LIABILITIES + TOTAL_EQUITY
        ///   GROSS_PROFIT - OPERATING_EXPENSES - FINANCE_COSTS
        /// </summary>
        [PXDBString(500, IsUnicode = true)]
        [PXUIField(DisplayName = "Formula")]
        public virtual string Formula { get; set; }
        public abstract class formula : PX.Data.BQL.BqlString.Field<formula> { }
        #endregion

        #region IsVisible
        /// <summary>
        /// If false, the engine still calculates this line (for use in formulas/subtotals)
        /// but the placeholder resolves to empty string instead of a number.
        /// Useful for intermediate calculation lines.
        /// </summary>
        [PXDBBool]
        [PXDefault(true)]
        [PXUIField(DisplayName = "Visible in Report")]
        public virtual bool? IsVisible { get; set; }
        public abstract class isVisible : PX.Data.BQL.BqlBool.Field<isVisible> { }
        #endregion

        #region SubaccountFilter
        /// <summary>
        /// Optional. When set, only rows whose Subaccount exactly matches this value are included.
        /// Leave blank to include all subaccounts (default behaviour).
        /// Example: "000-000"
        /// Only applied when LineType = ACCOUNT.
        /// </summary>
        [PXDBString(30, IsUnicode = true)]
        [PXUIField(DisplayName = "Subaccount Filter")]
        public virtual string SubaccountFilter { get; set; }
        public abstract class subaccountFilter : PX.Data.BQL.BqlString.Field<subaccountFilter> { }
        #endregion

        #region BranchFilter
        /// <summary>
        /// Optional. When set, only rows whose BranchID exactly matches this value are included.
        /// Leave blank to include all branches. Only applied when LineType = ACCOUNT.
        /// </summary>
        [PXDBString(30, IsUnicode = true)]
        [PXUIField(DisplayName = "Branch Filter")]
        [PXSelector(typeof(Search<Branch.branchCD>),
            typeof(Branch.branchCD),
            typeof(Branch.acctName),
            SubstituteKey = typeof(Branch.branchCD),
            ValidateValue = false)]
        public virtual string BranchFilter { get; set; }
        public abstract class branchFilter : PX.Data.BQL.BqlString.Field<branchFilter> { }
        #endregion

        #region OrganizationFilter
        /// <summary>
        /// Optional. When set, only rows whose OrganizationID exactly matches this value are included.
        /// Leave blank to include all organizations. Only applied when LineType = ACCOUNT.
        /// </summary>
        [PXDBString(30, IsUnicode = true)]
        [PXUIField(DisplayName = "Organization Filter")]
        [PXSelector(typeof(Search<Organization.organizationCD>),
            typeof(Organization.organizationCD),
            typeof(Organization.organizationName),
            SubstituteKey = typeof(Organization.organizationCD),
            ValidateValue = false)]
        public virtual string OrganizationFilter { get; set; }
        public abstract class organizationFilter : PX.Data.BQL.BqlString.Field<organizationFilter> { }
        #endregion

        #region LedgerFilter
        /// <summary>
        /// Optional. When set, only rows whose LedgerID matches this value are included.
        /// Leave blank to include all ledgers. Only applied when LineType = ACCOUNT.
        /// </summary>
        [PXDBString(20, IsUnicode = true)]
        [PXUIField(DisplayName = "Ledger Filter")]
        [PXSelector(typeof(Search<Ledger.ledgerCD>),
            typeof(Ledger.ledgerCD),
            typeof(Ledger.descr),
            SubstituteKey = typeof(Ledger.ledgerCD),
            ValidateValue = false)]
        public virtual string LedgerFilter { get; set; }
        public abstract class ledgerFilter : PX.Data.BQL.BqlString.Field<ledgerFilter> { }
        #endregion

        #region NoteID
        [PXNote]
        public virtual Guid? NoteID { get; set; }
        public abstract class noteID : PX.Data.BQL.BqlGuid.Field<noteID> { }
        #endregion

        #region Audit Fields
        [PXDBCreatedDateTime]
        public virtual DateTime? CreatedDateTime { get; set; }
        public abstract class createdDateTime : PX.Data.BQL.BqlDateTime.Field<createdDateTime> { }

        [PXDBCreatedByID]
        public virtual Guid? CreatedByID { get; set; }
        public abstract class createdByID : PX.Data.BQL.BqlGuid.Field<createdByID> { }

        [PXDBCreatedByScreenID]
        public virtual string CreatedByScreenID { get; set; }
        public abstract class createdByScreenID : PX.Data.BQL.BqlString.Field<createdByScreenID> { }

        [PXDBLastModifiedDateTime]
        public virtual DateTime? LastModifiedDateTime { get; set; }
        public abstract class lastModifiedDateTime : PX.Data.BQL.BqlDateTime.Field<lastModifiedDateTime> { }

        [PXDBLastModifiedByID]
        public virtual Guid? LastModifiedByID { get; set; }
        public abstract class lastModifiedByID : PX.Data.BQL.BqlGuid.Field<lastModifiedByID> { }

        [PXDBLastModifiedByScreenID]
        public virtual string LastModifiedByScreenID { get; set; }
        public abstract class lastModifiedByScreenID : PX.Data.BQL.BqlString.Field<lastModifiedByScreenID> { }

        [PXDBTimestamp]
        public virtual byte[] Tstamp { get; set; }
        public abstract class tstamp : PX.Data.BQL.BqlByteArray.Field<tstamp> { }
        #endregion

        #region Constants

        public static class LineItemType
        {
            public const string Account    = "ACCOUNT";
            public const string Subtotal   = "SUBTOTAL";
            public const string Calculated = "CALCULATED";
            public const string Heading    = "HEADING";
        }

        public static class SignRuleValue
        {
            public const string AsIs = "ASIS";
            public const string Flip = "FLIP";
        }

        public static class BalanceTypeValue
        {
            public const string Ending            = "ENDING";
            public const string Beginning         = "BEGINNING";
            /// <summary>
            /// Explicitly uses the BeginningBalance of period 01-{Year} (January).
            /// Equivalent to Beginning for December fiscal-year-end; differs for other month-ends.
            /// </summary>
            public const string JanuaryBeginning  = "JANBEGINNING";
            /// <summary>Year-to-date cumulative debit (fiscal start → selected month).</summary>
            public const string Debit             = "DEBIT";
            /// <summary>Year-to-date cumulative credit (fiscal start → selected month).</summary>
            public const string Credit            = "CREDIT";
            /// <summary>Year-to-date cumulative movement / net (fiscal start → selected month).</summary>
            public const string Movement          = "MOVEMENT";
            /// <summary>Debit for the selected period only (single month). Use for monthly reports.</summary>
            public const string PeriodDebit       = "PDEBIT";
            /// <summary>Credit for the selected period only (single month). Use for monthly reports.</summary>
            public const string PeriodCredit      = "PCREDIT";
            /// <summary>Net movement for the selected period only (single month). Use for monthly reports.</summary>
            public const string PeriodMovement    = "PMOVEMENT";
        }

        public static class AccountTypeValue
        {
            public const string Asset     = "A";
            public const string Liability = "L";
            public const string Expense   = "E";
            public const string Income    = "I";
            public const string Equity    = "Q";
        }

        #endregion
    }
}