# MBR Definition Setup — FR101004

This document describes how to create an **MBR Definition** (Monthly Board Report data source). An MBR Definition tells the AFS Financial Report engine how to pull values from **any Acumatica Generic Inquiry** — not just the GL Trial Balance — and exposes the results as named placeholders that the [MBR Report Generation](../02-generation/MBRReport_Generation.md) screen and Word templates can consume.

Where [ReportDefinition_Setup](ReportDefinition_Setup.md) is hard-wired to GL balance concepts (Account / EndingBalance / Debit / Credit), an MBR Definition is fully generic: you map any GI column to any output placeholder, with your own filters, aggregation, and formatting.

> **Screen ID:** FR101004
> **Screen Title:** AFS MBR Config
> **Menu path:** *AFS → Configuration → AFS MBR Config*
> **MBR =** Monthly Board Report — the executive-summary presentations that consume these data sources live on FR101003 (*AFS Monthly Board Report*).

---

## Prerequisites

- Tenant credentials saved in [Tenant Credentials Setup (FR101001)](TenantCredentials_Setup.md). The Detect Columns and Test Fetch actions both call OData using these credentials.
- A **published Generic Inquiry** that returns the data you want to surface (e.g. `PO-PurchaseOrder`, `AR-CustomerSummary`, a custom GI). The GI must be reachable via OData under the same tenant.
- A **unique Prefix** (2–10 alphanumeric chars) for namespacing this data source's placeholders. Must be unique across all MBR Definitions.

---

## Concepts

| Term | What it is |
|---|---|
| **Data Source** | One row in this screen — a header configuring the GI plus the filter columns. |
| **Column** | A child row defining one output placeholder. Four `LineType` flavours: `VALUE` (read + aggregate from GI), `MULTIROW` (expand top-N rows), `CALCULATED` (formula over other columns), `HEADING` (label only). |
| **Prefix** | Short alphanumeric tag prepended to every placeholder this data source emits — e.g. `PO_TOTAL_ORDERS`, `HR_HEADCOUNT`. |
| **Placeholder** | The token that lands in the merge dictionary: `{{<Prefix>_<ColumnAlias>}}` for VALUE / CALCULATED rows, `{{<Prefix>_<ColumnAlias>_<N>_<GIColumn>}}` for MULTIROW rows. |

---

## Steps

The walkthrough below builds a fresh data source from scratch — code `DEMODOCS`, prefix `DD`, GI `PO-PurchaseOrder`. Pick whatever values fit your scenario; the flow is the same.

### Step 1 — Navigate to AFS MBR Config

In the top search bar type **MBR** and select **AFS MBR Config** under *AFS → Configuration*. The second search hit, *AFS Monthly Board Report*, is the FR101003 presentation-generation screen — that's where you'll consume the placeholders later.

![Top search showing AFS MBR Config and AFS Monthly Board Report results](../images/mbr_definition/mbrdef_02_search.png)

The screen opens in **New Record** mode with an empty header and an empty Columns grid. The toolbar carries the standard Acumatica record-navigation actions (Back / Save / Cancel / Insert / Copy-Paste / Delete / First / Prev / Next / Last). Two screen-specific buttons sit above the grid:

- **Detect Columns** — disabled until **Generic Inquiry** is set. Calls OData against the chosen GI, captures the runtime column list, and stores it on the record so the column selectors below show real OData property names.
- **Test Fetch** — runs the full data source against live data using a year / month / branch / org / ledger filter you provide in a dialog, prints every resulting placeholder + value to the trace log and an in-screen dialog. Use it to sanity-check the configuration before wiring it into a presentation.

![FR101004 landing — empty New Record with all field groups visible](../images/mbr_definition/mbrdef_01_landing.png)

---

### Step 2 — Fill the Identity & Description

Type values into the four identity-group fields:

| Field             | Required | Notes                                                                                  |
| ----------------- | -------- | -------------------------------------------------------------------------------------- |
| **Data Source Code** | yes (key) | Unique identifier, max 50 chars. Locked after first save. |
| **Prefix**           | yes      | 2–10 alphanumeric chars. Namespaces every placeholder this row emits. Must be globally unique across **all** MBR Definitions and Report Definitions. |
| **Active**           | default ✓ | Uncheck to hide the data source from downstream screens without deleting it. |
| **Description**      | no       | Free-text label, max 255 chars. Shown in the selector. |

Once filled the screen breadcrumb updates to show the new code (`DEMODOCS`):

