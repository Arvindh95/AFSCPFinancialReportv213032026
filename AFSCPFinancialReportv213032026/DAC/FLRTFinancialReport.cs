using System;
using PX.Data;
using PX.Objects.GL; // For Ledger and Branch tables
using PX.Objects;
using PX.Objects.GL.FinPeriods.TableDefinition;
using PX.Data.BQL.Fluent;
using PX.Objects.GL.DAC;

namespace FinancialReport
{
  [Serializable]
  [PXCacheName("FLRTFinancialReport")]
  public class FLRTFinancialReport : PXBqlTable, IBqlTable
  {

    #region CompanyNum
    [PXDBInt]
    [PXUIField(DisplayName = "Company Number")]
    public virtual int? CompanyNum { get; set; }
    public abstract class companyNum : PX.Data.BQL.BqlInt.Field<companyNum> { }
    #endregion

    #region ReportID
    [PXDBIdentity(IsKey = true)]
    [PXUIField(DisplayName = "Report ID", Visible = false)]
    public virtual int? ReportID { get; set; }
    public abstract class reportID : PX.Data.BQL.BqlInt.Field<reportID> { }
    #endregion

    #region ReportCD
    [PXDBString(225, IsUnicode = true)]      
    [PXUIField(DisplayName = "Template Name")]
    public virtual string ReportCD { get; set; }
    public abstract class reportCD : PX.Data.BQL.BqlString.Field<reportCD> { }       
    #endregion

    #region Description
    [PXDBString(50, IsUnicode = true, InputMask = "")]
    [PXUIField(DisplayName = "Description")]
    public virtual string Description { get; set; }
    public abstract class description : PX.Data.BQL.BqlString.Field<description> { }
    #endregion

    #region CreatedDateTime
    [PXDBCreatedDateTime()]
    public virtual DateTime? CreatedDateTime { get; set; }
    public abstract class createdDateTime : PX.Data.BQL.BqlDateTime.Field<createdDateTime> { }
    #endregion

    #region CreatedByID
    [PXDBCreatedByID()]
    public virtual Guid? CreatedByID { get; set; }
    public abstract class createdByID : PX.Data.BQL.BqlGuid.Field<createdByID> { }
    #endregion

    #region CreatedByScreenID
    [PXDBCreatedByScreenID()]
    public virtual string CreatedByScreenID { get; set; }
    public abstract class createdByScreenID : PX.Data.BQL.BqlString.Field<createdByScreenID> { }
    #endregion

    #region LastModifiedDateTime
    [PXDBLastModifiedDateTime()]
    public virtual DateTime? LastModifiedDateTime { get; set; }
    public abstract class lastModifiedDateTime : PX.Data.BQL.BqlDateTime.Field<lastModifiedDateTime> { }
    #endregion

    #region LastModifiedByID
    [PXDBLastModifiedByID()]
    public virtual Guid? LastModifiedByID { get; set; }
    public abstract class lastModifiedByID : PX.Data.BQL.BqlGuid.Field<lastModifiedByID> { }
    #endregion

    #region LastModifiedByScreenID
    [PXDBLastModifiedByScreenID()]
    public virtual string LastModifiedByScreenID { get; set; }
    public abstract class lastModifiedByScreenID : PX.Data.BQL.BqlString.Field<lastModifiedByScreenID> { }
    #endregion

    #region Tstamp
    [PXDBTimestamp()]
    [PXUIField()]
    public virtual byte[] Tstamp { get; set; }
    public abstract class tstamp : PX.Data.BQL.BqlByteArray.Field<tstamp> { }
    #endregion

    #region Noteid
    [PXNote()]
    [PXUIField(DisplayName = "Note ID", Visible = false)]
    public virtual Guid? Noteid { get; set; }
    public abstract class noteid : PX.Data.BQL.BqlGuid.Field<noteid> { }
        #endregion

    #region Current Year
    [PXDBString(4, IsUnicode = true)]
    [PXUIField(DisplayName = "Current Year")]
    [PXSelector(typeof(SelectFrom<FinPeriod>
                .AggregateTo<GroupBy<FinPeriod.finYear>>       // distinct
                .OrderBy<FinPeriod.finYear.Desc>               // descending
                .SearchFor<FinPeriod.finYear>),                // the field you want
        new Type[] { typeof(FinPeriod.finYear) },
        DescriptionField = typeof(FinPeriod.finYear))]
    public virtual string CurrYear { get; set; }
    public abstract class currYear : PX.Data.BQL.BqlString.Field<currYear> { }
    #endregion

    #region Branch
    [PXDBString(10, IsUnicode = true)]
    [PXUIField(DisplayName = "Branch")]
    [PXSelector(typeof(Search<Branch.branchCD>))]
    public virtual string Branch { get; set; }
    public abstract class branch : PX.Data.BQL.BqlString.Field<branch> { }
        #endregion

    #region Organization
    [PXDBString(50, IsUnicode = true)]
    [PXUIField(DisplayName = "Organization")]
    // You can use a selector if you have an Organization table or list; if not, you may remove this.
    [PXSelector(typeof(Search<Organization.organizationCD>))]
    public virtual string Organization { get; set; }
    public abstract class organization : PX.Data.BQL.BqlString.Field<organization> { }
    #endregion

    #region Ledger
    [PXDBString(20, IsUnicode = true)]
    [PXUIField(DisplayName = "Ledger")]
    [PXSelector(typeof(Search<Ledger.ledgerCD>), typeof(Ledger.descr))]
    public virtual string Ledger { get; set; }
    public abstract class ledger : PX.Data.BQL.BqlString.Field<ledger> { }
    #endregion

