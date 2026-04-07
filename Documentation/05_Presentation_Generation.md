# Step 5: Presentation Generation (FR101003)

**Screen:** AFS > AFS Financial Presentation
**Screen ID:** FR101003
**Purpose:** Generate AI-powered PowerPoint presentations from financial data using the Gamma API.

---

## Prerequisites

- Tenant Credentials with Gamma API Key configured (FR101001)
- At least one of:
  - Report Definition with line items (FR101002) — for GL data
  - GI Data Source with columns (FR101004) — for any GI data
- All visible line items must have Descriptions filled in

---

## Step-by-Step

### 1. Navigate to FR101003

Go to **AFS > AFS Financial Presentation** or search `FR101003`.

### 2. Create a New Record

Click **+** in the toolbar.

### 3. Fill in the Header

| Field | What to Enter | Example | Required? |
|---|---|---|---|
| Presentation Name | Name for this config | `Q1 2026 PO Analysis` | Yes |
| Description | Optional description | `Quarterly purchase order review` | No |
| Current Year | Year to report on | `2026` | Yes |
| Financial Month | Month to report on | `March` | Yes |
| Organization | Filter (optional) | `PRODWHOLE` | No |
| Branch | Filter (optional) | `PRODWHOLE` | No |
| Ledger | Filter (optional) | `ACTUAL` | No |
| Presentation Title | Title on the presentation | `Purchase Order Analysis — Q1 2026` | **Yes (required)** |
| Presentation Description | Custom AI instructions | (see Step 5 below) | No |
| Presentation Template ID | Gamma template ID | `abc123def456` | No |

### 4. Link Data Sources

**Option A: GI Data Sources only**

1. Click the **GI DATA SOURCES** tab at the bottom
2. Click **+** to add a row
3. Select your data source (e.g., `PO_ORDERS`)
4. Prefix auto-fills (e.g., `PO`)
5. Add more data sources if needed

**Option B: Report Definitions only**

1. Click the **REPORT DEFINITIONS** tab
2. Click **+** to add a row
3. Select a definition (e.g., `BALANCE_SHEET`)
4. Add more definitions if needed

**Option C: Both together**

Link definitions on the REPORT DEFINITIONS tab AND data sources on the GI DATA SOURCES tab.
All placeholders from all sources are combined into one markdown.

### 5. Write the AI Prompt (Presentation Description)

This is the most important field for presentation quality. It tells the AI what kind of
presentation to create.

**If left blank:** A default CFO-level financial analysis prompt is used.

**If filled in:** Your text completely replaces the default. Write it as instructions to the AI.

**Example for Purchase Order analysis:**

```
You are a procurement analyst preparing a quarterly vendor spend review
for senior management.

Analyze the purchase order data below and create a professional presentation:

1. Executive Summary — total spend, order volume, key highlights
2. Top Vendors — who are the biggest suppliers, concentration risk
3. Order Status Breakdown — closed vs on-hold analysis
4. Average Order Analysis — trends in order size
5. Vendor Diversification — recommendations for reducing dependency
6. Procurement Efficiency — open quantity analysis
7. Key Takeaways and Recommendations

Use bar charts for vendor comparisons.
Use pie charts for status breakdowns.
Keep the tone professional but accessible.
Calculate percentage breakdowns from the raw numbers provided.
```

**Example for Financial Report:**

```
You are a CFO presenting quarterly results to the board.

Create a 12-slide presentation covering:
1. Executive Summary
2. Revenue and Profitability
3. Balance Sheet Highlights
4. Cash Position
5. Expense Analysis
6. Year-over-Year Comparison
7. Month-over-Month Trends
8. Key Risks
9. Strategic Recommendations

Highlight any figures that changed more than 10% from prior year.
Use waterfall charts for movement analysis.
```

**Tips for good prompts:**
- Be specific about the number of slides and topics
- Tell the AI what charts to use
- Specify the audience and tone
- Ask it to calculate percentages from the raw data
- Mention what to highlight (big changes, risks, etc.)

### 6. Save

Click **Save**.

---

## Step-by-Step: Generate the Presentation

### 7. Preview Markdown (Recommended)

Click **Preview Markdown** in the toolbar.

This:
1. Authenticates against the Acumatica API
2. Fetches data from all linked definitions and GI data sources
3. Runs calculations
4. Builds a structured markdown document
5. Saves it to the **PRESENTATION MARKDOWN** tab and as a .txt file attachment

**Review the markdown before generating.** Click the **PRESENTATION MARKDOWN** tab to see it.

The markdown looks like this:

