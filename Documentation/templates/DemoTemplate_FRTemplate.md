# AFS Financial Report — Placeholder Demo Template

Copy the content below into a Word document (.docx) and save it as `DemoTemplate_FRTemplate.docx`.
Use Word tables for the tabular sections. The `{{...}}` tokens are the actual placeholders.

---

## Report Header

Use these to label your columns:

|  | Current Year ({{CY}}) | Prior Year ({{PY}}) |
| - | --------------------- | ------------------- |

---

## Section 1: Basic CY / PY Placeholders

Every visible line item produces two placeholders — current year (`_CY`) and prior year (`_PY`). There is **no** `_PM` (prior-month) suffix; a `{{..._PM}}` token would simply render as `0`. For a month-over-month delta, subtract the two periods inside the template.

| Line Item                 | Current Year        | Prior Year          |
| ------------------------- | ------------------- | ------------------- |
| Cash and Equivalents      | {{DEMO_CASH_CY}}    | {{DEMO_CASH_PY}}    |
| Receivables               | {{DEMO_RECV_CY}}    | {{DEMO_RECV_PY}}    |
| Current Assets (Subtotal) | {{DEMO_CA_CY}}      | {{DEMO_CA_PY}}      |
| Fixed Assets              | {{DEMO_FA_CY}}      | {{DEMO_FA_PY}}      |
| Total Assets              | {{DEMO_TA_CY}}      | {{DEMO_TA_PY}}      |
| Payables (Flip Sign)      | {{DEMO_PAYABLE_CY}} | {{DEMO_PAYABLE_PY}} |
| Total Liabilities         | {{DEMO_TL_CY}}      | {{DEMO_TL_PY}}      |

---

## Section 2: Calculated Lines (Formulas)

These lines don't pull from GL directly. They use formulas referencing other Line Codes.

| Line Item        | Formula          | Current Year            | Prior Year              |
| ---------------- | ---------------- | ----------------------- | ----------------------- |
| Net Assets       | TA - TL          | {{DEMO_NA_CY}}          | {{DEMO_NA_PY}}          |
| Uses Hidden Line | CASH + HIDDEN    | {{DEMO_USES_HIDDEN_CY}} | {{DEMO_USES_HIDDEN_PY}} |

---

## Section 3: All Balance Types (using Cash account range)

Same account range, different balance type on each line.

| Balance Type                            | Current Year         | Prior Year           |
| --------------------------------------- | -------------------- | -------------------- |
| Ending Balance                          | {{DEMO_CASH_CY}}     | {{DEMO_CASH_PY}}     |
| Beginning Balance (Fiscal Year Opening) | {{DEMO_CASH_BEG_CY}} | {{DEMO_CASH_BEG_PY}} |
| YTD Debit (cumulative)                  | {{DEMO_YTD_DEB_CY}}  | {{DEMO_YTD_DEB_PY}}  |
| YTD Credit (cumulative)                 | {{DEMO_YTD_CRD_CY}}  | {{DEMO_YTD_CRD_PY}}  |
| YTD Movement (net)                      | {{DEMO_YTD_MOV_CY}}  | {{DEMO_YTD_MOV_PY}}  |
| Period Debit (single month)             | {{DEMO_P_DEB_CY}}    | {{DEMO_P_DEB_PY}}    |
| Period Credit (single month)            | {{DEMO_P_CRD_CY}}    | {{DEMO_P_CRD_PY}}    |
| Period Movement (single month)          | {{DEMO_P_MOV_CY}}    | {{DEMO_P_MOV_PY}}    |

---

## Section 4: Sign Rule Demo

Shows how Flip Sign affects the output.

| Line Item | Sign Rule | Placeholder         | What Happens                 |
| --------- | --------- | ------------------- | ---------------------------- |
| Cash      | As-Is     | {{DEMO_CASH_CY}}    | GL positive stays positive   |
| Payables  | Flip Sign | {{DEMO_PAYABLE_CY}} | GL negative becomes positive |

---

## Section 5: Subtotal Chain Demo

Shows how parent-child nesting works.

