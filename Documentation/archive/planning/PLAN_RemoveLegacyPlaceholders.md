# Plan: Remove Legacy Placeholder Path & Optimize Memory

## Context

The Financial Report generation pipeline has two code paths for resolving placeholders:
1. **Engine path** (current) — `ReportCalculationEngine.CalculateAll()` processes Report Definitions and produces `PREFIX_LINECODE_CY/PY/PM` placeholders
2. **Legacy path** (obsolete) — Scans the Word template for raw account codes like `{{A74101_CY}}`, classifies them, and resolves values directly from OData GL data

All templates now use Report Definitions exclusively. The legacy path runs but its output is always overridden by engine results at merge time. Removing it saves processing time, memory, and ~568 lines of dead code. Additionally, nulling API data after the engine runs reduces peak memory for large datasets.

---

## Item 1: Remove Legacy Placeholder Processing from ReportGenerationService.Execute()

**File:** `AFSCPFinancialReportv213032026\Services\ReportGenerationService.cs`

### Remove these lines/blocks:

| Lines | Code | Reason |
|-------|------|--------|
| 78 | `ExtractPlaceholderKeys(templatePath)` | PopulateTemplate scans internally |
| 82-83 | `needsPM` from placeholder scan | Replace with always-true (Item 3) |
| 120-127 | `SeparateAllPlaceholderTypes()` + logging | Legacy only |
| 192-197 | `UserSettings` creation | Legacy only |
| 200-203 | `AnalyzePlaceholders()` + `ProcessPlaceholdersFromFetchedData()` | Legacy only |
| 206-208 | `ProcessAccountRangePlaceholders()` | Legacy only |
| 211-213 | `ProcessWildcardRangePlaceholders()` | Legacy only |
| 246-262 | 3 merge fallback loops | No legacy values to merge |
| 267-271 | Legacy placeholder count logging | References removed variables |

### Keep these lines unchanged:

- Lines 56-60: `ReportDataPipeline.BuildContext()` and definition links
- Lines 62-75: Template file loading
- Lines 88-115: `needsDetail` and `needsCumulative` from line items (engine uses these)
- Lines 129-154: Period computation
- Lines 159-177: All API fetch tasks and `Task.WhenAll`
- Lines 179-186: Result destructuring
- Lines 222-242: `ReportCalculationEngine.CalculateAll()`
- Lines 265-266: Year constants (`CY`/`PY` added to dictionary)
- Line 276: `PopulateTemplate()` call

### After changes, Execute() flow becomes:

1. Load definitions + pipeline context
2. Get template file
3. Compute `needsDetail`, `needsCumulative` from line items
4. Compute period strings
5. Fetch API data in parallel (always include PM)
6. Run `ReportCalculationEngine.CalculateAll()` → `finalPlaceholders`
7. Add year constants to dictionary
8. Null out API data (Item 2)
9. `PopulateTemplate()` with engine dictionary
10. Save generated file

---

## Item 2: Null Out API Data After Engine Runs

**File:** `AFSCPFinancialReportv213032026\Services\ReportGenerationService.cs`

After `CalculateAll()` returns and `finalPlaceholders` is built, add:

```csharp
// Free API data — no longer needed after engine run
currYearData = null;
prevYearData = null;
prevYearPriorData = null;
prevMonthData = null;
januaryBeginningDataCY = null;
januaryBeginningDataPY = null;
cumulativeCYData = null;
cumulativePYData = null;
```

This lets GC reclaim the 8 `FinancialApiData` objects (including their `DetailRows` lists) before Word template processing begins.

---

## Item 3: Always Fetch PM Data

**File:** `AFSCPFinancialReportv213032026\Services\ReportGenerationService.cs`

Currently (lines 82-83 + 173-175):
```csharp
bool needsPM = extractedKeys.Any(k => k.EndsWith("_PM", ...));
var taskPM = needsPM ? Task.Run(...) : Task.FromResult<FinancialApiData>(null);
```

Change to always fetch (remove the conditional):
```csharp
var taskPM = Task.Run(() => localDataService.FetchAllApiData(
    _currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger,
    prevMonthPeriod, needsDetail, cancellationToken), cancellationToken);
```

**Rationale:** One extra API call, but eliminates dependency on template scanning. The engine always produces `_PM` placeholders — if the template uses them, the data is there. If not, PopulateTemplate ignores the unused keys.

---

## Item 4: Remove Dead Code from FinancialDataService + Supporting Files

### 4a. FinancialDataService.cs — Remove ~568 lines of legacy methods

**5 public methods (legacy-only, called only from ReportGenerationService.Execute()):**
- `SeparateAllPlaceholderTypes()` (~27 lines)
- `AnalyzePlaceholders()` (~389 lines)
- `ProcessPlaceholdersFromFetchedData()` (~30 lines)
- `ProcessAccountRangePlaceholders()` (~57 lines)
- `ProcessWildcardRangePlaceholders()` (~65 lines)

**16 private helper methods (only called from the 5 above):**
- `ConvertBalanceTypeCode()`
- `GetDataSourceForRangeCalculation()`
- `CalculateAccountRangeSum()`
- `IsAccountInRange()`
- `CompareAccountCodes()`
- `ExpandWildcardPattern()`
- `CalculateWildcardRangeSum()`
- `IsAccountInWildcardRange()`
- `DoesAccountMatchWildcardPattern()`
- `IncrementPrefix()`
- `ProcessSinglePlaceholder()`
- `ParsePrefixPlaceholder()`
- `FetchBalanceForPrefixPlaceholder()`
- `GetSelectColumnsForBalanceType()`
- `GetBalanceValueFromResult()`
- `ExecutePrefixFetchWithFallback()`

**3 public helper methods (only called from legacy path):**
- `HasAccountRangePattern()`
- `HasWildcardRangePattern()`
- `HasPrefixPattern()`

**1 unused public method (never called anywhere):**
- `ExecuteOptimizedApiRequests()`

**2 inner classes (legacy-only):**
- `PlaceholderRequest`
- `UserSettings`

### 4b. PlaceholderResultProcessor.cs — Delete entire file

Only called from `ExecuteOptimizedApiRequests()` which is itself unused.

### 4c. WordTemplateService.cs — Remove redundant call

Line 29 inside `PopulateTemplate()`:
```csharp
ExtractPlaceholderKeys(templatePath);  // result discarded — remove this line
```

This eliminates a redundant full document traversal (the doc is already scanned on line 28).

---

## Files Modified (summary)

| File | Action |
|------|--------|
| `Services\ReportGenerationService.cs` | Remove legacy steps, null API data, always fetch PM |
| `Services\FinancialDataService.cs` | Remove ~568 lines of legacy methods + helpers |
| `Services\PlaceholderResultProcessor.cs` | Delete entire file |
| `Services\WordTemplateService.cs` | Remove redundant line 29 |

---

## Verification

1. **Build** the DLL in Visual Studio — confirm no compilation errors
2. **Republish** the Financial Report CP
3. **Test report generation** — generate a report using a template with Report Definition placeholders (`{{BS_REVENUE_CY}}`, `{{PL_NET_INCOME_PY}}`, etc.) and verify values are populated correctly
4. **Test PM placeholders** — use a template with `_PM` suffixed placeholders and verify they resolve to values (not "0")
5. **Test missing placeholders** — any `{{...}}` in the template that doesn't match an engine key should resolve to "0"
6. **Check trace logs** — verify engine placeholder count is logged and no legacy processing logs appear
