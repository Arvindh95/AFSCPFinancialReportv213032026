# Financial Report Generation — FR101000

This document describes how to **generate a financial report** from the AFS Financial Report screen. Each record on this screen represents a *single run* of the Word-template merge engine: you pick a period, a Word template with `{{PLACEHOLDER}}` tokens, one or more **Report Definitions** (built in [Report Definition Setup](../01-setup/ReportDefinition_Setup.md)), and the engine fills every placeholder with the calculated GL figure and produces a downloadable `.docx`.

One record = one generated file. Status on the header tells you where the run is in its lifecycle.

---

## Prerequisites

- Tenant credentials saved in [Tenant Credentials (FR101001)](../01-setup/TenantCredentials_Setup.md). The generator uses them to read GL data through the same tenant the screen is opened in.
- At least one **Report Definition** created in [AFS Report Definition (FR101002)](../01-setup/ReportDefinition_Setup.md). The definition supplies the placeholders (`{{<Prefix>_<LineCode>_CY}}` and `{{<Prefix>_<LineCode>_PY}}`) that the template file will reference.
- A Word template (`.docx`) whose filename contains the literal token **`FRTemplate`** (for example `AFS-SalesDemo-Test_FRTemplate.docx`). The loader matches on that substring when resolving which attached file to merge.
- Fiscal periods open for the **Current Year** you intend to report on. The selector only lists years that exist in `FinPeriod`.

---

## Steps

### Step 1 — Navigate to the Financial Report screen

In the top search bar type **Financial Report** and select **AFS Financial Report** under the *AFS* workspace. The landing page opens in **New Record** mode with an empty header and an empty **Report Definitions** grid.

> **Screen ID:** FR101000

![AFS Financial Report landing screen](../images/report_generation/reportgen_01_landing.png)

The toolbar at the top carries the standard Acumatica navigation (Back, Save, Cancel, New, Delete, Copy/Paste, First/Prev/Next/Last). Directly underneath are the three **action buttons** wired on this screen:

- **Generate Report** — kicks off the background merge job.
- **Download Report** — returns the most recently produced `.docx`.
- **Reset Status** — clears a stuck `In Progress` or a `Failed` run back to `Pending` so it can be re-run.

Actions are described in detail below under *Header actions*.

---

### Step 2 — Create a new record

Click **+** (Add) in the toolbar. All header fields clear; **Status** defaults to `File not Generated` (the UI label for `Pending`). Nothing is committed until you save.

---

### Step 3 — Fill the header

The header is split into two column groups. The left group identifies the report and the period; the right group scopes the GL data and carries the run state.

| Field              | Required | Example                          | Notes                                                                                                                                                                                                                              |
| ------------------ | -------- | -------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Template Name**  | yes      | `BS Annual Report 2024`          | Free-text code identifying this report run, up to 225 chars. Shown in the FR401000 list and in the selector on downstream screens. Not the same thing as the Word filename — this is the *record* name.                           |
| **Description**    | no       | `Balance Sheet for December 2024`| Free-text label, up to 50 chars.                                                                                                                                                                                                   |
| **Current Year**   | yes      | `2024`                           | Fiscal year the report runs for. The dropdown is populated from distinct `FinPeriod.FinYear` values, sorted descending.                                                                                                            |
| **Financial Month**| yes      | `December`                       | Month-of-year (`01`–`12`). Defaults to `12` (December). Sets the **period end** for the fiscal-year-to-date window every linked Definition reads (FY start → end of selected month). Same window is used for the `_PY` pass against the prior year. |
| **Organization**   | no       | `PRODUCTS`                       | Optional filter. Blank = all organizations the tenant can see. Selector reads `Organization.OrganizationCD`.                                                                                                                       |
| **Branch**         | no       | `PRODWHOLE`                      | Optional filter. Blank = all branches. Selector reads `Branch.BranchCD`.                                                                                                                                                           |
| **Ledger**         | no       | `ACTUAL`                         | Optional filter. Blank = all ledgers. Selector reads `Ledger.LedgerCD`, description `Ledger.Descr`.                                                                                                                                |
| **Status**         | —        | *read-only*                      | Lifecycle field, managed by the engine — see the **Status lifecycle** section below.                                                                                                                                               |