    #region FinancialMonth
    [PXDBString(2, IsFixed = true)]
    [PXDefault("12")] // Default to December
    [PXUIField(DisplayName = "Financial Month")]
    [PXStringList(new string[]{"01", "02", "03", "04", "05", "06","07", "08", "09", "10", "11", "12"},new string[]{"January", "February", "March", "April", "May", "June","July", "August", "September", "October", "November", "December"})]
    public virtual string FinancialMonth { get; set; }
    public abstract class financialMonth : PX.Data.BQL.BqlString.Field<financialMonth> { }
    #endregion

    #region DefinitionID
    /// <summary>
    /// Legacy single-definition link. Superseded by FLRTReportDefinitionLink child table
    /// which supports multiple definitions per report with cross-definition formulas.
    ///
    /// If FLRTReportDefinitionLink rows exist for this report, this field is ignored.
    /// Kept for backward compatibility with reports created before multi-definition support.
    /// </summary>
    [PXDBInt]
    [PXUIField(DisplayName = "Report Definition (Legacy)", Visible = false)]
    [PXSelector(
        typeof(Search<FLRTReportDefinition.definitionID>),
        typeof(FLRTReportDefinition.definitionCD),
        typeof(FLRTReportDefinition.description),
        typeof(FLRTReportDefinition.reportType),
        SubstituteKey = typeof(FLRTReportDefinition.definitionCD),
        DescriptionField = typeof(FLRTReportDefinition.description)
    )]
    public virtual int? DefinitionID { get; set; }
    public abstract class definitionID : PX.Data.BQL.BqlInt.Field<definitionID> { }
    #endregion

    #region GeneratedFileID
    [PXDBGuid]
    [PXUIField(DisplayName = "Generated File ID", Visible = false)]
    public virtual Guid? GeneratedFileID { get; set; }
    public abstract class generatedFileID : PX.Data.BQL.BqlGuid.Field<generatedFileID> { }
    #endregion

    #region UploadedFileID
    [PXDBGuid]
    [PXUIField(DisplayName = "Uploaded File ID", Enabled = false, Visible = false)]
    public virtual Guid? UploadedFileID { get; set; }
    public abstract class uploadedFileID : PX.Data.BQL.BqlGuid.Field<uploadedFileID> { }
    #endregion

    #region UploadedFileIDDisplay
    [PXDBString(225, IsUnicode = true)]
    [PXUIField(DisplayName = "Uploaded File ID (Display Only)", Enabled = false, Visible = false)]
    public virtual string UploadedFileIDDisplay { get; set; }
    public abstract class uploadedFileIDDisplay : PX.Data.BQL.BqlString.Field<uploadedFileIDDisplay> { }
    #endregion

    #region Status
    [PXDBString(1, IsUnicode = true)]
    [PXDefault(ReportStatus.Pending)]
    [PXUIField(DisplayName = "Status", IsReadOnly = true)]
    [PXStringList(
        new[] { ReportStatus.Pending, ReportStatus.InProgress, ReportStatus.Completed, ReportStatus.Failed },
        new[] { "File not Generated", "In Progress", "Ready to Download", "Failed" })]
    public virtual string Status { get; set; }
    public abstract class status : PX.Data.BQL.BqlString.Field<status> { }
    #endregion

    #region PresentationTitle
    [PXDBString(500, IsUnicode = true)]
    [PXUIField(DisplayName = "Presentation Title")]
    public virtual string PresentationTitle { get; set; }
    public abstract class presentationTitle : PX.Data.BQL.BqlString.Field<presentationTitle> { }
    #endregion

    #region GammaTemplateId
    [PXDBString(100, IsUnicode = true)]
    [PXUIField(DisplayName = "Presentation Template ID")]
    public virtual string GammaTemplateId { get; set; }
    public abstract class gammaTemplateId : PX.Data.BQL.BqlString.Field<gammaTemplateId> { }
    #endregion

    #region PresentationDescription
    [PXDBString(2000, IsUnicode = true)]
    [PXUIField(DisplayName = "Presentation Description")]
    public virtual string PresentationDescription { get; set; }
    public abstract class presentationDescription : PX.Data.BQL.BqlString.Field<presentationDescription> { }
    #endregion

    #region SlideGeneratedFileID
    [PXDBGuid]
    [PXUIField(DisplayName = "Slide File ID", Visible = false)]
    public virtual Guid? SlideGeneratedFileID { get; set; }
    public abstract class slideGeneratedFileID : PX.Data.BQL.BqlGuid.Field<slideGeneratedFileID> { }
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
        new[] { "File not Generated", "In Progress", "Ready to Download", "Failed" })]
    public virtual string SlideStatus { get; set; }
    public abstract class slideStatus : PX.Data.BQL.BqlString.Field<slideStatus> { }
    #endregion

    #region Report Status
    public static class ReportStatus
    {
        public const string Pending    = "N";  // Not generated
        public const string InProgress = "P";  // Processing
        public const string Completed  = "C";  // Completed
        public const string Failed     = "F";  // Failed

        public class pending    : PX.Data.BQL.BqlString.Constant<pending>    { public pending()    : base(Pending)    { } }
        public class inProgress : PX.Data.BQL.BqlString.Constant<inProgress> { public inProgress() : base(InProgress) { } }
        public class completed  : PX.Data.BQL.BqlString.Constant<completed>  { public completed()  : base(Completed)  { } }
        public class failed     : PX.Data.BQL.BqlString.Constant<failed>     { public failed()     : base(Failed)     { } }
    }
    #endregion

    }
}