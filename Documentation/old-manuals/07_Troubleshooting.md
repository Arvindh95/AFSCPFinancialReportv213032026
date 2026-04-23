# Troubleshooting Guide

Common errors and how to fix them.

---

## Authentication Errors

| Error | Cause | Fix |
|---|---|---|
| `Failed to authenticate` | Wrong credentials | Check Client ID, Client Secret, Username, Password in FR101001 |
| `Access token not found in response` | Connected Application misconfigured | Verify OAuth2 flow is set to Resource Owner Password Credentials |
| `Failed to refresh token` | Token expired and refresh failed | Check credentials, try regenerating the Connected Application |

---

## Report Generation Errors (FR101000)

| Error | Cause | Fix |
|---|---|---|
| `Please select a template` | No record selected | Select or create a report record |
| `Template does not have any attached files` | No Word file attached | Attach a .docx file via the Files panel (paperclip icon) |
| `No files associated with this record` | File attached but name doesn't contain `FRTemplate` | Rename the file to include `FRTemplate` |
| `File generation already in progress` | Status is "In Progress" | Wait for completion, or click Reset Status |
| `Report generation timed out after 15 minutes` | Template too complex or API too slow | Simplify template, check network, try again |
| `Word document main part is null` | Corrupted .docx file | Re-create the Word template |

---

## Report Definition Errors (FR101002)

| Error | Cause | Fix |
|---|---|---|
| `Definition Code is required` | Empty Definition Code | Enter a unique code |
| `Definition Prefix is required` | Empty Prefix | Enter a 2-10 char alphanumeric prefix |
| `Prefix must be unique` | Another definition uses the same prefix | Choose a different prefix |
| `Line Code is required` | Empty Line Code in grid | Enter a unique code for each line |
| `Line Code must be unique` | Duplicate Line Code in same definition | Rename one of the duplicates |
| `Account From is required` | Account Range line missing start | Fill in Account From |
| `Formula is required` | Calculated line missing formula | Enter a formula |
| `Circular dependency detected` | Formula A references B, B references A | Restructure formulas to break the cycle |
| `Formula references unknown Line Code` | Typo in formula | Check spelling matches an existing Line Code |
| `Duplicate line codes detected` | Same PREFIX_LINECODE across definitions | Ensure each definition has unique line codes |

---

## Presentation Errors (FR101003)

| Error | Cause | Fix |
|---|---|---|
| `Presentation API Key is not configured` | No Gamma key in FR101001 | Add the key to the Presentation API Key column |
| `Please enter a Presentation Title` | Title field is empty | Fill in Presentation Title |
| `No Report Definitions or GI Data Sources are linked` | Nothing on either tab | Add at least one definition or data source |
| `Visible line items are missing descriptions` | Line items in FR101002 have empty Description | Fill in Description for all visible lines |
| `Generation failed: [error from Gamma]` | Gamma API rejected the request | Check the error message, simplify the prompt |
| `Timed out waiting for generation` | Gamma took too long | Try again, or use a simpler prompt |
| `No exportUrl found` | Gamma completed but didn't produce a file | Check Gamma API key is valid, try again |

---

## GI Data Source Errors (FR101004)

| Error | Cause | Fix |
|---|---|---|
| `FormatException: Input string was not in a correct format` | **GI Column name is wrong** — reading text as a number | Run **Detect Columns**, use the exact OData name |
| `No columns detected` | GI name is wrong or returns no data | Verify GI name, check GI has data |
| `Failed to fetch data from GI` | API credentials wrong or GI not OData-accessible | Check FR101001, verify GI works in browser |
| `Data source is null` | Header not saved before adding columns | Save the header first |
| `Column Alias must be unique` | Duplicate alias in same data source | Rename one |
| `GI Column is required for Value lines` | Value line missing GI Column | Select a column from the dropdown |
| `Formula is required for Calculated lines` | Calculated line missing formula | Enter a formula |
| All values are 0 | Filters too restrictive or wrong column names | Try Test Fetch with no filters, check column names |

### The FormatException Fix (Most Common Error)

This error means the system tried to parse a non-numeric value as a decimal.

**Root cause:** The GI Column name you entered doesn't match the actual OData property name.
The system ends up reading a different column (like a text field) and can't convert it to a number.

**Fix:**
1. Go to FR101004
2. Click **Detect Columns**
3. Note the exact column names in the dialog
4. Update your column definitions to use those exact names
5. Save and retry

**Example:**
- You entered: `Order Total` (with space)
- OData actual name: `OrderTotal` (no space)
- Result: system can't find `Order Total`, reads wrong data, crashes

---

## Where to Find Logs

All operations write detailed trace information.

**Location:** System > Management > Trace

**Key prefixes in the trace log:**

| Prefix | What It Logs |
|---|---|
| `[Step N]` | Report generation pipeline steps |
| `[Gamma]` | Presentation API communication |
| `[Slide]` | Markdown generation |
| `[GIDataFetch]` | GI Data Source fetching and aggregation |
| `[Pipeline]` | Data pipeline context building |
| `[Cache]` | Credential cache operations |
| `[Decrypt]` | Credential loading from database |

**Useful trace entries:**
- `OData columns (N): col1, col2, ...` — shows actual column names from the API response
- `Available OData columns: col1, col2, ...` — shows columns detected from GI
- `Fetched N rows from 'GIName'` — confirms data was retrieved
- `Produced N placeholders` — confirms values were calculated
- `Filter=...` — shows the exact OData filter being sent

---

## Reset Status

If a report or presentation is stuck in "In Progress" or "Failed":

1. Open the record in FR101000 or FR101003
2. Click **Reset Status** in the toolbar
3. Confirm the reset
4. Status returns to "File not Generated" / "Not Generated"
5. You can now regenerate