```
CASH   (parent = CA)  →  {{DEMO_CASH_CY}}
RECV   (parent = CA)  →  {{DEMO_RECV_CY}}
                          ─────────────────
CA = CASH + RECV      →  {{DEMO_CA_CY}}

CA     (parent = TA)  →  {{DEMO_CA_CY}}
FA     (parent = TA)  →  {{DEMO_FA_CY}}
                          ─────────────────
TA = CA + FA          →  {{DEMO_TA_CY}}
```

---

## Section 6: Hidden Line Demo

Line `HIDDEN` has IsVisible = unchecked. It does NOT produce a placeholder.
But line `USES_HIDDEN` references it in a formula and still gets the value.

| Line Item                   | Visible | Placeholder                               |
| --------------------------- | ------- | ----------------------------------------- |
| HIDDEN                      | No      | (no output — calculated internally only) |
| USES_HIDDEN = CASH + HIDDEN | Yes     | {{DEMO_USES_HIDDEN_CY}}                   |

---

## Section 7: Multi-Definition Demo

When two definitions are linked to one report, each prefix is independent.
Placeholders from all definitions coexist in one Word template.

| Line Item  | Balance Sheet (BS) | Profit and Loss (PL) |
| ---------- | ------------------ | -------------------- |
| First line | {{BS_CASH_CY}}     | {{PL_REVENUE_CY}}    |
| Totals     | {{BS_TA_CY}}       | {{PL_NI_CY}}         |

Cross-definition formula (in a Cash Flow definition with prefix CF):

| Line Item | Formula       | Value            |
| --------- | ------------- | ---------------- |
| CF Result | BS_TA - PL_NI | {{CF_RESULT_CY}} |

---

## Report Definition Setup Reference

You need to create **3 separate definitions** in FR101002 to demo everything.

---

### Definition 1: DEMO (covers Sections 1–6)

- Definition Code: `DEMO`
- Prefix: `DEMO`
- Report Type: Balance Sheet
- GI Name: TrialBalance
- Rounding: Units / 0 decimals

Line Items:

| Sort | Line Code   | Type          | Acct From | Acct To | Balance Type      | Sign  | Parent | Formula          | Visible |
| ---- | ----------- | ------------- | --------- | ------- | ----------------- | ----- | ------ | ---------------- | ------- |
| 10   | CASH        | Account Range | 10100     | 10199   | Ending Balance    | As-Is | CA     |                  | Yes     |
| 20   | RECV        | Account Range | 11100     | 11199   | Ending Balance    | As-Is | CA     |                  | Yes     |
| 30   | CA          | Subtotal      |           |         |                   |       | TA     |                  | Yes     |
| 40   | FA          | Account Range | 15100     | 15999   | Ending Balance    | As-Is | TA     |                  | Yes     |
| 50   | TA          | Subtotal      |           |         |                   |       |        |                  | Yes     |
| 60   | PAYABLE     | Account Range | 20100     | 20199   | Ending Balance    | Flip  | TL     |                  | Yes     |
| 70   | TL          | Subtotal      |           |         |                   |       |        |                  | Yes     |
| 90   | NA          | Calculated    |           |         |                   |       |        | TA - TL          | Yes     |
| 110  | CASH_BEG    | Account Range | 10100     | 10199   | Beginning Balance | As-Is |        |                  | Yes     |
| 120  | YTD_DEB     | Account Range | 10100     | 10199   | Debit (YTD)       | As-Is |        |                  | Yes     |
| 130  | YTD_CRD     | Account Range | 10100     | 10199   | Credit (YTD)      | As-Is |        |                  | Yes     |
| 140  | YTD_MOV     | Account Range | 10100     | 10199   | Movement (YTD)    | As-Is |        |                  | Yes     |
| 150  | P_DEB       | Account Range | 10100     | 10199   | Period Debit      | As-Is |        |                  | Yes     |
| 160  | P_CRD       | Account Range | 10100     | 10199   | Period Credit     | As-Is |        |                  | Yes     |
| 170  | P_MOV       | Account Range | 10100     | 10199   | Period Movement   | As-Is |        |                  | Yes     |
| 180  | HIDDEN      | Account Range | 10100     | 10199   | Ending Balance    | As-Is |        |                  | No      |
| 190  | USES_HIDDEN | Calculated    |           |         |                   |       |        | CASH + HIDDEN    | Yes     |
| 200  | HEADING1    | Heading       |           |         |                   |       |        |                  | No      |

