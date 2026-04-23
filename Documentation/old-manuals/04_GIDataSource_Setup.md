# Step 4: GI Data Source Setup (FR101004)

**Screen:** AFS > GI-Config-Screen
**Screen ID:** FR101004
**Purpose:** Pull data from ANY Acumatica Generic Inquiry and produce placeholders for presentations.

---

## When Do You Need This?

- When you want data from a GI other than Trial Balance (e.g., Purchase Orders, Sales, HR)
- When you want multi-row data (e.g., "Top 10 Vendors")
- When you want to combine GI data with GL data in one presentation

---

## Important: Detect Columns First

The OData column names often differ from what you see on the GI screen. For example:

| What you see on screen | Actual OData name (example) |
|---|---|
| Order Total | `OrderTotal` or `POOrder__OrderTotal` |
| Vendor Name | `VendorName` or `BAccountR__AcctName` |
| Order Nbr | `OrderNbr` or `POOrder__OrderNbr` |

**Always run Detect Columns before configuring column definitions.** The detected names are the real OData property names that the system uses to query data.

---

## Step-by-Step: Create a GI Data Source

### 1. Navigate to FR101004

Go to **AFS > GI-Config-Screen** or search `FR101004`.

### 2. Fill in the Header

| Field | What to Enter | Example | Notes |
|---|---|---|---|
| Data Source Code | Unique ID | `PO_ORDERS` | Locked after save |
| Prefix | Short code | `PO` | Locked after save. Part of every placeholder. |
| Description | What this does | `Purchase Order Analysis` | Shows in presentation markdown |
| Active | Leave checked | ✓ | |

### 3. Select the Generic Inquiry

| Field | What to Enter | Example |
|---|---|---|
| GI Name | Pick from dropdown | `PO-PurchaseOrder` |
| Key Column | Row identifier column | `Vendor` |

### 4. Save the Header

Click **Save** — you must save before Detect Columns works.

### 5. Click Detect Columns

Click **Detect Columns** in the toolbar. A dialog appears listing all OData column names.

**Write these down or screenshot them.** You'll need the exact names for the next step.

Example detected columns for PO-PurchaseOrder:
```
Type, OrderNbr, Status, Date, Vendor, VendorName, 
OrderQty, OpenQuantity, OrderTotal, Currency, Branch
```

(Your actual names may differ — use what Detect Columns shows you.)

### 6. Configure Filter Columns (Optional)

Map your GI's columns to standard filters. Only configure what your GI supports.

**For PO-PurchaseOrder example:**

| Field | Value | Why |
|---|---|---|
| Period Filter Column | `Date` | Filter POs by order date |
| Period Type | `Date` | The Date column is a date type |
| Period Scope | `Monthly` | Filter to one month of POs |
| Period Template | `{YEAR}-{MONTH}-01` | Builds the date value |
| Branch Filter Column | `Branch` | Filter by branch |
| Branch Type | `String` | Branch codes are strings |

**Period Template tokens:**
- `{YEAR}` = the year from the presentation header (e.g., `2026`)
- `{MONTH}` = the month, zero-padded (e.g., `03`)

**Period Scope options:**
- `Exact` — single value match (for string period columns like `032026`)
- `Monthly` — date range: first of month to first of next month
- `Yearly` — date range: Jan 1 to Jan 1 of next year

Leave Org and Ledger blank if your GI doesn't have those columns.

### 7. Save Again

Click **Save** after configuring filters.

---

## Step-by-Step: Add Column Definitions

In the **Columns** grid at the bottom, add rows for each value to extract.

### Column Line Types

| Type | What It Does | Key Fields |
|---|---|---|
| Value (from GI) | Reads a GI column, aggregates matching rows | GI Column, Column Type, Aggregate |
| Multi-Row Expand | Expands top N rows into individual placeholders | Order By Column, Sort Direction, Row Limit |
| Calculated | Formula referencing other aliases | Formula |
| Heading | Label only | (none) |

### Aggregate Functions

| Function | What It Does | Works With |
|---|---|---|
| Sum | Adds all values | Decimal, Integer |
| Count | Counts rows | Any type |
| First | Takes first row's value | Any type |
| Max | Largest value | Decimal, Integer, Date |
| Min | Smallest value | Decimal, Integer, Date |

### Column Types

| Type | When to Use |
|---|---|
| Decimal | Numbers with decimals (amounts, quantities) |
| Integer | Whole numbers |
| String | Text values (names, codes) |
| Date | Date values |
| Boolean | True/false values |

---

## Example: PO-PurchaseOrder Data Source

**Header:**
- Data Source Code: `PO_ORDERS`
- Prefix: `PO`
- Description: `Purchase Order Analysis`
- GI Name: `PO-PurchaseOrder`
- Key Column: `Vendor`
- Period Filter Column: `Date`, Type: `Date`, Scope: `Monthly`, Template: `{YEAR}-{MONTH}-01`
- Branch Filter Column: `Branch`, Type: `String`

**Column Definitions:**

