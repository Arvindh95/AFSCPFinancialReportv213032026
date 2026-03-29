using System;
using PX.Data;
using PX.Data.Maintenance.GI;
using FinancialReport.Helper;

namespace FinancialReport
{
    /// <summary>
    /// Defines a generic GI-based data source that can pull any column from any Generic Inquiry
    /// and expose the results as placeholders for use in presentations.
    ///
    /// Unlike FLRTReportDefinition (which is GL Trial Balance-specific), this entity is fully
    /// generic: the user configures which GI to query, which column acts as the row key,
    /// which columns hold the filter parameters (period, branch, org, ledger), and what type
    /// each filter column holds so the OData query is formatted correctly.
    ///
    /// Child rows (FLRTGIDataSourceColumn) define the individual columns to extract and
    /// how to aggregate them into placeholder values.
    ///
    /// Placeholders produced: {{PREFIX_ALIAS}} for each visible column row.
    /// </summary>
    [Serializable]
    [PXCacheName("FLRT GI Data Source")]
    public class FLRTGIDataSource : PXBqlTable, IBqlTable
    {
        #region DataSourceID
        [PXDBIdentity]
        [PXUIField(DisplayName = "Data Source ID", Visible = false)]
        public virtual int? DataSourceID { get; set; }
        public abstract class dataSourceID : PX.Data.BQL.BqlInt.Field<dataSourceID> { }
        #endregion

        #region DataSourceCD
        [PXDBString(50, IsUnicode = true, IsKey = true, InputMask = "")]
        [PXDefault]
        [PXUIField(DisplayName = "Data Source Code")]
        [PXSelector(typeof(FLRTGIDataSource.dataSourceCD),
            typeof(FLRTGIDataSource.dataSourceCD),
            typeof(FLRTGIDataSource.prefix),
            typeof(FLRTGIDataSource.description),
            typeof(FLRTGIDataSource.giName),
            Filterable = true)]
        public virtual string DataSourceCD { get; set; }
        public abstract class dataSourceCD : PX.Data.BQL.BqlString.Field<dataSourceCD> { }
        #endregion

        #region Description
        [PXDBString(255, IsUnicode = true)]
        [PXUIField(DisplayName = "Description")]
        public virtual string Description { get; set; }
        public abstract class description : PX.Data.BQL.BqlString.Field<description> { }
        #endregion

        #region Prefix
        /// <summary>
        /// Short alphanumeric prefix (2-10 chars) used to namespace placeholder keys.
        /// Example: "HR" produces {{HR_HEADCOUNT}} in the Word/Gamma template.
        /// Must be unique across all data sources and definitions.
        /// </summary>
        [PXDBString(10, IsUnicode = true)]
        [PXDefault]
        [PXUIField(DisplayName = "Prefix")]
        public virtual string Prefix { get; set; }
        public abstract class prefix : PX.Data.BQL.BqlString.Field<prefix> { }
        #endregion

        #region IsActive
        [PXDBBool]
        [PXDefault(true)]
        [PXUIField(DisplayName = "Active")]
        public virtual bool? IsActive { get; set; }
        public abstract class isActive : PX.Data.BQL.BqlBool.Field<isActive> { }
        #endregion

        // ─── GI Configuration ────────────────────────────────────────────────────

        #region GIName
        [PXDBString(100, IsUnicode = true)]
        [PXDefault]
        [PXUIField(DisplayName = "Generic Inquiry")]
        [PXSelector(typeof(Search<GIDesign.name>),
            typeof(GIDesign.name),
            SubstituteKey = typeof(GIDesign.name),
            ValidateValue = false)]
        public virtual string GIName { get; set; }
        public abstract class giName : PX.Data.BQL.BqlString.Field<giName> { }
        #endregion

        #region KeyColumn
        /// <summary>
        /// The GI column that acts as the row identifier (analogous to Account in Trial Balance).
        /// Used by column rows to define KeyFrom / KeyTo ranges for row filtering.
        /// Leave blank if no key-based filtering is needed.
        /// </summary>
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Key Column")]
        [GIDataSourceColumnSelector]
        public virtual string KeyColumn { get; set; }
        public abstract class keyColumn : PX.Data.BQL.BqlString.Field<keyColumn> { }
        #endregion

        // ─── Filter Column Mapping ────────────────────────────────────────────────
        // Each filter column has a Name (which GI column) and a Type (how to format
        // the OData predicate value). Type defaults to String for safety.

        #region PeriodFilterColumn
        /// <summary>
        /// GI column used to filter by financial period. Leave blank to skip period filtering.
        /// </summary>
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Period Filter Column")]
        [GIDataSourceColumnSelector]
        public virtual string PeriodFilterColumn { get; set; }
        public abstract class periodFilterColumn : PX.Data.BQL.BqlString.Field<periodFilterColumn> { }
        #endregion

        #region PeriodFilterType
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(GIColumnType.String)]
        [PXUIField(DisplayName = "Period Type")]
        [PXStringList(
            new string[] { GIColumnType.String, GIColumnType.Integer, GIColumnType.Decimal, GIColumnType.Date, GIColumnType.Boolean },
            new string[] { "String", "Integer", "Decimal", "Date", "Boolean" })]
        public virtual string PeriodFilterType { get; set; }
        public abstract class periodFilterType : PX.Data.BQL.BqlString.Field<periodFilterType> { }
        #endregion

