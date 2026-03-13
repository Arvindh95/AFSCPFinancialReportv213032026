using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinancialReport.Helper
{
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

        /// <summary>
        /// The suffix used for Previous Month placeholders (e.g., "BS_CASH_PM").
        /// Represents the single period immediately before the selected financial month.
        /// </summary>
        public const string PreviousMonthSuffix = "PM";

        /// <summary>
        /// Maximum number of placeholders allowed in a single template.
        /// Prevents performance issues and potential abuse.
        /// </summary>
        public const int MaxPlaceholdersPerTemplate = 1000;
    }
}

