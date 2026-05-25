using System;
using System.Collections.Generic;
using FinancialReport.Helper;
using FinancialReport.Services;
using PX.Data;
using Xunit;

namespace FinancialReport.Tests
{
    /// <summary>
    /// Unit tests for the pure (graph-independent) logic of ReportCalculationEngine:
    /// sign normalization, balance-type selection, account-range comparison, the formula
    /// evaluator, and value formatting/rounding. These methods are internal static and do
    /// not touch the PXGraph, so they run without an Acumatica runtime.
    /// </summary>
    public class ReportCalculationEngineTests
    {
        // ── Sign normalization (ApplyAccountTypeSign) ──────────────────────────────

        [Theory]
        [InlineData("A", 100, 100)]   // Asset    — as-is
        [InlineData("E", 100, 100)]   // Expense  — as-is
        [InlineData("L", 100, -100)]  // Liability — negated
        [InlineData("I", 100, -100)]  // Income    — negated
        [InlineData(null, 100, 100)]  // unknown/null — as-is
        [InlineData("X", 100, 100)]   // unrecognized — as-is
        public void ApplyAccountTypeSign_normalizes_by_account_type(string type, decimal raw, decimal expected)
        {
            Assert.Equal(expected, ReportCalculationEngine.ApplyAccountTypeSign(raw, type));
        }

        // ── Balance-type selection (GetBalanceByType) ──────────────────────────────

        [Fact]
        public void GetBalanceByType_selects_the_right_field()
        {
            var d = new FinancialPeriodData { BeginningBalance = 10m, EndingBalance = 20m, Debit = 30m, Credit = 12m };

            Assert.Equal(20m, ReportCalculationEngine.GetBalanceByType(d, FLRTReportLineItem.BalanceTypeValue.Ending));
            Assert.Equal(10m, ReportCalculationEngine.GetBalanceByType(d, FLRTReportLineItem.BalanceTypeValue.Beginning));
            Assert.Equal(30m, ReportCalculationEngine.GetBalanceByType(d, FLRTReportLineItem.BalanceTypeValue.Debit));
            Assert.Equal(12m, ReportCalculationEngine.GetBalanceByType(d, FLRTReportLineItem.BalanceTypeValue.Credit));
            Assert.Equal(18m, ReportCalculationEngine.GetBalanceByType(d, FLRTReportLineItem.BalanceTypeValue.Movement)); // Debit - Credit
            Assert.Equal(20m, ReportCalculationEngine.GetBalanceByType(d, "UNRECOGNIZED")); // defaults to Ending
        }

        // ── Account-range comparison (IsAccountInRange / CompareAccountCodes) ───────

        [Theory]
        [InlineData("10500", "10000", "19999", true)]
        [InlineData("10000", "10000", "19999", true)]   // inclusive lower
        [InlineData("19999", "10000", "19999", true)]   // inclusive upper
        [InlineData("20000", "10000", "19999", false)]
        [InlineData("09999", "10000", "19999", false)]
        public void IsAccountInRange_handles_inclusive_bounds(string acct, string from, string to, bool expected)
        {
            Assert.Equal(expected, ReportCalculationEngine.IsAccountInRange(acct, from, to));
        }

        [Fact]
        public void CompareAccountCodes_compares_numerically_not_lexically()
        {
            // Lexically "10000" < "9000" (because '1' < '9'); numerically 10000 > 9000.
            Assert.True(ReportCalculationEngine.CompareAccountCodes("10000", "9000") > 0);
            // So an account of 9000 is NOT inside 10000..19999 ...
            Assert.False(ReportCalculationEngine.IsAccountInRange("9000", "10000", "19999"));
            // ... but IS inside 9000..11000.
            Assert.True(ReportCalculationEngine.IsAccountInRange("10000", "9000", "11000"));
        }

        [Fact]
        public void IsAccountInRange_handles_dashed_segmented_codes()
        {
            Assert.True(ReportCalculationEngine.IsAccountInRange("10100-05", "10100-01", "10100-10"));
            Assert.False(ReportCalculationEngine.IsAccountInRange("10100-20", "10100-01", "10100-10"));
        }

        // ── Formula evaluator (EvaluateFormula) ────────────────────────────────────

        private static readonly HashSet<string> KnownPrefixes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BS", "PL" };

        private static Dictionary<string, decimal> Globals() =>
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["BS_TOTAL_ASSETS"] = 1000m,
                ["PL_NET_INCOME"] = 250m,
                ["BS_CASH"] = 100m,
            };

        [Fact]
        public void EvaluateFormula_respects_operator_precedence()
        {
            Assert.Equal(14m, ReportCalculationEngine.EvaluateFormula("2 + 3 * 4", "BS", KnownPrefixes, Globals()));
        }

        [Fact]
        public void EvaluateFormula_respects_parentheses()
        {
            Assert.Equal(20m, ReportCalculationEngine.EvaluateFormula("(2 + 3) * 4", "BS", KnownPrefixes, Globals()));
        }

        [Fact]
        public void EvaluateFormula_handles_unary_minus()
        {
            Assert.Equal(5m, ReportCalculationEngine.EvaluateFormula("-5 + 10", "BS", KnownPrefixes, Globals()));
        }

        [Fact]
        public void EvaluateFormula_resolves_explicit_cross_definition_prefix()
        {
            // From a CF definition referencing BS and PL lines explicitly.
            Assert.Equal(750m, ReportCalculationEngine.EvaluateFormula(
                "BS_TOTAL_ASSETS - PL_NET_INCOME", "CF", KnownPrefixes, Globals()));
        }