| Sort | Alias | Description | Type | GI Column | Col Type | Aggregate | Row Filter | Format | Visible |
|---|---|---|---|---|---|---|---|---|---|
| 10 | TOTAL_AMOUNT | Total Purchase Order Value | Value | OrderTotal | Decimal | Sum | | N0 | Yes |
| 20 | TOTAL_QTY | Total Quantity Ordered | Value | OrderQty | Decimal | Sum | | N0 | Yes |
| 30 | ORDER_COUNT | Number of Purchase Orders | Value | OrderNbr | String | Count | | N0 | Yes |
| 40 | OPEN_QTY | Open (Unfulfilled) Quantity | Value | OpenQuantity | Decimal | Sum | | N0 | Yes |
| 50 | AVG_ORDER | Average Purchase Order Value | Calculated | | | | | N0 | Yes |
| 60 | CLOSED_AMT | Total Value of Closed Orders | Value | OrderTotal | Decimal | Sum | `Status eq 'Closed'` | N0 | Yes |
| 70 | ONHOLD_AMT | Total Value of On-Hold Orders | Value | OrderTotal | Decimal | Sum | `Status eq 'On Hold'` | N0 | Yes |
| 80 | TOP_VENDORS | Top 10 Vendors by Spend | Multi-Row | | | | | | Yes |

**Line 50 Formula:** `TOTAL_AMOUNT / ORDER_COUNT`

**Line 80 Multi-Row settings:**
- Order By Column: `OrderTotal`
- Sort Direction: `Descending`
- Row Limit: `10`
- Display Columns: `VendorName,OrderTotal`

**Placeholders produced:**

| Placeholder | Description |
|---|---|
| `{{PO_TOTAL_AMOUNT}}` | Total PO value |
| `{{PO_TOTAL_QTY}}` | Total quantity |
| `{{PO_ORDER_COUNT}}` | Number of POs |
| `{{PO_OPEN_QTY}}` | Open quantity |
| `{{PO_AVG_ORDER}}` | Average PO value |
| `{{PO_CLOSED_AMT}}` | Closed PO total |
| `{{PO_ONHOLD_AMT}}` | On-hold PO total |
| `{{PO_TOP_VENDORS_1_VendorName}}` | #1 vendor name |
| `{{PO_TOP_VENDORS_1_OrderTotal}}` | #1 vendor amount |
| `{{PO_TOP_VENDORS_2_VendorName}}` | #2 vendor name |
| ... | up to rank 10 |

---

## Row Filter Examples

The Row Filter field lets you filter rows beyond the key range. Uses OData syntax.

| Filter | What It Does |
|---|---|
| `Status eq 'Closed'` | Only closed orders |
| `Status eq 'On Hold'` | Only on-hold orders |
| `Status ne 'Closed'` | Everything except closed |
| `Currency eq 'USD'` | Only USD orders |
| `Status eq 'Closed' and Currency eq 'USD'` | Closed USD orders only |

**Supported operators:** `eq` (equals), `ne` (not equals)
**Multiple conditions:** join with `and`

---

## Key From / Key To Examples

Filter rows by the Key Column value range (inclusive).

| Key Column | Key From | Key To | What It Does |
|---|---|---|---|
| Vendor | `A` | `M` | Vendors A through M only |
| Vendor | `ELEMCCOVER` | `ELEMCCOVER` | Only ELEMCCOVER vendor |
| (blank) | (blank) | (blank) | All rows (no key filtering) |

---

## Format String Examples

| Format | Input | Output | Use For |
|---|---|---|---|
| `N0` | 1234567.89 | 1,234,568 | Whole numbers |
| `N2` | 1234567.89 | 1,234,567.89 | 2 decimal places |
| `#,##0` | 1234567 | 1,234,567 | Custom number format |
| `P1` | 0.453 | 45.3% | Percentages |
| `dd MMM yyyy` | 2026-03-15 | 15 Mar 2026 | Dates |
| `MMM yyyy` | 2026-03-15 | Mar 2026 | Month-year |

Leave blank for default formatting.

---

## Testing Your Configuration

### Test Fetch

1. Click **Test Fetch** in the toolbar
2. A dialog appears:

| Field | What to Enter | Example |
|---|---|---|
| Year | Year to test | `2026` |
| Month | Month to test | `03` |
| Branch | Branch filter (optional) | `PRODWHOLE` |
| Organization | Org filter (optional) | |
| Ledger | Ledger filter (optional) | |

3. Click **Fetch**
4. Results appear in a dialog showing each placeholder and its value
5. Full results also appear in the Acumatica trace log (System > Management > Trace)

### What to Check

- Are the values reasonable? (not all zeros, not absurdly large)
- Are the column names correct? (check trace log for "Available OData columns")
- Are the filters working? (try with and without filters)

---

## Common Errors and Fixes

| Error | Cause | Fix |
|---|---|---|
| `FormatException: Input string was not in a correct format` | GI Column name is wrong — the system is reading a text column as a number | Run **Detect Columns**, use the exact OData name from the dropdown |
| `No columns detected` | GI name is wrong or GI returns no data | Verify the GI name, check that the GI has data |
| `Failed to fetch data from GI` | API credentials are wrong or GI is not accessible via OData | Check FR101001 credentials, verify the GI works in the browser |
| All values are 0 | Filters are too restrictive | Try Test Fetch with no filters first |
| `Data source is null` | Forgot to save the header before adding columns | Save the header first |

### The #1 Most Common Error

**`FormatException`** — this almost always means the GI Column name doesn't match the actual OData property name.

What you see on the GI screen: `Order Total`
What OData actually calls it: `OrderTotal` or `POOrder__OrderTotal`

**Fix:** Always use the names from **Detect Columns**, not what you see on the GI screen.
