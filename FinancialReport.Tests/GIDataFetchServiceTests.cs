using System.Collections.Generic;
using System.Linq;
using FinancialReport;
using FinancialReport.Services;
using Newtonsoft.Json.Linq;
using PX.Data;
using Xunit;

namespace FinancialReport.Tests
{
    /// <summary>
    /// Unit tests for the pure logic of GIDataFetchService: the client-side RowFilter
    /// evaluator, key-range filtering, aggregation, typed/date OData filter building, the
    /// arithmetic formula evaluator, and CALCULATED-column topological sort.
    ///
    /// The service is constructed with a stub AuthService (no network is exercised — none of
    /// the tested methods make HTTP calls).
    /// </summary>
    public class GIDataFetchServiceTests
    {
        private static GIDataFetchService NewService() =>
            new GIDataFetchService(
                new AuthService("https://example.local", "cid", "secret", "user", "pass"),
                "https://example.local",
                "TestTenant");

        private static List<JToken> Rows(params JObject[] objs) => objs.Cast<JToken>().ToList();

        // ── Arithmetic evaluator (EvaluateArithmeticExpression) ─────────────────────

        [Theory]
        [InlineData("2+3*4", 14)]
        [InlineData("(2+3)*4", 20)]
        [InlineData("10/0", 0)]      // division by zero is swallowed to 0
        [InlineData("-5+3", -2)]
        [InlineData("1.5 + 2.5", 4)]
        [InlineData("100 - 40 - 10", 50)]
        public void EvaluateArithmeticExpression_computes_correctly(string expr, decimal expected)
        {
            Assert.Equal(expected, NewService().EvaluateArithmeticExpression(expr));
        }

        // ── Formula evaluator over aliases (EvaluateFormula) ────────────────────────

        [Fact]
        public void EvaluateFormula_substitutes_alias_values()
        {
            var values = new Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["REVENUE"] = 100m,
                ["COST"] = 40m,
            };
            Assert.Equal(60m, NewService().EvaluateFormula("REVENUE - COST", values));
            Assert.Equal(0.6m, NewService().EvaluateFormula("(REVENUE - COST) / REVENUE", values));
        }

        [Fact]
        public void EvaluateFormula_unknown_alias_defaults_to_zero()
        {
            var values = new Dictionary<string, object> { ["REVENUE"] = 100m };
            // GI formula path defaults missing references to 0 (warns) — unlike the GL engine.
            Assert.Equal(100m, NewService().EvaluateFormula("REVENUE + MISSING", values));
        }

        // ── ConvertToDecimal ────────────────────────────────────────────────────────

        [Fact]
        public void ConvertToDecimal_handles_common_types()
        {
            var s = NewService();
            Assert.Equal(5m, s.ConvertToDecimal(5));
            Assert.Equal(7m, s.ConvertToDecimal(7L));
            Assert.Equal(2.5m, s.ConvertToDecimal(2.5));
            Assert.Equal(3m, s.ConvertToDecimal(3m));
            Assert.Equal(12.5m, s.ConvertToDecimal("12.5"));
            Assert.Equal(0m, s.ConvertToDecimal(null));
            Assert.Equal(0m, s.ConvertToDecimal("not-a-number"));
        }

        // ── Numeric aggregation (AggregateNumeric) ──────────────────────────────────

        private static List<JToken> AmountRows() => Rows(
            new JObject { ["Amount"] = 100m },
            new JObject { ["Amount"] = 250m },
            new JObject { ["Amount"] = 50m });

        [Fact]
        public void AggregateNumeric_supports_each_function()
        {
            var s = NewService();
            var rows = AmountRows();
            Assert.Equal(400m, s.AggregateNumeric(rows, "Amount", FLRTGIDataSourceColumn.AggregateFunctionType.Sum));
            Assert.Equal(250m, s.AggregateNumeric(rows, "Amount", FLRTGIDataSourceColumn.AggregateFunctionType.Max));
            Assert.Equal(50m, s.AggregateNumeric(rows, "Amount", FLRTGIDataSourceColumn.AggregateFunctionType.Min));
            Assert.Equal(400m / 3m, s.AggregateNumeric(rows, "Amount", FLRTGIDataSourceColumn.AggregateFunctionType.Avg));
            Assert.Equal(100m, s.AggregateNumeric(rows, "Amount", FLRTGIDataSourceColumn.AggregateFunctionType.First));
        }

