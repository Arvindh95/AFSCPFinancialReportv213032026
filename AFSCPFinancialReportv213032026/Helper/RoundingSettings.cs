namespace FinancialReport.Helper
{
    /// <summary>
    /// Carries rounding configuration from the Report Definition
    /// to the ReportCalculationEngine formatting step.
    /// </summary>
    public class RoundingSettings
    {
        public string RoundingLevel { get; set; } = FLRTReportDefinition.RoundingLevelType.Units;
        public int DecimalPlaces    { get; set; } = 0;

        public static RoundingSettings FromDefinition(FLRTReportDefinition def)
        {
            if (def == null) return new RoundingSettings();
            return new RoundingSettings
            {
                RoundingLevel = def.RoundingLevel ?? FLRTReportDefinition.RoundingLevelType.Units,
                DecimalPlaces = def.DecimalPlaces ?? 0
            };
        }
    }
}
