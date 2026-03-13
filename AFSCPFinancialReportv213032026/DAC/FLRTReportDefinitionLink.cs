using System;
using PX.Data;
using PX.Data.BQL.Fluent;

namespace FinancialReport
{
    /// <summary>
    /// Links one or more Report Definitions to a single Financial Report record.
    /// Replaces the legacy single DefinitionID field on FLRTFinancialReport.
    ///
    /// Each linked definition contributes its calculated line values (prefixed by
    /// FLRTReportDefinition.DefinitionPrefix) to the unified placeholder dictionary.
    /// Cross-definition formula references are resolved via topological sort in
    /// ReportCalculationEngine.CalculateAll().
    /// </summary>
    [Serializable]
    [PXCacheName("FLRT Report Definition Link")]
    public class FLRTReportDefinitionLink : PXBqlTable, IBqlTable
    {
        #region LinkID
        [PXDBIdentity(IsKey = true)]
        [PXUIField(DisplayName = "Link ID", Visible = false)]
        public virtual int? LinkID { get; set; }
        public abstract class linkID : PX.Data.BQL.BqlInt.Field<linkID> { }
        #endregion

        #region ReportID
        [PXDBInt]
        [PXDBDefault(typeof(FLRTFinancialReport.reportID))]
        [PXParent(typeof(SelectFrom<FLRTFinancialReport>
            .Where<FLRTFinancialReport.reportID.IsEqual<reportID.FromCurrent>>))]
        [PXUIField(DisplayName = "Report ID", Visible = false)]
        public virtual int? ReportID { get; set; }
        public abstract class reportID : PX.Data.BQL.BqlInt.Field<reportID> { }
        #endregion

        #region DefinitionID
        [PXDBInt]
        [PXUIField(DisplayName = "Definition")]
        [PXDefault]
        [PXSelector(
            typeof(Search<FLRTReportDefinition.definitionID>),
            typeof(FLRTReportDefinition.definitionCD),
            typeof(FLRTReportDefinition.definitionPrefix),
            typeof(FLRTReportDefinition.description),
            typeof(FLRTReportDefinition.reportType),
            SubstituteKey = typeof(FLRTReportDefinition.definitionCD),
            DescriptionField = typeof(FLRTReportDefinition.description))]
        public virtual int? DefinitionID { get; set; }
        public abstract class definitionID : PX.Data.BQL.BqlInt.Field<definitionID> { }
        #endregion

        #region DefinitionPrefix
        /// <summary>
        /// Read-only display of the selected definition's prefix.
        /// Populated via FieldSelecting event — not stored in DB.
        /// </summary>
        [PXString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Prefix", Enabled = false)]
        public virtual string DefinitionPrefix { get; set; }
        public abstract class definitionPrefix : PX.Data.BQL.BqlString.Field<definitionPrefix> { }
        #endregion

        #region DisplayOrder
        /// <summary>
        /// Controls the display order of this row in the grid only.
        /// Has NO effect on calculation order — calculation order is determined
        /// entirely by topological sort of cross-definition dependencies.
        /// </summary>
        [PXDBInt]
        [PXDefault(0)]
        [PXUIField(DisplayName = "Display Order")]
        public virtual int? DisplayOrder { get; set; }
        public abstract class displayOrder : PX.Data.BQL.BqlInt.Field<displayOrder> { }
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
    }
}