        [Fact]
        public void AggregateBoolean_sum_counts_true_values()
        {
            var rows = Rows(
                new JObject { ["Flag"] = true },
                new JObject { ["Flag"] = false },
                new JObject { ["Flag"] = true });
            Assert.Equal(2, NewService().AggregateBoolean(rows, "Flag", FLRTGIDataSourceColumn.AggregateFunctionType.Sum));
        }

        [Fact]
        public void AggregateString_first_and_count()
        {
            var rows = Rows(
                new JObject { ["Name"] = "Bravo" },
                new JObject { ["Name"] = "Alpha" });
            var s = NewService();
            Assert.Equal("Bravo", s.AggregateString(rows, "Name", FLRTGIDataSourceColumn.AggregateFunctionType.First));
            Assert.Equal("2", s.AggregateString(rows, "Name", FLRTGIDataSourceColumn.AggregateFunctionType.Count));
            Assert.Equal("Bravo", s.AggregateString(rows, "Name", FLRTGIDataSourceColumn.AggregateFunctionType.Max));
            Assert.Equal("Alpha", s.AggregateString(rows, "Name", FLRTGIDataSourceColumn.AggregateFunctionType.Min));
        }

        // ── Key-range filtering (FilterRowsByKey) ───────────────────────────────────

        [Fact]
        public void FilterRowsByKey_includes_only_rows_in_range()
        {
            var rows = Rows(
                new JObject { ["Acct"] = "10500" },
                new JObject { ["Acct"] = "15000" },
                new JObject { ["Acct"] = "20000" });
            var result = NewService().FilterRowsByKey(rows, "Acct", "10000", "19999");
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void FilterRowsByKey_no_key_column_returns_all()
        {
            var rows = AmountRows();
            Assert.Equal(rows.Count, NewService().FilterRowsByKey(rows, null, "10000", "19999").Count);
        }

        // ── Row filter (ApplyRowFilter) ─────────────────────────────────────────────

        private static List<JToken> OrderRows() => Rows(
            new JObject { ["Status"] = "Open", ["Amount"] = 1500m },
            new JObject { ["Status"] = "Closed", ["Amount"] = 800m },
            new JObject { ["Status"] = "Open", ["Amount"] = 200m });

        [Fact]
        public void ApplyRowFilter_eq_filters_by_value()
        {
            var result = NewService().ApplyRowFilter(OrderRows(), "Status eq 'Open'");
            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.Equal("Open", (string)r["Status"]));
        }

        [Fact]
        public void ApplyRowFilter_combines_conditions_with_and()
        {
            var result = NewService().ApplyRowFilter(OrderRows(), "Status eq 'Open' and Amount gt 1000");
            Assert.Single(result);
            Assert.Equal(1500m, (decimal)result[0]["Amount"]);
        }

        [Fact]
        public void ApplyRowFilter_contains_operator()
        {
            var rows = Rows(
                new JObject { ["Vendor"] = "Acme Corp" },
                new JObject { ["Vendor"] = "Globex" });
            Assert.Single(NewService().ApplyRowFilter(rows, "Vendor contains 'Acme'"));
        }

