using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinancialReport.Helper
{
    /// <summary>
    /// Status constants shared by both the Financial Report and Presentation Generation screens.
    /// </summary>
    public static class ReportStatus
    {
        public const string Pending    = "N";
        public const string InProgress = "P";
        public const string Completed  = "C";
        public const string Failed     = "F";

        public class pending    : PX.Data.BQL.BqlString.Constant<pending>    { public pending()    : base(Pending)    { } }
        public class inProgress : PX.Data.BQL.BqlString.Constant<inProgress> { public inProgress() : base(InProgress) { } }
        public class completed  : PX.Data.BQL.BqlString.Constant<completed>  { public completed()  : base(Completed)  { } }
        public class failed     : PX.Data.BQL.BqlString.Constant<failed>     { public failed()     : base(Failed)     { } }
    }

    /// <summary>
    /// Contains constant string values used throughout the application to avoid "magic strings".
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// A string contained in the filename of an uploaded Word template to identify it.
        /// </summary>
        public const string TemplateFileFilter = "FRTemplate";

        /// <summary>
        /// The suffix used for Current Year placeholders (e.g., "A11101_CY").
        /// </summary>
        public const string CurrentYearSuffix = "CY";

        /// <summary>
        /// The suffix used for Previous Year placeholders (e.g., "A11101_PY").
        /// </summary>
        public const string PreviousYearSuffix = "PY";
    }
}

