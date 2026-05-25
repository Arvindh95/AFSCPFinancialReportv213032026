# 04 — Testing

| File | Screen | Contents |
|---|---|---|
| [ReportDefinition_TestCases.md](ReportDefinition_TestCases.md) | FR101002 | 69 manual test cases covering header validations, all 4 line types, all 5 balance types, sign rules, dimension filters, subtotals, calculated formulas, IsVisible, full BS scenario. v2.1.3 — message text aligned to current `Helper/Messages.cs`. |

> Test IDs are stable across versions. Removed tests (A-10, B-14, B-15, B-16, G-05) keep their slot with an inline removal note so older test logs remain traceable.

## Automated unit tests

In addition to the manual cases above, the repo ships an xUnit suite at **`FinancialReport.Tests/`** (net48, 60 tests). It covers the pure logic of `ReportCalculationEngine` (sign rules, balance-type selection, account-range comparison, formula evaluator, rounding/formatting, account-line summation) and `GIDataFetchService` (arithmetic evaluator, RowFilter operators, aggregations, typed/date OData filters, CALCULATED topo-sort). Run with `dotnet test FinancialReport.Tests/FinancialReport.Tests.csproj`. See [HANDOVER.md § 10](../../HANDOVER.md) for how the PX assembly resolver and `InternalsVisibleTo` wiring work.
