# MBR Report Generation — FR101003

This document describes how to generate an **AI-driven Monthly Board Report** (MBR) from one or more linked **MBR Definitions** (data sources) using the Gamma presentation API. Each record on this screen represents a single run of the markdown-builder + Gamma pipeline:

1. The engine resolves every linked MBR Definition (FR101004) against live OData data for the chosen period and dimensions.
2. It builds a structured **markdown prompt** containing the resolved values, audience hints, and the user-supplied title / description.
3. It POSTs the markdown to the Gamma API, which returns a generated `.pptx` file.
4. The file is stored on the record and surfaced via the **Download Presentation** action.

> **Screen ID:** FR101003
> **Screen Title:** AFS Monthly Board Report (MBR)
> **Menu path:** *AFS → Reports → AFS Monthly Board Report (MBR)*

---

## Prerequisites

- Tenant credentials saved in [Tenant Credentials Setup (FR101001)](../01-setup/TenantCredentials_Setup.md), including the **Presentation API Key** (Gamma) field. The Generate Presentation action throws `Presentation API Key is not configured` if it's blank.
- At least one linked data source: an **Active** [MBR Definition (FR101004)](../01-setup/MBRDefinition_Setup.md) and/or a [Report Definition (FR101002)](../01-setup/ReportDefinition_Setup.md). If neither is linked, generation fails with *"No Report Definitions or GI Data Sources are linked to this presentation."* (Generation does **not** separately check that a data source has placeholder-producing columns.) Report Definitions are linked at the database level — see [Linking Report Definitions](#linking-report-definitions-legacy-database-only).
- Fiscal periods open for the **Current Year** you intend to report on. The selector only lists years that exist in `FinPeriod`.

---

## Steps

### Step 1 — Navigate to AFS Monthly Board Report (MBR)

In the top search bar type **Monthly Board** and select **AFS Monthly Board Report (MBR)** under *AFS → Reports*. Note the related **AFS MBR Config** under *AFS → Configuration* — that's [FR101004](../01-setup/MBRDefinition_Setup.md), where the data sources you'll link here are defined.

![Top search showing AFS Monthly Board Report (MBR) result](../images/mbr_report/mbrgen_02_search.png)

The screen opens in **New Record** mode with three header column groups, two tabs (`GI DATA SOURCES` / `PRESENTATION MARKDOWN`), and four action buttons.

![FR101003 landing — empty New Record](../images/mbr_report/mbrgen_01_landing.png)

#### Toolbar + actions overview

The standard Acumatica record-navigation toolbar (Back / Save / Cancel / Insert / Copy-Paste / Delete / First / Prev / Next / Last) sits across the top of the form. Four screen-specific actions live in the right-hand action panel:

| Button | Behaviour |
| ------ | --------- |
| **Preview Markdown** | Resolves every linked data source against live OData, builds the Gamma prompt, persists it to the **Presentation Markdown** field. Does **not** call Gamma — use it to inspect what the model will see before incurring an API cost. Status doesn't change. |
| **Generate Presentation** | Same resolve-and-build step, then POSTs the markdown to Gamma. Saves the returned `.pptx` to the record. Status: `Pending` → `In Progress` → `Ready to Download`. 15-minute timeout. If `PresentationMarkdown` is already populated by a prior Preview Markdown, the engine skips the rebuild and submits the stored markdown unchanged. |
| **Download Presentation** | Returns the most recently generated `.pptx`. Hard-disabled until `SlideGeneratedFileID` is populated. |
| **Reset Status** | Confirmation dialog → clears `SlideStatus` to `Pending` and **nulls `SlideGeneratedFileID`**. Use after a stuck `In Progress` run, or after `Failed`, before re-running Generate. |

> While Status = `In Progress`, every header field is disabled and **Preview Markdown** + **Generate Presentation** are greyed out. **Reset Status** stays clickable so you can recover from a stuck run.

---

### Step 2 — Fill the Header

The header is split into three column groups.

#### Identity (left)

| Field             | Required | Notes                                                                                  |
| ----------------- | -------- | -------------------------------------------------------------------------------------- |
| **Presentation Name** | yes  | Free-text identifier, max 225 chars. Shown in the records list and on the breadcrumb. |
| **Description**       | no   | Short label, max 50 chars. |
| **Current Year**      | yes  | Fiscal year. Selector lists distinct `FinYear` values from `FinPeriod` (latest first). |
| **Financial Month**   | yes  | Month-of-year (`01`–`12`). Default `12`. The **fiscal-year START month** of the window every linked Report Definition reads (with Current Year as the FY *end* year), **and** the value fed into the `{MONTH}` token of every linked MBR Definition's Period Filter Template. For a calendar-year report set this to **January**. |

#### Scope (middle)

| Field             | Required | Notes                                                                                  |
| ----------------- | -------- | -------------------------------------------------------------------------------------- |
| **Organization**     | optional | Optional dimension filter. Selector reads `Organization.OrganizationCD`. Fills the `OrgFilterColumn` of every linked MBR Definition that has one configured. |
| **Branch**           | optional | Selector reads `Branch.BranchCD`. Fills `BranchFilterColumn`. |
| **Ledger**           | optional | Selector reads `Ledger.LedgerCD`. Fills `LedgerFilterColumn`. |
| **Presentation Status** | —      | Read-only lifecycle field. See [Status lifecycle](#status-lifecycle). |

#### Gamma prompt fields (right)

| Field             | Required for Generate | Notes                                                                                  |
| ----------------- | --------------------- | -------------------------------------------------------------------------------------- |
| **Presentation Title** | yes  | Up to 500 chars. Becomes the title of the generated `.pptx`. Required by Generate Presentation — the action throws `Presentation Title is required` if blank. Preview Markdown does **not** require it. |
| **Presentation Description** | no | Up to 2000 chars. Optional context paragraph the markdown builder includes in the Gamma prompt's audience / framing block. |
| **Presentation Template ID** | no | Optional Gamma template ID. If set, `GeneratePresentationFromTemplate` is used instead of `GeneratePresentation` — the deck inherits the visual style of the named Gamma template. Leave blank for default Gamma styling. |

After filling, the form looks like the screenshot below — Presentation Name, Description, Current Year, Title, and Description are populated; Org/Branch/Ledger left blank for a tenant-wide run.

![Header — identity, scope, and Gamma prompt fields filled](../images/mbr_report/mbrgen_03_header_filled.png)

> Save (`Ctrl+S`) before opening the GI Data Sources tab — the child grid needs a saved parent record before it can attach links.

---

### Step 3 — Link MBR Definitions (GI Data Sources)

Switch to the **GI DATA SOURCES** tab below the header. This child grid is what tells the engine which MBR Definitions to resolve when building the prompt. Each row pulls in the placeholders that data source emits.

Click **+** on the grid toolbar to add a row, then click the magnifier in the **Data Source** cell (or press F3) — the selector opens listing every active MBR Definition:

![Data Source selector — PURCHASEORDER (PO) is the only active MBR Definition in this tenant](../images/mbr_report/mbrgen_04_datasource_selector.png)

Double-click the data source you want. The grid populates with three columns:

| Column           | Source                                               | Purpose                                                                  |
| ---------------- | ---------------------------------------------------- | ------------------------------------------------------------------------ |
| **Data Source**    | `FLRTGIDataSource.dataSourceCD`                       | Key of the link row. One MBR Definition can be linked at most once per presentation. |
| **Prefix**         | `FLRTGIDataSource.Prefix` *(read-only display)*       | Convenience copy of the data source's prefix, so you can tell at a glance which placeholder namespace each row supplies. |
| **Display Order**  | integer (default 0)                                   | Sort order in the markdown preview's Data section. Doesn't affect Gamma rendering. |

![PURCHASEORDER linked — grid shows Prefix = PO, Display Order = 0](../images/mbr_report/mbrgen_05_datasource_linked.png)

> **Multi-source presentations.** The markdown builder concatenates the placeholder dictionaries from every linked data source before composing the Gamma prompt. A presentation can mix, say, a `PURCHASEORDER` data source (Prefix `PO`) and a `SALESORDER` data source (Prefix `SO`) — every placeholder lands in one merged dictionary. Two data sources cannot collide on Prefix because each Prefix is already globally unique across all MBR Definitions (enforced on FR101004). Note: unlike the Report-Definition links, the GI Data Source link grid has **no** per-presentation duplicate-prefix validation of its own — uniqueness comes from the data-source records themselves.

#### Linking Report Definitions (legacy / database-only)

The graph also exposes a `DefinitionLinks` view over `FLRTPresentationDefinitionLink` for linking [Report Definitions](../01-setup/ReportDefinition_Setup.md), but **the FR101003 page does not surface that grid in the UI**. Records inserted into that table at the database layer are still resolved by the markdown builder (so the legacy field survives), but the canonical way to compose an MBR presentation is through the GI Data Sources grid above.

---

### Step 4 — Preview the Markdown (optional but recommended)

Switch to the **PRESENTATION MARKDOWN** tab. It starts empty:

![Empty Presentation Markdown tab](../images/mbr_report/mbrgen_06_markdown_tab_empty.png)

Click **Preview Markdown** in the action panel. The engine:

1. Authenticates against the tenant's OData endpoint.
2. Resolves every linked MBR Definition (full fetch + aggregate + format for VALUE rows, top-N expansion for MULTIROW rows, formula evaluation for CALCULATED rows).
3. Builds the Gamma prompt — a markdown document with header context (period, scope), the user-supplied audience/framing block, and a **Data** section listing every placeholder produced.
4. Persists the markdown to `PresentationMarkdown` and saves the record.

The whole step is wrapped in a 15-minute `PXLongOperation`. A toast at the top-right shows `Executing` while it runs and `The operation has completed.` when it finishes. The textarea on the tab populates with the generated markdown:

![Presentation Markdown tab populated with the generated Gamma prompt](../images/mbr_report/mbrgen_07_markdown_populated.png)

**Why preview before generating?**
- Verifies every data source resolves without OData errors.
- Lets you spot empty placeholders (data source mis-configured, period filter wrong, etc.) before incurring a Gamma API call.
- The persisted markdown is **reused** by the next Generate Presentation — Generate skips the rebuild step entirely if `PresentationMarkdown` is already populated. So a clean preview is also a way to "freeze" the prompt before committing it to Gamma.

> If the preview comes back with `**Reporting Period:** FY2026 (November 2026)` but you expected April 2026, your **Current Year** / **Financial Month** are out of sync. Edit the header, save, and re-run Preview to overwrite.

> The textarea is editable — you can hand-tweak the markdown before clicking Generate Presentation. Useful for adding extra prompt instructions Gamma should see, but be aware that the next Preview Markdown overwrites your edits.

---

### Step 5 — Generate the Presentation

Click **Generate Presentation**. The engine:

1. Validates: presentation selected, Status not already `In Progress`, **Presentation Title** set, tenant `GammaApiKey` configured.
2. If `PresentationMarkdown` is already populated (from Step 4), it's reused unchanged. Otherwise the markdown builder runs in-flight.
3. Status flips to `In Progress`. Header fields and Preview / Generate buttons disable.
4. Markdown is POSTed to the Gamma API. If a `Presentation Template ID` is set, `GeneratePresentationFromTemplate` is called instead of the default endpoint.
5. Gamma returns a `.pptx` byte stream. The engine saves it as `<PresentationName>_Presentation_<yyyyMMdd_HHmm>.pptx` and stores the file's GUID on `SlideGeneratedFileID`.
6. Status flips to `Ready to Download`.

You can navigate away from the screen during the run — Status updates the next time you reload the record. The 15-minute timeout produces a `Failed` status (and a trace error) rather than hanging the screen.

---

### Step 6 — Download the Presentation

Once Status = `Ready to Download`, click **Download Presentation**. The browser downloads the generated `.pptx`. The Gamma deck is fully editable in PowerPoint — Gamma generates the slide layouts, charts, and bullet structure from the markdown prompt, but doesn't lock the output.

---

### Step 7 — *(Optional)* Reset Status

Open the record and click **Reset Status** — a confirmation dialog appears showing the current status:

![Reset Status confirmation — "Reset this presentation from 'Pending' to 'Pending'? This will allow regeneration."](../images/mbr_report/mbrgen_08_reset_dialog.png)

> Dialog message: `Reset this presentation from '<Current Status>' to 'Pending'? This will allow regeneration.`

- **Yes** — Status moves to `Pending` **and `SlideGeneratedFileID` is cleared**. The previously generated `.pptx` is detached from the record. The next Generate Presentation rebuilds from scratch (or from the stored `PresentationMarkdown` if you preserved it).
- **No** — Status is unchanged.

Use Reset after an `In Progress` run exceeds its timeout, or after a `Failed` run once you've fixed the underlying cause (missing Gamma key, malformed data source, etc.).

---

### Step 8 — *(Optional)* Delete the Record

Click the **Delete** icon on the toolbar and confirm. Deletes the presentation record plus its child Data Source / Definition Link rows. The screen returns to **New Record** mode:

![Clean state after delete — New Record with empty fields](../images/mbr_report/mbrgen_09_after_delete.png)

Deleting a record does not delete any previously downloaded `.pptx` file from your local downloads.

---

## Status lifecycle

`SlideStatus` is a one-character field on the DAC (`FLRTPresentationGeneration.SlideStatus`) rendered with user-friendly labels:

| DB value | UI label              | Meaning                                                                                                 |
| -------- | --------------------- | ------------------------------------------------------------------------------------------------------- |
| `N`      | **Not Generated**     | Initial state. No `.pptx` attached. Generate Presentation is allowed; Download Presentation is disabled.|
| `P`      | **In Progress**       | `PXLongOperation` is running. Header + Generate buttons disable; only Reset Status remains clickable.   |
| `C`      | **Ready to Download** | Job finished; merged `.pptx` is stored on the record. Download Presentation now returns the file.       |
| `F`      | **Failed**            | Job threw — typically a Gamma API error, a data source resolution failure, or a 15-minute timeout. Read the trace log, fix the cause, then **Reset Status** → **Generate Presentation**. |

```
Not Generated ──Generate──► In Progress ──success──► Ready to Download
                                       └──error──► Failed ──Reset──► Not Generated
```

> The DB codes are shared with the Financial Report screen (FR101000) — same `Helper/ReportStatus` constants. The UI label "Not Generated" here corresponds to "File not Generated" on FR101000; both map to DB code `N`.

---

## How the Engine Builds the Markdown

For reference — what `SlideGenerationService.BuildMarkdownPreview` actually emits when you click Preview Markdown or Generate Presentation:

1. **Header context block** — fiscal year, financial month formatted as "FY{Year} ({MonthName} {Year})", plus the Org / Branch / Ledger scope (or `N/A` when blank).
2. **Audience / framing block** — the user-supplied `PresentationDescription`, plus a hard-coded set of "Generate a professional monthly financial report slide deck…" instructions for Gamma.
3. **Data block** — Report-Definition line items render as small `Period | Value` markdown tables (CY and PY rows). GI Data Sources render as bullet lists: for VALUE / CALCULATED rows, `- **<Description>:** <value>`; for MULTIROW rows, a parent bullet plus one nested `- Row N: <col>: <val>, …` bullet per ranked row (filtered by `DisplayColumns`) — **not** a markdown table. HEADING rows print as a section label.

The full markdown is what goes to Gamma — it's also what you can hand-edit on the Presentation Markdown tab. Inspect the populated markdown after a Preview Markdown run to see exactly what your tenant's data looks like.

---

## Validation Errors

| Error message                                                                 | Trigger                                                                                       | Fix |
| ----------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- | --- |
| `Please select a template to generate the report.`                            | Generate / Preview clicked with no record loaded.                                              | Open a record first. |
| `Please enter a Presentation Title before generating a presentation.`         | Generate Presentation clicked with **Presentation Title** blank.                               | Fill Presentation Title and re-click. |
| `Presentation API Key is not configured. Please enter your API Key in the Tenant Credentials screen.` | Generate Presentation clicked but the tenant's `GammaApiKey` is blank on FR101001. | Add the Gamma key on Tenant Credentials and Save. |
| `A presentation generation process is already running for this report.`        | Generate Presentation clicked while another run is `In Progress`.                              | Wait for the run to finish, or click **Reset Status** if it's stuck. |
| `No presentation is available for download. Please generate a presentation first.` | Download Presentation clicked but `SlideGeneratedFileID` is null.                          | Run Generate Presentation, wait for `Ready to Download`, then download. |
| `Prefix 'XX' is already used by another linked definition in this report.`     | Two **Report Definition** links with the same prefix (this check applies to the Report Definition link grid, not the GI Data Source grid).  | Delete one of the duplicate rows; pick another definition whose Prefix is unique. |
| `The following visible line items are missing descriptions: …`                 | A linked **Report Definition** has visible line items with blank Descriptions (checked on Preview Markdown and Generate Presentation).         | Fill in a Description on each listed line item in FR101002. |
| `Tenant mapping not found.`                                                    | The presentation was created under a Company that has no row in `FLRTTenantCredentials`.       | Add a Tenant Credentials row for that company on FR101001. |

---

## DAC Field Reference

### `FLRTPresentationGeneration` (header)

| DAC field             | Display name                | Type / List                                              | Notes |
| --------------------- | --------------------------- | -------------------------------------------------------- | ----- |
| `PresentationCD`      | Presentation Name           | `PXDBString(225)`                                         | Identifier shown in records list. |
| `Description`         | Description                 | `PXDBString(50)`                                          | Free-text label. |
| `CurrYear`            | Current Year                | `PXDBString(4)`                                           | Selector over distinct `FinPeriod.finYear` desc. |
| `FinancialMonth`      | Financial Month             | `01`–`12` PXStringList                                    | Default `12`. |
| `Organization` / `Branch` / `Ledger` | scope filters     | selectors over `Organization.organizationCD` / `Branch.branchCD` / `Ledger.ledgerCD` | Optional. |
| `PresentationTitle`   | Presentation Title          | `PXDBString(500)`                                         | Required by Generate Presentation. |
| `PresentationDescription` | Presentation Description | `PXDBString(2000)`                                        | Optional context for Gamma prompt. |
| `GammaTemplateId`     | Presentation Template ID    | `PXDBString(100)`                                         | Optional Gamma template; switches API call to `GeneratePresentationFromTemplate`. |
| `PresentationMarkdown`| Presentation Markdown        | `PXDBText`                                                | Stored markdown prompt. Populated by Preview Markdown / Generate. Editable on the tab. |
| `SlideStatus`         | Presentation Status         | `N`/`P`/`C`/`F`                                           | Default `N`. Read-only in UI. |
| `SlideGeneratedFileID`| Slide File ID *(hidden)*    | `PXDBGuid`                                                | FK to the generated `.pptx` `UploadFile` row. |

### `FLRTPresentationDataSourceLink` (children — GI DATA SOURCES tab)

| DAC field          | Display name   | Type                                                  | Notes |
| ------------------ | -------------- | ----------------------------------------------------- | ----- |
| `DataSourceID`     | Data Source    | int FK → `FLRTGIDataSource`                            | Key of the link row. Selector restricted to active MBR Definitions. |
| `DataSourcePrefix` | Prefix         | display-only string                                    | Read-only mirror of the linked data source's `Prefix`. |
| `DisplayOrder`     | Display Order  | int (default 0)                                        | Sort order in the markdown Data section. |

### `FLRTPresentationDefinitionLink` (children — Report Definitions, **DB only**, no UI)

| DAC field          | Display name   | Type                                                  | Notes |
| ------------------ | -------------- | ----------------------------------------------------- | ----- |
| `DefinitionID`     | Definition     | int FK → `FLRTReportDefinition`                        | Same shape as `FLRTReportDefinitionLink` on FR101000. |
| `DefinitionPrefix` | Prefix         | display-only string                                    | Read-only mirror of the linked definition's prefix. |
| `DisplayOrder`     | Display Order  | int (default 0)                                        | Sort order. |

> The graph view + DAC for `FLRTPresentationDefinitionLink` exist for backward compatibility, but the FR101003.aspx page does not render a tab for it. Insert rows directly via SQL or add a tab in your customization if you need to link Report Definitions to a presentation through the UI.

---

## Next Step

Need to define more data sources? Head back to [MBR Definition Setup (FR101004)](../01-setup/MBRDefinition_Setup.md) and add another. The full placeholder catalogue produced by every linked definition + data source is documented in [Placeholder Reference](../03-reference/Placeholder_Reference.md). Common errors and recovery steps live in [Troubleshooting](../03-reference/Troubleshooting.md).