---

### Step 4 — Pick the Current Year

Click the magnifier next to **Current Year** to open the selector. It shows every distinct `FinYear` on file (latest first). Double-click a row, or type the year directly into the field and press Tab.

![Current Year selector — 2023 through 2027](../images/report_generation/reportgen_02_year_selector.png)

---

### Step 5 — Pick the Financial Month

**Financial Month** is a fixed dropdown (`January` … `December`). It defines the **period end** of the fiscal-year-to-date window the engine reads. Every line item in every linked Definition resolves twice — once for `_CY`, once for `_PY` — against this same FY-to-date window:

- `_CY` — fiscal-year-to-date through end of the selected month, in the **Current Year**.
- `_PY` — fiscal-year-to-date through end of the same month, in the **previous fiscal year**.

There is no single-period (month-only) placeholder. If a month-only delta is needed, compute it inside the Word template (`{{PFX_X_CY}} - {{PFX_X_PY}}`) — formulas inside a Definition cannot mix periods.

Default is `December` (month `12`) so annual statements need no change.

![Financial Month dropdown open](../images/report_generation/reportgen_03_month_dropdown.png)

---

### Step 6 — Scope the GL data (Organization / Branch / Ledger)

The right-hand column group narrows *which GL rows* the linked Report Definitions see. Each field is optional — leave blank to include everything the tenant can see.

| Field            | Selector source               | Example      |
| ---------------- | ----------------------------- | ------------ |
| **Organization** | `Organization.OrganizationCD` | `PRODUCTS`   |
| **Branch**       | `Branch.BranchCD`             | `PRODWHOLE`  |
| **Ledger**       | `Ledger.LedgerCD`             | `ACTUAL`     |

> The three filters are AND-combined with any per-line filters you set inside the Report Definition (*Organization Filter*, *Branch Filter*, *Ledger Filter* on each line item). The header values act as a hard upper bound; per-line filters may further narrow, never widen.

![Organization selector](../images/report_generation/reportgen_04_org_selector.png)

![Branch selector](../images/report_generation/reportgen_05_branch_selector.png)

![Ledger selector](../images/report_generation/reportgen_06_ledger_selector.png)

Once all header fields are populated the form looks like the below. Save (`Ctrl+S`) before attaching files or adding definitions — the Report Definitions grid and the Files panel both need a saved parent record.

![Header fully filled](../images/report_generation/reportgen_07_header_filled.png)

---

### Step 7 — Attach the Word template

Click the **Files** button (paperclip icon, top-right). An empty Files dialog opens.

![Empty Files panel](../images/report_generation/reportgen_08_files_panel.png)

Click **Browse** (or drag-drop), pick your `.docx`, and upload. **The filename must contain the substring `FRTemplate`** — the generator scans the attached files on this record and picks the first one whose name matches. Example: `DemoTemplate_FRTemplate.docx` or `BS2024_FRTemplate.docx`.

![Files panel populated with the FRTemplate docx](../images/report_generation/reportgen_09_files_populated.png)

The Files button badge shows the attachment count (`Files(1)`). You can upload additional supporting files — only the one matching `FRTemplate` is merged; others are ignored.

---

### Step 8 — Link one or more Report Definitions

Switch to the **REPORT DEFINITIONS** tab below the header. This child grid is what tells the engine *which definitions* to run against the Word template. Every row adds another block of placeholders to the merge dictionary.

Click **+** on the grid toolbar to add a blank row.

![Empty grid row added — Add Row action highlighted](../images/report_generation/reportgen_10_grid_row_added.png)

With the row focused, press **F3** (or click the magnifier) in the **Definition** cell. The selector opens and lists every definition marked *Active* on FR101002, showing `Definition Code`, `Prefix`, `Description`, and `Report Type`.

![Definition selector — GENERIC (GC) and MON-REP (MR)](../images/report_generation/reportgen_11_definition_selector.png)

Pick the definition you want. The **Prefix** column on the grid fills automatically from the definition record and is read-only here — it is the same prefix that appears on the placeholders emitted by that definition.

#### Grid columns

