# Placeholder Reference

Complete catalogue of every placeholder type the AFS Financial Report module emits into the Word merge dictionary or the Gamma markdown prompt.

> **v2.1.x model.** Every placeholder is keyed by **exactly one of two period suffixes**: `_CY` (current FY-to-date) or `_PY` (previous FY same window). The legacy `_PM` (previous-month) suffix was removed in the v2.1.x refactor — see [../_index.md § Project-truth notes](../_index.md#project-truth-notes). Compute month-only deltas inside the Word template (`{{PFX_X_CY}} − {{PFX_X_PY}}`); a Definition formula cannot mix periods.

---

## 1. Report Definition Placeholders

Produced by every visible line item on a [Report Definition (FR101002)](../01-setup/ReportDefinition_Setup.md). One pair per visible line:

```
{{<Prefix>_<LineCode>_CY}}
{{<Prefix>_<LineCode>_PY}}
```

| Component | Source                                              | Examples                  |
|-----------|-----------------------------------------------------|---------------------------|
| `<Prefix>` | `FLRTReportDefinition.DefinitionPrefix`              | `BS`, `PL`, `CF`, `CU`, `DEMO` |
| `<LineCode>` | `FLRTReportLineItem.LineCode`                      | `CASH`, `TOTAL_ASSETS`, `NI`, `GROSS_PROFIT` |
| Period suffix | `_CY` or `_PY` *(constants in `Helper/Constants.cs`)* | `_CY`, `_PY` |

### Period suffixes

| Suffix | Meaning |
|--------|---------|
| `_CY`  | **Current Year** — fiscal-year-to-date through the end of the selected month, in the year picked on FR101000 / FR101003. |
| `_PY`  | **Previous Year** — fiscal-year-to-date through the same month, in the previous fiscal year. |

> No `_PM` suffix exists. If a template inherited from the v2.0 era still has `{{X_X_PM}}` tokens, they are rendered as `0` in the Word document (the same default-to-zero behaviour applied to any placeholder the engine doesn't produce). No trace warning is emitted for unmatched template placeholders.

### Examples

| Placeholder              | Meaning                                              |
|--------------------------|------------------------------------------------------|
| `{{BS_CASH_CY}}`         | Balance Sheet definition (`BS`), Line Code `CASH`, current FY-to-date.   |
| `{{BS_CASH_PY}}`         | Same line, previous FY same window.                  |
| `{{PL_REVENUE_CY}}`      | P&L definition, Line Code `REVENUE`, current period. |
| `{{PL_NI_PY}}`           | P&L Net Income, prior year.                          |
| `{{CF_OP_CASH_CY}}`      | Cash Flow definition, `OP_CASH` line, current.       |

### Lines that emit **blank** placeholders

These lines still emit `_CY` / `_PY` keys, but with an **empty-string value** (the engine writes the key as `""` in `BuildPlaceholderMap`). So a `{{PFX_X_CY}}` placeholder referencing one of them renders **blank**, not `0` — the empty-string emission is exactly what prevents the unknown-placeholder default-to-`0`.

- **`HEADING` lines** — display-only section labels; their `_CY` / `_PY` keys are emitted as empty strings.
- **Lines with `Visible = false`** — still calculated so other formulas / subtotals can reference them, but their `_CY` / `_PY` keys are emitted as empty strings.
- **`SUBTOTAL` / `CALCULATED` lines with `Visible = false`** — same: computed internally, emitted blank.

---

## 2. Year-Label Placeholders

Two convenience tokens added to the dictionary at the end of every report run by `ReportGenerationService`:

| Placeholder | Resolves to                       | Example |
|-------------|-----------------------------------|---------|
| `{{CY}}`    | The current year as a 4-digit string | `2026`  |
| `{{PY}}`    | The previous year as a 4-digit string | `2025`  |

> Source: `ReportGenerationService.cs` lines 181–182 — `finalPlaceholders[Constants.CurrentYearSuffix] = currYear;` and the matching previous-year line. `currYear` falls back to the current system year if **Current Year** is unset, so these tokens are always populated (never blank/null).

Use in column headers and section labels:

```
| Account             | As at December {{CY}} | As at December {{PY}} |
|---------------------|-----------------------|-----------------------|
| Cash                | {{BS_CASH_CY}}        | {{BS_CASH_PY}}        |
```

---

## 3. MBR Definition Placeholders

Produced by every visible column row on an [MBR Definition (FR101004)](../01-setup/MBRDefinition_Setup.md). Three flavours, one per `LineType`.

### 3a. Single Value (`LineType = VALUE`)

One placeholder per visible Column Alias:

```
{{<Prefix>_<ColumnAlias>}}
```

| Component         | Source                                  | Example         |
|-------------------|-----------------------------------------|-----------------|
| `<Prefix>`        | `FLRTGIDataSource.Prefix`                | `PO`, `HR`, `SO` |
| `<ColumnAlias>`   | `FLRTGIDataSourceColumn.ColumnAlias`     | `TOTAL_AMOUNT`, `HEADCOUNT`, `OPEN_ORDERS` |

| Placeholder              | Meaning                                                  |
|--------------------------|----------------------------------------------------------|
| `{{PO_TOTAL_AMOUNT}}`    | PO data source, `TOTAL_AMOUNT` column, aggregate result. |
| `{{HR_HEADCOUNT}}`       | HR data source, `HEADCOUNT` column.                      |
| `{{SO_OPEN_ORDERS}}`     | Sales Order data source, count of open orders.           |

> No `_CY` / `_PY` suffix on MBR Definition placeholders. The MBR pipeline runs once per record against a single user-supplied period (Year + Month + dimensions on FR101003), not against two periods like Report Definitions do.

### 3b. Multi-Row Expand (`LineType = MULTIROW`)

Each ranked row produces one placeholder **per GI column** in the row:

```
{{<Prefix>_<ColumnAlias>_<Rank>_<GIColumn>}}
```

| Placeholder                              | Meaning                                                     |
|------------------------------------------|-------------------------------------------------------------|
| `{{PO_TOP_VENDORS_1_VendorName}}`        | Rank-1 row of `TOP_VENDORS` MULTIROW, value of `VendorName`. |
| `{{PO_TOP_VENDORS_1_OrderTotal}}`        | Rank-1 row, value of `OrderTotal`.                           |
| `{{PO_TOP_VENDORS_2_VendorName}}`        | Rank-2 row.                                                  |
| `{{PO_TOP_VENDORS_10_OrderTotal}}`       | Rank-10 row, OrderTotal column.                              |

Rank goes from `1` to the **Row Limit** configured on the column row (default 10). A placeholder referencing a rank higher than `Row Limit` (or a GI column excluded by `DisplayColumns`) is never produced by the engine, so it falls to the unknown-placeholder default and renders as `0` (not blank).

### 3c. Calculated (`LineType = CALCULATED`)

Same shape as a VALUE placeholder — `{{<Prefix>_<ColumnAlias>}}` — but the value comes from evaluating the row's `Formula` against the other Column Aliases in the same data source.

```
{{PO_GROSS_MARGIN}}     ← Formula: REVENUE - COGS
{{PO_NET_MARGIN_PCT}}   ← Formula: NET / REVENUE  (with Format String = P1)
```

> Cross-data-source formulas are **not** supported on FR101004 — keep MBR Definition formulas scoped to one data source. Cross-definition formulas are supported on Report Definitions (FR101002), see [Cross-Definition Formula Placeholders](#cross-definition-formula-placeholders) below.

---

## 4. Cross-Definition Formula Placeholders

A `CALCULATED` line in one Report Definition can reference Line Codes in another Report Definition, **provided both are linked to the same FR101000 / FR101003 record**. Tokens use the qualified `<Prefix>_<LineCode>` form (no period suffix — the engine substitutes `_CY` and `_PY` automatically on each evaluation pass).

| Formula in Definition (Prefix) | Token resolution                   | Placeholders emitted                      |
|--------------------------------|------------------------------------|-------------------------------------------|
| `BS_TA - PL_NI` *(in CF, prefix CF)* | `BS.TA` + `PL.NI` from linked defs | `{{CF_RESULT_CY}}`, `{{CF_RESULT_PY}}`    |
| `PL_REVENUE * 0.3` *(in CU, prefix CU)* | `PL.REVENUE` from linked def     | `{{CU_TAX_EST_CY}}`, `{{CU_TAX_EST_PY}}`  |
| `(REVENUE - COGS) / REVENUE` *(in PL)* | implicit prefix → `PL.REVENUE`, `PL.COGS` | `{{PL_GROSS_MARGIN_CY}}`, `{{PL_GROSS_MARGIN_PY}}` |

The formula is evaluated **twice** — once against the CY dictionary, once against the PY dictionary — producing both `_CY` and `_PY` placeholders automatically. See [ReportDefinition_Setup.md § Cross-Definition Formulas](../01-setup/ReportDefinition_Setup.md#cross-definition-formulas) for the full resolution rules and worked examples.

---

## 5. Placeholder Resolution Rules

| Rule | Source |
|------|--------|
| **Case-insensitive lookup** — `{{BS_CASH_CY}}` matches `{{bs_cash_cy}}` and `{{Bs_Cash_Cy}}`. | `WordTemplateService` does case-insensitive Replace at merge time. |
| **Unmatched placeholders → `0`** — any `{{...}}` token in the template that the engine never produced is silently replaced with `0`. No trace warning is emitted for this. `{{CY}}` / `{{PY}}` are always populated (never blank). | `WordTemplateService.PopulateTemplate` (defaults unknown keys to `"0"`). |
| **No placeholder-count cap.** There is no runtime limit on the number of placeholders in a template — every `{{...}}` token is processed. | `WordTemplateService.PopulateTemplate`. |
| **Document scope** — placeholders work in the document body, headers, footers, and all table cells. The merge service walks every paragraph. | `WordTemplateService.cs`. |
| **Type each placeholder in one go in Word** — Word's auto-correct / spell-check sometimes splits `{{` or the underscore mid-typing, breaking the merge token. If a placeholder isn't replacing, retype the whole `{{...}}` token without pausing. | Word behaviour, not a project rule. |
| **HEADING and invisible lines emit blank placeholders** (empty-string value, not absent) — see [§ 1 — Lines that emit blank placeholders](#lines-that-emit-blank-placeholders). | `ReportCalculationEngine.BuildPlaceholderMap`. |

---

## 6. Quick Lookup — Where Each Placeholder Comes From

| Placeholder shape                         | Producer                                       | Suffix (period)         |
|-------------------------------------------|------------------------------------------------|-------------------------|
| `{{<Prefix>_<LineCode>_CY}}` / `_PY`       | Report Definition line item (FR101002)          | `_CY` / `_PY`           |
| `{{<Prefix>_<LineCode>_CY}}` / `_PY` from `CALCULATED` | Report Definition formula (cross-def OK) | `_CY` / `_PY` (2-pass)  |
| `{{<Prefix>_<ColumnAlias>}}`              | MBR Definition VALUE / CALCULATED column (FR101004) | none                |
| `{{<Prefix>_<ColumnAlias>_<N>_<GICol>}}`  | MBR Definition MULTIROW column (FR101004)       | none                    |
| `{{CY}}` / `{{PY}}`                       | `ReportGenerationService` year-label injector   | year string itself      |

---

## 7. Diagnosing a Stale or Wrong Placeholder

| Symptom                                         | Likely cause                                                                  | Fix                                              |
|-------------------------------------------------|-------------------------------------------------------------------------------|--------------------------------------------------|
| `{{X_Y_PM}}` rendered as `0` in output          | Legacy `_PM` token; engine doesn't emit it, so it hits the default-to-`0` path. | Replace with `{{X_Y_CY}}` and `{{X_Y_PY}}` columns; compute the month delta inside the template if needed. |
| Placeholder shows `0` instead of expected value | Line Code typo, Prefix mismatch, or referenced definition not linked to record.| Verify the `<Prefix>_<LineCode>` matches a line on a linked definition (FR101002) or column on a linked data source (FR101004). |
| Placeholder shows `-` (single dash)             | The line was calculated but the value rounded to zero.                        | Expected — zero values render as `-` for readability. |
| Placeholder shows `(1,234)` (parentheses)       | The line evaluated to a negative number.                                      | Expected — negatives render in parentheses, accounting style. |
| Run **fails** with `Formula references unknown Line Code '<key>'…` | A `CALCULATED` Report Definition formula references a Line Code/prefix that doesn't exist on a linked definition. The engine **throws** (it does not warn-and-zero) and the run lands on `Failed`. | Open FR101000 / FR101003 and add the missing definition, or fix the Line Code in the formula. |

For a full error catalogue see [Troubleshooting](Troubleshooting.md).
