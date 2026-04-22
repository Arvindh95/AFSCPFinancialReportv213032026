using System;
using PX.Data;
using PX.Data.BQL.Fluent;
using FinancialReport.Helper;

namespace FinancialReport
{
    /// <summary>
    /// Defines a single output column for a GI Data Source.
    ///
    /// Each row describes one value to extract from the GI:
    ///   - Which GI column to read (GIColumn)
    ///   - What type the column holds (ColumnType) — drives how the value is parsed
    ///   - How to aggregate multiple matching rows (AggregateFunction)
    ///   - Optional key range filter (KeyFrom / KeyTo) — rows whose KeyColumn value
    ///     falls in this range are included; leave blank to include all rows
    ///   - Optional additional OData row filter (RowFilter) — appended to the main
    ///     header-level filter with AND, e.g. "Status eq 'Active'"
    ///   - Display format (FormatString) — .NET format string applied to the final value
    ///
    /// The resulting placeholder is: {{PREFIX_COLUMNALIAS}}
    ///
    /// LineType controls the calculation path:
    ///   VALUE      = read + aggregate from GI
    ///   CALCULATED = arithmetic formula referencing other ColumnAlias values
    ///   HEADING    = label only, no placeholder produced
    /// </summary>
    [Serializable]
    [PXCacheName("FLRT GI Data Source Column")]
    public class FLRTGIDataSourceColumn : PXBqlTable, IBqlTable
    {
        #region ColumnID
        [PXDBIdentity(IsKey = true)]
        [PXUIField(DisplayName = "Column ID", Visible = false)]
        public virtual int? ColumnID { get; set; }
        public abstract class columnID : PX.Data.BQL.BqlInt.Field<columnID> { }
        #endregion

        #region DataSourceID
        [PXDBInt]
        [PXDBDefault(typeof(FLRTGIDataSource.dataSourceID))]
        [PXParent(typeof(SelectFrom<FLRTGIDataSource>
            .Where<FLRTGIDataSource.dataSourceID.IsEqual<dataSourceID.FromCurrent>>))]
        [PXUIField(DisplayName = "Data Source ID", Visible = false)]
        public virtual int? DataSourceID { get; set; }
        public abstract class dataSourceID : PX.Data.BQL.BqlInt.Field<dataSourceID> { }
        #endregion

        #region SortOrder
        [PXDBInt]
        [PXDefault(0)]
        [PXUIField(DisplayName = "Sort Order")]
        public virtual int? SortOrder { get; set; }
        public abstract class sortOrder : PX.Data.BQL.BqlInt.Field<sortOrder> { }
        #endregion

        #region ColumnAlias
        /// <summary>
        /// Short code used as the placeholder key: {{PREFIX_ALIAS}}.
        /// Must be unique within the data source.
        /// Use only letters, digits, and underscores.
        /// </summary>
        [PXDBString(100, IsUnicode = true)]
        [PXDefault]
        [PXUIField(DisplayName = "Column Alias")]
        public virtual string ColumnAlias { get; set; }
        public abstract class columnAlias : PX.Data.BQL.BqlString.Field<columnAlias> { }
        #endregion

        #region Description
        [PXDBString(255, IsUnicode = true)]
        [PXUIField(DisplayName = "Description")]
        public virtual string Description { get; set; }
        public abstract class description : PX.Data.BQL.BqlString.Field<description> { }
        #endregion

        #region LineType
        [PXDBString(20, IsUnicode = true)]
        [PXDefault(ColumnLineType.Value)]
        [PXUIField(DisplayName = "Line Type")]
        [PXStringList(
            new string[] { ColumnLineType.Value, ColumnLineType.MultiRow, ColumnLineType.Calculated, ColumnLineType.Heading },
            new string[] { "Value (from GI)", "Multi-Row Expand", "Calculated (formula)", "Heading" })]
        public virtual string LineType { get; set; }
        public abstract class lineType : PX.Data.BQL.BqlString.Field<lineType> { }
        #endregion

        // ─── Value Line Fields ────────────────────────────────────────────────────

