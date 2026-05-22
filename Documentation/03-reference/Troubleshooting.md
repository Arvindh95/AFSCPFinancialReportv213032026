# Troubleshooting Guide

Common errors and how to fix them. Error message text matches `Helper/Messages.cs` verbatim where applicable — search the trace log for the exact string.

---

## Authentication & Tenant Errors

| Error message                                                                              | Cause                                                              | Fix |
|--------------------------------------------------------------------------------------------|--------------------------------------------------------------------|-----|
| `Failed to authenticate`                                                                    | OAuth2 call to `{BaseURL}/identity/connect/token` returned an error. | Re-paste Client ID, Client Secret, Username, Password on [Tenant Credentials (FR101001)](../01-setup/TenantCredentials_Setup.md). All five values are encrypted at rest — overwrite by typing again, then Save. |
| `Failed to authenticate. Please check credentials.`                                         | Same as above, friendlier wording for invalid credentials.          | Same fix. |
| `Access token not found in response.`                                                       | Connected Application's OAuth2 Flow is wrong — must be **Resource Owner Password Credentials**. | On [Connected Application Setup (SM303010)](../01-setup/ConnectedApplication_Setup.md), confirm Flow = `Resource Owner Password Credentials`. |
| `Token expiration not found in response.`                                                   | Same Connected App misconfiguration as above.                       | Same fix. |
| `Failed to refresh token`                                                                   | Refresh token expired and the renewal call failed.                  | Logout / Login again will re-issue. If persistent, regenerate the Connected Application's Shared Secret. |
| `Tenant Name is required.`                                                                  | Saving FR101001 with a blank Tenant Name.                           | Fill the Tenant Name (must match the tenant segment in the Acumatica URL). |
| `Tenant Name must be unique.`                                                               | Two FR101001 rows share a Tenant Name.                              | Edit the existing row instead of inserting; or pick a unique Tenant Name. |
| `Company Number is required.`                                                               | Saving FR101001 with a blank Company Number.                         | Fill the integer Company Number from *System → Manage → Companies*. |
| `Tenant mapping not found.`                                                                  | A report / presentation was created under a Company that has no row in `FLRTTenantCredentials`. | Add a Tenant Credentials row for that company on FR101001. |
| `Tenant mapping is missing in Web.config.` / `Tenant mapping is missing in Database`         | Legacy fallbacks — should not occur in v2.1.x with FR101001 in place. | Add the FR101001 row. |

---

## Report Generation Errors (FR101000)