---

### Definition 2: BS (Balance Sheet — for multi-definition demo, Section 7)

- Definition Code: `BALANCE_SHEET`
- Prefix: `BS`
- Report Type: Balance Sheet
- GI Name: TrialBalance
- Rounding: Units / 0 decimals

Line Items:

| Sort | Line Code | Type          | Acct From | Acct To | Balance Type   | Sign  | Parent | Formula | Visible |
| ---- | --------- | ------------- | --------- | ------- | -------------- | ----- | ------ | ------- | ------- |
| 10   | CASH      | Account Range | 10100     | 10199   | Ending Balance | As-Is | CA     |         | Yes     |
| 20   | RECV      | Account Range | 11100     | 11199   | Ending Balance | As-Is | CA     |         | Yes     |
| 30   | CA        | Subtotal      |           |         |                |       | TA     |         | Yes     |
| 40   | FA        | Account Range | 15100     | 15999   | Ending Balance | As-Is | TA     |         | Yes     |
| 50   | TA        | Subtotal      |           |         |                |       |        |         | Yes     |
| 60   | PAYABLE   | Account Range | 20100     | 20199   | Ending Balance | Flip  | TL     |         | Yes     |
| 70   | TL        | Subtotal      |           |         |                |       |        |         | Yes     |

---

### Definition 3: PL (Profit & Loss — for multi-definition demo, Section 7)

- Definition Code: `PROFIT_LOSS`
- Prefix: `PL`
- Report Type: Profit & Loss
- GI Name: TrialBalance
- Rounding: Units / 0 decimals

Line Items:

| Sort | Line Code | Type          | Acct From | Acct To | Balance Type   | Sign  | Parent | Formula        | Visible |
| ---- | --------- | ------------- | --------- | ------- | -------------- | ----- | ------ | -------------- | ------- |
| 10   | REVENUE   | Account Range | 40100     | 40999   | Ending Balance | Flip  |        |                | Yes     |
| 20   | COGS      | Account Range | 50100     | 50999   | Ending Balance | As-Is |        |                | Yes     |
| 30   | GP        | Calculated    |           |         |                |       |        | REVENUE - COGS | Yes     |
| 40   | OPEX      | Account Range | 60100     | 69999   | Ending Balance | As-Is |        |                | Yes     |
| 50   | NI        | Calculated    |           |         |                |       |        | GP - OPEX      | Yes     |

---

### Definition 4 (Optional): CF (Cash Flow — cross-definition formula demo)

- Definition Code: `CASH_FLOW`
- Prefix: `CF`
- Report Type: Cash Flow
- GI Name: TrialBalance
- Rounding: Units / 0 decimals

Line Items:

| Sort | Line Code | Type       | Acct From | Acct To | Balance Type | Sign | Parent | Formula       | Visible |
| ---- | --------- | ---------- | --------- | ------- | ------------ | ---- | ------ | ------------- | ------- |
| 10   | RESULT    | Calculated |           |         |              |      |        | BS_TA - PL_NI | Yes     |

This line references BS and PL definitions by their prefix. The engine resolves it automatically.

---

### How to Link Multiple Definitions to One Report (FR101000)

1. Go to FR101000 (Financial Report)
2. Create a new report, fill in Year/Month/Branch/Ledger
3. Go to the **Report Definitions** tab at the bottom
4. Add rows:

| Row | Definition           | Prefix (auto-filled) |
| --- | -------------------- | -------------------- |
| 1   | BALANCE_SHEET        | BS                   |
| 2   | PROFIT_LOSS          | PL                   |
| 3   | CASH_FLOW (optional) | CF                   |

5. Attach the Word template containing `{{BS_...}}`, `{{PL_...}}`, and `{{CF_...}}` placeholders
6. Click Generate Report

All three definitions run in one pass. The engine resolves cross-definition references automatically via topological sort.

---

### How to Link Multiple Definitions to One Presentation (FR101003)

Same concept, different screen:

1. Go to FR101003 (Financial Presentation)
2. Create a new record, fill in Year/Month/Branch/Ledger
3. Go to the **Report Definitions** tab
4. Add the same definitions (BS, PL, CF)
5. Optionally add GI Data Sources on the second tab
6. Click Preview Markdown, then Generate Presentation
