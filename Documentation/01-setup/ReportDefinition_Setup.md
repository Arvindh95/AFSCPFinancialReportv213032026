# Report Definition Setup — FR101002

This document describes how to create an **AFS Report Definition**. A Report Definition is the blueprint that tells the Financial Report engine:

1. Which **Generic Inquiry (GI)** to pull GL data from,
2. Which **columns** on that GI carry the balance figures, filter keys, and period tags, and
3. Which **line items** (account ranges, subtotals, calculated lines, headings) to emit as placeholders in the final report.

One definition is typically created per statement type (Balance Sheet, P&L, Cash Flow, etc.) and then linked to the **[Financial Report (FR101000)](../02-generation/FinancialReport_Generation.md)** or **[MBR Report Generation (FR101003)](../02-generation/MBRReport_Generation.md)** screen.

> **Period model.** All balances surfaced by a definition are **fiscal-year-to-date** figures (FY start → selected period end). Each visible line emits **two** placeholders: `_CY` (current year) and `_PY` (previous year). There is no single-period / month-only balance type.

---

## Prerequisites

- Tenant credentials already saved in [Tenant Credentials (FR101001)](TenantCredentials_Setup.md).
- A Generic Inquiry is published that exposes the GL balances you want to consume. The stock GI is **`AFS-Trial-Balance`**; any GI with account / balance / period columns works.
- You know the GL account ranges that make up each line of the statement you are modelling.

---

## Steps

### Step 1 — Navigate to AFS Report Definition

1. In the top search bar type **AFS Report** and select **AFS Report Definition** under the *AFS* workspace.

> **Screen ID:** FR101002

![AFS Report Definition landing screen](../images/report_definition/reportdef_01_landing.png)

---

### Step 2 — Create a New Record

Click the **+** (Add) button. The **Definition Code** field shows `<NEW>` until saved.

---

### Step 3 — Fill the Header

Enter the four required header fields:

| Field                | Example             | Notes                                                                                                                                             |
| -------------------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Definition Code**  | `DEMO-BS`           | Unique key, max 50 chars. This is the code the Financial Report / Presentation screen will select.                                                |
| **Prefix**           | `DB`                | Short alphanumeric tag, max 10 chars (`^[A-Za-z0-9]+$` — letters and digits only, no separators). Namespaces all placeholders emitted by this definition, e.g. `{{DB_TOTAL_ASSETS_CY}}`. **Must be unique across all definitions in the tenant.** |
| **Description**      | `Demo Balance Sheet`| Free-text label, max 255 chars. Appears in the selector dropdown on downstream screens.                                                           |
| **Report Type**      | `Balance Sheet`     | Metadata tag — see Step 4.                                                                                                                        |

![Header filled — TESTER record showing Definition Code, Prefix, Report Type, and Data Source](../images/report_definition/reportdef_02_header_filled.png)

---

### Step 4 — Choose the Report Type

**Report Type** tags the definition so downstream screens can group it. It does not alter calculation — it is metadata only.

| Value | Label                |
| ----- | -------------------- |
| `BS`  | Balance Sheet (default) |
| `PL`  | Profit & Loss        |
| `CF`  | Cash Flow            |
| `CU`  | Custom               |

![Report Type dropdown open — 5 values](../images/report_definition/reportdef_03_reporttype_dropdown.png)

Also visible on the same header row:

- **Active** — uncheck to hide this definition from the selector on FR101000 / FR101003 without deleting it.

---

### Step 5 — Pick the Generic Inquiry