| Error message                                                                              | Cause                                                              | Fix |
|--------------------------------------------------------------------------------------------|--------------------------------------------------------------------|-----|
| `Please select a template to generate the report.`                                          | Generate Report clicked with no record loaded.                      | Open or insert a record first. |
| `No report selected or report ID is missing.`                                               | The header didn't save before Generate Report was clicked.          | Save the record first (`Ctrl+S`), then Generate. |
| `No report definitions linked. Please add at least one definition on the Report Definitions tab before generating a report.` | Generate Report clicked with zero rows on the **Report Definitions** child grid. Previously this produced a `.docx` with every numeric placeholder silently filled as `0`. | Add at least one definition link on the **Report Definitions** tab, then re-run Generate. |
| `Generic Inquiry '{0}' was not found in tenant '{1}'. Check the GI Name on the linked Report Definition...` | The **GI Name** on a linked Report Definition does not match any published GI in the tenant (typo, unpublished, or wrong tenant). Probe runs before parallel fetches so the user sees the GI name, not the generic OData failure. | On FR101002, set **GI Name** to the exact published GI name (e.g. `AFS-Trial-Balance` — match dashes and case as returned by Acumatica's OData service document). |
| `The selected template does not have any attached files.`                                   | Generate Report clicked but no `.docx` is attached via the Files panel. | Open Files (paperclip icon), upload a `.docx` whose filename contains `FRTemplate`. |
| `No files are associated with this record.`                                                  | Same — no attachments at all.                                       | Same fix. |
| `Failed to retrieve the file content.`                                                       | Attachment exists but file content can't be loaded.                 | Re-upload the file via the Files panel. |
| `The selected template file is empty or could not be retrieved.`                             | Attached file is 0 bytes.                                           | Replace with a real `.docx`. |
| `Word document main part is null.`                                                          | The attached file is corrupted or not a valid `.docx`.              | Re-create the Word template. Open in Word, save again, re-upload. |
| `Unable to save the generated file.`                                                         | File system write failed for the merged output.                     | Check Acumatica server's `App_Data` permissions; check disk space. |
| `A report generation process is already running for this template.`                          | Generate clicked while Status = `In Progress`.                      | Wait, or click **Reset Status** if the run is stuck. |
| `Report generation timed out after 15 minutes.`                                              | The merge or OData fetch exceeded the `PXLongOperation` timeout.    | Simplify the template or split the report. Check that OData is reachable from the server. |
| `Template contains {0} placeholders. Maximum allowed is {1}.`                                | The template has more than 1,000 placeholders (`Constants.MaxPlaceholdersPerTemplate`). | Split the template into multiple report records, or consolidate placeholders. |
| `No generated file is available for download. Please generate the report first.`             | Download Report clicked but `GeneratedFileID` is null.              | Run Generate Report and wait for `Ready to Download`. |
| `Please select a report to reset.` / `No record is selected`                                 | Reset Status / Download clicked with no record loaded.              | Open a record. |
| `ReportID cannot be null when retrieving CompanyID.` / `No CompanyID found for ReportID {0}.` | Internal consistency error — record persisted with no `CompanyID`. | Delete the record and recreate it. |

> **Status codes are `N` / `P` / `C` / `F`**, not `P` / `IP` / `C` / `F`. The UI label "File not Generated" maps to DB code `N`. See [FinancialReport_Generation.md § Status lifecycle](../02-generation/FinancialReport_Generation.md#status-lifecycle).

---

## Report Definition Errors (FR101002)

Validation runs in `FLRTReportDefinitionMaint.RowPersisting` — save is blocked until each is fixed.

| Error message                                                                                   | Cause                                                                |
|-------------------------------------------------------------------------------------------------|----------------------------------------------------------------------|
| `Definition Code is required.`                                                                  | Empty Definition Code.                                                |
| `Definition Code must be unique.`                                                               | Another definition uses the same Definition Code.                    |
| `Definition Prefix is required. Enter a short alphanumeric code (e.g. BS, PL, CF).`              | Empty Prefix.                                                         |
| `Definition Prefix must contain letters and digits only — no spaces, underscores, or special characters.` | Prefix has non-alphanumeric chars.                                |
| `Definition Prefix must be unique across all definitions. Another definition already uses this prefix.` | Another FR101002 row uses the same Prefix.                          |
| `Line Code is required.`                                                                         | Empty Line Code on a line item.                                      |
| `Line Code must be unique within the same definition.`                                           | Two line items in the same definition share a Line Code.             |
| `Account From is required for Account Range line types.`                                         | `LineType = ACCOUNT` but Account From cell is blank.                 |
| `Account To is required for Account Range line types.`                                            | `LineType = ACCOUNT` but Account To cell is blank.                  |
| `Formula is required for Calculated line types.`                                                 | `LineType = CALCULATED` but Formula is blank.                       |
| `Formula references unknown Line Code '{0}'. Ensure it is defined with a lower Sort Order.`      | Formula references a token that doesn't match any Line Code on the linked definitions. *(Caught at generation, not save.)* | Fix the typo, or link the missing definition. |
| `Formula evaluation failed for line '{0}': {1}`                                                  | Formula syntax error or runtime exception during arithmetic.          | Check parentheses and operators; the inner exception names the failing token. |
| `Circular dependency detected in report definitions. The following line codes form a cycle and cannot be resolved: {0}. Please revise the formulas to break the cycle.` | Two CALCULATED lines reference each other directly or transitively. *(Caught at generation.)* | Restructure: demote one shared computation into a third helper line. |
| `Duplicate line code(s) detected: {0}. Each definition prefix and line code combination must be unique across all linked definitions.` | Two definitions linked to the same record produce the same `<Prefix>_<LineCode>` global key. | One of the definitions must change its Line Code or Prefix. |

---

## Multi-Definition Link Errors (FR101000 / FR101003 child grids)

| Error message                                                                                                | Trigger |
|--------------------------------------------------------------------------------------------------------------|---------|
| `Please select a Report Definition.`                                                                          | Saving a Definition Link row with no Definition picked. |
| `Prefix '{0}' is already used by another linked definition in this report. Each definition must have a unique prefix.` | Two link rows on the same record reference definitions with the same Prefix. |

---

## MBR Report Generation Errors (FR101003)

| Error message                                                                              | Cause                                                              | Fix |
|--------------------------------------------------------------------------------------------|--------------------------------------------------------------------|-----|
| `Please enter a Presentation Title before generating a presentation.`                       | Generate Presentation clicked with **Presentation Title** blank.    | Fill the title and re-click. |
| `Presentation API Key is not configured. Please enter your API Key in the Tenant Credentials screen.` | Tenant's `GammaApiKey` is blank.                                | Add the key on FR101001 (Presentation API Key field) and Save. |
| `A presentation generation process is already running for this report.`                     | Generate clicked while Status = `In Progress`.                      | Wait, or **Reset Status** if stuck. |
| `No report definitions linked. Please add at least one definition on the Report Definitions tab before generating a presentation.` | Saved a presentation with no children on the Definition Link table and no rows on the GI Data Source link. | Add at least one MBR Definition on the **GI Data Sources** tab (or insert a Definition Link row at the database layer). |
| `The following visible line items are missing descriptions: {0}. Please fill in all descriptions before generating a presentation.` | A linked Report Definition has visible line items with empty Description — Gamma needs them as labels. | Open the offending definition on FR101002 and fill the Description column on every visible line. |
| `No presentation is available for download. Please generate a presentation first.`           | Download Presentation clicked but `SlideGeneratedFileID` is null.   | Run Generate Presentation and wait for `Ready to Download`. |
| `Markdown preview saved. Download it from the Files panel (paperclip icon).`                  | Informational, not an error — Preview Markdown completed.           | — |
| `Presentation generation timed out after {0} minutes.`                                       | The Gamma call exceeded the 15-minute `PXLongOperation` timeout.    | Try again. Reduce data source volume or simplify the Gamma prompt. |
| `Failed to fetch OData`                                                                       | A linked MBR Definition's OData call failed (HTTP 4xx/5xx). The trace log has the actual server response. | Open the trace log; usually a malformed Row Filter, wrong `Type` on a dimension filter, or a renamed GI column. See [MBRDefinition_Setup.md § Row Filter (OData)](../01-setup/MBRDefinition_Setup.md#row-filter-odata). |

---

## MBR Definition Errors (FR101004)

Validation in `FLRTGIDataSourceMaint.RowPersisting`.

| Error message                                                       | Cause                                                       |
|---------------------------------------------------------------------|-------------------------------------------------------------|
| `Data Source Code is required.`                                      | Empty Data Source Code.                                     |
| `Prefix is required.`                                                | Empty Prefix.                                               |
| `Prefix must contain only letters and digits.`                       | Prefix has spaces, underscores, or special characters.       |
| `Prefix must be unique across all GI Data Sources.`                  | Another MBR Definition has the same Prefix.                  |
| `Column Alias is required.`                                          | Empty Column Alias on a column row.                          |
| `GI Column is required for Value lines.`                             | `LineType = VALUE` but GI Column cell is blank.              |
| `Formula is required for Calculated lines.`                          | `LineType = CALCULATED` but Formula is blank.                |
| `Column Alias must be unique within the data source.`                | Two column rows on the same data source share a Column Alias.|
| `Generic Inquiry name is required before detecting columns.`         | Detect Columns clicked without a GI selected.                | 
| `No API credentials found. Configure tenant credentials first.`      | Detect Columns / Test Fetch clicked but no FR101001 row exists for the tenant. |
| `No columns detected from GI '{0}'. Verify the GI name and API credentials.` | Detect Columns succeeded against OData but the GI returned no metadata. | 
| `Failed to detect columns from GI '{0}': {1}`                        | OData failure during Detect Columns. Inner exception in `{1}`.|

### `FormatException: Input string was not in a correct format` (most common runtime error)

Means the engine tried to parse a non-numeric value as a `decimal`.

**Root cause:** The **GI Column** name on a VALUE row doesn't match the actual OData property name returned at run time, so the JSON path picks up a different column (often a string) and fails the cast.

**Fix:**
1. Open FR101004, load the data source.
2. Click **Detect Columns** — note the exact OData column names in the dialog.
3. Update each Column row's **GI Column** cell using the dropdown (the dropdown reads the captured `DetectedColumns` field — it shows the runtime names, not the design-time `ObjectName_Field` form).
4. Save, retry Test Fetch.

**Example:**
- You typed: `Order Total` *(with space)*
- OData actual name: `OrderTotal` *(no space)*
- Result: VALUE row reads a non-existent column, gets a default text response, can't cast to decimal → `FormatException`.

---

## "All values come back as 0"

| Likely cause                                                  | Diagnostic                                                                |
|--------------------------------------------------------------|---------------------------------------------------------------------------|
| Period filter excluded every row                              | Click **Test Fetch** with a known-good period. Drop the dimension filters first. |
| Wrong Column Type / Aggregate combination (e.g. `Sum` on a `String` column) | Open the data source, change Column Type to match the GI property's actual type. |
| Header dimension filters (Branch / Org / Ledger) too narrow   | Test Fetch with all four blank — values appearing means the filters are the problem. |
| Wrong **Period Template** for the column type (e.g. `{MONTH}{YEAR}` on a Date column with `Period Scope = Monthly`) | Date columns with `Monthly` scope ignore the template; the engine uses period boundaries. Set Type = Date. |
| Account-type sign normalization flipped the value             | Check the underlying GL data — credit-normal account types (L / I) get auto-flipped to positive. |
| Cross-definition formula references a definition not linked    | Generation now **fails** with `Formula references unknown Line Code 'PFX_LINE'` (previously the engine returned 0 silently). Add the missing definition to the FR101000 / FR101003 link grid, or fix the typo. |

---

## Reset Status

If a record is stuck in `In Progress` or `Failed`:

1. Open the record on FR101000 (Financial Report) or FR101003 (MBR Report).
2. Click **Reset Status** in the action panel.
3. Confirm the dialog: *"Reset this report from '\<Current Status>' to 'Pending'? This will allow regeneration."*
4. Status returns to **Pending** (UI label: `File not Generated` on FR101000, `Not Generated` on FR101003 — both DB code `N`).
5. **Important:** `GeneratedFileID` / `SlideGeneratedFileID` is **also cleared** by Reset — the previously generated `.docx` / `.pptx` is detached. The next Generate run produces a fresh file.

For Reset Status on FR101003, see [MBRReport_Generation.md § Step 7 — Reset Status](../02-generation/MBRReport_Generation.md#step-7--optional-reset-status).

---

## Where to Find Logs

All operations write detailed trace information.

**Location:** *System → Management → Trace*

### Trace prefix catalogue

The codebase uses prefixed trace messages so you can filter by subsystem:

| Prefix                       | Subsystem                                                                                  | Examples |
|------------------------------|--------------------------------------------------------------------------------------------|----------|
| `[Step N]`                   | Report generation pipeline phases (FR101000 Generate Report).                                | `[Step 5] 12 API calls completed`, `[Step 7] Final placeholder count: 234` |
| `[Pipeline]`                 | Report Definition link resolution.                                                            | `[Pipeline] 2 definition(s) linked — prefixes: [BS, PL]` |
| `[Pipeline/Presentation]`    | Same as `[Pipeline]` but for FR101003 paths.                                                  | `[Pipeline/Presentation] 1 definition(s) — prefixes: [BS]` |
| `[Engine]`                   | `ReportCalculationEngine` warnings (no match found, division by zero, etc.).                  | `[Engine] No match: range 10000:10999 filters Sub='000'`, `[Engine] Division by zero in formula — result set to 0.` |
| `[Slide]`                    | Markdown builder for FR101003.                                                                 | `[Slide] FY periods — CY:01-2026→04-2026, PY:01-2025→04-2025`, `[Slide] Markdown built — 4823 chars` |
| `[Gamma]`                    | Gamma API communication (FR101003 Generate Presentation).                                      | `[Gamma] Authenticated for SalesDemo.`, `[Gamma] Downloaded 286731 bytes.` |
| `[GIDataFetch]`              | MBR Definition OData fetch + aggregation (FR101003 / FR101004 Test Fetch).                    | `[GIDataFetch] Fetched 124 rows from 'PO-PurchaseOrder'`, `[GIDataFetch] Available OData columns: ...` |
| `[GIDataFetchService]`       | Lower-level fetch service warnings (row-cap reached, etc.).                                  | `[GIDataFetchService] Row cap reached: fetched 5000 rows (limit=5000) ...` |
| `[GIDataSource]`             | Detect Columns action on FR101004.                                                             | `[GIDataSource] Detected 33 columns from 'PO-PurchaseOrder': ...` |
| `[GIDataSource TestFetch]`   | Test Fetch action result on FR101004.                                                          | `[GIDataSource TestFetch] Test Results for PURCHASEORDER ...` |
| `[Cache]`                    | Credential cache (in-memory).                                                                | `[Cache] Credentials retrieved from cache for tenant: SalesDemo` |
| `[Decrypt]`                  | Credential loading from `FLRTTenantCredentials` (RSA decrypt).                                | `[Decrypt] Loading credentials for tenant: SalesDemo` |
| `[GIColumnSelector]`         | OData column-list cache feeding the GI Column dropdowns on FR101004.                          | `[GIColumnSelector] OData fetch failed for 'PO-PurchaseOrder': ...` |

### Useful trace entries to grep for

| Entry                                                                | What it tells you                                                |
|----------------------------------------------------------------------|------------------------------------------------------------------|
| `[GIDataFetch] Available OData columns: <list>`                       | Exact runtime column names — copy from here when fixing `FormatException`. |
| `[GIDataFetch] Filter=<odata>`                                       | The exact `$filter` clause sent to OData. Inspect this when "all values are 0" or fetch returns 0 rows. |
| `[GIDataFetch] Fetched N rows from '<GIName>'`                        | Confirms data was retrieved (or wasn't). |
| `[GIDataFetch] Produced N placeholders.`                             | Confirms how many tokens landed in the dictionary. |
| `[Step 7] Final placeholder count: N`                                 | End-of-pipeline placeholder total for FR101000. |
| `ReportCalculationEngine.CalculateAll: N definition(s), prefixes: [...]` | Confirms which definitions / prefixes were resolved. |
| `[Engine] No match: range A:B filters Sub='X'`                        | A line item's account range / dimension filter combo matched zero GL rows. |
| `Trace error: 'Failed to fetch OData'` *(with HTTP body)*             | Look for this body — it's the raw OData error, usually pointing at the bad property name or filter clause. |

---

## Quick Reference — Where to Fix What

| Problem                                              | Where                                                       |
|------------------------------------------------------|-------------------------------------------------------------|
| Wrong credentials, missing API key                    | [Tenant Credentials (FR101001)](../01-setup/TenantCredentials_Setup.md) |
| Definition Code / Prefix / Line Code validation       | [Report Definition Setup (FR101002)](../01-setup/ReportDefinition_Setup.md) |
| Word template contains `_PM` placeholders             | Edit the template — replace with `_CY` / `_PY`. See [Placeholder_Reference.md](Placeholder_Reference.md). |
| GI Column doesn't match OData property                | [MBR Definition Setup (FR101004)](../01-setup/MBRDefinition_Setup.md) — run Detect Columns. |
| Generate stuck or failed                               | Click **Reset Status** on FR101000 or FR101003. |
| Need to know exact `$filter` sent to OData             | Trace log, search `[GIDataFetch] Filter=`. |
| Run details for a presentation                         | Trace log, search `[Slide]` and `[Gamma]`. |
| Run details for a financial report                     | Trace log, search `[Step 1]` … `[Step 7]` and `[Pipeline]`. |
