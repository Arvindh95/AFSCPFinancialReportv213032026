using System;
using PX.Data;
using PX.Data.BQL.Fluent;

namespace FinancialReport
{
    /// <summary>
    /// Links one or more GI Data Sources to a single Presentation Generation record.
    /// Mirrors FLRTPresentationDefinitionLink but references FLRTGIDataSource instead.
    /// </summary>
    [Serializable]
    [PXCacheName("FLRT Presentation Data Source Link")]
    public class FLRTPresentationDataSourceLink : PXBqlTable, IBqlTable
    {
        #region LinkID
        [PXDBIdentity(IsKey = true)]
        [PXUIField(DisplayName = "Link ID", Visible = false)]
        public virtual int? LinkID { get; set; }
        public abstract class linkID : PX.Data.BQL.BqlInt.Field<linkID> { }
        #endregion

        #region PresentationID
        [PXDBInt]
        [PXDBDefault(typeof(FLRTPresentationGeneration.presentationID))]
        [PXParent(typeof(SelectFrom<FLRTPresentationGeneration>
            .Where<FLRTPresentationGeneration.presentationID.IsEqual<presentationID.FromCurrent>>))]
        [PXUIField(DisplayName = "Presentation ID", Visible = false)]
        public virtual int? PresentationID { get; set; }
        public abstract class presentationID : PX.Data.BQL.BqlInt.Field<presentationID> { }
        #endregion

        #region DataSourceID
        [PXDBInt]
        [PXUIField(DisplayName = "Data Source")]
        [PXDefault]
        [PXSelector(
            typeof(Search<FLRTGIDataSource.dataSourceID,
                Where<FLRTGIDataSource.isActive, Equal<True>>>),
            typeof(FLRTGIDataSource.dataSourceCD),
            typeof(FLRTGIDataSource.prefix),
            typeof(FLRTGIDataSource.description),
            typeof(FLRTGIDataSource.giName),
            SubstituteKey = typeof(FLRTGIDataSource.dataSourceCD),
            DescriptionField = typeof(FLRTGIDataSource.description))]
        public virtual int? DataSourceID { get; set; }
        public abstract class dataSourceID : PX.Data.BQL.BqlInt.Field<dataSourceID> { }
        #endregion

        #region DataSourcePrefix
        /// <summary>
        /// Read-only display of the selected data source's prefix. Populated via FieldSelecting event.
        /// </summary>
        [PXString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Prefix", Enabled = false)]
        public virtual string DataSourcePrefix { get; set; }
        public abstract class dataSourcePrefix : PX.Data.BQL.BqlString.Field<dataSourcePrefix> { }
        #endregion

        #region DisplayOrder
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
