# Step 2: Report Definition Setup (FR101002)

**Screen:** AFS > Report Definition
**Screen ID:** FR101002
**Purpose:** Define the structure of a financial statement — which GL accounts map to which report lines, how to calculate subtotals, and how to format values.

---

## When Do You Need This?

- When you want to generate Word document reports from GL Trial Balance data
- When you want GL-based financial data in presentations
- You do NOT need this if you only use GI Data Sources (FR101004)

---

## Step-by-Step: Create a Report Definition

### 1. Navigate to FR101002

Go to **AFS > Report Definition** or search `FR101002`.

### 2. Fill in the Header

| Field | What to Enter | Example | Notes |
|---|---|---|---|
| Definition Code | Unique ID | `BALANCE_SHEET` | Locked after first save |
| Prefix | Short code (2-10 chars, letters/digits only) | `BS` | Locked after first save. Becomes part of every placeholder. |
| Report Type | Pick one | `Balance Sheet` | Classification label only |
| Description | What this definition is for | `Balance Sheet — Main Company` | |
| Active | Leave checked | ✓ | Makes it available for selection |

### 3. Configure the Data Source

These fields tell the system which GI to query and which columns to read.

| Field | Default | When to Change |
|---|---|---|
| Generic Inquiry Name | `TrialBalance` | Only if you use a custom Trial Balance GI |
| Account Column | `Account` | Only if your GI names it differently |
| Account Type Column | `Type` | Only if your GI names it differently |
| Beginning Balance Column | `BeginningBalance` | Only if your GI names it differently |
| Ending Balance Column | `EndingBalance` | Only if your GI names it differently |
| Debit Column | `Debit` | Only if your GI names it differently |
| Credit Column | `Credit` | Only if your GI names it differently |

**Tip:** Click **Detect Columns** to auto-detect and fill these from your GI.

### 4. Set Formatting

| Field | Options | Example |
|---|---|---|
| Rounding Level | Units / Thousands / Millions | `Units` (no rounding) |
| Decimal Places | 0 / 1 / 2 | `0` |

### 5. Save the Header

Click **Save** before adding line items.

---

## Step-by-Step: Add Line Items

The grid at the bottom is where you define each line of the financial report.

### Line Types Explained

| Type | What It Does | Required Fields |
|---|---|---|
| Account Range | Sums GL accounts in a range | Account From, Account To, Balance Type, Sign Rule |
| Subtotal | Sums all lines whose Parent Line = this Line Code | (none — children reference this line) |
| Calculated | Evaluates a formula | Formula |
| Heading | Label only, no value | (none) |

### Balance Types Explained

| Balance Type | What It Returns | When to Use |
|---|---|---|
| Ending Balance | Balance at end of selected period | Balance Sheet items (most common) |
| Beginning Balance | Fiscal year opening balance | Opening balances, equity movements |
| Debit (YTD) | Cumulative debits for the full fiscal year | P&L analysis, cash flow |
| Credit (YTD) | Cumulative credits for the full fiscal year | P&L analysis, cash flow |
| Movement (YTD) | Net movement (Debit - Credit) for fiscal year | P&L items |
| Period Debit | Debits for the selected month only | Monthly reports |
| Period Credit | Credits for the selected month only | Monthly reports |
| Period Movement | Net movement for the selected month only | Monthly reports |

### Sign Rule Explained

| Rule | What It Does | Use For |
|---|---|---|
| As-Is | Keeps the GL value unchanged | Assets, Expenses |
| Flip Sign | Multiplies by -1 | Liabilities, Income, Equity (GL stores these as negative) |

---

## Example: Balance Sheet Definition

**Header:** Code = `BALANCE_SHEET`, Prefix = `BS`, Type = Balance Sheet

**Line Items:**

| Sort | Line Code | Description | Type | Acct From | Acct To | Balance Type | Sign Rule | Parent | Formula |
|---|---|---|---|---|---|---|---|---|---|
| 10 | CASH | Cash and Equivalents | Account Range | 10100 | 10199 | Ending Balance | As-Is | CA | |
| 20 | RECV | Trade Receivables | Account Range | 11100 | 11199 | Ending Balance | As-Is | CA | |
| 30 | INVENTORY | Inventory | Account Range | 12100 | 12199 | Ending Balance | As-Is | CA | |
| 40 | CA | Total Current Assets | Subtotal | | | | | TA | |
| 50 | FA | Fixed Assets (Net) | Account Range | 15100 | 15999 | Ending Balance | As-Is | TA | |
| 60 | TA | TOTAL ASSETS | Subtotal | | | | | | |
| 70 | PAYABLE | Trade Payables | Account Range | 20100 | 20199 | Ending Balance | Flip Sign | CL | |
| 80 | CL | Total Current Liabilities | Subtotal | | | | | TL | |
| 90 | LTD | Long-Term Debt | Account Range | 25100 | 25999 | Ending Balance | Flip Sign | TL | |
| 100 | TL | TOTAL LIABILITIES | Subtotal | | | | | | |
| 110 | EQUITY | Total Equity | Account Range | 30100 | 30999 | Ending Balance | Flip Sign | | |
| 120 | NA | Net Assets (TA - TL) | Calculated | | | | | | TA - TL |
| 130 | CHECK | Balance Check (should be 0) | Calculated | | | | | | TA - TL - EQUITY |