        #region GIColumn
        /// <summary>
        /// The GI column whose value is read and aggregated.
        /// Only used when LineType = VALUE.
        /// </summary>
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "GI Column")]
        [GIDataSourceColumnSelector]
        public virtual string GIColumn { get; set; }
        public abstract class gIColumn : PX.Data.BQL.BqlString.Field<gIColumn> { }
        #endregion

        #region ColumnType
        /// <summary>
        /// Data type of the GI column. Determines how the raw JSON value is parsed
        /// and how the aggregated result is formatted.
        ///   Decimal / Integer → numeric; supports Sum/Min/Max/Count/First
        ///   Boolean           → true=1 / false=0; supports Sum (count of true) / First
        ///   Date              → supports First/Min/Max; combined with FormatString for display
        ///   String            → supports First only; passes text through to the placeholder
        /// </summary>
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(FLRTGIDataSource.GIColumnType.Decimal)]
        [PXUIField(DisplayName = "Column Type")]
        [PXStringList(
            new string[] { FLRTGIDataSource.GIColumnType.Decimal, FLRTGIDataSource.GIColumnType.Integer,
                           FLRTGIDataSource.GIColumnType.Boolean, FLRTGIDataSource.GIColumnType.Date,
                           FLRTGIDataSource.GIColumnType.String },
            new string[] { "Decimal", "Integer", "Boolean", "Date", "String" })]
        public virtual string ColumnType { get; set; }
        public abstract class columnType : PX.Data.BQL.BqlString.Field<columnType> { }
        #endregion

        #region AggregateFunction
        /// <summary>
        /// How to combine multiple GI rows that match the key range / row filter.
        ///   Sum   = add all values (Decimal/Integer/Boolean only)
        ///   First = take the first matching row's value (any type)
        ///   Max   = largest value (Decimal/Integer/Date)
        ///   Min   = smallest value (Decimal/Integer/Date)
        ///   Count = number of matching rows regardless of column value
        /// </summary>
        [PXDBString(10, IsUnicode = true)]
        [PXDefault(AggregateFunctionType.Sum)]
        [PXUIField(DisplayName = "Aggregate")]
        [PXStringList(
            new string[] { AggregateFunctionType.Sum, AggregateFunctionType.First,
                           AggregateFunctionType.Max, AggregateFunctionType.Min,
                           AggregateFunctionType.Avg, AggregateFunctionType.Count },
            new string[] { "Sum", "First", "Max", "Min", "Avg", "Count" })]
        public virtual string AggregateFunction { get; set; }
        public abstract class aggregateFunction : PX.Data.BQL.BqlString.Field<aggregateFunction> { }
        #endregion

        #region KeyFrom
        /// <summary>
        /// Start of the key range to include (inclusive).
        /// Compared against the parent data source's KeyColumn value.
        /// Leave blank to include all rows.
        /// </summary>
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Key From")]
        public virtual string KeyFrom { get; set; }
        public abstract class keyFrom : PX.Data.BQL.BqlString.Field<keyFrom> { }
        #endregion

        #region KeyTo
        /// <summary>
        /// End of the key range to include (inclusive).
        /// Leave blank to include all rows from KeyFrom onward.
        /// </summary>
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Key To")]
        public virtual string KeyTo { get; set; }
        public abstract class keyTo : PX.Data.BQL.BqlString.Field<keyTo> { }
        #endregion

        #region RowFilter
        /// <summary>
        /// Optional additional OData filter expression appended to the header-level
        /// filters with AND. Applied per row, not per column.
        /// Example: "Status eq 'Active'" or "Department eq 'Finance'"
        /// Leave blank for no additional filtering.
        /// </summary>
        [PXDBString(500, IsUnicode = true)]
        [PXUIField(DisplayName = "Row Filter (OData)")]
        public virtual string RowFilter { get; set; }
        public abstract class rowFilter : PX.Data.BQL.BqlString.Field<rowFilter> { }
        #endregion

        // ─── Calculated Line Fields ───────────────────────────────────────────────

        #region Formula
        /// <summary>
        /// Arithmetic expression using other ColumnAlias values in this data source.
        /// Supports +, -, *, / and parentheses.
        /// Only used when LineType = CALCULATED.
        /// Example: "REVENUE - TOTAL_COST"
        /// </summary>
        [PXDBString(500, IsUnicode = true)]
        [PXUIField(DisplayName = "Formula")]
        public virtual string Formula { get; set; }
        public abstract class formula : PX.Data.BQL.BqlString.Field<formula> { }
        #endregion

        // ─── Multi-Row Expand Fields ──────────────────────────────────────────────

        #region OrderByColumn
        /// <summary>
        /// For LineType = MULTIROW: the GI column to sort rows by before taking the top N.
        /// Example: "OrderTotal" sorts descending so rank 1 = highest order.
        /// Leave blank to use natural GI order.
        /// </summary>
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Order By Column")]
        [GIDataSourceColumnSelector]
        public virtual string OrderByColumn { get; set; }
        public abstract class orderByColumn : PX.Data.BQL.BqlString.Field<orderByColumn> { }
        #endregion

        #region OrderByDirection
        /// <summary>
        /// Sort direction for Multi-Row Expand: ASC or DESC.
        /// </summary>
        [PXDBString(4, IsUnicode = true)]
        [PXDefault(OrderByDirectionType.Desc, PersistingCheck = PXPersistingCheck.Nothing)]
        [PXUIField(DisplayName = "Sort Direction")]
        [PXStringList(
            new string[] { OrderByDirectionType.Desc, OrderByDirectionType.Asc },
            new string[] { "Descending", "Ascending" })]
        public virtual string OrderByDirection { get; set; }
        public abstract class orderByDirection : PX.Data.BQL.BqlString.Field<orderByDirection> { }
        #endregion

        #region RowLimit
        /// <summary>
        /// For LineType = MULTIROW: how many top rows to expand into placeholders.
        /// Each row produces PREFIX_ALIAS_N_COLUMNNAME placeholders.
        /// </summary>
        [PXDBInt]
        [PXDefault(10, PersistingCheck = PXPersistingCheck.Nothing)]
        [PXUIField(DisplayName = "Row Limit")]
        public virtual int? RowLimit { get; set; }
        public abstract class rowLimit : PX.Data.BQL.BqlInt.Field<rowLimit> { }
        #endregion

        #region DisplayColumns
        /// <summary>
        /// For LineType = MULTIROW: comma-separated list of GI column names to include in the
        /// markdown preview (e.g. "Vendor,OrderTotal"). Leave blank to show all columns.
        /// Does not affect Word template placeholders — those always include all columns.
        /// </summary>
        [PXDBString(500, IsUnicode = true)]
        [PXUIField(DisplayName = "Display Columns (markdown)")]
        public virtual string DisplayColumns { get; set; }
        public abstract class displayColumns : PX.Data.BQL.BqlString.Field<displayColumns> { }
        #endregion

        // ─── Display Fields ───────────────────────────────────────────────────────

        #region FormatString
        /// <summary>
        /// Optional .NET format string applied to the final value before writing
        /// to the placeholder dictionary.
        /// Numeric examples: "N0", "N2", "#,##0", "P1"
        /// Date examples:    "dd MMM yyyy", "MMM yyyy", "yyyy"
        /// Leave blank for default formatting (no rounding, no special formatting).
        /// </summary>
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "Format String")]
        public virtual string FormatString { get; set; }
        public abstract class formatString : PX.Data.BQL.BqlString.Field<formatString> { }
        #endregion

        #region IsVisible
        /// <summary>
        /// When false the placeholder is not written to the output dictionary.
        /// The column is still evaluated (other CALCULATED columns may reference it).
        /// </summary>
        [PXDBBool]
        [PXDefault(true)]
        [PXUIField(DisplayName = "Visible")]
        public virtual bool? IsVisible { get; set; }
        public abstract class isVisible : PX.Data.BQL.BqlBool.Field<isVisible> { }
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

        public static class ColumnLineType
        {
            public const string Value      = "VALUE";
            public const string MultiRow   = "MULTIROW";
            public const string Calculated = "CALCULATED";
            public const string Heading    = "HEADING";
        }

        public static class OrderByDirectionType
        {
            public const string Asc  = "ASC";
            public const string Desc = "DESC";
        }

        public static class AggregateFunctionType
        {
            public const string Sum   = "SUM";
            public const string First = "FIRST";
            public const string Max   = "MAX";
            public const string Min   = "MIN";
            public const string Avg   = "AVG";
            public const string Count = "COUNT";
        }

        #endregion
    }
}
