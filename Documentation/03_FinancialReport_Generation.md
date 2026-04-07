# Step 3: Financial Report Generation (FR101000)

**Screen:** AFS > Financial Report
**Screen ID:** FR101000
**Purpose:** Generate Word documents (.docx) by replacing `{{PLACEHOLDER}}` tokens with calculated financial values.

---

## Prerequisites

- Tenant Credentials configured (FR101001)
- At least one Report Definition with line items (FR101002)
- A Word template (.docx) with placeholders

---

## Step-by-Step

### 1. Navigate to FR101000

Go to **AFS > Financial Report** or search `FR101000`.

### 2. Create a New Record

Click **+** in the toolbar.

### 3. Fill in the Header

| Field | What to Enter | Example |
|---|---|---|
| Template Name | Name for this report | `BS Annual Report 2025` |
| Description | Optional description | `Balance Sheet for December 2025` |
| Current Year | Year to report on (dropdown) | `2025` |
| Financial Month | Month to report on | `December` |
| Organization | Filter to specific org (optional) | `PRODWHOLE` |
| Branch | Filter to specific branch (optional) | `PRODWHOLE` |
| Ledger | Filter to specific ledger (optional) | `ACTUAL` |

### 4. Attach the Word Template

1. Click the **paperclip icon** (Files panel) in the top-right area of the form
2. Click **Upload File**
3. Select your Word template
4. **Important:** The filename MUST contain `FRTemplate` (e.g., `BS_FRTemplate_2025.docx`)
5. Close the Files panel

### 5. Link Report Definitions

1. Click the **REPORT DEFINITIONS** tab at the bottom
2. Click **+** to add a row
3. Select a definition (e.g., `BALANCE_SHEET`)
4. The Prefix column auto-fills (e.g., `BS`)
5. Add more definitions if your template uses multiple prefixes

**Example — Single Definition:**

| Definition | Prefix | Display Order |
|---|---|---|
| BALANCE_SHEET | BS | 0 |

**Example — Multi-Definition:**

| Definition | Prefix | Display Order |
|---|---|---|
| BALANCE_SHEET | BS | 1 |
| PROFIT_LOSS | PL | 2 |
| CASH_FLOW | CF | 3 |

### 6. Save

Click **Save**.

### 7. Generate the Report

Click **Generate Report** in the toolbar.

What happens behind the scenes:
1. System authenticates against the Acumatica OData API
2. Fetches GL data for multiple periods in parallel (CY, PY, PM, etc.)
3. Runs the calculation engine across all linked definitions
4. Extracts `{{...}}` placeholders from the Word template
5. Replaces each placeholder with its calculated value
6. Saves the generated document as a file attachment

Status changes: `File not Generated` → `In Progress` → `Ready to Download`

### 8. Download the Report

When status shows **Ready to Download**, click **Download Report**.

---

## Status Lifecycle

```
File not Generated  ──→  In Progress  ──→  Ready to Download
                                       ──→  Failed
```

If stuck on "In Progress" or "Failed", click **Reset Status** to return to "File not Generated".

---

## Example: Complete Workflow

**Goal:** Generate a Balance Sheet for December 2025.

1. **FR101001:** Tenant credentials already configured
2. **FR101002:** `BALANCE_SHEET` definition (prefix `BS`) with line items for Cash, Receivables, etc.
3. **Word Template:** `AnnualBS_FRTemplate.docx` containing:

```
                    BALANCE SHEET
                As at 31 December {{CY}}

                                    {{CY}}          {{PY}}
ASSETS
  Cash                          {{BS_CASH_CY}}    {{BS_CASH_PY}}
  Receivables                   {{BS_RECV_CY}}    {{BS_RECV_PY}}
  Total Current Assets          {{BS_CA_CY}}      {{BS_CA_PY}}
  Fixed Assets                  {{BS_FA_CY}}      {{BS_FA_PY}}
  TOTAL ASSETS                  {{BS_TA_CY}}      {{BS_TA_PY}}

LIABILITIES
  Payables                      {{BS_PAYABLE_CY}} {{BS_PAYABLE_PY}}
  TOTAL LIABILITIES             {{BS_TL_CY}}      {{BS_TL_PY}}

EQUITY                          {{BS_EQUITY_CY}}  {{BS_EQUITY_PY}}
```

4. **FR101000:** Create report, set Year=2025, Month=December, attach template, link BS definition
5. Click **Generate Report**
6. Download — all `{{...}}` tokens are replaced with real numbers

---

## Word Template Tips

- Type each placeholder in one go (don't pause mid-typing)
- Apply formatting to the entire placeholder at once
- If a placeholder isn't replaced, delete it and retype it
- Use Paste as Plain Text (Ctrl+Shift+V) when copying placeholders
- Placeholders work in document body, headers, and footers
- Maximum 1,000 placeholders per template
- Unmatched placeholders resolve to `0`

---

## Performance Notes

- Data is fetched in parallel (CY, PY, PM, January balances, cumulative ranges)
- Optional fetches are skipped when not needed
- 15-minute timeout protects against runaway processes
- Temporary files are cleaned up automatically