| Column         | Source                                  | Purpose                                                                                                                              |
| -------------- | --------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| **Definition** | `FLRTReportDefinition.DefinitionCD`     | The definition to run. Key of the link row. One definition may be linked at most once per report.                                    |
| **Prefix**     | `FLRTReportDefinition.Prefix` (display) | Convenience read-only copy of the definition's prefix, so you can tell at a glance which placeholder namespace each row supplies.    |

#### Why link more than one definition?

The merge engine concatenates the placeholder dictionaries from every linked definition before scanning the template. That means a single report can mix, say, a **Balance Sheet** definition and a **P&L** definition — the template just references both prefixes (`{{BS_CASH_CY}}` and `{{PL_REVENUE_CY}}`) and each token is resolved from whichever definition emitted it. Cross-definition **CALCULATED** lines are supported: a formula in definition *A* may reference a Line Code emitted by definition *B*, provided both are linked on the same report.

> **Legacy single-definition field.** The DAC retains a hidden `Report Definition (Legacy)` field for reports created before multi-definition support shipped. If any rows exist in this child grid the legacy field is ignored, so new reports should always link definitions through the grid.

---

### Step 9 — Save

Press `Ctrl+S`. The graph does **not** validate header completeness at save time — a record with blank Template Name / Current Year / no linked Definition will persist. Validation is deferred to **Generate Report**, which throws if Template Name is missing, no Definition is linked, or no `*FRTemplate*.docx` file is attached. Fill the header before clicking Generate to avoid an immediate failure.

---

## Header actions

The three buttons below the header are the only way to drive the lifecycle of a report record.

### Generate Report

Queues a background job that reads the GL data for the chosen period, evaluates every Line Item in every linked Definition, merges the resulting dictionary into the attached `FRTemplate` Word document, and writes the output back onto the record.

Status immediately transitions to `In Progress`. The job runs under `PXLongOperation` with a 15-minute timeout; you can navigate away from the screen and come back — Status will update on refresh.