```markdown
# Purchase Order Analysis — Q1 2026

**Organization:** PRODWHOLE | **Branch:** PRODWHOLE | **Ledger:** ACTUAL
**Reporting Period:** March 2026

---

You are a procurement analyst preparing a quarterly vendor spend review...

---

## Financial Data

## Additional Data (Generic Inquiries)

### Purchase Order Analysis

- **Total Purchase Order Value:** 4,523,100
- **Total Quantity Ordered:** 28,450
- **Number of Purchase Orders:** 2,850
- **Open (Unfulfilled) Quantity:** 1,230
- **Average Purchase Order Value:** 1,587
- **Total Value of Closed Orders:** 4,100,000
- **Total Value of On-Hold Orders:** 423,100
- **Top 10 Vendors by Spend:**
  - Row 1: VendorName: Westerly Good Foods, OrderTotal: 614,101
  - Row 2: VendorName: Good Hardware Pte., OrderTotal: 456,256
  - Row 3: VendorName: Periphery Distribution, OrderTotal: 245,462
  ...
```

**You can edit the markdown directly** in the PRESENTATION MARKDOWN tab before generating.
This lets you fix labels, add context, or remove sections.

### 8. Generate the Presentation

Click **Generate Presentation** in the toolbar.

This:
1. Takes the markdown (from Preview, or generates fresh if you skipped it)
2. Sends it to the Gamma API
3. Polls every 5 seconds until Gamma finishes (up to 5 minutes)
4. Downloads the PPTX file
5. Saves it as a file attachment

Status changes: `Not Generated` → `In Progress` → `Ready to Download`

### 9. Download

Click **Download Presentation** to get the PPTX file.

---

## Using a Gamma Template (Optional)

If you have an existing Gamma presentation with a design you like:

1. Open it on gamma.app — the URL looks like `https://gamma.app/docs/XXXXXXXX`
2. Copy the ID part (`XXXXXXXX`)
3. Paste it into the **Presentation Template ID** field
4. When you generate, Gamma uses that template's design instead of creating one from scratch

Leave blank to let Gamma design from scratch.

---

## Example: Complete PO-PurchaseOrder Presentation

**Prerequisites done:**
- FR101001: Credentials + Gamma API key configured
- FR101004: `PO_ORDERS` data source with columns (TOTAL_AMOUNT, ORDER_COUNT, TOP_VENDORS, etc.)

**FR101003 setup:**

| Field | Value |
|---|---|
| Presentation Name | `March 2026 PO Review` |
| Current Year | `2026` |
| Financial Month | `March` |
| Branch | `PRODWHOLE` |
| Presentation Title | `Purchase Order Analysis — March 2026` |
| Presentation Description | (procurement analyst prompt from Step 5) |

**GI DATA SOURCES tab:**

| Data Source | Prefix | Display Order |
|---|---|---|
| PO_ORDERS | PO | 0 |

**Workflow:**
1. Save
2. Click Preview Markdown → review on PRESENTATION MARKDOWN tab
3. Click Generate Presentation → wait for completion
4. Click Download Presentation → open in PowerPoint

---

## Example: Mixed GL + GI Presentation

**FR101003 setup:**

| Field | Value |
|---|---|
| Presentation Name | `Q1 2026 Full Financial Review` |
| Presentation Title | `Quarterly Financial Review — Q1 2026` |
| Presentation Description | `Create a comprehensive financial review covering balance sheet, P&L, and procurement analysis...` |

**REPORT DEFINITIONS tab:**

| Definition | Prefix |
|---|---|
| BALANCE_SHEET | BS |
| PROFIT_LOSS | PL |

**GI DATA SOURCES tab:**

| Data Source | Prefix |
|---|---|
| PO_ORDERS | PO |

The markdown will contain GL financial data (BS/PL) AND purchase order data (PO) together.
The AI creates one unified presentation from all sources.

---

## Status Lifecycle

```
Not Generated  ──→  In Progress  ──→  Ready to Download
                                  ──→  Failed
```

Use **Reset Status** to return to "Not Generated" from any state.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| "Presentation API Key is not configured" | FR101001 → fill in Gamma API Key |
| "No Report Definitions or GI Data Sources are linked" | Add at least one on the tabs |
| "Visible line items are missing descriptions" | FR101002 → fill Description for all visible lines |
| "Please enter a Presentation Title" | Fill in the Presentation Title field |
| Status stuck on "In Progress" | Click Reset Status, check trace log |
| Presentation content is wrong | Edit markdown on PRESENTATION MARKDOWN tab, regenerate |
| Gamma timeout | Internet issue or Gamma overloaded — try again |
| `FormatException` during Preview Markdown | GI column name mismatch — see FR101004 Detect Columns |
