using System;
using PX.Data;
using PX.Objects.GL;
using PX.Objects.GL.FinPeriods.TableDefinition;
using PX.Data.BQL.Fluent;
using PX.Objects.GL.DAC;
using FinancialReport.Helper;

namespace FinancialReport
{
    [Serializable]
    [PXCacheName("FLRT Presentation Generation")]
    public class FLRTPresentationGeneration : PXBqlTable, IBqlTable
    {
        #region CompanyNum
        [PXDBInt]
        [PXUIField(DisplayName = "Company Number")]
        public virtual int? CompanyNum { get; set; }
        public abstract class companyNum : PX.Data.BQL.BqlInt.Field<companyNum> { }
        #endregion

        #region PresentationID
        [PXDBIdentity(IsKey = true)]
        [PXUIField(DisplayName = "Presentation ID", Visible = false)]
        public virtual int? PresentationID { get; set; }
        public abstract class presentationID : PX.Data.BQL.BqlInt.Field<presentationID> { }
        #endregion

        #region PresentationCD
        [PXDBString(225, IsUnicode = true)]
        [PXUIField(DisplayName = "Presentation Name")]
        public virtual string PresentationCD { get; set; }
        public abstract class presentationCD : PX.Data.BQL.BqlString.Field<presentationCD> { }
        #endregion

        #region Description
        [PXDBString(50, IsUnicode = true, InputMask = "")]
        [PXUIField(DisplayName = "Description")]
        public virtual string Description { get; set; }
        public abstract class description : PX.Data.BQL.BqlString.Field<description> { }
        #endregion

        #region CurrYear
        [PXDBString(4, IsUnicode = true)]
        [PXUIField(DisplayName = "Current Year")]
        [PXSelector(typeof(SelectFrom<FinPeriod>
                    .AggregateTo<GroupBy<FinPeriod.finYear>>
                    .OrderBy<FinPeriod.finYear.Desc>
                    .SearchFor<FinPeriod.finYear>),
            new Type[] { typeof(FinPeriod.finYear) },
            DescriptionField = typeof(FinPeriod.finYear))]
        public virtual string CurrYear { get; set; }
        public abstract class currYear : PX.Data.BQL.BqlString.Field<currYear> { }
        #endregion

        #region FinancialMonth
        [PXDBString(2, IsFixed = true)]
        [PXDefault("12")]
        [PXUIField(DisplayName = "Financial Month")]
        [PXStringList(
            new string[] { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12" },
            new string[] { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" })]
        public virtual string FinancialMonth { get; set; }
        public abstract class financialMonth : PX.Data.BQL.BqlString.Field<financialMonth> { }
        #endregion

        #region Organization
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "Organization")]
        [PXSelector(typeof(Search<Organization.organizationCD>))]
        public virtual string Organization { get; set; }
        public abstract class organization : PX.Data.BQL.BqlString.Field<organization> { }
        #endregion

        #region Branch
        [PXDBString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Branch")]
        [PXSelector(typeof(Search<Branch.branchCD>))]
        public virtual string Branch { get; set; }
        public abstract class branch : PX.Data.BQL.BqlString.Field<branch> { }
        #endregion

        #region Ledger
        [PXDBString(20, IsUnicode = true)]
        [PXUIField(DisplayName = "Ledger")]
        [PXSelector(typeof(Search<Ledger.ledgerCD>), typeof(Ledger.descr))]
        public virtual string Ledger { get; set; }
        public abstract class ledger : PX.Data.BQL.BqlString.Field<ledger> { }
        #endregion

        #region PresentationTitle
        [PXDBString(500, IsUnicode = true)]
        [PXUIField(DisplayName = "Presentation Title")]
        public virtual string PresentationTitle { get; set; }
        public abstract class presentationTitle : PX.Data.BQL.BqlString.Field<presentationTitle> { }
        #endregion

        #region PresentationDescription
        [PXDBString(2000, IsUnicode = true)]
        [PXUIField(DisplayName = "Presentation Description")]
        public virtual string PresentationDescription { get; set; }
        public abstract class presentationDescription : PX.Data.BQL.BqlString.Field<presentationDescription> { }
        #endregion

        #region GammaTemplateId
        [PXDBString(100, IsUnicode = true)]
        [PXUIField(DisplayName = "Presentation Template ID")]
        public virtual string GammaTemplateId { get; set; }
        public abstract class gammaTemplateId : PX.Data.BQL.BqlString.Field<gammaTemplateId> { }
        #endregion

        #region PresentationMarkdown
        [PXDBText(IsUnicode = true)]
        [PXUIField(DisplayName = "Presentation Markdown")]
        public virtual string PresentationMarkdown { get; set; }
        public abstract class presentationMarkdown : PX.Data.BQL.BqlString.Field<presentationMarkdown> { }
        #endregion

        #region SlideStatus
        [PXDBString(1, IsUnicode = true)]
        [PXDefault(ReportStatus.Pending, PersistingCheck = PXPersistingCheck.Nothing)]
        [PXUIField(DisplayName = "Presentation Status", IsReadOnly = true)]
        [PXStringList(
            new[] { ReportStatus.Pending, ReportStatus.InProgress, ReportStatus.Completed, ReportStatus.Failed },
            new[] { "Not Generated", "In Progress", "Ready to Download", "Failed" })]
        public virtual string SlideStatus { get; set; }
        public abstract class slideStatus : PX.Data.BQL.BqlString.Field<slideStatus> { }
        #endregion

        #region SlideGeneratedFileID
        [PXDBGuid]
        [PXUIField(DisplayName = "Slide File ID", Visible = false)]
        public virtual Guid? SlideGeneratedFileID { get; set; }
        public abstract class slideGeneratedFileID : PX.Data.BQL.BqlGuid.Field<slideGeneratedFileID> { }
        #endregion

        #region Noteid
        [PXNote()]
        [PXUIField(DisplayName = "Note ID", Visible = false)]
        public virtual Guid? Noteid { get; set; }
        public abstract class noteid : PX.Data.BQL.BqlGuid.Field<noteid> { }
        #endregion

        #region Audit Fields
        [PXDBCreatedDateTime()]
        public virtual DateTime? CreatedDateTime { get; set; }
        public abstract class createdDateTime : PX.Data.BQL.BqlDateTime.Field<createdDateTime> { }

        [PXDBCreatedByID()]
        public virtual Guid? CreatedByID { get; set; }
        public abstract class createdByID : PX.Data.BQL.BqlGuid.Field<createdByID> { }

        [PXDBCreatedByScreenID()]
        public virtual string CreatedByScreenID { get; set; }
        public abstract class createdByScreenID : PX.Data.BQL.BqlString.Field<createdByScreenID> { }

        [PXDBLastModifiedDateTime()]
        public virtual DateTime? LastModifiedDateTime { get; set; }
        public abstract class lastModifiedDateTime : PX.Data.BQL.BqlDateTime.Field<lastModifiedDateTime> { }

        [PXDBLastModifiedByID()]
        public virtual Guid? LastModifiedByID { get; set; }
        public abstract class lastModifiedByID : PX.Data.BQL.BqlGuid.Field<lastModifiedByID> { }

        [PXDBLastModifiedByScreenID()]
        public virtual string LastModifiedByScreenID { get; set; }
        public abstract class lastModifiedByScreenID : PX.Data.BQL.BqlString.Field<lastModifiedByScreenID> { }

        [PXDBTimestamp()]
        public virtual byte[] Tstamp { get; set; }
        public abstract class tstamp : PX.Data.BQL.BqlByteArray.Field<tstamp> { }
        #endregion
    }
}
