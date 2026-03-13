using PX.Data;
using System;

namespace FinancialReport
{
    [Serializable]
    [PXCacheName("Tenant Credentials")]
    public class FLRTTenantCredentials : PXBqlTable, IBqlTable
    {
        #region CompanyNum
        [PXDBInt(IsKey = true)]
        [PXUIField(DisplayName = "Company Number")]
        public virtual int? CompanyNum { get; set; }
        public abstract class companyNum : PX.Data.BQL.BqlInt.Field<companyNum> { }
        #endregion
          
        #region TenantName
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "Tenant Name")]
        public virtual string TenantName { get; set; }
        public abstract class tenantName : PX.Data.BQL.BqlString.Field<tenantName> { }
        #endregion

        #region PasswordNew
        [PXRSACryptString]
        [PXUIField(DisplayName = "Password")]
        public virtual string PasswordNew { get; set; }
        public abstract class passwordNew : PX.Data.BQL.BqlString.Field<passwordNew> { }
        #endregion

        #region UsernameNew
        [PXRSACryptString]
        [PXUIField(DisplayName = "Username")]
        public virtual string UsernameNew { get; set; }
        public abstract class usernameNew : PX.Data.BQL.BqlString.Field<usernameNew> { }
        #endregion

        #region ClientSecretNew
        [PXRSACryptString]
        [PXUIField(DisplayName = "Client Secret")]
        public virtual string ClientSecretNew { get; set; }
        public abstract class clientSecretNew : PX.Data.BQL.BqlString.Field<clientSecretNew> { }
        #endregion

        #region ClientIDNew
        [PXRSACryptString]
        [PXUIField(DisplayName = "Client ID")]
        public virtual string ClientIDNew { get; set; }
        public abstract class clientIDNew : PX.Data.BQL.BqlString.Field<clientIDNew> { }
        #endregion

        #region BaseURL
        [PXDBString(255, IsUnicode = true)]
        [PXUIField(DisplayName = "Base URL")]
        public virtual string BaseURL { get; set; }
        public abstract class baseURL : PX.Data.BQL.BqlString.Field<baseURL> { }
        #endregion

        #region CreatedDateTime
        [PXDBCreatedDateTime()]
        public virtual DateTime? CreatedDateTime { get; set; }
        public abstract class createdDateTime :
        PX.Data.BQL.BqlDateTime.Field<createdDateTime>
        { }
        #endregion
        #region CreatedByID
        [PXDBCreatedByID()]
        public virtual Guid? CreatedByID { get; set; }
        public abstract class createdByID :
        PX.Data.BQL.BqlGuid.Field<createdByID>
        { }
        #endregion
        #region CreatedByScreenID
        [PXDBCreatedByScreenID()]
        public virtual string CreatedByScreenID { get; set; }
        public abstract class createdByScreenID :
        PX.Data.BQL.BqlString.Field<createdByScreenID>
        { }
        #endregion
        #region LastModifiedDateTime
        [PXDBLastModifiedDateTime()]
        public virtual DateTime? LastModifiedDateTime { get; set; }
        public abstract class lastModifiedDateTime :
        PX.Data.BQL.BqlDateTime.Field<lastModifiedDateTime>
        { }
        #endregion
        #region LastModifiedByID
        [PXDBLastModifiedByID()]
        public virtual Guid? LastModifiedByID { get; set; }
        public abstract class lastModifiedByID :
        PX.Data.BQL.BqlGuid.Field<lastModifiedByID>
        { }
        #endregion
        #region LastModifiedByScreenID
        [PXDBLastModifiedByScreenID()]
        public virtual string LastModifiedByScreenID { get; set; }
        public abstract class lastModifiedByScreenID :
        PX.Data.BQL.BqlString.Field<lastModifiedByScreenID>
        { }
        #endregion
        #region Tstamp
        [PXDBTimestamp()]
        public virtual byte[] Tstamp { get; set; }
        public abstract class tstamp :
        PX.Data.BQL.BqlByteArray.Field<tstamp>
        { }
        #endregion
        #region NoteID
        [PXNote()]
        public virtual Guid? NoteID { get; set; }
        public abstract class noteID :
        PX.Data.BQL.BqlGuid.Field<noteID>
        { }
        #endregion

        #region GammaApiKey
        [PXRSACryptString]
        [PXUIField(DisplayName = "Presentation API Key")]
        public virtual string GammaApiKey { get; set; }
        public abstract class gammaApiKey : PX.Data.BQL.BqlString.Field<gammaApiKey> { }
        #endregion

    }
}