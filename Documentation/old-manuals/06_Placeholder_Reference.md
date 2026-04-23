# Placeholder Reference

Complete reference for all placeholder types supported by the AFS Financial Report module.

---

## Report Definition Placeholders

**Pattern:** `{{PREFIX_LINECODE_PERIOD}}`

| Component | Source | Examples |
|---|---|---|
| PREFIX | Definition Prefix field in FR101002 | `BS`, `PL`, `CF`, `DEMO` |
| LINECODE | Line Code field in the line items grid | `CASH`, `TOTAL_ASSETS`, `NI` |
| PERIOD | Fixed suffix | `CY`, `PY`, `PM` |

### Period Suffixes

| Suffix | Meaning |
|---|---|
| `CY` | Current Year — the year and month selected on the report |
| `PY` | Prior Year — same month, one year back |
| `PM` | Prior Month — the month immediately before the selected month |

### Examples

| Placeholder | Meaning |
|---|---|
| `{{BS_CASH_CY}}` | Balance Sheet Cash, Current Year |
| `{{BS_CASH_PY}}` | Balance Sheet Cash, Prior Year |
| `{{BS_CASH_PM}}` | Balance Sheet Cash, Prior Month |
| `{{PL_REVENUE_CY}}` | P&L Revenue, Current Year |
| `{{PL_NI_PY}}` | P&L Net Income, Prior Year |
| `{{CF_OP_CASH_CY}}` | Cash Flow Operating Cash, Current Year |

---

## Year Label Placeholders

| Placeholder | Resolves To | Example |
|---|---|---|
| `{{CY}}` | Current year number | `2026` |
| `{{PY}}` | Previous year number | `2025` |

Use in column headers: `As at December {{CY}}`

---

## GI Data Source Placeholders

### Single Value

**Pattern:** `{{PREFIX_ALIAS}}`

| Placeholder | Meaning |
|---|---|
| `{{PO_TOTAL_AMOUNT}}` | PO data source, TOTAL_AMOUNT column alias |
| `{{HR_HEADCOUNT}}` | HR data source, HEADCOUNT column alias |

### Multi-Row Expand

**Pattern:** `{{PREFIX_ALIAS_RANK_COLUMNNAME}}`

| Placeholder | Meaning |
|---|---|
| `{{PO_TOP_VENDORS_1_VendorName}}` | Top vendor #1, VendorName column |
| `{{PO_TOP_VENDORS_1_OrderTotal}}` | Top vendor #1, OrderTotal column |
| `{{PO_TOP_VENDORS_2_VendorName}}` | Top vendor #2, VendorName column |
| `{{PO_TOP_VENDORS_10_OrderTotal}}` | Top vendor #10, OrderTotal column |

Rank starts at 1 and goes up to the Row Limit configured on the column.

---

## Cross-Definition Formula Placeholders

When a Calculated line in one definition references another definition's line:

| Formula | In Definition | Produces |
|---|---|---|
| `BS_TA - PL_NI` | CF (prefix CF) | `{{CF_RESULT_CY}}`, `{{CF_RESULT_PY}}` |
| `PL_REVENUE * 0.3` | CUSTOM (prefix CU) | `{{CU_TAX_EST_CY}}` |

The engine resolves cross-definition references automatically. All referenced definitions
must be linked to the same report.

---

## Invisible Line Placeholders

Lines with `Visible = unchecked`:
- Are still calculated internally
- Can be referenced by other formulas
- Do NOT produce output placeholders
- Useful for intermediate calculations

---

## Placeholder Rules

1. Case-insensitive: `{{BS_CASH_CY}}` = `{{bs_cash_cy}}`
2. Unmatched placeholders resolve to `0`
3. Maximum 1,000 placeholders per Word template
4. Placeholders work in document body, headers, and footers
5. Type each placeholder in one go in Word (don't pause mid-typing)