        #region PeriodFilterTemplate
        /// <summary>
        /// Template for building the period filter value from the presentation header.
        /// Tokens: {YEAR} = CurrYear (e.g. "2025"), {MONTH} = FinancialMonth zero-padded (e.g. "01").
        /// Examples:
        ///   "{MONTH}{YEAR}"  → "012025"  (Trial Balance format)
        ///   "{YEAR}-{MONTH}" → "2025-01"
        ///   "{YEAR}"         → "2025"
        /// Leave blank to skip period filtering entirely.
        /// </summary>
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "Period Template")]
        public virtual string PeriodFilterTemplate { get; set; }
        public abstract class periodFilterTemplate : PX.Data.BQL.BqlString.Field<periodFilterTemplate> { }
        #endregion

        #region PeriodScope
        /// <summary>
        /// Determines how the period filter is applied for Date-type columns:
        ///   Exact   — eq single value (for String/Integer period columns like "012025")
        ///   Monthly — ge first of month, lt first of next month
        ///   Yearly  — ge Jan 1 of year, lt Jan 1 of next year
        /// </summary>
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(PeriodScopeType.Monthly)]
        [PXUIField(DisplayName = "Period Scope")]
        [PXStringList(
            new string[] { PeriodScopeType.Exact, PeriodScopeType.Monthly, PeriodScopeType.Yearly },
            new string[] { "Exact", "Monthly", "Yearly" })]
        public virtual string PeriodScope { get; set; }
        public abstract class periodScope : PX.Data.BQL.BqlString.Field<periodScope> { }
        #endregion

        #region BranchFilterColumn
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Branch Filter Column")]
        [GIDataSourceColumnSelector]
        public virtual string BranchFilterColumn { get; set; }
        public abstract class branchFilterColumn : PX.Data.BQL.BqlString.Field<branchFilterColumn> { }
        #endregion

        #region BranchFilterType
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(GIColumnType.String)]
        [PXUIField(DisplayName = "Branch Type")]
        [PXStringList(
            new string[] { GIColumnType.String, GIColumnType.Integer, GIColumnType.Decimal, GIColumnType.Date, GIColumnType.Boolean },
            new string[] { "String", "Integer", "Decimal", "Date", "Boolean" })]
        public virtual string BranchFilterType { get; set; }
        public abstract class branchFilterType : PX.Data.BQL.BqlString.Field<branchFilterType> { }
        #endregion

        #region OrgFilterColumn
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Org Filter Column")]
        [GIDataSourceColumnSelector]
        public virtual string OrgFilterColumn { get; set; }
        public abstract class orgFilterColumn : PX.Data.BQL.BqlString.Field<orgFilterColumn> { }
        #endregion

        #region OrgFilterType
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(GIColumnType.String)]
        [PXUIField(DisplayName = "Org Type")]
        [PXStringList(
            new string[] { GIColumnType.String, GIColumnType.Integer, GIColumnType.Decimal, GIColumnType.Date, GIColumnType.Boolean },
            new string[] { "String", "Integer", "Decimal", "Date", "Boolean" })]
        public virtual string OrgFilterType { get; set; }
        public abstract class orgFilterType : PX.Data.BQL.BqlString.Field<orgFilterType> { }
        #endregion

        #region LedgerFilterColumn
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Ledger Filter Column")]
        [GIDataSourceColumnSelector]
        public virtual string LedgerFilterColumn { get; set; }
        public abstract class ledgerFilterColumn : PX.Data.BQL.BqlString.Field<ledgerFilterColumn> { }
        #endregion

        #region LedgerFilterType
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(GIColumnType.String)]
        [PXUIField(DisplayName = "Ledger Type")]
        [PXStringList(
            new string[] { GIColumnType.String, GIColumnType.Integer, GIColumnType.Decimal, GIColumnType.Date, GIColumnType.Boolean },
            new string[] { "String", "Integer", "Decimal", "Date", "Boolean" })]
        public virtual string LedgerFilterType { get; set; }
        public abstract class ledgerFilterType : PX.Data.BQL.BqlString.Field<ledgerFilterType> { }
        #endregion

        #region DetectedColumns
        /// <summary>
        /// Comma-separated list of OData column names discovered by the Detect Columns action.
        /// Used to populate the column selector dropdowns with the actual OData property names.
        /// </summary>
        [PXDBString(4000, IsUnicode = true)]
        [PXUIField(DisplayName = "Detected Columns", Visible = false, Enabled = false)]
        public virtual string DetectedColumns { get; set; }
        public abstract class detectedColumns : PX.Data.BQL.BqlString.Field<detectedColumns> { }
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

        #region Period Scope Constants
        public static class PeriodScopeType
        {
            public const string Exact   = "Exact";
            public const string Monthly = "Monthly";
            public const string Yearly  = "Yearly";
        }
        #endregion

        #region Column Type Constants
        public static class GIColumnType
        {
            public const string String  = "String";
            public const string Integer = "Integer";
            public const string Decimal = "Decimal";
            public const string Date    = "Date";
            public const string Boolean = "Boolean";
        }
        #endregion
    }
}
