namespace FinancialReport.Helper
{
    /// <summary>
    /// Data transfer object that carries GI name and column mapping settings
    /// from the Report Definition through to the data service and calculation engine.
    /// </summary>
    public class GIColumnMapping
    {
        public string GIName          { get; set; } = "TrialBalance";
        public string AccountColumn   { get; set; } = "Account";
        public string TypeColumn      { get; set; } = "Type";
        public string BeginningBalCol { get; set; } = "BeginningBalance";
        public string EndingBalCol    { get; set; } = "EndingBalance";
        public string DebitColumn     { get; set; } = "Debit";
        public string CreditColumn    { get; set; } = "Credit";
        public string MovementColumn  { get; set; } = "Movement";

        // Period column used in OData $filter for FinancialPeriod eq '...' clauses
        public string PeriodColumn       { get; set; } = "FinancialPeriod";

        // Dimension columns used for per-line filtering and OData $filter clauses
        public string SubaccountColumn   { get; set; } = "Subaccount";
        public string BranchColumn       { get; set; } = "BranchID";
        public string OrganizationColumn { get; set; } = "OrganizationID";
        public string LedgerColumn       { get; set; } = "LedgerID";

        /// <summary>
        /// Builds the OData $select clause from mapped column names.
        /// </summary>
        public string BuildSelectColumns()
        {
            return string.Join(",", new[]
            {
                AccountColumn, TypeColumn, BeginningBalCol,
                EndingBalCol, DebitColumn, CreditColumn, MovementColumn,
                SubaccountColumn, BranchColumn, OrganizationColumn, LedgerColumn
            });
        }

        /// <summary>
        /// Creates a GIColumnMapping from a Report Definition DAC record.
        /// Returns default mapping if definition is null (legacy mode).
        /// </summary>
        public static GIColumnMapping FromDefinition(FLRTReportDefinition def)
        {
            if (def == null) return new GIColumnMapping();
            return new GIColumnMapping
            {
                GIName             = def.GIName ?? "TrialBalance",
                AccountColumn      = def.AccountColumn ?? "Account",
                TypeColumn         = def.TypeColumn ?? "Type",
                BeginningBalCol    = def.BeginningBalColumn ?? "BeginningBalance",
                EndingBalCol       = def.EndingBalColumn ?? "EndingBalance",
                DebitColumn        = def.DebitColumn ?? "Debit",
                CreditColumn       = def.CreditColumn ?? "Credit",
                MovementColumn     = def.MovementColumn ?? "Movement",
                PeriodColumn       = def.PeriodColumn ?? "FinancialPeriod",
                SubaccountColumn   = def.SubaccountColumn ?? "Subaccount",
                BranchColumn       = def.BranchColumn ?? "BranchID",
                OrganizationColumn = def.OrganizationColumn ?? "OrganizationID",
                LedgerColumn       = def.LedgerColumn ?? "LedgerID"
            };
        }
    }
}
