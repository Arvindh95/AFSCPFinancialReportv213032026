using System;
using PX.Data;
using PX.Data.BQL.Fluent;
using PX.Data.Maintenance.GI;
using FinancialReport.Helper;

namespace FinancialReport
{
    [Serializable]
    [PXCacheName("FLRT Report Definition")]
    public class FLRTReportDefinition : PXBqlTable, IBqlTable
    {
        #region DefinitionID
        [PXDBIdentity]
        [PXUIField(DisplayName = "Definition ID", Visible = false)]
        public virtual int? DefinitionID { get; set; }
        public abstract class definitionID : PX.Data.BQL.BqlInt.Field<definitionID> { }
        #endregion

        #region DefinitionCD
        [PXDBString(50, IsUnicode = true, IsKey = true, InputMask = "")]
        [PXUIField(DisplayName = "Definition Code")]
        [PXDefault]
        [PXSelector(typeof(FLRTReportDefinition.definitionCD),
            typeof(FLRTReportDefinition.definitionCD),
            typeof(FLRTReportDefinition.definitionPrefix),
            typeof(FLRTReportDefinition.description),
            typeof(FLRTReportDefinition.reportType),
            Filterable = true)]
        public virtual string DefinitionCD { get; set; }
        public abstract class definitionCD : PX.Data.BQL.BqlString.Field<definitionCD> { }
        #endregion

        #region DefinitionPrefix
        /// <summary>
        /// Short alphanumeric prefix (2-10 chars) used to namespace placeholder keys
        /// when this definition is used in a multi-definition report.
        /// Example: "BS" produces {{BS_TOTAL_ASSETS_CY}} in the Word template.
        /// Must be unique across all definitions and contain only letters/digits.
        /// </summary>
        [PXDBString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Prefix")]
        [PXDefault]
        public virtual string DefinitionPrefix { get; set; }
        public abstract class definitionPrefix : PX.Data.BQL.BqlString.Field<definitionPrefix> { }
        #endregion

        #region Description
        [PXDBString(255, IsUnicode = true)]
        [PXUIField(DisplayName = "Description")]
        public virtual string Description { get; set; }
        public abstract class description : PX.Data.BQL.BqlString.Field<description> { }
        #endregion

        #region ReportType
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(ReportDefinitionType.BalanceSheet)]
        [PXUIField(DisplayName = "Report Type")]
        [PXStringList(
            new string[] { ReportDefinitionType.BalanceSheet, ReportDefinitionType.ProfitAndLoss, ReportDefinitionType.CashFlow, ReportDefinitionType.EquityChanges, ReportDefinitionType.Custom },
            new string[] { "Balance Sheet", "Profit & Loss", "Cash Flow", "Changes in Equity", "Custom" }
        )]
        public virtual string ReportType { get; set; }
        public abstract class reportType : PX.Data.BQL.BqlString.Field<reportType> { }
        #endregion

        #region IsActive
        [PXDBBool]
        [PXDefault(true)]
        [PXUIField(DisplayName = "Active")]
        public virtual bool? IsActive { get; set; }
        public abstract class isActive : PX.Data.BQL.BqlBool.Field<isActive> { }
        #endregion

        #region GI Name
        [PXDBString(100, IsUnicode = true)]
        [PXDefault("TrialBalance")]
        [PXUIField(DisplayName = "Generic Inquiry Name")]
        [PXSelector(typeof(Search<GIDesign.name>),
            typeof(GIDesign.name),
            SubstituteKey = typeof(GIDesign.name),
            ValidateValue = false)]
        public virtual string GIName { get; set; }
        public abstract class giName : PX.Data.BQL.BqlString.Field<giName> { }
        #endregion

        #region Column Mapping Fields

        #region AccountColumn
        [PXDBString(100, IsUnicode = true)]
        [PXDefault("Account")]
        [PXUIField(DisplayName = "Account Column")]
        [GIColumnSelector]
        public virtual string AccountColumn { get; set; }
        public abstract class accountColumn : PX.Data.BQL.BqlString.Field<accountColumn> { }
        #endregion

        #region TypeColumn
        [PXDBString(100, IsUnicode = true)]
        [PXDefault("Type")]
        [PXUIField(DisplayName = "Account Type Column")]
        [GIColumnSelector]
        public virtual string TypeColumn { get; set; }
        public abstract class typeColumn : PX.Data.BQL.BqlString.Field<typeColumn> { }
        #endregion

        #region BeginningBalColumn
        [PXDBString(100, IsUnicode = true)]
        [PXDefault("BeginningBalance")]
        [PXUIField(DisplayName = "Beginning Balance Column")]
        [GIColumnSelector]
        public virtual string BeginningBalColumn { get; set; }
        public abstract class beginningBalColumn : PX.Data.BQL.BqlString.Field<beginningBalColumn> { }
        #endregion

        #region EndingBalColumn
        [PXDBString(100, IsUnicode = true)]
        [PXDefault("EndingBalance")]
        [PXUIField(DisplayName = "Ending Balance Column")]
        [GIColumnSelector]
        public virtual string EndingBalColumn { get; set; }
        public abstract class endingBalColumn : PX.Data.BQL.BqlString.Field<endingBalColumn> { }
        #endregion

        #region DebitColumn
        [PXDBString(100, IsUnicode = true)]
        [PXDefault("Debit")]
        [PXUIField(DisplayName = "Debit Column")]
        [GIColumnSelector]
        public virtual string DebitColumn { get; set; }
        public abstract class debitColumn : PX.Data.BQL.BqlString.Field<debitColumn> { }
        #endregion

        #region CreditColumn
        [PXDBString(100, IsUnicode = true)]
        [PXDefault("Credit")]
        [PXUIField(DisplayName = "Credit Column")]
        [GIColumnSelector]
        public virtual string CreditColumn { get; set; }
        public abstract class creditColumn : PX.Data.BQL.BqlString.Field<creditColumn> { }
        #endregion

        #endregion

        #region Rounding Fields

        #region RoundingLevel
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(RoundingLevelType.Units)]
        [PXUIField(DisplayName = "Rounding Level")]
        [PXStringList(
            new string[] { RoundingLevelType.Units, RoundingLevelType.Thousands, RoundingLevelType.Millions },
            new string[] { "Units", "Thousands", "Millions" }
        )]
        public virtual string RoundingLevel { get; set; }
        public abstract class roundingLevel : PX.Data.BQL.BqlString.Field<roundingLevel> { }
        #endregion

        #region DecimalPlaces
        [PXDBInt]
        [PXDefault(0)]
        [PXUIField(DisplayName = "Decimal Places")]
        [PXIntList(
            new int[] { 0, 1, 2 },
            new string[] { "0", "1", "2" }
        )]
        public virtual int? DecimalPlaces { get; set; }
        public abstract class decimalPlaces : PX.Data.BQL.BqlInt.Field<decimalPlaces> { }
        #endregion

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

        #region Report Definition Types
        public static class ReportDefinitionType
        {
            public const string BalanceSheet   = "BS";
            public const string ProfitAndLoss  = "PL";
            public const string CashFlow       = "CF";
            public const string EquityChanges  = "EQ";
            public const string Custom         = "CU";
        }
        #endregion

        #region Rounding Level Types
        public static class RoundingLevelType
        {
            public const string Units     = "UNITS";
            public const string Thousands = "THOUS";
            public const string Millions  = "MILL";
        }
        #endregion
    }
}