        [Fact]
        public void EvaluateFormula_resolves_implicit_own_prefix()
        {
            // Bare CASH inside the BS definition resolves to BS_CASH.
            Assert.Equal(100m, ReportCalculationEngine.EvaluateFormula("CASH", "BS", KnownPrefixes, Globals()));
        }

        [Fact]
        public void EvaluateFormula_division_by_zero_yields_zero()
        {
            Assert.Equal(0m, ReportCalculationEngine.EvaluateFormula("100 / 0", "BS", KnownPrefixes, Globals()));
        }

        [Fact]
        public void EvaluateFormula_unknown_line_code_throws()
        {
            // A typo'd/removed line code must fail loudly, not silently resolve to 0.
            Assert.Throws<PXException>(() =>
                ReportCalculationEngine.EvaluateFormula("NONEXISTENT", "BS", KnownPrefixes, Globals()));
        }

        // ── Value formatting (FormatFinancialValue / ApplyRounding) ─────────────────

        private static RoundingSettings Units(int dp = 0) =>
            new RoundingSettings { RoundingLevel = FLRTReportDefinition.RoundingLevelType.Units, DecimalPlaces = dp };

        [Fact]
        public void FormatFinancialValue_renders_zero_as_dash()
        {
            Assert.Equal("-", ReportCalculationEngine.FormatFinancialValue(0m, Units()));
        }

        [Fact]
        public void FormatFinancialValue_wraps_negatives_in_parentheses()
        {
            Assert.Equal("(1,234)", ReportCalculationEngine.FormatFinancialValue(-1234m, Units()));
        }

        [Fact]
        public void FormatFinancialValue_adds_thousands_separator()
        {
            Assert.Equal("1,234,567", ReportCalculationEngine.FormatFinancialValue(1234567m, Units()));
        }

        [Fact]
        public void FormatFinancialValue_scales_to_thousands()
        {
            // 1,234,567 / 1000 = 1234.567 → round to 0 dp away-from-zero = 1235
            var thous = new RoundingSettings { RoundingLevel = FLRTReportDefinition.RoundingLevelType.Thousands, DecimalPlaces = 0 };
            Assert.Equal("1,235", ReportCalculationEngine.FormatFinancialValue(1234567m, thous));
        }

        [Fact]
        public void FormatFinancialValue_scales_to_millions_with_decimals()
        {
            var mill = new RoundingSettings { RoundingLevel = FLRTReportDefinition.RoundingLevelType.Millions, DecimalPlaces = 1 };
            Assert.Equal("1.5", ReportCalculationEngine.FormatFinancialValue(1500000m, mill));
        }

        [Fact]
        public void ApplyRounding_rounds_away_from_zero()
        {
            Assert.Equal(1235m, ReportCalculationEngine.ApplyRounding(1234.5m, Units()));
            Assert.Equal(-1235m, ReportCalculationEngine.ApplyRounding(-1234.5m, Units()));
        }

        // ── Account line calculation (CalculateAccountLine) ─────────────────────────

        private static FLRTReportLineItem AccountLine(string from, string to,
            string sign = "ASIS", string balance = "ENDING", string typeFilter = null) =>
            new FLRTReportLineItem
            {
                LineType = FLRTReportLineItem.LineItemType.Account,
                AccountFrom = from,
                AccountTo = to,
                SignRule = sign,
                BalanceType = balance,
                AccountTypeFilter = typeFilter,
            };

        private static FinancialApiData DataWith(params (string acct, string type, decimal ending)[] rows)
        {
            var d = new FinancialApiData();
            foreach (var (acct, type, ending) in rows)
                d.AccountData[acct] = new FinancialPeriodData { Account = acct, AccountType = type, EndingBalance = ending };
            return d;
        }

        [Fact]
        public void CalculateAccountLine_sums_only_accounts_in_range()
        {
            var line = AccountLine("10000", "19999");
            var data = DataWith(
                ("10500", "A", 500m),
                ("15000", "A", 300m),
                ("20000", "A", 999m)); // out of range
            Assert.Equal(800m, ReportCalculationEngine.CalculateAccountLine(line, data));
        }

        [Fact]
        public void CalculateAccountLine_applies_flip_sign_rule()
        {
            var line = AccountLine("10000", "19999", sign: FLRTReportLineItem.SignRuleValue.Flip);
            var data = DataWith(("10500", "A", 500m), ("15000", "A", 300m));
            Assert.Equal(-800m, ReportCalculationEngine.CalculateAccountLine(line, data));
        }

        [Fact]
        public void CalculateAccountLine_negates_liability_via_account_type()
        {
            // Liability account, ASIS sign rule: ApplyAccountTypeSign flips L to negative.
            var line = AccountLine("20000", "29999");
            var data = DataWith(("21000", "L", 400m));
            Assert.Equal(-400m, ReportCalculationEngine.CalculateAccountLine(line, data));
        }

        [Fact]
        public void CalculateAccountLine_honors_account_type_filter()
        {
            var line = AccountLine("10000", "19999", typeFilter: FLRTReportLineItem.AccountTypeValue.Asset);
            var data = DataWith(
                ("10500", "A", 500m),
                ("11000", "E", 999m)); // Expense — excluded by the A filter
            Assert.Equal(500m, ReportCalculationEngine.CalculateAccountLine(line, data));
        }

        [Fact]
        public void CalculateAccountLine_returns_zero_when_range_unset()
        {
            var line = new FLRTReportLineItem { LineType = FLRTReportLineItem.LineItemType.Account };
            var data = DataWith(("10500", "A", 500m));
            Assert.Equal(0m, ReportCalculationEngine.CalculateAccountLine(line, data));
        }
    }
}