        [Fact]
        public void ApplyRowFilter_or_is_unsupported_and_filter_is_ignored()
        {
            // "or" is not part of the supported grammar; the condition is unparseable and
            // skipped, so ALL rows pass through (documented behaviour).
            var result = NewService().ApplyRowFilter(OrderRows(), "Status eq 'Open' or Status eq 'Closed'");
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void ExtractRowFilterColumns_returns_referenced_columns()
        {
            var cols = GIDataFetchService.ExtractRowFilterColumns("Status eq 'Open' and Amount gt 1000").ToList();
            Assert.Equal(new[] { "Status", "Amount" }, cols);
        }

        // ── Typed OData filter building (BuildTypedFilter) ──────────────────────────

        [Fact]
        public void BuildTypedFilter_quotes_and_escapes_strings()
        {
            Assert.Equal("Col eq 'O''Brien'",
                NewService().BuildTypedFilter("Col", FLRTGIDataSource.GIColumnType.String, "O'Brien"));
        }

        [Fact]
        public void BuildTypedFilter_formats_by_type()
        {
            var s = NewService();
            Assert.Equal("Col eq 5", s.BuildTypedFilter("Col", FLRTGIDataSource.GIColumnType.Integer, "5"));
            Assert.Equal("Col eq 5.5m", s.BuildTypedFilter("Col", FLRTGIDataSource.GIColumnType.Decimal, "5.5"));
            Assert.Equal("Col eq true", s.BuildTypedFilter("Col", FLRTGIDataSource.GIColumnType.Boolean, "True"));
            Assert.Equal("Col eq datetime'2026-03-01T00:00:00'",
                s.BuildTypedFilter("Col", FLRTGIDataSource.GIColumnType.Date, "2026-03-01"));
        }

        // ── Date range filter (BuildDateRangeFilter) ────────────────────────────────

        [Fact]
        public void BuildDateRangeFilter_monthly_spans_one_month()
        {
            Assert.Equal(
                "D ge datetime'2026-03-01T00:00:00' and D lt datetime'2026-04-01T00:00:00'",
                NewService().BuildDateRangeFilter("D", "2026", "3", FLRTGIDataSource.PeriodScopeType.Monthly));
        }

        [Fact]
        public void BuildDateRangeFilter_december_rolls_to_next_year()
        {
            Assert.Equal(
                "D ge datetime'2026-12-01T00:00:00' and D lt datetime'2027-01-01T00:00:00'",
                NewService().BuildDateRangeFilter("D", "2026", "12", FLRTGIDataSource.PeriodScopeType.Monthly));
        }

        [Fact]
        public void BuildDateRangeFilter_yearly_spans_full_year()
        {
            Assert.Equal(
                "D ge datetime'2026-01-01T00:00:00' and D lt datetime'2027-01-01T00:00:00'",
                NewService().BuildDateRangeFilter("D", "2026", "6", FLRTGIDataSource.PeriodScopeType.Yearly));
        }

        // ── Header filter assembly (BuildHeaderFilter) ──────────────────────────────

        [Fact]
        public void BuildHeaderFilter_builds_exact_period_from_template()
        {
            var ds = new FLRTGIDataSource
            {
                PeriodFilterColumn = "FinancialPeriod",
                PeriodFilterType = FLRTGIDataSource.GIColumnType.String,
                PeriodScope = FLRTGIDataSource.PeriodScopeType.Exact,
                PeriodFilterTemplate = "{MONTH}{YEAR}",
            };
            Assert.Equal("FinancialPeriod eq '032025'",
                NewService().BuildHeaderFilter(ds, "2025", "3", null, null, null));
        }

        [Fact]
        public void BuildHeaderFilter_returns_tautology_when_no_filters_configured()
        {
            Assert.Equal("1 eq 1", NewService().BuildHeaderFilter(new FLRTGIDataSource(), "2025", "3", null, null, null));
        }

        // ── CALCULATED column topological sort (TopoSortCalculated) ─────────────────

        private static FLRTGIDataSourceColumn Calc(string alias, string formula, int sort) =>
            new FLRTGIDataSourceColumn
            {
                ColumnAlias = alias,
                Formula = formula,
                SortOrder = sort,
                LineType = FLRTGIDataSourceColumn.ColumnLineType.Calculated,
            };

        [Fact]
        public void TopoSortCalculated_orders_dependencies_before_dependents()
        {
            // GROSS depends on NET; declared in the "wrong" sort order on purpose.
            var cols = new List<FLRTGIDataSourceColumn>
            {
                Calc("GROSS", "NET + 5", 0),
                Calc("NET", "10", 1),
            };
            var sorted = NewService().TopoSortCalculated(cols);
            Assert.Equal("NET", sorted[0].ColumnAlias);
            Assert.Equal("GROSS", sorted[1].ColumnAlias);
        }

        [Fact]
        public void TopoSortCalculated_throws_on_cycle()
        {
            var cols = new List<FLRTGIDataSourceColumn>
            {
                Calc("A", "B + 1", 0),
                Calc("B", "A + 1", 1),
            };
            Assert.Throws<PXException>(() => NewService().TopoSortCalculated(cols));
        }
    }
}