**How the subtotal chain works:**
```
CASH (parent=CA) ──┐
RECV (parent=CA) ──┤── CA = CASH + RECV + INVENTORY
INVENTORY (parent=CA)─┘
                       CA (parent=TA) ──┐
                       FA (parent=TA) ──┤── TA = CA + FA
                                        └
```

**Placeholders produced:**
- `{{BS_CASH_CY}}`, `{{BS_CASH_PY}}`, `{{BS_CASH_PM}}`
- `{{BS_TA_CY}}`, `{{BS_TA_PY}}`, `{{BS_TA_PM}}`
- `{{BS_NA_CY}}`, `{{BS_NA_PY}}`, `{{BS_NA_PM}}`
- etc.

---

## Example: Profit & Loss Definition

**Header:** Code = `PROFIT_LOSS`, Prefix = `PL`, Type = Profit & Loss

| Sort | Line Code | Description | Type | Acct From | Acct To | Balance Type | Sign Rule | Parent | Formula |
|---|---|---|---|---|---|---|---|---|---|
| 10 | REVENUE | Revenue | Account Range | 40100 | 40999 | Ending Balance | Flip Sign | | |
| 20 | COGS | Cost of Goods Sold | Account Range | 50100 | 50999 | Ending Balance | As-Is | | |
| 30 | GP | Gross Profit | Calculated | | | | | | REVENUE - COGS |
| 40 | OPEX | Operating Expenses | Account Range | 60100 | 69999 | Ending Balance | As-Is | | |
| 50 | EBIT | Earnings Before Interest & Tax | Calculated | | | | | | GP - OPEX |
| 60 | FINANCE | Finance Costs | Account Range | 70100 | 70999 | Ending Balance | As-Is | | |
| 70 | NI | Net Income | Calculated | | | | | | EBIT - FINANCE |

**Placeholders produced:**
- `{{PL_REVENUE_CY}}`, `{{PL_GP_CY}}`, `{{PL_NI_CY}}`
- `{{PL_REVENUE_PY}}`, `{{PL_GP_PY}}`, `{{PL_NI_PY}}`
- etc.

---

## Example: Cross-Definition Formula (Cash Flow)

**Header:** Code = `CASH_FLOW`, Prefix = `CF`, Type = Cash Flow

| Sort | Line Code | Description | Type | Formula |
|---|---|---|---|---|
| 10 | OP_CASH | Operating Cash Flow | Calculated | PL_NI + BS_RECV - BS_INVENTORY |
| 20 | INVEST | Investing Activities | Calculated | BS_FA |
| 30 | NET_CASH | Net Cash Movement | Calculated | OP_CASH - INVEST |

This definition references `PL_NI`, `BS_RECV`, `BS_INVENTORY`, and `BS_FA` from the other two definitions using their prefixes. The engine resolves these automatically.

**Important:** All three definitions (BS, PL, CF) must be linked to the same report in FR101000 or FR101003 for cross-definition formulas to work.

---

## Dimension Filters (Optional Advanced Feature)

Each Account Range line can optionally filter by dimension:

| Filter | What It Does | Example |
|---|---|---|
| Subaccount Filter | Only include rows matching this subaccount | `000-000` |
| Branch Filter | Only include rows from this branch | `HEADOFFICE` |
| Organization Filter | Only include rows from this organization | `PRODWHOLE` |
| Ledger Filter | Only include rows from this ledger | `ACTUAL` |

Leave blank to include all. These filters are per-line, so different lines can pull from different branches/subaccounts.

---

## Actions

| Button | What It Does |
|---|---|
| Detect Columns | Connects to the GI, discovers column names, auto-maps them |
| Copy Definition | Creates a complete copy (header + all line items) with "_COPY" suffix |

---

## Common Mistakes

| Mistake | What Happens | Fix |
|---|---|---|
| Forgot to set Prefix | Error on save | Enter a 2-10 char alphanumeric prefix |
| Duplicate Prefix | Error on save | Each definition must have a unique prefix |
| Subtotal with no children | Value = 0 | Set Parent Line on child lines to match the subtotal's Line Code |
| Formula references unknown Line Code | Error during generation | Check spelling, ensure referenced line has a lower Sort Order |
| Circular formula (A references B, B references A) | Error during generation | Break the cycle by restructuring formulas |
| Visible line without Description | Error in presentation generation | Fill in Description for all visible lines |