**Pre-flight checks** (all run before the background job starts — see [Troubleshooting](../03-reference/Troubleshooting.md#report-generation-errors-fr101000)):

- A record is loaded and saved (has a `ReportID`).
- A `.docx` is attached via the Files panel.
- Status is not currently `In Progress`.
- **At least one Report Definition is linked** on the **Report Definitions** tab. Without this the job would silently produce a `.docx` full of zeros.
- The **GI Name** on every linked Definition resolves to a published GI in this tenant. A 1-row probe runs against the GI before the parallel fetch tasks fan out, so a mistyped or unpublished GI name fails immediately with a clear message rather than a generic OData error.

### Download Report

Returns the last successfully generated `.docx` attached to this record (the file referenced by `GeneratedFileID`). The button is hard-disabled only while a generation is **In Progress**; in all other states it is clickable but throws *"No generated file is available for download."* if no file has ever been produced (or it was just cleared by Reset Status). Effectively this means: only useful when Status = **Ready to Download**.

### Reset Status

Clears a stuck or failed run so the record can be re-queued. The button opens a confirmation dialog showing the current status:

![Reset Status confirmation dialog](../images/report_generation/reportgen_17_reset_dialog.png)

> Dialog message — `Reset this report from '<Current Status>' to 'Pending'? This will allow regeneration.`

- **Yes** — Status moves to `Pending` (`File not Generated`) **and `GeneratedFileID` is cleared**. The previously generated `.docx` is detached from the record so it cannot be downloaded; the next successful run produces a fresh file. Files panel attachments are not affected.
- **No** — Status is unchanged.

Use Reset after an `In Progress` run exceeds its timeout with no output (rare — typically only happens if the web role recycles mid-run), or after a `Failed` run once you have fixed the underlying cause.

---

## Status lifecycle

Status is a one-character field on the DAC (`FLRTFinancialReport.Status`) rendered with user-friendly labels:

| DB value | UI label              | Meaning                                                                                                 |
| -------- | --------------------- | ------------------------------------------------------------------------------------------------------- |
| `N`      | **File not Generated**| Initial state. No `.docx` attached yet. **Generate Report** is allowed; **Download Report** is disabled.|
| `P`      | **In Progress**       | `PXLongOperation` job is running. While in this state every header field plus **Generate Report** and **Download Report** is disabled — only **Reset Status** is clickable. Polling badge updates on the FR401000 list. |
| `C`      | **Ready to Download** | Job finished; merged `.docx` is stored on the record. **Download Report** now returns the file.         |
| `F`      | **Failed**            | Job threw — typically a missing placeholder in a definition, a malformed formula, or a template parse error. Check the trace log, fix the definition or template, then **Reset Status** → **Generate Report**. |

> Constants live in [`Helper/ReportStatus`](../AFSCPFinancialReportv213032026/Helper/Constants.cs) — note the DB code for *In Progress* is `P`, not `IP`. The `N`/`P`/`C`/`F` codes are what land in the `Status` column on the database; the UI labels above are the user-facing strings.

Transitions:

```
Pending ──Generate──► In Progress ──success──► Ready to Download
                                   └──error──► Failed ──Reset──► Pending
```

---

## Worked Example — BS Annual Report 2024

A complete walkthrough of a typical run. Input artefacts: a Word template with Balance Sheet placeholders and a single linked definition.

### 1 — Load or create the record

Open the record in edit mode. Header looks like the below:

![Loaded record — BS Annual Report 2024](../images/report_generation/reportgen_13_record_loaded.png)

| Field            | Value                           |
| ---------------- | ------------------------------- |
| Template Name    | `BS Annual Report 2024`         |
| Description      | `Balance Sheet for December 2024`|
| Current Year     | `2024 - 2024`                   |
| Financial Month  | `December`                      |
| Organization     | `PRODUCTS`                      |
| Branch           | `PRODWHOLE`                     |
| Ledger           | `ACTUAL`                        |
| Status           | `File not Generated`            |

### 2 — Link the definition

In the **Report Definitions** grid, add a row and pick **GENERIC** (Prefix `GC`):

![GENERIC definition linked in grid](../images/report_generation/reportgen_14_definition_linked.png)

### 3 — Attach the template

Open the Files panel and upload `BS2024_FRTemplate.docx` (or similarly named file containing the `FRTemplate` token). The Files badge becomes `Files(1)`.

### 4 — Generate and download

Click **Generate Report**. Status flips to `In Progress`. Within a minute or two (depending on GL volume) Status lands on `Ready to Download`:

![Ready to Download — AFS-SalesDemo-Test example with 2 linked definitions](../images/report_generation/reportgen_15_ready_to_download.png)

Click **Download Report** to pull the merged `.docx`. Every `{{GC_*_CY}}` and `{{GC_*_PY}}` placeholder in the template is now replaced with its calculated figure, rounded per the definition's Rounding Level / Decimal Places settings.

---

## Worked Example — Recovering from a Failed run

The example below shows a record that threw during generation.

![Failed record — AFS-SalesDemo-Test December 2025](../images/report_generation/reportgen_16_failed_record.png)

To recover:

1. Open the record. Read the trace log (top-right trace icon) to identify the error — typical causes are a `{{PREFIX_LINECODE_CY}}` (or `_PY`) placeholder in the template with no matching Line Code on any linked definition, or a CALCULATED formula referencing an unknown Line Code.
2. Fix the root cause in either the template (remove the dangling placeholder or correct its spelling) or the definition (add the missing Line Code / fix the formula).
3. Click **Reset Status** and confirm **Yes** in the dialog. Status returns to `File not Generated`.
4. Click **Generate Report** to re-run.

---

## The Records List — FR401000

The companion list screen **AFS-Financial-Report (FR401000)** shows every record on the system at a glance, grouped by status column. Click any `Report ID` link to jump to the record on FR101000.

![FR401000 records list — all four lifecycle states visible](../images/report_generation/reportgen_12_records_list.png)

The column set mirrors the FR101000 header: *Template Name*, *Description*, *Current Year*, *Financial Month*, *Status*, *Organization*, *Branch*. Use the standard Acumatica toolbar filters and column sort to narrow down when running many reports per period.

---

## Next step

Once the Word report is generating cleanly, proceed to [MBR Report Generation (FR101003)](MBRReport_Generation.md) to roll up one or more generated reports into an executive-summary PowerPoint. The placeholder catalogue produced by every linked definition is documented in [Placeholder Reference](../03-reference/Placeholder_Reference.md).