![Header — Data Source Code, Prefix, Description filled](../images/mbr_definition/mbrdef_03_header_typed.png)

---

### Step 3 — Pick the Generic Inquiry

Click the magnifier next to **Generic Inquiry** (or focus the field and press F3). The selector lists every published GI in the tenant; type into the Search box at the top to narrow the list.

![Generic Inquiry selector — search filter "PO-Purchase" showing matching GIs](../images/mbr_definition/mbrdef_04_gi_selector.png)

Double-click the GI you want — for the example we pick `PO-PurchaseOrder`. The header now shows it bound to that GI, and **Detect Columns** turns on:

![Generic Inquiry picked — Detect Columns now enabled](../images/mbr_definition/mbrdef_05_gi_picked.png)

> **Key Column** is optional and identifies which GI column is the row "key" (analogue of `Account` in Trial Balance). It only matters if column rows below set `Key From / Key To` ranges. Leave blank to skip key-range filtering altogether.

---

### Step 4 — Detect Columns

Click **Detect Columns**. The graph opens an OData connection to the chosen GI, captures the runtime column list, stores it on the hidden `DetectedColumns` field, saves the record, and shows the result in a dialog:

![Detect Columns dialog — 33 OData columns from PO-PurchaseOrder](../images/mbr_definition/mbrdef_06_detect_columns_dialog.png)

After clicking OK, every "GI Column" / "Filter Column" selector below now shows the **real OData property names** captured from the live GI (rather than the design-time `ObjectName_Field` format that GIResult would otherwise return).

> **Note:** the action also persists the record — `Data Source Code` and `Prefix` are key-locked from this point on. If you need to change either, delete the record and recreate it.

---

### Step 5 — Configure the Period Filter

The Period Filter group tells the engine **which GI column** carries the period and **how to format the OData predicate** when the presentation fires. All four fields are optional — leave **Period Filter Column** blank to skip period filtering entirely.

| Field                  | Notes                                                                                 |
| ---------------------- | ------------------------------------------------------------------------------------- |
| **Period Filter Column** | The GI column the engine adds to the OData `$filter` clause. Selector lists the columns Detect Columns captured. |
| **Period Type**          | How to format the OData predicate value. `String` / `Integer` / `Decimal` / `Date` / `Boolean`. |
| **Period Scope**         | How wide a window the period covers. `Exact` = `eq <value>` (Trial Balance-style strings like `"012025"`); `Monthly` = `ge first-of-month and lt first-of-next-month` (Date columns); `Yearly` = whole-year range. |
| **Period Template**      | Builds the value from the presentation header. Tokens: `{YEAR}`, `{MONTH}`. Examples: `{MONTH}{YEAR}` → `"012025"`, `{YEAR}-{MONTH}` → `"2025-01"`, `{YEAR}` → `"2025"`. For a Date column with `Monthly` scope the engine ignores the template and uses the period boundaries. |

For a `PO-PurchaseOrder` example the **Date** column with **Period Type = Date** and **Period Scope = Monthly** is the right shape — the engine will scope to the chosen month each time the presentation runs:

![Configuring Period Filter — Date column, Date type, Monthly scope, autocomplete dropdowns](../images/mbr_definition/mbrdef_07_period_configured.png)

---

### Step 5b — Configure the Dimension Filters (Branch / Organization / Ledger)

Three companion groups on the right side of the header — **Branch Filter**, **Organization Filter**, **Ledger Filter** — narrow the GI rows by dimension. They are independent of the Period Filter and of each other; configure only the ones that matter for the GI you bound. Leaving a Column field blank disables that filter entirely.

#### What each field does

Each group has the same two-field shape:

| Field            | Purpose |
| ---------------- | ------- |
| **\<X> Filter Column** | The GI column the engine adds to the OData `$filter` clause for this dimension. Selector lists every column Detect Columns captured. Blank = no `$filter` predicate is added for this dimension. |
| **\<X> Type**          | How to format the predicate value when the engine substitutes the user-supplied value. `String` (wraps in single quotes — `eq 'HQ'`), `Integer` / `Decimal` (no quotes — `eq 2`), `Date` (ISO literal — `eq 2026-04-01T00:00:00`), `Boolean` (`true`/`false`). Pick the type that matches the OData property's actual type — wrong type produces a 400 from OData. |

#### Where the run-time values come from

The Filter Column and Type are *configuration only* — they declare the schema. The actual value to filter on is supplied at run time:

| Caller | Source |
| ------ | ------ |
| **Test Fetch** dialog (this screen) | The **Branch** / **Organization** / **Ledger** text boxes you fill in the dialog (see Step 7 below). |
| **Generate Presentation** on FR101003 | The presentation header's **Branch** / **Organization** / **Ledger** fields (same role they play on FR101000). |

Whichever dimension fields the caller leaves blank become **inactive** for that run — the engine simply omits the predicate, so the data source returns rows for all branches / orgs / ledgers (of whichever dimensions weren't filled).

#### How the predicates combine

Every active filter is AND-combined into the single `$filter` clause sent to the GI:

```
($filter = <PeriodPredicate>
       and <BranchPredicate>
       and <OrgPredicate>
       and <LedgerPredicate>
       and <Per-row Key Range from each Column row>
       and <Per-row Row Filter from each Column row>)
```

Worked example — a data source mapped to a sales GI with `BranchID` (String), `OrganizationID` (String), `LedgerID` (Integer) columns. Test Fetch supplies *Branch = `MAIN`*, *Organization = `CENSOF`*, *Ledger = `2`*; the column row also sets `Status eq 'Closed'` as its Row Filter. The engine emits:

```odata
$filter=Date ge 2026-04-01T00:00:00 and Date lt 2026-05-01T00:00:00
       and BranchID eq 'MAIN'
       and OrganizationID eq 'CENSOF'
       and LedgerID eq 2
       and Status eq 'Closed'
```

#### When to skip these filters

The dimension filters exist to mirror the FR101000 / FR101003 *header scope* — they are mainly useful when the same data source is consumed across tenants or branches and the presentation user picks the scope. Skip them when:

- The GI is already inherently scoped (custom SQL view that joins on a fixed branch).
- You want a single global metric that ignores the presentation header (leave all three Column fields blank).
- The GI returns rows for one ledger only.

> **Type-mismatch trap.** Setting **Branch Type = Integer** but pointing **Branch Filter Column** at a string column produces an OData 400 error at fetch time, not a save-time validation error. If Test Fetch fails with `Bad Request — Invalid filter expression`, the wrong **Type** dropdown is the first thing to check.

> The five header-level filters (Period + Branch + Org + Ledger) are AND-combined with each column row's optional `Key From/To` range and `Row Filter (OData)` to form the final `$filter` clause sent to the GI.

---

### Step 6 — Add Column Rows

Each column row in the grid produces one output placeholder (or a set of placeholders for `MULTIROW` lines). Click **+** on the grid toolbar to add a row. The new row appears with default values: `Sort Order = 0`, `Line Type = Value (from GI)`, `Column Type = Decimal`, `Aggregate = Sum`, `Visible = ✓`.

![Empty column row inserted with defaults](../images/mbr_definition/mbrdef_08_column_row_added.png)

The grid has 17 columns. Their relevance depends on `LineType` — fields that don't apply to the current type are auto-disabled by the graph.

#### Common columns

| Column            | Required | Notes                                                                                |
| ----------------- | -------- | ------------------------------------------------------------------------------------ |
| **Sort Order**       | default 0 | Display order in the grid and in the markdown preview. Does **not** drive evaluation order — that uses topological sort of formula references, so a CALCULATED row can sit at Sort Order 10 above the VALUE rows it consumes at 20/30 without breaking. |
| **Column Alias**     | yes      | Unique within the data source. Allowed chars: letters, digits, underscore. Forms the placeholder key: `{{<Prefix>_<ColumnAlias>}}`. Spaces, hyphens, dots in the alias break the placeholder lookup at merge time — keep it `[A-Z0-9_]+`. |
| **Description**     | optional | Human-readable label. Shown as the row label in the markdown preview that Test Fetch / FR101003 produce. The Word merge dictionary doesn't reference it. |
| **Line Type**       | yes      | One of four — see table below. The graph auto-disables fields that don't apply when you change Line Type, and on the next save it nulls those fields too. |
| **Visible**         | default ✓ | Uncheck to **calculate but not emit**. Useful for intermediate VALUE rows that only exist as inputs to a CALCULATED row's Formula. Disabled (and forced false) on `HEADING` rows. The dictionary still contains the value — just not under a `{{ }}` key, so templates can't reference it directly but formulas can. |
| **Format String**   | optional | Standard .NET format string applied at the final write step (after Aggregate and CALCULATED arithmetic). See the [Format String reference](#format-string-reference) below. Blank = pass through (numbers as `123456.78`, dates in ISO). |

#### Line Type values

Click the **Line Type** cell to open the dropdown:

![Line Type dropdown — Value (from GI) / Multi-Row Expand / Calculated (formula) / Heading](../images/mbr_definition/mbrdef_09_line_type_dropdown.png)

| Code         | Label                  | What it does |
| ------------ | ---------------------- | --- |
| `VALUE`      | Value (from GI)        | Read **GI Column**, aggregate matching rows with **Aggregate**. Single placeholder. |
| `MULTIROW`   | Multi-Row Expand       | Sort rows by **Order By Column** in **Sort Direction**, take the top **Row Limit**, expand each row into placeholders keyed `{{<Prefix>_<ColumnAlias>_<N>_<GICol>}}`. Use for "Top 10 Customers"-style content. |
| `CALCULATED` | Calculated (formula)   | Evaluate **Formula** at run time — arithmetic over other Column Aliases (`+ - * /`, parentheses). Same engine as `ReportDefinition` formulas. |
| `HEADING`    | Heading                | Label only. Emits no placeholder; sets Visible = false; disables every value-related field. |

#### VALUE-line columns

Editable when `LineType = VALUE`:

| Column          | Notes |
| --------------- | --- |
| **GI Column**     | The OData property to read. After Detect Columns runs, the dropdown shows the real GI columns. Required for VALUE rows — save fails with `"GI Column is required for Value lines."` if blank. |
| **Column Type**   | How to parse the JSON value returned by OData. `Decimal` / `Integer` / `Boolean` / `Date` / `String`. Determines which Aggregates make sense — see the table below. Mismatch (e.g. `Decimal` on a String column) yields `0` per row, not an error. |
| **Aggregate**     | How to combine multiple matching rows — see Aggregate table below. |
| **Key From / To** | See [Key range](#key-range-key-from--key-to) below. |
| **Row Filter (OData)** | See [Row Filter](#row-filter-odata) below. |

Click the **Aggregate** cell — 6 functions:

![Aggregate dropdown — Sum / First / Max / Min / Avg / Count](../images/mbr_definition/mbrdef_10_aggregate_dropdown.png)

| Code  | Works on                              | Returns                                |
| ----- | ------------------------------------- | -------------------------------------- |
| `SUM`   | Decimal / Integer / Boolean (counts trues) | Total of values across matching rows. Empty result set → `0`. |
| `FIRST` | any                                   | First matching row's value (in OData natural order). Use with `String` / `Date` where Sum is meaningless. Empty result set → empty string / `0` / null per type. |
| `MAX`   | Decimal / Integer / Date              | Largest value. Empty result set → `0` (or `0001-01-01` for Date). |
| `MIN`   | Decimal / Integer / Date              | Smallest value. Empty result set → `0`. |
| `AVG`   | Decimal / Integer                     | Arithmetic mean across matching rows. Empty result set → `0` (no division-by-zero error). |
| `COUNT` | any                                   | Number of matching rows. The column value is **ignored** — even rows with `null` in `GIColumn` count. Combine with `Row Filter` to count "rows where Status = Closed". |

##### Key Range (Key From / Key To)

Filters rows by comparing the value in the **parent data source's `Key Column`** (set on the header) against this range. Both bounds are **inclusive**.

| Pattern | Meaning |
| ------- | ------- |
| `Key From = 4000`, `Key To = 4999` | Include rows where `KeyColumn` is between `4000` and `4999`. |
| `Key From = 4000`, `Key To` blank   | Include rows where `KeyColumn ≥ 4000`. |
| `Key From` blank, `Key To = 4999`   | Include rows where `KeyColumn ≤ 4999`. |
| Both blank                          | Include all rows (no key-range predicate). |

Comparison is **string-lex** when the GI column is a string and **numeric** when the column is numeric — Acumatica decides per-column from the GI metadata. For numeric account-style columns (`AccountID`, `OrderNbr`) numeric ordering is what you want; for code columns (`BranchCD`, `Status`) string ordering applies, so `Key From = 'A'` to `Key To = 'M'` works as expected.

Key Range applies only when **Key Column** is set on the header. Without a Key Column, the engine has no reference field to compare against — Key From / Key To values you type are ignored at fetch time.

##### Row Filter (OData)

Free-text **OData predicate** AND-combined with the header filters and the per-row Key Range. This is the escape hatch that lets one VALUE row apply filters the standard header / key-range fields can't express. The string is appended verbatim to the `$filter` clause sent to the GI — no parsing, no substitution, no escaping.

**Common patterns:**

| Goal | Row Filter |
| ---- | ---------- |
| Only closed orders | `Status eq 'Closed'` |
| Open orders above $1,000 | `Status eq 'Open' and OrderTotal gt 1000` |
| Anything but cancelled | `Status ne 'Cancelled'` |
| Multiple statuses | `Status eq 'Open' or Status eq 'Pending'` |
| Vendor name contains "Acme" | `contains(VendorName,'Acme')` |
| Vendor name starts with "A" | `startswith(VendorName,'A')` |
| Vendor name ends with "Inc" | `endswith(VendorName,'Inc')` |
| Created since a date | `CreatedOn ge 2025-01-01T00:00:00` |
| Date in current quarter | `Date ge 2026-01-01T00:00:00 and Date lt 2026-04-01T00:00:00` |
| Field is null | `Description eq null` |
| Field is not null | `Description ne null` |
| Boolean true | `IsActive eq true` |
| Numeric in a list | `LedgerID eq 1 or LedgerID eq 2 or LedgerID eq 3` |

**Operators supported by Acumatica OData:** `eq`, `ne`, `lt`, `le`, `gt`, `ge`, `and`, `or`, `not`, plus the `contains` / `startswith` / `endswith` / `tolower` / `toupper` string functions.

**Quoting rules:**
- String literals: single quotes — `'Closed'`, `'MAIN'`. Embed a single-quote by doubling it (`'O''Brien'`).
- Numeric literals: bare — `1000`, `4.5`.
- Date / DateTime literals: ISO format with no quotes — `2025-01-01T00:00:00`.
- Boolean literals: `true` / `false` (lowercase, no quotes).
- Column names: bare (no quotes around the property name).

**How it composes with the other filters.** The final `$filter` for a single VALUE row is:

```
<header Period predicate>
  and <header Branch predicate>
  and <header Org predicate>
  and <header Ledger predicate>
  and <Key Range from this row>
  and <Row Filter from this row>
```

Different VALUE rows can have different Row Filters — each row hits OData with its own `$filter`, so two rows on the same data source can read the same GI with completely different predicates (one for "open orders sum", one for "closed orders sum") in a single Test Fetch / Generate run. The engine de-duplicates queries when two rows happen to produce identical filter strings.

**Common gotchas:**
- **No quoting around column names.** `'OrderTotal' gt 1000` is wrong — it compares the literal string `'OrderTotal'` against `1000`. Drop the quotes: `OrderTotal gt 1000`.
- **Case-sensitive operators.** `Status EQ 'Open'` fails — use lowercase `eq`.
- **Property names from Detect Columns.** If you typed a property name freehand, run Detect Columns first and copy from the dialog — the runtime OData property names sometimes differ from the design-time field names shown in the GI Result table.
- **Failures surface only at fetch time.** A malformed Row Filter saves successfully. Test Fetch (or Generate Presentation) raises `Failed to fetch OData` with the OData 400 message — read the trace log for the actual server response.

#### MULTIROW-line columns

Editable when `LineType = MULTIROW`. **Key From / To** and **Row Filter** also apply (same semantics as for VALUE rows).

| Column                       | Notes |
| ---------------------------- | --- |
| **Order By Column**            | GI column to sort rows by before slicing. Selector lists Detect-Columns output. Required for MULTIROW — leaving it blank gives natural OData order, which is undefined for most GIs. |
| **Sort Direction**             | `Descending` (default — top values first, e.g. largest `OrderTotal`) or `Ascending` (smallest first, oldest dates first). |
| **Row Limit**                  | How many top rows to expand into placeholders. Default 10. The GI `$top` clause uses this value directly — pulling 1000 rows is fine for OData but slow for the Word merge step. Keep it ≤ 50 for templates. |
| **Display Columns (markdown)** | Comma-separated list of GI column names to include in the markdown preview that Test Fetch / FR101003 produce. Example: `Vendor,OrderTotal,Status` — only those three columns appear in the preview table. **Word placeholders always include every column from the GI row** regardless of this setting; this field affects only the markdown output. |

A MULTIROW row produces one set of placeholders **per ranked row**, named `{{<Prefix>_<ColumnAlias>_<N>_<GIColumn>}}` where `N` is the 1-based rank. For example with Prefix `PO`, Alias `TOPVEND`, Row Limit 3, the engine emits placeholders like:

```
{{PO_TOPVEND_1_Vendor}}      → "ACME-001"
{{PO_TOPVEND_1_OrderTotal}}  → "150,000"
{{PO_TOPVEND_2_Vendor}}      → "BIGCO-002"
{{PO_TOPVEND_2_OrderTotal}}  → "120,000"
{{PO_TOPVEND_3_Vendor}}      → "WIDGET-003"
{{PO_TOPVEND_3_OrderTotal}}  → "98,500"
```

Reference these in a Word template's table cells to produce a "Top N" block that fills automatically each run.

#### CALCULATED-line columns

Editable when `LineType = CALCULATED`:

| Column      | Notes |
| ----------- | --- |
| **Formula**   | Arithmetic over other Column Aliases in **this same data source**. Operators: `+`, `−`, `*`, `/`, parentheses. Cross-data-source references are **not supported** on FR101004 — keep the formula scoped to one data source (Report Definitions on FR101002 do support cross-definition formulas; MBR Definitions don't). |

**Examples:**

```
REVENUE - COST                          → gross margin
(REVENUE - COST) / REVENUE              → margin ratio
TOTAL_OPEN + TOTAL_PENDING              → backlog rolling total
(NET_SALES - LAST_YEAR_NET) / LAST_YEAR_NET   → YoY growth ratio
```

**Resolution rules:**
- Token must match a `Column Alias` defined in this same data source. Case-insensitive.
- Tokens are looked up against the dictionary built so far — topological sort orders rows so a Calculated row's inputs are always evaluated first, regardless of `Sort Order`.
- Unknown token → resolves to `0` and the engine logs a `PXTrace` warning (run still completes).
- Division by zero → result is `0` (no exception).
- Circular references between CALCULATED rows are caught at evaluation time; the run lands on `Failed` with `Circular dependency detected`.

**Format String** still applies on the final result. Use it to express ratios as percentages — `Formula = NET / REVENUE` with `Format String = P1` outputs `42.5%` rather than `0.425`.

#### HEADING-line columns

Editable fields collapse to almost none — Heading rows are pure labels. The graph auto-disables every value-related column (GI Column, Column Type, Aggregate, Key From/To, Row Filter, Formula, Order By Column, etc.) and forces `Visible = false` so no placeholder is ever emitted. The only fields that matter are `Sort Order` and `Description` — the Description prints as a section header in the markdown preview.

Use HEADING rows to group related VALUE / MULTIROW / CALCULATED rows in the markdown output (e.g. an `OPERATIONS` heading above all the operations metrics, then `FINANCIALS` above the cost / margin rows).

#### Format String reference

`Format String` is a standard .NET `ToString(format)` string applied to the final value just before it lands in the placeholder dictionary. Blank = pass-through (raw `decimal.ToString()` or ISO date).

| Type | Format | Input → Output |
| ---- | ------ | -------------- |
| Numeric | `N0`        | `1234567.89` → `1,234,568` |
| Numeric | `N2`        | `1234567.89` → `1,234,567.89` |
| Numeric | `#,##0`     | `1234567.89` → `1,234,568` (same as `N0`, more explicit) |
| Numeric | `#,##0.00`  | `1234567.89` → `1,234,567.89` |
| Numeric | `C0`        | `1234567.89` → `$1,234,568` (uses tenant locale) |
| Numeric | `P0`        | `0.42` → `42 %` |
| Numeric | `P1`        | `0.425` → `42.5 %` |
| Numeric | `0.0%`      | `0.425` → `42.5%` (no space) |
| Numeric | `0.00`      | `1234.56789` → `1234.57` (no thousands separator) |
| Numeric | `E2`        | `1234567.89` → `1.23E+006` (scientific notation) |
| Date    | `yyyy-MM-dd`     | → `2026-04-28` |
| Date    | `dd MMM yyyy`    | → `28 Apr 2026` |
| Date    | `MMM yyyy`       | → `Apr 2026` |
| Date    | `MMMM dd, yyyy`  | → `April 28, 2026` |

Format String is applied **once at the end**, after Aggregate (for VALUE) or Formula evaluation (for CALCULATED) or row-expansion (for MULTIROW). It does not affect cross-row formula references — those use the raw decimal value internally regardless of Format String.

For multi-row expanded placeholders (`{{PFX_ALIAS_N_GICol}}`), the Format String applies to **every column in the expanded row** with the same format. Mixed types in one MULTIROW row (e.g. Vendor name + numeric total) require the Format String to be a numeric format only — string columns ignore it cleanly, but numeric columns get the format treatment.

---

### Step 7 — Save and Test Fetch

Press `Ctrl+S` (or click the toolbar Save button). The graph runs validation; persistence is blocked until the offending field is fixed (see [Validation Errors](#validation-errors) below).

After save, click **Test Fetch** to verify the configuration end-to-end against live data. A dialog asks for the period plus optional dimension filters:

![Test Fetch Parameters dialog — Year / Month / Branch / Organization / Ledger](../images/mbr_definition/mbrdef_11_test_fetch_dialog.png)

| Field          | Default      | Notes |
| -------------- | ------------ | ----- |
| **Year**         | `2026`         | Required. Fed into the `{YEAR}` token in **Period Template**. |
| **Month**        | `12 - Dec`     | Optional. Fed into `{MONTH}` (zero-padded). |
| **Branch**       | blank        | Optional value for the **Branch Filter Column**. |
| **Organization** | blank        | Optional value for the **Org Filter Column**. |
| **Ledger**       | blank        | Optional value for the **Ledger Filter Column**. |

Click **Fetch**. The engine builds the OData query, calls the GI, aggregates per VALUE column, evaluates CALCULATED formulas, expands MULTIROW rows, then displays every resulting `{{Placeholder}} = value` pair in a dialog (also written to the trace log). Check that the values match what you expect before linking the data source on a presentation.

---

### Step 8 — *(Optional)* Discard / Delete

To remove a record, click the **Delete** icon on the toolbar. Acumatica asks for confirmation: *"The current FLRT GI Data Source record will be deleted."* — click **Confirm**. The screen returns to **New Record** mode:

![Clean state after delete — New Record with empty fields](../images/mbr_definition/mbrdef_12_after_delete.png)

To discard pending changes without deleting the saved record, click the **Cancel (Esc)** toolbar button instead.

---

## Validation Errors

#### Header (`FLRTGIDataSource`)

| Check                                            | Error message                                    |
| ------------------------------------------------ | ------------------------------------------------ |
| Data Source Code required                        | `Data Source Code is required.`                  |
| Prefix required                                  | `Prefix is required.`                            |
| Prefix must match `^[A-Za-z0-9]+$`               | `Prefix must contain only letters and digits.`   |
| Prefix unique across all MBR Definitions         | `Prefix must be unique across all GI Data Sources.` |

#### Columns (`FLRTGIDataSourceColumn`)

| Check                                              | Error message                                          |
| -------------------------------------------------- | ------------------------------------------------------ |
| Column Alias required                              | `Column Alias is required.`                            |
| `VALUE` lines must have a GI Column                | `GI Column is required for Value lines.`               |
| `CALCULATED` lines must have a Formula             | `Formula is required for Calculated lines.`            |
| Column Alias unique within the data source        | `Column Alias must be unique within the data source.`  |

---

## How the Engine Uses an MBR Definition

When **Generate Presentation** runs on FR101003 against a presentation that links this data source:

1. `GIDataFetchService` builds the OData query — `$select` covers every GI column referenced by the column rows (key, period, branch, org, ledger, plus every VALUE / MULTIROW / Order-By column). `$filter` is the AND of header filters (period + branch + org + ledger, populated from the presentation header) and per-row `Key From/To` / `Row Filter`.
2. The GI is called once per data source — the query result is cached in memory for the run.
3. `FetchAndAggregate` processes each column row in topological order (so CALCULATED rows see their inputs already resolved):
   - **VALUE** rows aggregate matching rows with the chosen `Aggregate`.
   - **MULTIROW** rows sort the result set by `Order By Column`, slice the top `Row Limit`, and expand each row into one set of `_<N>_<GICol>` placeholders.
   - **CALCULATED** rows evaluate `Formula` against the dictionary built so far.
   - **HEADING** rows are skipped.
4. `FormatString` is applied at write time; the resulting strings land in the same merge dictionary that report definition placeholders use.

Test Fetch runs steps 1–4 against a manually-supplied period and dumps the dictionary to a dialog — same code path as the real generation run.

---

## DAC Field Reference

### `FLRTGIDataSource` (header)

| DAC field             | Display name           | Type / List                                                | Notes |
| --------------------- | ---------------------- | ---------------------------------------------------------- | ----- |
| `DataSourceCD`        | Data Source Code       | `PXDBString(50)` (key)                                     | Locked after save. |
| `Prefix`              | Prefix                 | `PXDBString(10)`                                           | `^[A-Za-z0-9]+$`, globally unique. Locked after save. |
| `Description`         | Description            | `PXDBString(255)`                                          | |
| `IsActive`            | Active                 | `PXDBBool` (default true)                                  | |
| `GIName`              | Generic Inquiry        | `PXDBString(100)`                                          | Selector over `GIDesign.name`. |
| `KeyColumn`           | Key Column             | `PXDBString(100)`                                          | GI column selector. |
| `PeriodFilterColumn`  | Period Filter Column   | `PXDBString(100)`                                          | GI column selector. |
| `PeriodFilterType`    | Period Type            | `String`/`Integer`/`Decimal`/`Date`/`Boolean`              | Default `String`. |
| `PeriodFilterTemplate`| Period Template        | `PXDBString(50)`                                           | `{YEAR}`, `{MONTH}` tokens. |
| `PeriodScope`         | Period Scope           | `Exact`/`Monthly`/`Yearly`                                 | Default `Monthly`. |
| `BranchFilterColumn`  | Branch Filter Column   | `PXDBString(100)`                                          | |
| `BranchFilterType`    | Branch Type            | `String`/`Integer`/`Decimal`/`Date`/`Boolean`              | Default `String`. |
| `OrgFilterColumn`     | Org Filter Column      | `PXDBString(100)`                                          | |
| `OrgFilterType`       | Org Type               | `String`/`Integer`/`Decimal`/`Date`/`Boolean`              | Default `String`. |
| `LedgerFilterColumn`  | Ledger Filter Column   | `PXDBString(100)`                                          | |
| `LedgerFilterType`    | Ledger Type            | `String`/`Integer`/`Decimal`/`Date`/`Boolean`              | Default `String`. |
| `DetectedColumns`     | *(hidden)*             | `PXDBString(4000)`                                         | Comma-separated OData columns captured by **Detect Columns**. Powers the GI column selector dropdowns. |

### `FLRTGIDataSourceColumn` (children)

| DAC field            | Display name              | Type / List                                                | Notes |
| -------------------- | ------------------------- | ---------------------------------------------------------- | ----- |
| `SortOrder`          | Sort Order                | `PXDBInt`                                                  | |
| `ColumnAlias`        | Column Alias              | `PXDBString(100)`                                          | Unique within data source. |
| `Description`        | Description               | `PXDBString(255)`                                          | |
| `LineType`           | Line Type                 | `VALUE`/`MULTIROW`/`CALCULATED`/`HEADING`                  | Default `VALUE`. |
| `GIColumn`           | GI Column                 | `PXDBString(100)`                                          | VALUE lines only. |
| `ColumnType`         | Column Type               | `Decimal`/`Integer`/`Boolean`/`Date`/`String`              | Default `Decimal`. |
| `AggregateFunction`  | Aggregate                 | `SUM`/`FIRST`/`MAX`/`MIN`/`AVG`/`COUNT`                    | Default `SUM`. |
| `KeyFrom` / `KeyTo`  | Key From / Key To         | `PXDBString(100)`                                          | Inclusive key range. |
| `RowFilter`          | Row Filter (OData)        | `PXDBString(500)`                                          | Extra `$filter` clause. |
| `OrderByColumn`      | Order By Column           | `PXDBString(100)`                                          | MULTIROW lines. |
| `OrderByDirection`   | Sort Direction            | `ASC`/`DESC`                                               | Default `DESC`. |
| `RowLimit`           | Row Limit                 | `PXDBInt`                                                  | Default 10. |
| `DisplayColumns`     | Display Columns (markdown)| `PXDBString(500)`                                          | MULTIROW preview only. |
| `Formula`            | Formula                   | `PXDBString(500)`                                          | CALCULATED lines. |
| `FormatString`       | Format String             | `PXDBString(50)`                                           | .NET format. |
| `IsVisible`          | Visible                   | `PXDBBool` (default true)                                  | Disabled for HEADING. |

---

## Next Step

Proceed to [MBR Report Generation (FR101003)](../02-generation/MBRReport_Generation.md) to link this data source to a presentation and produce the `.pptx` / merged `.docx` output. The full placeholder catalogue is documented in [Placeholder Reference](../03-reference/Placeholder_Reference.md).
