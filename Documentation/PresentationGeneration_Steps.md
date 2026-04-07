# AFS Financial Presentation Generation — Step-by-Step Guide

This guide walks through generating an AI-powered PowerPoint presentation
using GI Data Sources (any Generic Inquiry) and optionally Report Definitions.

---

## Overview: What Are GI Data Sources?

GI Data Sources let you pull data from **any** Acumatica Generic Inquiry — not just
Trial Balance. Examples:

- Top 10 vendors by purchase amount
- Sales by region
- Headcount by department
- Budget vs Actual comparison
- Any custom GI you've built

Each GI Data Source produces placeholders like `{{PREFIX_ALIAS}}` that feed into
the presentation markdown.

---

## Prerequisites

| What | Where | Status |
|---|---|---|
| Tenant Credentials configured | FR101001 | Must exist |
| Gamma API Key entered | FR101001 (Presentation API Key column) | Required |
| At least one Generic Inquiry exists in Acumatica | GI screen | Must return data |

---

## Step 1: Get and Configure the Gamma API Key

1. Go to [gamma.app](https://gamma.app), sign up or log in
2. Go to account settings, generate an API key
3. Navigate to **AFS > Tenant Credentials** (FR101001)
4. Paste the key into the **Presentation API Key** column
5. Click **Save**

---

## Step 2: Create a GI Data Source (FR101004)

Navigate to **AFS > GI-Config-Screen** (FR101004).

### 2a: Fill in the Header

| Field | What to enter | Example |
|---|---|---|
| Data Source Code | Unique ID (locked after save) | `TOP_VENDORS` |
| Prefix | Short code for placeholders (locked after save) | `TV` |
| Description | What this data source does | `Top 10 Vendors by Purchase Amount` |
| Active | Leave checked | Yes |

### 2b: Select the Generic Inquiry

| Field | What to enter | Notes |
|---|---|---|
| GI Name | Pick from the dropdown | e.g., `PurchaseOrderSummary` |
| Key Column | The column that identifies each row | e.g., `VendorID` or `VendorName` |

### 2c: Configure Filter Columns (Optional)

These map your GI's columns to the standard filters (period, branch, org, ledger).
Only configure the ones your GI supports.

**Period Filter:**

| Field | What to enter | Example |
|---|---|---|
| Period Filter Column | GI column name for period | `OrderDate` or `FinancialPeriod` |
| Period Type | Data type of that column | `Date` or `String` |
| Period Scope | How to filter | `Monthly` (date range) or `Exact` (string match) |
| Period Template | How to build the filter value | `{YEAR}-{MONTH}-01` for dates, `{MONTH}{YEAR}` for strings |

**Other Filters (if your GI has these columns):**

| Field | What to enter |
|---|---|
| Branch Filter Column | GI column for branch (e.g., `BranchID`) |
| Branch Type | Usually `String` |
| Org Filter Column | GI column for organization |
| Org Type | Usually `String` |
| Ledger Filter Column | GI column for ledger |
| Ledger Type | Usually `String` |

Leave blank any filter your GI doesn't support.

### 2d: Click Detect Columns

Click the **Detect Columns** button in the toolbar. This:
1. Connects to your GI via OData
2. Fetches one row
3. Discovers all available column names
4. Stores them so the column dropdowns work

You'll see a dialog listing all detected columns. These are the OData property names
you'll use in the next step.

### 2e: Save the Header

Click **Save** before adding columns.

---

## Step 3: Add Column Definitions (FR101004 grid)

In the **Columns** grid at the bottom of FR101004, add rows for each value you want
to extract from the GI.

### Column Line Types

| Line Type | When to use |
|---|---|
| Value (from GI) | Read one column, aggregate matching rows into a single number |
| Multi-Row Expand | Expand top N rows into individual placeholders (e.g., top 10 list) |
| Calculated | Formula referencing other column aliases in this data source |
| Heading | Label only, no value |

### Example: Single Value Columns

For a GI that has vendor purchase totals:

| Sort | Column Alias | Line Type | GI Column | Column Type | Aggregate | Key From | Key To |
|---|---|---|---|---|---|---|---|
| 10 | TOTAL_PURCHASES | Value | OrderTotal | Decimal | Sum | | |
| 20 | VENDOR_COUNT | Value | VendorID | String | Count | | |
| 30 | AVG_ORDER | Calculated | | | | | |

For line 30 (AVG_ORDER), set Formula = `TOTAL_PURCHASES / VENDOR_COUNT`

The placeholders produced:
- `{{TV_TOTAL_PURCHASES}}` — sum of all OrderTotal values
- `{{TV_VENDOR_COUNT}}` — count of rows
- `{{TV_AVG_ORDER}}` — calculated average

### Example: Multi-Row Expand (Top 10 List)

| Sort | Column Alias | Line Type | GI Column | Order By Column | Sort Direction | Row Limit | Display Columns |
|---|---|---|---|---|---|---|---|
| 40 | TOP_VENDORS | Multi-Row | | OrderTotal | Descending | 10 | VendorName,OrderTotal |

This produces placeholders for each row:
- `{{TV_TOP_VENDORS_1_VendorName}}` — #1 vendor name
- `{{TV_TOP_VENDORS_1_OrderTotal}}` — #1 vendor amount
- `{{TV_TOP_VENDORS_2_VendorName}}` — #2 vendor name
- `{{TV_TOP_VENDORS_2_OrderTotal}}` — #2 vendor amount
- ... up to `{{TV_TOP_VENDORS_10_VendorName}}`

### Key From / Key To (Optional Row Filtering)

If you only want rows where the Key Column falls in a range:

| Field | Example | What it does |
|---|---|---|
| Key From | `A` | Only include rows where VendorID >= "A" |
| Key To | `M` | Only include rows where VendorID <= "M" |

Leave both blank to include all rows.

### Row Filter (Optional OData Filter)

For additional filtering beyond the key range:

| Example | What it does |
|---|---|
| `Status eq 'Open'` | Only open orders |
| `Department eq 'Finance'` | Only finance department |
| `Status eq 'Active' and Country eq 'MY'` | Multiple conditions with "and" |

### Format String (Optional)

| Format | Result | Use for |
|---|---|---|
| `N0` | 1,234,567 | Whole numbers |
| `N2` | 1,234,567.89 | 2 decimal places |
| `P1` | 45.3% | Percentages |
| `dd MMM yyyy` | 15 Mar 2025 | Dates |

### Visible Checkbox

- Checked = placeholder is included in output
- Unchecked = value is still calculated (for use in formulas) but not output

---

## Step 4: Test Your GI Data Source

Before using it in a presentation, verify it works:

1. Click the **Test Fetch** button in the toolbar
2. A dialog appears asking for Year, Month, Branch, Organization, Ledger
3. Fill in the values and click **Fetch**
4. Results appear in a dialog showing each placeholder and its value
5. Full results are also written to the Acumatica trace log

If you get no results:
- Check that Detect Columns was run
- Verify the GI name is correct
- Check your filter column configuration
- Try Test Fetch with no filters first, then add them one by one

---

## Step 5: Create the Presentation Record (FR101003)

Navigate to **AFS > AFS Financial Presentation** (FR101003).

### 5a: Fill in the Header

| Field | What to enter | Example |
|---|---|---|
| Presentation Name | Name for this presentation | `Q1 2025 Vendor Analysis` |
| Description | Optional description | `Quarterly vendor spend review` |
| Current Year | Year to report on | `2025` |
| Financial Month | Month to report on | `March` |
| Organization | Filter (optional) | Your org |
| Branch | Filter (optional) | Your branch |
| Ledger | Filter (optional) | `ACTUAL` |
| Presentation Title | **Required** — title shown on the presentation | `Q1 2025 Vendor Spend Analysis` |
| Presentation Description | Custom AI instructions (optional — see Step 6) | |
| Presentation Template ID | Gamma template ID (optional — see Step 7) | |

### 5b: Link GI Data Sources (GI DATA SOURCES tab)

1. Click the **GI DATA SOURCES** tab at the bottom
2. Click the **+** button to add a row
3. In the **Data Source** column, select your GI Data Source (e.g., `TOP_VENDORS`)
4. The **Prefix** column auto-fills (e.g., `TV`)
5. Set **Display Order** if you have multiple data sources (controls order in markdown)
6. Add more rows for additional data sources if needed

### 5c: Optionally Link Report Definitions Too (REPORT DEFINITIONS tab)

You can mix GI Data Sources with Report Definitions in one presentation:

1. Click the **REPORT DEFINITIONS** tab
2. Add your BS, PL, or other definitions
3. These contribute `{{BS_CASH_CY}}` style placeholders alongside the GI placeholders

This is optional. A presentation can use only GI Data Sources, only Report Definitions,
or both together.

### 5d: Save

Click **Save**.

---

## Step 6: Customize the AI Prompt (Optional)

The **Presentation Description** field controls what the AI does with your data.

**If left blank:** The system uses a default CFO-level prompt that produces:
1. Executive Summary
2. Financial Position Overview
3. Asset Analysis
4. Income Performance
5. Expense Analysis
6. Equity and Capital Structure
7. Liability Analysis
8. Month-over-Month Key Movements
9. Financial Health Assessment
10. Risks and Observations
11. Strategic Recommendations
12. Key Takeaways

**If you fill it in:** Your text completely replaces the default prompt. Examples:

For a vendor analysis presentation:
```
You are a procurement analyst preparing a vendor spend review for management.

Analyze the vendor data below and create a presentation covering:
1. Total spend overview
2. Top vendors by purchase amount
3. Vendor concentration risk
4. Month-over-month spend trends
5. Recommendations for vendor consolidation

Use charts where appropriate. Keep the tone professional but accessible.
```

For a simple summary:
```
Create a 5-slide executive summary of the financial data.
Focus on the biggest changes from prior year.
Use bar charts for comparisons.
```

---

## Step 7: Use a Gamma Template (Optional)

If you have an existing Gamma presentation with a design you like:

1. Open the presentation on gamma.app
2. Look at the URL: `https://gamma.app/docs/XXXXXXXX`
3. Copy the ID part (the `XXXXXXXX`)
4. Paste it into the **Presentation Template ID** field on FR101003

When set, Gamma uses that template's design/layout instead of generating one from scratch.
When blank, Gamma creates the design from scratch based on the markdown content.

---

## Step 8: Preview the Markdown

Before generating the actual presentation, preview what will be sent to the AI:

1. Click **Preview Markdown** in the toolbar
2. Wait for the long operation to complete (fetches data, runs calculations, builds markdown)
3. When done, go to the **PRESENTATION MARKDOWN** tab at the bottom
4. Review the generated markdown text

The markdown contains:
- Report header (org, branch, period labels)
- Your custom prompt (or the default CFO prompt)
- Financial data from Report Definitions (if linked): CY, PM, PY values per line
- GI Data Source values: single values and multi-row tables

You can **edit the markdown directly** in this tab before generating. This lets you:
- Fix any labels
- Add extra context
- Remove sections you don't want
- Tweak the AI instructions

The markdown is also saved as a `.txt` file attachment (check the Files panel / paperclip icon).

---

## Step 9: Generate the Presentation

1. Click **Generate Presentation** in the toolbar
2. The system:
   - Takes the markdown (from Step 8, or generates it fresh if you skipped Preview)
   - Sends it to the Gamma API
   - Polls every 5 seconds until Gamma finishes (up to 5 minutes)
   - Downloads the PPTX file
   - Saves it as a file attachment
3. Status changes to **Ready to Download**

---

## Step 10: Download

1. Click **Download Presentation** in the toolbar
2. The PPTX file downloads to your browser
3. Open it in PowerPoint

---

## Troubleshooting

| Problem | Solution |
|---|---|
| "Presentation API Key is not configured" | Go to FR101001, fill in the Gamma API Key column |
| "No Report Definitions or GI Data Sources are linked" | Add at least one on the tabs in FR101003 |
| "Visible line items are missing descriptions" | Go to FR101002, fill in Description for all visible lines |
| "Please enter a Presentation Title" | Fill in the Presentation Title field on FR101003 header |
| Status stuck on "In Progress" | Click **Reset Status**, check trace log for errors |
| GI Data Source returns no data | Go to FR101004, run **Test Fetch** to debug filters |
| Presentation looks wrong | Edit the markdown in the PRESENTATION MARKDOWN tab, then regenerate |
| Gamma API timeout | Check your internet connection; Gamma may be under heavy load, try again |

---

## Quick Reference: Presentation Workflow

```
FR101001                FR101004                 FR101002
Tenant Credentials  →   GI Data Source(s)    →   Report Definition(s)
(API keys)              (any GI)                 (Trial Balance)
       │                      │                        │
       │                      │                        │
       └──────────────────────┼────────────────────────┘
                              │
                              ▼
                         FR101003
                    Financial Presentation
                              │
                    ┌─────────┼─────────┐
                    │         │         │
              Preview    Edit       Generate
              Markdown   Markdown   Presentation
                              │
                              ▼
                         Download
                          PPTX
```

---

## Example: Complete GI Data Source Presentation

**Scenario:** Monthly vendor spend analysis using a "PurchaseOrderSummary" GI.

**Step 1:** FR101001 — Gamma API key is configured.

**Step 2:** FR101004 — Create data source:
- Code: `VENDOR_SPEND`, Prefix: `VS`
- GI Name: `PurchaseOrderSummary`
- Key Column: `VendorName`
- Period Filter: `OrderDate`, Type: `Date`, Scope: `Monthly`, Template: `{YEAR}-{MONTH}-01`

**Step 3:** FR101004 — Add columns:

| Sort | Alias | Type | GI Column | Aggregate | Format |
|---|---|---|---|---|---|
| 10 | TOTAL | Value | OrderTotal | Sum | N0 |
| 20 | COUNT | Value | OrderNbr | Count | N0 |
| 30 | AVG | Calculated | | | N0 |
| 40 | TOP10 | Multi-Row | | | |

- Line 30 Formula: `TOTAL / COUNT`
- Line 40: Order By = `OrderTotal`, Direction = Descending, Row Limit = 10

**Step 4:** FR101004 — Click Test Fetch, verify results.

**Step 5:** FR101003 — Create presentation:
- Name: `March 2025 Vendor Spend`
- Year: 2025, Month: March
- Title: `Monthly Vendor Spend Analysis — March 2025`
- Description: `Analyze vendor spending patterns. Highlight top vendors, concentration risk, and month-over-month trends.`
- GI DATA SOURCES tab: link `VENDOR_SPEND`

**Step 6:** Click Preview Markdown, review.

**Step 7:** Click Generate Presentation.

**Step 8:** Click Download Presentation. Open in PowerPoint.

Placeholders in the markdown:
- `{{VS_TOTAL}}` — total purchase amount
- `{{VS_COUNT}}` — number of orders
- `{{VS_AVG}}` — average order value
- `{{VS_TOP10_1_VendorName}}` — #1 vendor name
- `{{VS_TOP10_1_OrderTotal}}` — #1 vendor amount
- ... through `{{VS_TOP10_10_VendorName}}`
