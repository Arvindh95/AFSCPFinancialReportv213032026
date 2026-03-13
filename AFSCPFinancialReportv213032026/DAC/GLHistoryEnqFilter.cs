using PX.Data;
using PX.Objects.CS;
using PX.Objects.GL.Attributes;
using PX.Objects.GL.DAC;
using PX.Objects.GL.FinPeriods;
using PX.Objects.GL;
using PX.Objects;
using System.Collections.Generic;
using System;

namespace FinancialReport
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
    public class GLHistoryEnqFilterExt : PXCacheExtension<PX.Objects.GL.GLHistoryEnqFilter>
  {
        #region UsrSumEndingBalance
        [PXDecimal]
        [PXUIField(DisplayName = "Sum Ending Balance")]
        public decimal? UsrSumEndingBalance { get; set; }
        public abstract class usrSumEndingBalance : PX.Data.BQL.BqlDecimal.Field<usrSumEndingBalance> { }
        #endregion
  }
}