**Generic Inquiry Name** drives *where* the engine pulls GL balances from. The DAC default is **`TrialBalance`** (Acumatica's stock GI name). If your tenant ships only the AFS-branded variant **`AFS-Trial-Balance`** — which is the case for the SalesDemo deployment — change this field before saving; the selector (magnifier icon) lists every published GI in the tenant.

> Generate Report runs a 1-row probe against the configured GI before fetching data. A mismatch surfaces immediately as `Generic Inquiry '<name>' was not found in tenant '<tenant>'.` rather than as a generic OData failure — so the typo is obvious. See [Troubleshooting](../03-reference/Troubleshooting.md#report-generation-errors-fr101000).

![Generic Inquiry selector](../images/report_definition/reportdef_03_gi_selector.png)

The selector lists all `GIDesign` records in the tenant. Any GI that returns rows keyed by account with balance figures can be used — the column mapping in Step 6 tells the engine which GI column carries each concept.

---

### Step 6 — Map the GI Columns (Data Source)

This section tells the engine *which column in the chosen GI* holds each balance concept, filter key, or period tag. Each field is a **GI-column selector** — it only shows columns that exist on the GI picked in Step 5. The defaults match Acumatica's stock **`AFS-Trial-Balance`** GI.

#### Balance columns

| Field                      | Default Column       | Purpose                                                                              |
| -------------------------- | -------------------- | ------------------------------------------------------------------------------------ |
| **Account Column**         | `Account`            | GL account code. Used for account-range filtering on `ACCOUNT` lines.                |
| **Account Type Column**    | `Type`               | Returns `A/L/E/I`. Drives automatic account-type sign normalization and the optional *Account Type Filter* on line items. |
| **Beginning Balance Column** | `BeginningBalance` | Opening balance at the start of the fiscal year. Read when `Balance Type = BEGINNING`.|
| **Ending Balance Column**  | `EndingBalance`      | Closing balance as at the selected period end. Read when `Balance Type = ENDING` *(default)*. |
| **Debit Column**           | `Debit`              | Fiscal-year-to-date debit sum. Read when `Balance Type = DEBIT`.                     |
| **Credit Column**          | `Credit`             | Fiscal-year-to-date credit sum. Read when `Balance Type = CREDIT`.                   |
| **Movement Column**        | `Movement`           | Listed in the OData `$select` clause for completeness. **Not consumed by the engine** — when `Balance Type = MOVEMENT` the engine derives the value as `Debit − Credit` from the columns mapped above. Safe to leave at the default. |

#### Period & filter columns

| Field                    | Default Column      | Purpose                                                                                  |
| ------------------------ | ------------------- | ---------------------------------------------------------------------------------------- |
| **Period Column**        | `FinancialPeriod`   | Period tag on each GI row (e.g. `04-2026`). The engine uses this to slice CY vs PY rows. |
| **Subaccount Column**    | `Subaccount`        | Column holding the Subaccount code. Referenced by the optional **Subaccount Filter** on each line item. |
| **Branch Column**        | `BranchID`          | Branch code column. Referenced by **Branch Filter**.                                     |
| **Organization Column**  | `OrganizationID`    | Organization code column. Referenced by **Organization Filter**.                         |
| **Ledger Column**        | `LedgerID`          | Ledger code column. Referenced by **Ledger Filter**.                                     |

> If you clone or customize the stock GI, rename these columns here to match the new field names. The 13 mapping fields default to the stock `AFS-Trial-Balance` column names — save is **not** blocked if you blank one out, but the engine will fall back to the default name (`Account`, `EndingBalance`, …) at fetch time. Set them explicitly when working against a renamed GI.

![Account Column selector open](../images/report_definition/reportdef_04_accountcolumn_selector.png)

---

### Step 7 — Configure Formatting (Rounding)

Controls how numeric placeholders are formatted when they appear in the final Word / markdown output.

| Field                | Values                                | Notes                                                                            |
| -------------------- | ------------------------------------- | -------------------------------------------------------------------------------- |
| **Rounding Level**   | `UNITS` (default), `THOUS`, `MILL`    | Divides all numbers by 1 / 1,000 / 1,000,000 before rendering.                   |
| **Decimal Places**   | `0` (default), `1`, `2`               | Digits shown after the decimal point.                                            |

![Formatting section expanded — Rounding Level = Units, Decimal Places = 0](../images/report_definition/reportdef_04_formatting_rounding.png)

Example: With Rounding Level = `THOUS` and Decimal Places = `1`, a raw value of `1,234,567.89` renders as `1,234.6`.

**Save** the header (`Ctrl+S`) before adding line items.

---

## Line Items Grid

Each row in **Line Items** is a single placeholder that will appear in the generated report. The engine calculates one CY and one PY value per visible row. The grid columns are as follows.

![Line Items grid — TESTER record with 5 rows demonstrating all 5 balance types](../images/report_definition/reportdef_05_line_items_grid.png)

### Columns — overview

| Column                 | Required         | Description                                                                                                                                           |
| ---------------------- | ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Sort Order**         | yes              | Integer controlling row order in the report. Lines are emitted in ascending `SortOrder`.                                                              |
| **Line Code**          | yes              | Unique token within this definition (≤100 chars). Forms the placeholder key, e.g. `CASH` → `{{DB_CASH_CY}}`, `{{DB_CASH_PY}}`.                        |
| **Description**        | yes if visible   | Human-readable label, shown in the generated markdown table.                                                                                          |
| **Line Type**          | yes              | Determines how the engine computes this line. See table below.                                                                                        |
| **Account From / To**  | Account lines    | Inclusive GL account range to sum. Example: `10000`…`10999`.                                                                                          |
| **Account Type Filter**| optional         | Restrict the range to a single GL account type.                                                                                                       |
| **Sign Rule**          | Account lines    | `ASIS` keeps the raw GL sign; `FLIP` multiplies by −1 for presentation (typical for Liability / Income).                                              |
| **Balance Type**       | Account lines    | Which GI balance column to read. Five options — all fiscal-year-to-date except the two point-in-time balances. See [Balance Type values](#balance-type-values). |
| **Group / Parent Line**| optional         | The subtotal `LineCode` this row rolls up into. Editable on `ACCOUNT`, `SUBTOTAL`, and `CALCULATED` lines (so subtotals can nest, and a calculated KPI can roll into a parent subtotal). Disabled on `HEADING`. Example: child rows CASH, AR, INV all set Parent = `CURRENT_ASSETS`. |
| **Formula**            | Calculated lines | Arithmetic expression over other `LineCode`s using `+ − × ÷` and parentheses. Example: `REVENUE - COGS`. References may also cross definitions using the fully qualified `<Prefix>_<LineCode>` form — see [Cross-Definition Formulas](#cross-definition-formulas). |
| **Visible in Report**  | yes              | Default true. Uncheck to keep the value internal (usable in formulas / subtotals) without emitting a placeholder.                                     |
| **Subaccount Filter**  | optional         | Exact-match subaccount, e.g. `000-000`. Blank = all subaccounts.                                                                                      |
| **Branch Filter**      | optional         | `BranchCD` selector. Blank = all branches.                                                                                                            |
| **Organization Filter**| optional         | `OrganizationCD` selector. Blank = all organizations.                                                                                                 |
| **Ledger Filter**      | optional         | `LedgerCD` selector. Blank = all ledgers.                                                                                                             |

> All filter columns only apply when **Line Type** = `ACCOUNT`. They are ignored by Subtotal / Calculated / Heading lines.

---

### Line Type values

| Code         | Label          | Behaviour                                                                                                                        |
| ------------ | -------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `ACCOUNT`    | Account Range  | Sum all GI rows where the account code falls inside `AccountFrom`…`AccountTo` (inclusive), apply Sign Rule, use Balance Type.    |
| `SUBTOTAL`   | Subtotal       | Sum all lines whose `ParentLineCode` equals this row's `LineCode`.                                                               |
| `CALCULATED` | Calculated     | Evaluate `Formula` at runtime, referencing other `LineCode`s. Supports `+ − × ÷` and parentheses.                                |
| `HEADING`    | Heading        | Display-only. No value is computed; used to emit a section header into the report.                                               |

![Line Type dropdown open — 4 values](../images/report_definition/reportdef_07_line_type_dropdown.png)

---

### Balance Type values

Applies only to `ACCOUNT` lines. Determines which column from the GI mapping is read. All movement figures (`DEBIT`, `CREDIT`, `MOVEMENT`) are **fiscal-year-to-date** — they accumulate from the FY start through the selected period end. There is no single-month balance type.

| Code        | Label              | Column used                    | Typical use                                                    |
| ----------- | ------------------ | ------------------------------ | -------------------------------------------------------------- |
| `ENDING`    | Ending Balance *(default)* | **Ending Balance Column**    | Balance Sheet lines (point-in-time snapshot at period end).    |
| `BEGINNING` | Beginning Balance  | **Beginning Balance Column**   | Opening-balance columns, capital roll-forwards.                |
| `DEBIT`     | Debit (YTD)        | **Debit Column**               | Gross debit activity FY-to-date.                               |
| `CREDIT`    | Credit (YTD)       | **Credit Column**              | Gross credit activity FY-to-date.                              |
| `MOVEMENT`  | Movement (YTD)     | **Debit Column − Credit Column** *(derived; the Movement Column mapping is fetched but not consumed)* | P&L lines — revenue, expenses, net movement FY-to-date. |

![Balance Type dropdown open — 5 values](../images/report_definition/reportdef_06_balance_type_dropdown.png)

> **Why no single-period (month-only) balance type?** The stock AFS workflow reports fiscal-year performance (CY vs prior-year CY). If you need a month-only view, filter the underlying GI by period, or compute the delta in the template (`{{BS_X_CY}} - {{BS_X_PY}}`).

---

### Sign Rule values

Sign handling is **two-stage** inside the engine:

1. **Automatic account-type normalization** (`ApplyAccountTypeSign`). When the GI row has an **Account Type** (`A`/`L`/`E`/`I`) in the mapped type column, the engine multiplies the raw GL value by `−1` for credit-normal types (`L`, `I`). Asset/Expense values pass through unchanged. This turns credit-normal GL values (Acumatica stores them negative) into the positive figures expected on a financial statement.
2. **User-controlled Sign Rule** (this column). Applied **after** the automatic normalization.

| Code   | Label       | Math               | When to use                                                                                                                                                     |
| ------ | ----------- | ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ASIS` | As-Is       | no extra multiply  | **Default — correct for nearly every line.** The engine has already normalized credit-normal types for you.                                                    |
| `FLIP` | Flip Sign   | `× −1`             | Use only when (a) the GI's type column is blank/unmapped so automatic normalization did not fire, or (b) you specifically want the opposite of the normal sign. |

![Sign Rule dropdown open — As-Is / Flip Sign](../images/report_definition/reportdef_08_sign_rule_dropdown.png)

> **Gotcha.** Setting `FLIP` on a Liability/Income line when the type column *is* populated will double-flip (`−1 × −1 = +1` relative to raw, i.e. a negative value on the statement). If liabilities show up negative after generation, check this column first.

---

### Account Type Filter values

Optional hard-filter on the GL account type. Uses the column mapped in **Account Type Column**.

| Code   | Label         |
| ------ | ------------- |
| *(blank)* | All Types  |
| `A`    | Asset         |
| `L`    | Liability     |
| `E`    | Expense       |
| `I`    | Income        |

Leave blank unless you want to clamp a range that spans multiple account types.

---

## Worked Examples

Below are the minimum column values for each `LineType`.

### ACCOUNT line — sum a range

```
Sort Order   : 10
Line Code    : CASH
Description  : Cash and Cash Equivalents
Line Type    : Account Range
Account From : 10100
Account To   : 10199
Sign Rule    : As-Is
Balance Type : Ending Balance
Visible      : ✓
```

### SUBTOTAL line — sum child lines

```
Sort Order           : 100
Line Code            : CURRENT_ASSETS
Description          : Total Current Assets
Line Type            : Subtotal
Group / Parent Line  : (leave blank — this IS the parent)
Visible              : ✓
```
…and on the rows above, set **Group / Parent Line = `CURRENT_ASSETS`** for each child you want rolled up.

### CALCULATED line — formula over other codes

```
Sort Order  : 200
Line Code   : NET_INCOME
Description : Net Income
Line Type   : Calculated
Formula     : REVENUE - COGS - OPEX - TAX
Visible     : ✓
```

> Unqualified tokens (`REVENUE`, `COGS`, …) resolve **within this definition**. Prefix the token (`PL_REVENUE`, `BS_CASH`) to pull from another definition linked on the same Financial Report — see [Cross-Definition Formulas](#cross-definition-formulas).

### HEADING line — section divider

```
Sort Order  : 5
Line Code   : HDR_ASSETS
Description : ASSETS
Line Type   : Heading
Visible     : ✓   (unchecked headings are discarded silently)
```

---

## Placeholder Key Format

Each visible line emits **two** placeholders into the report:

```
{{<Prefix>_<LineCode>_CY}}   → current year, fiscal-year-to-date through selected period
{{<Prefix>_<LineCode>_PY}}   → previous year, same fiscal-year-to-date window
```

Example — with **Prefix = DB** and **Line Code = CASH**:

```
{{DB_CASH_CY}}
{{DB_CASH_PY}}
```

See [Placeholder Reference](../03-reference/Placeholder_Reference.md) for the full placeholder catalogue.

---

## End-to-End Example — Mini Balance Sheet

This example walks through a complete **Balance Sheet** definition: the raw GL data that lands in the GI, the Line Items grid rows, and the placeholder values that come out the other end.

### 1 — Raw GI data (input)

Assume the `AFS-Trial-Balance` GI returns the following rows as at **end of April 2026** (Branch = `MAIN`, Ledger = `ACTUAL`):

| Account | Type | BeginningBalance | EndingBalance | Debit (YTD) | Credit (YTD) | Movement (YTD) |
| ------- | ---- | ---------------- | ------------- | ----------- | ------------ | -------------- |
| 10100   | A    | 40,000.00        | 52,500.00     | 18,000      | 5,500        | 12,500         |
| 10200   | A    | 80,000.00        | 96,000.00     | 30,000      | 14,000       | 16,000         |
| 10300   | A    | 25,000.00        | 31,500.00     | 10,000      | 3,500        | 6,500          |
| 20100   | L    | −20,000.00       | −28,000.00    | 2,000       | 10,000       | −8,000         |
| 20200   | L    | −50,000.00       | −60,000.00    | 5,000       | 15,000       | −10,000        |

> Liabilities carry negative signs at the GL level (credit-normal). The engine's automatic **account-type normalization** (see [Sign Rule values](#sign-rule-values)) converts these to positive for presentation before Sign Rule is applied. All lines below use `Sign Rule = ASIS` — the engine has already done the sign work.

### 2 — Definition header

```
Definition Code : DEMO-BS
Prefix          : DB
Description     : Demo Balance Sheet
Report Type     : Balance Sheet
Active          : ✓
Generic Inquiry : AFS-Trial-Balance
Account Column      : Account
Account Type Col    : Type
Beginning Bal Col   : BeginningBalance
Ending Bal Col      : EndingBalance
Debit Column        : Debit
Credit Column       : Credit
Movement Column     : Movement
Period Column       : FinancialPeriod
Subaccount Column   : Subaccount
Branch Column       : BranchID
Organization Column : OrganizationID
Ledger Column       : LedgerID
Rounding Level      : UNITS
Decimal Places      : 0
```

### 3 — Line Items grid

| Sort | Line Code         | Description               | Line Type  | Acct From | Acct To | Sign Rule | Balance Type | Parent          | Formula                                  | Visible |
| ---- | ----------------- | ------------------------- | ---------- | --------- | ------- | --------- | ------------ | --------------- | ---------------------------------------- | ------- |
| 5    | HDR_ASSETS        | ASSETS                    | Heading    |           |         |           |              |                 |                                          | ✓       |
| 10   | CASH              | Cash and Cash Equivalents | Account    | 10100     | 10100   | ASIS      | ENDING       | CURRENT_ASSETS  |                                          | ✓       |
| 20   | AR                | Accounts Receivable       | Account    | 10200     | 10200   | ASIS      | ENDING       | CURRENT_ASSETS  |                                          | ✓       |
| 30   | INVENTORY         | Inventory                 | Account    | 10300     | 10300   | ASIS      | ENDING       | CURRENT_ASSETS  |                                          | ✓       |
| 100  | CURRENT_ASSETS    | Total Current Assets      | Subtotal   |           |         |           |              |                 |                                          | ✓       |
| 105  | HDR_LIAB          | LIABILITIES               | Heading    |           |         |           |              |                 |                                          | ✓       |
| 110  | AP                | Accounts Payable          | Account    | 20100     | 20100   | ASIS      | ENDING       | TOTAL_LIAB      |                                          | ✓       |
| 120  | LOAN              | Long-Term Loan            | Account    | 20200     | 20200   | ASIS      | ENDING       | TOTAL_LIAB      |                                          | ✓       |
| 200  | TOTAL_LIAB        | Total Liabilities         | Subtotal   |           |         |           |              |                 |                                          | ✓       |
| 300  | NET_ASSETS        | Net Assets (A − L)        | Calculated |           |         |           |              |                 | `CURRENT_ASSETS - TOTAL_LIAB`            | ✓       |

### 4 — How each row computes

| Line Code         | Computation (auto type-sign × ASIS)                                                | Value (CY)  |
| ----------------- | ---------------------------------------------------------------------------------- | ----------- |
| `CASH`            | `EndingBalance[10100]` × type-sign(A)=+1 × ASIS = `52,500 × +1 × +1`               | `52,500`    |
| `AR`              | `EndingBalance[10200]` × type-sign(A)=+1 × ASIS                                    | `96,000`    |
| `INVENTORY`       | `EndingBalance[10300]` × type-sign(A)=+1 × ASIS                                    | `31,500`    |
| `CURRENT_ASSETS`  | Subtotal of children where `Parent = CURRENT_ASSETS`                               | `180,000`   |
| `AP`              | `EndingBalance[20100]` × type-sign(L)=−1 × ASIS = `−28,000 × −1 × +1`              | `28,000`    |
| `LOAN`            | `EndingBalance[20200]` × type-sign(L)=−1 × ASIS = `−60,000 × −1 × +1`              | `60,000`    |
| `TOTAL_LIAB`      | Subtotal of children where `Parent = TOTAL_LIAB`                                   | `88,000`    |
| `NET_ASSETS`      | `CURRENT_ASSETS − TOTAL_LIAB`                                                      | `92,000`    |

`NET_ASSETS` is the residual after liabilities — useful as a tie-out figure for downstream placeholder mappings.

### 5 — Placeholders emitted

With **Prefix = `DB`**, the engine writes the following key/value pairs into the placeholder dictionary (the same keys appear in the generated markdown and in any Word template mail-merge):

| Placeholder                | Value (April-2026 CY / FY-to-date) |
| -------------------------- | ---------------------------------- |
| `{{DB_CASH_CY}}`           | `52,500`                           |
| `{{DB_AR_CY}}`             | `96,000`                           |
| `{{DB_INVENTORY_CY}}`      | `31,500`                           |
| `{{DB_CURRENT_ASSETS_CY}}` | `180,000`                          |
| `{{DB_AP_CY}}`             | `28,000`                           |
| `{{DB_LOAN_CY}}`           | `60,000`                           |
| `{{DB_TOTAL_LIAB_CY}}`     | `88,000`                           |
| `{{DB_NET_ASSETS_CY}}`     | `92,000`                           |

Each key is **also** emitted with a `_PY` suffix — same formula evaluated against the prior-year GI rows (April 2025, same FY-to-date window).

> Heading rows (`HDR_ASSETS`, `HDR_LIAB`) do **not** emit placeholders — they only print the description as a section header in the markdown.

### 6 — Effect of Rounding Level

If you switch **Rounding Level = `THOUS`** and **Decimal Places = `1`**, the same placeholders render as:

| Placeholder                | UNITS / 0   | THOUS / 1   |
| -------------------------- | ----------- | ----------- |
| `{{DB_CASH_CY}}`           | `52,500`    | `52.5`      |
| `{{DB_CURRENT_ASSETS_CY}}` | `180,000`   | `180.0`     |
| `{{DB_NET_ASSETS_CY}}`     | `92,000`    | `92.0`      |

Useful when the audience is senior management and you want figures in thousands or millions.

---

## End-to-End Example — Mini Profit & Loss

A parallel P&L definition that feeds off the **same** FY-to-date April 2026 GI window. This example is referenced by the Cross-Definition Formulas section below (the Ratios and Cash Flow examples both consume `DP_*` lines from here).

### 1 — Raw GI data (input)

Assume the `AFS-Trial-Balance` GI returns the following **FY-to-date** figures through April 2026 (same scope as the Mini BS — Branch `MAIN`, Ledger `ACTUAL`). P&L lines read the `Movement` column (FY-to-date `Debit − Credit`):

| Account | Type | Debit (YTD) | Credit (YTD) | Movement (YTD) |
| ------- | ---- | ----------- | ------------ | -------------- |
| 40100   | I    | 500         | 250,500      | −250,000       |
| 50100   | E    | 100,100     | 100          | 100,000        |
| 60100   | E    | 60,000      | 0            | 60,000         |
| 70100   | E    | 20,000      | 0            | 20,000         |

> Income accounts are credit-normal — their FY-to-date movement is negative at the GL level. The engine's automatic **account-type normalization** (`Type = I` → `× −1`) converts this to a positive figure before Sign Rule is applied, so REVENUE below uses `Sign Rule = ASIS`. Expense accounts (`Type = E`) are debit-normal positive — also ASIS. `Balance Type = MOVEMENT` reads FY-to-date `Debit − Credit`.

### 2 — Definition header

Same GI and column mappings as Mini BS; only header identity fields differ:

```
Definition Code : DEMO-PL
Prefix          : DP
Description     : Demo Profit & Loss
Report Type     : Profit & Loss
Active          : ✓
Generic Inquiry : AFS-Trial-Balance
(all 13 column mappings default to stock TrialBalance names — see Mini BS above)
Rounding Level  : UNITS
Decimal Places  : 0
```

### 3 — Line Items grid

| Sort | Line Code | Description               | Line Type  | Acct From | Acct To | Sign Rule | Balance Type | Formula              | Visible |
| ---- | --------- | ------------------------- | ---------- | --------- | ------- | --------- | ------------ | -------------------- | ------- |
| 5    | HDR_PL    | PROFIT & LOSS             | Heading    |           |         |           |              |                      | ✓       |
| 10   | REVENUE   | Revenue                   | Account    | 40100     | 40100   | ASIS      | MOVEMENT     |                      | ✓       |
| 20   | COGS      | Cost of Goods Sold        | Account    | 50100     | 50100   | ASIS      | MOVEMENT     |                      | ✓       |
| 30   | GP        | Gross Profit              | Calculated |           |         |           |              | `REVENUE - COGS`     | ✓       |
| 40   | OPEX      | Operating Expenses        | Account    | 60100     | 60100   | ASIS      | MOVEMENT     |                      | ✓       |
| 50   | TAX       | Income Tax                | Account    | 70100     | 70100   | ASIS      | MOVEMENT     |                      | ✓       |
| 60   | NI        | Net Income                | Calculated |           |         |           |              | `GP - OPEX - TAX`    | ✓       |

### 4 — How each row computes

| Line Code | Computation (auto type-sign × ASIS)                                        | Value (CY) |
| --------- | -------------------------------------------------------------------------- | ---------- |
| `REVENUE` | `Movement[40100]` × type-sign(I)=−1 × ASIS = `−250,000 × −1 × +1`          | `250,000`  |
| `COGS`    | `Movement[50100]` × type-sign(E)=+1 × ASIS                                 | `100,000`  |
| `GP`      | `REVENUE − COGS`                                                           | `150,000`  |
| `OPEX`    | `Movement[60100]` × type-sign(E)=+1 × ASIS                                 | `60,000`   |
| `TAX`     | `Movement[70100]` × type-sign(E)=+1 × ASIS                                 | `20,000`   |
| `NI`      | `GP − OPEX − TAX`                                                          | `70,000`   |

### 5 — Placeholders emitted

| Placeholder              | Value (April-2026 CY / FY-to-date) |
| ------------------------ | ---------------------------------- |
| `{{DP_REVENUE_CY}}`      | `250,000`                          |
| `{{DP_COGS_CY}}`         | `100,000`                          |
| `{{DP_GP_CY}}`           | `150,000`                          |
| `{{DP_OPEX_CY}}`         | `60,000`                           |
| `{{DP_TAX_CY}}`          | `20,000`                           |
| `{{DP_NI_CY}}`           | `70,000`                           |

Each key is also emitted with a `_PY` suffix — same formulas evaluated against the prior-year FY-to-date dictionary.

---

## Cross-Definition Formulas

A `CALCULATED` line is not restricted to Line Codes declared inside its own definition. When two or more definitions are **linked on the same report record** (child grid on [Financial Report Generation — Step 8](../02-generation/FinancialReport_Generation.md#step-8--link-one-or-more-report-definitions)), the engine concatenates all of their placeholder dictionaries into one namespace before evaluating formulas. That means a line in one definition can reference a line in another simply by qualifying the token with the other definition's **Prefix**.

### Reference syntax

| Form                        | Resolved against                                              | Example                |
| --------------------------- | ------------------------------------------------------------- | ---------------------- |
| `LINE_CODE` *(unqualified)* | The current definition only.                                  | `REVENUE - COGS`       |
| `PREFIX_LINE_CODE`          | The definition whose `Prefix` matches the leading token.      | `PL_NI + BS_RECV`      |

The separator is a single underscore (the same one used in placeholder keys — `{{PL_NI_CY}}` becomes `PL_NI` inside a formula). Prefixes and Line Codes are matched case-insensitively on the parsed token; case of the typed text is preserved only for display.

> **Rule of thumb.** If you would spell the placeholder `{{PL_NI_CY}}` in the Word template, you write `PL_NI` in the formula. Drop the `{{ }}` and the period suffix.

### Resolution order

Formula tokens never include a period suffix — they address a Line Code only. Global keys are of the form `PREFIX_LINECODE` (no `_CY` / `_PY` on the end). For each token the resolver:

1. Tries each known Prefix (longest first) — if the token starts with `<Prefix>_`, treat the token as already fully qualified (`PL_NI` → global key `PL_NI`).
2. Otherwise treat as implicit — prepend the **current definition's** Prefix (`NI` inside the PL definition → `PL_NI`).
3. If the resulting global key is missing from the dictionary → warning logged, value defaults to `0`.

### Automatic CY / PY evaluation

Each CALCULATED line's formula is evaluated **twice** — once against the Current-Year dictionary, once against the Previous-Year dictionary. Same formula, two different input dictionaries:

```csharp
cyVal = EvaluateFormula(formula, prefix, knownPrefixes, _cyGlobal);
pyVal = EvaluateFormula(formula, prefix, knownPrefixes, _pyGlobal);
```

That is how the two placeholder forms are produced:

| Placeholder             | Which dictionary is used |
| ----------------------- | ------------------------ |
| `{{PREFIX_LINE_CY}}`    | `_cyGlobal`              |
| `{{PREFIX_LINE_PY}}`    | `_pyGlobal`              |

Consequence: **you cannot mix periods inside a single formula.** A formula like `DB_CASH - DB_CASH_PY` does not work — `DB_CASH_PY` is not a valid global key and resolves to `0`. Whatever period the outer evaluation pass is running, every token is looked up against that same period's dictionary.

> **Rule.** Write formulas in terms of Line Codes only (`DB_CASH`, `DP_NI`). The engine picks the right dictionary automatically on each pass. Both output placeholders (`_CY` and `_PY`) drop out of that for free.

### Worked example A — Financial Ratios (consumes `DB` + `DP`)

This example uses the two definitions already worked through above — **Mini Balance Sheet** (Prefix `DB`) and **Mini Profit & Loss** (Prefix `DP`) — and adds a third definition that computes ratios by referencing both. All three are linked to the same Financial Report record for April 2026.

**Available formula tokens** (from the two prior examples — all reference Line Codes, no period suffix):

| From DB (Balance Sheet)             | April 2026 (CY) | From DP (Profit & Loss) | April 2026 (CY) |
| ----------------------------------- | --------------- | ----------------------- | --------------- |
| `DB_CASH`                           | `52,500`        | `DP_REVENUE`            | `250,000`       |
| `DB_AR`                             | `96,000`        | `DP_COGS`               | `100,000`       |
| `DB_INVENTORY`                      | `31,500`        | `DP_GP`                 | `150,000`       |
| `DB_CURRENT_ASSETS`                 | `180,000`       | `DP_OPEX`               | `60,000`        |
| `DB_AP`                             | `28,000`        | `DP_TAX`                | `20,000`        |
| `DB_LOAN`                           | `60,000`        | `DP_NI`                 | `70,000`        |
| `DB_TOTAL_LIAB`                     | `88,000`        |                         |                 |
| `DB_NET_ASSETS`                     | `92,000`        |                         |                 |

> The values shown are the April-2026 (CY) figures. The same tokens resolve to different values on the PY pass (April 2025), so the same formula produces `_CY` and `_PY` placeholders automatically. See **Automatic CY / PY evaluation** above.

> Mini BS has no separate Fixed Assets range, so `DB_CURRENT_ASSETS` = Total Assets for this dataset. `NET_ASSETS = CURRENT_ASSETS − TOTAL_LIAB` (`180,000 − 88,000 = 92,000`) is the residual figure for this dataset.

**Ratios definition header**

```
Definition Code : DEMO-RATIOS
Prefix          : DR
Description     : Demo Financial Ratios (cross-definition)
Report Type     : Custom
Active          : ✓
Rounding Level  : UNITS
Decimal Places  : 2        ← ratios are fractional; show 2 decimals
```

**Ratios line items**

| Sort | Line Code       | Description           | Line Type  | Formula                                              |
| ---- | --------------- | --------------------- | ---------- | ---------------------------------------------------- |
| 10   | `LIAB_RATIO`    | Liability Ratio       | Calculated | `DB_TOTAL_LIAB / DB_CURRENT_ASSETS`                  |
| 20   | `GROSS_MARGIN`  | Gross Margin          | Calculated | `DP_GP / DP_REVENUE`                                 |
| 30   | `NET_MARGIN`    | Net Margin            | Calculated | `DP_NI / DP_REVENUE`                                 |
| 40   | `ROA`           | Return on Assets      | Calculated | `DP_NI / DB_CURRENT_ASSETS`                          |
| 50   | `QUICK_RATIO`   | Quick Ratio           | Calculated | `(DB_CASH + DB_AR) / DB_AP`                          |
| 60   | `CHECK_ID`      | Identity tie-out      | Calculated | `DB_CURRENT_ASSETS - DB_TOTAL_LIAB - DB_NET_ASSETS`  |

**Resolution of every token**

| Token                   | Resolved against                                           |
| ----------------------- | ---------------------------------------------------------- |
| `DB_TOTAL_LIAB`         | Mini Balance Sheet definition (`DB`), Line Code `TOTAL_LIAB`. |
| `DB_NET_ASSETS`         | `DB.NET_ASSETS`.                                           |
| `DP_GP`, `DP_REVENUE`   | Mini P&L definition (`DP`).                                |
| `DP_NI`                 | `DP.NI` = `70,000`.                                        |
| `DB_CURRENT_ASSETS`     | `DB.CURRENT_ASSETS` = `180,000`.                           |

**Computed values**

| Line Code       | Computation                                    | Value (CY) |
| --------------- | ---------------------------------------------- | ---------- |
| `LIAB_RATIO`    | `88,000 / 180,000`                             | `0.49`     |
| `GROSS_MARGIN`  | `150,000 / 250,000`                            | `0.60`     |
| `NET_MARGIN`    | `70,000 / 250,000`                             | `0.28`     |
| `ROA`           | `70,000 / 180,000`                             | `0.39`     |
| `QUICK_RATIO`   | `(52,500 + 96,000) / 28,000`                   | `5.30`     |
| `CHECK_ID`      | `180,000 − 88,000 − 92,000`                    | `0.00`     |

**Placeholders emitted**

Each formula is evaluated twice (once per period dictionary), so every ratio line drops two placeholders into the merge dictionary:

| Line Code      | `{{..._CY}}` (April 2026) | `{{..._PY}}` (April 2025) |
| -------------- | ------------------------- | ------------------------- |
| `LIAB_RATIO`   | `0.49`                    | *(prior-year BS totals)*  |
| `GROSS_MARGIN` | `0.60`                    | *(prior-year PL totals)*  |
| `NET_MARGIN`   | `0.28`                    | ″                         |
| `ROA`          | `0.39`                    | ″                         |
| `QUICK_RATIO`  | `5.30`                    | ″                         |
| `CHECK_ID`     | `0.00`                    | `0.00`                    |

The CY values are what the April-2026 inputs above produce. The PY values are the same formulas evaluated against the prior-year dictionary — they are computed automatically, you do not author separate formulas for them.

To make this run end-to-end: open the FR101000 record, set Current Year = `2026`, Financial Period = `04-2026`, attach a Word template that references any of the `{{DR_*}}` / `{{DB_*}}` / `{{DP_*}}` placeholders, and link **all three** definitions (`DEMO-BS`, `DEMO-PL`, `DEMO-RATIOS`) in the Report Definitions grid. Miss any one of the three and the Ratios formulas resolve to `0` and log a warning.

---

### Worked example B — Cash Flow statement

A third definition that produces a cash-flow view by referencing BS and PL totals. The engine's automatic 2-pass evaluation means the same formulas emit `_CY` and `_PY` placeholders covering April 2026 vs. April 2025 — without any per-period formula authoring.

**Cash Flow definition header**

```
Definition Code : DEMO-CF
Prefix          : DC
Description     : Demo Cash Flow (cross-definition)
Report Type     : Cash Flow
Active          : ✓
Rounding Level  : UNITS
Decimal Places  : 0
```

**Cash Flow line items**

| Sort | Line Code      | Description                  | Line Type  | Formula                                      |
| ---- | -------------- | ---------------------------- | ---------- | -------------------------------------------- |
| 10   | `NI_INPUT`     | Net Income (from P&L)        | Calculated | `DP_NI`                                      |
| 20   | `OPER_WC`      | Operating Working Capital    | Calculated | `DB_AR + DB_INVENTORY - DB_AP`               |
| 30   | `CASH_POS`     | Cash Position (BS)           | Calculated | `DB_CASH`                                    |
| 40   | `DEBT_POS`     | Debt Position (BS)           | Calculated | `DB_LOAN`                                    |
| 50   | `FREE_CF_APPROX` | Free Cash Flow (approx.)   | Calculated | `NI_INPUT - OPER_WC`                         |
| 60   | `DEBT_COVER`   | Cash / Debt coverage ratio   | Calculated | `CASH_POS / DEBT_POS`                        |

Values for each of the two periods drop out automatically from the 2-pass evaluation. For the **CY pass** (April 2026) the linked DB and DP dictionaries hold the values shown in the *Available formula tokens* table above, so:

| Line Code          | Expression (CY)                                   | Value (CY) |
| ------------------ | ------------------------------------------------- | ---------- |
| `NI_INPUT`         | `DP_NI` = `70,000`                                | `70,000`   |
| `OPER_WC`          | `96,000 + 31,500 − 28,000`                        | `99,500`   |
| `CASH_POS`         | `DB_CASH` = `52,500`                              | `52,500`   |
| `DEBT_POS`         | `DB_LOAN` = `60,000`                              | `60,000`   |
| `FREE_CF_APPROX`   | `70,000 − 99,500`                                 | `−29,500`  |
| `DEBT_COVER`       | `52,500 / 60,000`                                 | `0.88`     |

**Placeholders emitted**

| Placeholder                   | CY value   | PY     |
| ----------------------------- | ---------- | ------ |
| `{{DC_NI_INPUT_CY}}`          | `70,000`   | auto   |
| `{{DC_OPER_WC_CY}}`           | `99,500`   | auto   |
| `{{DC_CASH_POS_CY}}`          | `52,500`   | auto   |
| `{{DC_FREE_CF_APPROX_CY}}`    | `−29,500`  | auto   |
| `{{DC_DEBT_COVER_CY}}`        | `0.88`     | auto   |

`_PY` counterparts are computed by the exact same formulas, evaluated against `_pyGlobal`. The Word template can reference both periods side by side — typical for a comparative report showing "This year / Prior year" columns.

> **If you need an explicit year-over-year delta** (e.g. "cash this year minus cash last year"), compute that inside the **template**, not in the definition. A Word template macro / field can subtract `{{DC_CASH_POS_PY}}` from `{{DC_CASH_POS_CY}}` directly. A formula cannot, because tokens always resolve against the single dictionary for the pass currently running.

### Requirements & caveats

1. **All referenced definitions must be linked on the same Financial Report record.** The Financial Report screen does not auto-add definitions; you link them explicitly in the **Report Definitions** grid. If a formula token cannot be resolved (the referenced Prefix is unknown, or the LineCode does not exist on the linked definition), the engine **logs a warning via `PXTrace`** and returns `0` for that token — the run still completes with Status `Ready to Download`, but the affected placeholders will be wrong. Always check the trace log after a run that references cross-definition tokens for the first time.
2. **Unique prefixes.** Two definitions with the same Prefix cannot be linked to the same record — the resolver would be ambiguous. The save-time uniqueness check on FR101002 already prevents two definitions sharing a Prefix tenant-wide, so this is enforced at the source.
3. **No circular references.** `CF_OP_CASH` may reference `PL_NI`, but `PL_NI` cannot in turn reference `CF_*`. The engine runs topological sort (Kahn's algorithm) at the start of evaluation and raises a `CircularDependencyDetected` error listing the offending Line Codes; break the cycle by restructuring formulas or by demoting a shared computation into one of the definitions.
4. **Sort Order across definitions.** Sort Order is a pure presentation field — it never drives evaluation order. Within a single definition *and* across definitions, the engine builds a global dependency graph from all formulas / parent-child subtotals and evaluates in topological order. Write formulas in any order; the engine will compute inputs before dependents automatically.
5. **Rounding is applied only at the final placeholder step.** The `_cyGlobal` / `_pyGlobal` dictionaries that feed every formula store **raw `decimal` values**. Rounding is applied only by `BuildPlaceholderMap` when formatting each value into the string that goes into the Word-merge dictionary. Consequence: a cross-definition formula like `DR_ROA = DP_NI / DB_CURRENT_ASSETS` uses the full-precision numerator and denominator from the PL and BS definitions, regardless of those definitions' Rounding settings. Rounding only affects what the template prints, never what downstream formulas consume.

---

## Save and Validate

Save (`Ctrl+S`) to persist the definition and all line items. The graph runs the following validations in `RowPersisting` — save is blocked and an error marker appears on the offending field until the condition is fixed:

**Header — `FLRTReportDefinition`**

| Check                                         | Error message                              |
| --------------------------------------------- | ------------------------------------------ |
| Definition Code required.                     | `DefinitionCodeRequired`                   |
| Definition Code unique across all definitions.| `DefinitionCodeMustBeUnique`               |
| Prefix required.                              | `DefinitionPrefixRequired`                 |
| Prefix matches `^[A-Za-z0-9]+$` (letters & digits only). | `DefinitionPrefixMustBeAlphanumeric` |
| Prefix unique across all definitions.         | `DefinitionPrefixMustBeUnique`             |

**Line items — `FLRTReportLineItem`**

| Check                                              | Error message                 |
| -------------------------------------------------- | ----------------------------- |
| Line Code required.                                | `LineCodeRequired`            |
| Line Code unique within the definition.            | `LineCodeMustBeUnique`        |
| `ACCOUNT` lines — Account From required.           | `AccountFromRequired`         |
| `ACCOUNT` lines — Account To required.             | `AccountToRequired`           |
| `CALCULATED` lines — Formula required.             | `FormulaRequired`             |

Validation that is **not** enforced at save (but will bite at generation time):

- **Description on visible lines.** A visible line with a blank Description emits an empty label in the markdown output — ugly but not blocked. Fill it in manually.
- **Formula token resolvability.** Unknown tokens are not detected at save. They surface at generation as a `PXTrace` warning and a `0` value (see [Cross-Definition Formulas — Requirements & caveats](#requirements--caveats)).
- **Circular dependencies.** Detected only at generation time, by the topological sort. Error surfaces as `CircularDependencyDetected`.

---

## Next Step

Proceed to [Financial Report Generation](../02-generation/FinancialReport_Generation.md) or [MBR Report Generation](../02-generation/MBRReport_Generation.md) and link this definition to produce output.
