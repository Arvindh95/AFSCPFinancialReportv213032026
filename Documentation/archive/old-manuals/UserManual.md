# AFS Financial Report — User Manual & Technical Documentation

**Application:** AFS CP Financial Report v2.1  
**Platform:** Acumatica ERP 2025R2  
**Workspace:** Financials > AFS  
**Version Date:** March 2026

---

## Table of Contents

1. [Overview](#1-overview)
2. [Navigation & Screen Map](#2-navigation--screen-map)
3. [Initial Setup — Tenant Credentials (FR101001)](#3-initial-setup--tenant-credentials-fr101001)
4. [Report Definition (FR101002)](#4-report-definition-fr101002)
5. [Financial Report Generation (FR101000)](#5-financial-report-generation-fr101000)
6. [Financial Presentation Generation (FR101003)](#6-financial-presentation-generation-fr101003)
7. [GI Data Source Configuration (FR101004)](#7-gi-data-source-configuration-fr101004)
8. [Generic Inquiries](#8-generic-inquiries)
9. [Placeholder Reference](#9-placeholder-reference)
10. [Word Template Authoring Guide](#10-word-template-authoring-guide)
11. [Calculation Engine — How It Works](#11-calculation-engine--how-it-works)
12. [Presentation (Gamma AI) Integration](#12-presentation-gamma-ai-integration)
13. [Data Flow & Architecture](#13-data-flow--architecture)
14. [Troubleshooting](#14-troubleshooting)
15. [Glossary](#15-glossary)

---

## 1. Overview

The AFS Financial Report module is a customization for Acumatica ERP that automates the generation of
financial reports and AI-powered presentations directly from General Ledger data. It eliminates the
manual process of extracting trial balance data, copying it into spreadsheets, and formatting Word
documents or PowerPoint slides.

### What It Does

- Pulls live GL trial balance data from Acumatica via OData API
- Maps account ranges to named financial line items (e.g., "Total Assets", "Net Income")
- Supports multiple report definitions per report (Balance Sheet + P&L + Cash Flow in one document)
- Replaces `{{PLACEHOLDER}}` tokens in Word (.docx) templates with calculated financial values
- Generates AI-powered PowerPoint presentations via the Gamma API
- Supports any Generic Inquiry as a data source (not just Trial Balance)

### Key Capabilities

- Multi-definition reports with cross-definition formula references
- Current Year (CY), Previous Year (PY), and Previous Month (PM) comparisons
- Configurable rounding (Units / Thousands / Millions) with 0–2 decimal places
- Sign flipping for liability/income accounts
- Multiple balance types: Ending, Beginning, Debit, Credit, Movement, Period-specific
- Dimension-level filtering: Subaccount, Branch, Organization, Ledger per line item
- Generic Inquiry data sources with aggregation, multi-row expansion, and calculated columns
- Encrypted credential storage using Acumatica's RSA encryption

---

## 2. Navigation & Screen Map

All screens are located under the **AFS** workspace in the **Financials** area.

| Screen ID  | Title                      | Category  | Purpose |
|------------|----------------------------|-----------|---------|
| FR101001   | Tenant Credentials         | Reports   | Configure API credentials and Gamma API key per tenant |
| FR101002   | Report Definition          | Reports   | Define financial statement structure (line items, formulas, account ranges) |
| FR101000   | Financial Report           | Reports   | Generate Word documents from templates + GL data |
| FR101003   | AFS Financial Presentation | Reports   | Generate AI-powered PowerPoint presentations |
| FR101004   | GI Data Source             | Reports   | Configure generic inquiry data sources for presentations |
| FR401000   | AFS-Financial-Report       | Inquiries | Generic Inquiry listing all financial reports |
| FR401002   | AFS-Report-Definition      | Inquiries | Generic Inquiry listing all report definitions |

---

## 3. Initial Setup — Tenant Credentials (FR101001)

This is the first screen to configure. It stores the API credentials that the system uses to
authenticate against the Acumatica OData API and the Gamma presentation API.

### Screen Layout

The screen is a simple grid (ListView) with the following columns:

| Field           | Description |
|-----------------|-------------|
| Tenant Name     | The Acumatica tenant name (e.g., "CompanyA"). Must be unique. |
| Base URL        | The Acumatica instance URL (e.g., `https://erp.example.com`). Use HTTPS in production. |
| Company Number  | The internal Acumatica company number. Links this credential set to a specific company. |
| Client ID       | OAuth2 client ID for the Connected Application. Encrypted at rest. |
| Client Secret   | OAuth2 client secret. Encrypted at rest. |
| Username        | API user account username. Encrypted at rest. |
| Password        | API user account password. Encrypted at rest. |
| Presentation API Key | Gamma API key for AI presentation generation. Encrypted at rest. Optional — only needed for FR101003. |

### Setup Steps

1. Navigate to **AFS > Tenant Credentials** (FR101001)
2. Click the **+** button to add a new row
3. Enter the Company Number (find this in Acumatica under System > Manage > Companies)
4. Enter the Tenant Name exactly as it appears in the Acumatica URL
5. Enter the Base URL (the root URL of your Acumatica instance, without trailing slash)
6. Enter the OAuth2 Connected Application credentials (Client ID, Client Secret)
7. Enter the API user credentials (Username, Password)
8. Optionally enter the Gamma API Key if you plan to use the Presentation screen
9. Click **Save**

### Security Notes

- All credential fields (Client ID, Client Secret, Username, Password, Gamma API Key) are encrypted
  using Acumatica's built-in RSA encryption (`[PXRSACryptString]` attribute)
- Credentials are cached in memory for 10 minutes to reduce database lookups during report generation
- The cache is automatically cleared after each report generation cycle
- Always use HTTPS for the Base URL in production environments

---

## 4. Report Definition (FR101002)

The Report Definition screen is where accountants configure the structure of a financial statement.
Each definition maps GL account ranges to named line items, defines subtotals and calculated fields,
and controls how values are presented.

### Header Fields

| Field             | Description |
|-------------------|-------------|
| Definition Code   | Unique identifier for this definition (e.g., "BALANCE_SHEET"). Locked after first save. |
| Prefix            | Short alphanumeric code (2–10 chars, e.g., "BS", "PL", "CF"). Used to namespace placeholders in templates. Locked after first save. Must be unique across all definitions. |
| Report Type       | Classification: Balance Sheet, Profit & Loss, Cash Flow, or Custom |
| Description       | Free-text description |
| Active            | Whether this definition is available for selection in reports |

### Data Source Section

| Field                  | Description |
|------------------------|-------------|
| Generic Inquiry Name   | The GI to query for trial balance data (default: "TrialBalance") |
| Account Column         | GI column containing the account code (default: "Account") |
| Account Type Column    | GI column containing the account type A/L/E/I (default: "Type") |
| Beginning Balance Col  | GI column for beginning balance (default: "BeginningBalance") |
| Ending Balance Col     | GI column for ending balance (default: "EndingBalance") |
| Debit Column           | GI column for debit amounts (default: "Debit") |
| Credit Column          | GI column for credit amounts (default: "Credit") |

Use the **Detect Columns** button to auto-detect and map columns from the selected GI.

### Formatting Section

| Field           | Description |
|-----------------|-------------|
| Rounding Level  | Units (no rounding), Thousands (÷1,000), or Millions (÷1,000,000) |
| Decimal Places  | 0, 1, or 2 decimal places after rounding |

### Line Items Grid

Each row in the grid defines one line of the financial report.

| Field              | Description |
|--------------------|-------------|
| Sort Order         | Controls display order in the grid (does NOT affect calculation order — that is determined automatically by dependency resolution) |
| Line Code          | Unique identifier within this definition (e.g., "CASH", "TOTAL_ASSETS"). Used in formulas and as part of the placeholder key. |
| Description        | Human-readable label. Required for visible lines used in presentations. |
| Line Type          | See Line Types below |
| Account From       | Start of GL account range (inclusive). Only for Account Range type. |
| Account To         | End of GL account range (inclusive). Only for Account Range type. |
| Account Type Filter| Restrict to specific account type: Asset (A), Liability (L), Expense (E), Income (I), or All Types |
| Balance Type       | Which balance to use. See Balance Types below. |
| Sign Rule          | As-Is (keep raw GL sign) or Flip Sign (multiply by -1). Typically flip for Liability, Income. |
| Group / Parent Line| For grouping: set this to the Line Code of the SUBTOTAL line that should sum this line |
| Formula            | Arithmetic expression for Calculated lines. References other Line Codes. |
| Visible in Report  | If unchecked, the line is still calculated (for use in formulas) but the placeholder resolves to empty |
| Subaccount Filter  | Optional: restrict to a specific subaccount (e.g., "000-000") |
| Branch Filter      | Optional: restrict to a specific branch |
| Organization Filter| Optional: restrict to a specific organization |
| Ledger Filter      | Optional: restrict to a specific ledger |

### Line Types

| Type           | Description |
|----------------|-------------|
| Account Range  | Sums all GL accounts in the AccountFrom–AccountTo range. The core building block. |
| Subtotal       | Sums all lines whose Parent Line Code equals this line's Line Code. |
| Calculated     | Evaluates a formula expression referencing other Line Codes (e.g., `REVENUE - TOTAL_EXPENSES`). |
| Heading        | Display-only label. No value calculated. Not visible in output. |

### Balance Types

| Type                    | Description |
|-------------------------|-------------|
| Ending Balance          | The ending balance for the selected period. Most common for Balance Sheet items. |
| Beginning Balance       | The ending balance of the prior fiscal year-end period (i.e., the opening balance for the current fiscal year). |
| Debit (YTD)             | Year-to-date cumulative debit from fiscal year start to selected month. |
| Credit (YTD)            | Year-to-date cumulative credit from fiscal year start to selected month. |
| Movement (YTD)          | Year-to-date net movement (Debit - Credit) from fiscal year start to selected month. |
| Period Debit            | Debit for the selected single period only. Use for monthly reports. |
| Period Credit           | Credit for the selected single period only. |
| Period Movement         | Net movement for the selected single period only. |

### Formula Syntax

Formulas support `+`, `-`, `*`, `/`, and parentheses. Tokens are Line Codes.

**Within the same definition (implicit prefix):**
```
REVENUE - TOTAL_EXPENSES
(GROSS_PROFIT - OPERATING_EXPENSES) / REVENUE
TOTAL_ASSETS - TOTAL_LIABILITIES
```

**Cross-definition references (explicit prefix):**
```
BS_TOTAL_ASSETS - PL_NET_INCOME
CF_OPERATING_CASH + BS_CASH
```

The engine automatically detects whether a token has a known prefix. If it does, it's treated as a
cross-definition reference. If not, it's resolved within the current definition.

### Actions

| Button          | Description |
|-----------------|-------------|
| Detect Columns  | Connects to the GI via OData, retrieves column names, and auto-maps them to the column fields |
| Copy Definition | Creates a complete copy of the current definition (header + all line items) with a "_COPY" suffix |

### Example: Setting Up a Balance Sheet Definition

1. Create a new definition with Code = "BALANCE_SHEET", Prefix = "BS", Type = "Balance Sheet"
2. Click **Detect Columns** to auto-map the GI columns
3. Add line items:

| Sort | Line Code       | Type          | Account From | Account To | Sign Rule | Parent Line |
|------|-----------------|---------------|-------------|------------|-----------|-------------|
| 10   | CASH            | Account Range | 10100       | 10199      | As-Is     | CURRENT_ASSETS |
| 20   | RECEIVABLES     | Account Range | 11100       | 11199      | As-Is     | CURRENT_ASSETS |
| 30   | INVENTORY       | Account Range | 12100       | 12199      | As-Is     | CURRENT_ASSETS |
| 40   | CURRENT_ASSETS  | Subtotal      |             |            |           | TOTAL_ASSETS |
| 50   | FIXED_ASSETS    | Account Range | 15100       | 15999      | As-Is     | TOTAL_ASSETS |
| 60   | TOTAL_ASSETS    | Subtotal      |             |            |           |             |
| 70   | PAYABLES        | Account Range | 20100       | 20199      | Flip Sign | TOTAL_LIAB |
| 80   | TOTAL_LIAB      | Subtotal      |             |            |           |             |
| 100  | NET_ASSETS      | Calculated    |             |            |           |             |

For line 100, set Formula = `TOTAL_ASSETS - TOTAL_LIAB`

---

## 5. Financial Report Generation (FR101000)

This is the main screen for generating Word document reports from templates.

### Header Fields

| Field            | Description |
|------------------|-------------|
| Template Name    | Name for this report configuration |
| Description      | Free-text description |
| Current Year     | The fiscal year to report on (dropdown from Acumatica financial periods) |
| Financial Month  | The month within the year (January–December, default: December) |
| Organization     | Filter data to a specific organization |
| Branch           | Filter data to a specific branch |
| Ledger           | Filter data to a specific ledger (e.g., "ACTUAL") |
| Status           | Read-only: File not Generated / In Progress / Ready to Download / Failed |

### Report Definitions Tab

A grid where you link one or more Report Definitions to this report. Each linked definition
contributes its calculated values (prefixed by the definition's Prefix) to the unified placeholder
dictionary used to populate the Word template.

| Column     | Description |
|------------|-------------|
| Definition | Select a Report Definition |
| Prefix     | Read-only display of the selected definition's prefix |
| Display Order | Controls grid display order (not calculation order) |

### How to Generate a Report

1. Create a new record and fill in the header fields
2. Attach a Word template file to the record using the Files panel (paperclip icon). The filename must contain "FRTemplate" (e.g., `BS_FRTemplate_2026.docx`)
3. On the Report Definitions tab, add one or more definitions
4. Click **Generate Report**
5. The system will:
   - Authenticate against the Acumatica OData API
   - Fetch GL trial balance data for CY, PY, PM, and other required periods (in parallel)
   - Run the calculation engine across all linked definitions
   - Extract `{{PLACEHOLDER}}` tokens from the Word template
   - Replace each placeholder with its calculated value
   - Save the generated document as a file attachment
6. When status changes to "Ready to Download", click **Download Report**

### Actions

| Button          | Description |
|-----------------|-------------|
| Generate Report | Starts the report generation process (runs as a background long operation) |
| Download Report | Downloads the generated Word document |
| Reset Status    | Resets a stuck "In Progress" or "Failed" status back to "Pending" to allow regeneration |

### Status Lifecycle

```
File not Generated → In Progress → Ready to Download
                                 → Failed
```

Use **Reset Status** to return to "File not Generated" from any state.

### Performance Notes

- The system fetches data in parallel (CY, PY, PM, cumulative ranges)
- Optional fetches (cumulative Debit/Credit/Movement, Previous Month) are skipped if no line items or template placeholders require them
- A 15-minute timeout protects against runaway generation processes
- Temporary files are cleaned up automatically after generation

---

## 6. Financial Presentation Generation (FR101003)

This screen generates AI-powered PowerPoint presentations using the Gamma API. It combines
GL data from Report Definitions with data from Generic Inquiry Data Sources.

### Header Fields

| Field                    | Description |
|--------------------------|-------------|
| Presentation Name        | Name for this presentation configuration |
| Description              | Free-text description |
| Current Year             | Fiscal year to report on |
| Financial Month          | Month within the year |
| Organization / Branch / Ledger | Data filters (same as Financial Report) |
| Presentation Status      | Not Generated / In Progress / Ready to Download / Failed |
| Presentation Title       | Title for the generated presentation (required before generation) |
| Presentation Description | Custom instructions for the AI. If blank, a default CFO-level prompt is used. |
| Presentation Template ID | Optional Gamma template ID (from gamma.app/docs/{id}). If set, the presentation uses this template's design. |

### Tabs

**Report Definitions Tab:** Same as FR101000 — link one or more Report Definitions.

**GI Data Sources Tab:** Link one or more GI Data Sources (configured in FR101004). Each data source
contributes its placeholder values to the presentation.

**Presentation Markdown Tab:** Shows the generated markdown text that was sent to the Gamma API.
Editable — you can modify the markdown before generating the presentation.

### How to Generate a Presentation

1. Create a new record and fill in the header fields
2. Add Report Definitions and/or GI Data Sources
3. Ensure all visible line items have descriptions filled in (required for the markdown)
4. Click **Preview Markdown** to generate and review the markdown content
5. Optionally edit the markdown in the Presentation Markdown tab
6. Click **Generate Presentation** to send the markdown to Gamma and produce a PPTX
7. When status changes to "Ready to Download", click **Download Presentation**

### Actions

| Button                | Description |
|-----------------------|-------------|
| Preview Markdown      | Fetches GL data, runs calculations, builds markdown, and saves it as a .txt attachment |
| Generate Presentation | Sends markdown to Gamma API, polls until complete, downloads PPTX, saves as attachment |
| Download Presentation | Downloads the generated PPTX file |
| Reset Status          | Resets status to allow regeneration |

### Markdown Structure

The generated markdown includes:
- Report header (organization, branch, ledger, period labels)
- CFO-level instruction prompt (customizable via Presentation Description)
- Financial data section with CY, PM, PY values for each visible line item
- GI Data Source section with placeholder values from linked data sources

---

## 7. GI Data Source Configuration (FR101004)

This screen allows you to configure any Acumatica Generic Inquiry as a data source for presentations.
Unlike Report Definitions (which are GL Trial Balance-specific), GI Data Sources are fully generic.

### Header Fields

| Field           | Description |
|-----------------|-------------|
| Data Source Code | Unique identifier (locked after first save) |
| Prefix          | Short alphanumeric code for namespacing placeholders (locked after first save) |
| Active          | Whether this data source is available for selection |
| Description     | Free-text description |

### Generic Inquiry Section

| Field      | Description |
|------------|-------------|
| GI Name    | The Generic Inquiry to query (selected from Acumatica's GI list) |
| Key Column | The GI column that acts as the row identifier for key-range filtering |

### Filter Columns Section

Configure which GI columns correspond to standard filter dimensions. Each filter has a Column name
and a Type (String, Integer, Decimal, Date, Boolean) that controls OData query formatting.

| Filter              | Description |
|---------------------|-------------|
| Period Filter Column | GI column for period filtering |
| Period Type          | Data type of the period column |
| Period Scope         | Exact (single value), Monthly (date range), or Yearly (date range) |
| Period Template      | Template for building the period value. Tokens: `{YEAR}`, `{MONTH}`. Example: `{MONTH}{YEAR}` → "012025" |
| Branch Filter Column | GI column for branch filtering |
| Org Filter Column    | GI column for organization filtering |
| Ledger Filter Column | GI column for ledger filtering |

### Columns Grid

Each row defines one output value to extract from the GI.

| Field              | Description |
|--------------------|-------------|
| Sort Order         | Display and processing order |
| Column Alias       | Placeholder key name. The final placeholder is `{{PREFIX_ALIAS}}` |
| Description        | Human-readable label |
| Line Type          | Value (from GI), Multi-Row Expand, Calculated (formula), or Heading |
| GI Column          | Which GI column to read (for Value type) |
| Column Type        | Data type: Decimal, Integer, Boolean, Date, String |
| Aggregate          | How to combine multiple rows: Sum, First, Max, Min, Count |
| Key From / Key To  | Optional key range filter (inclusive) |
| Row Filter (OData) | Additional client-side filter (e.g., `Status eq 'Active'`) |
| Order By Column    | For Multi-Row: column to sort by |
| Sort Direction     | Ascending or Descending |
| Row Limit          | For Multi-Row: how many top rows to expand |
| Display Columns    | For Multi-Row: comma-separated list of columns to show in markdown |
| Formula            | For Calculated: arithmetic expression referencing other Column Aliases |
| Format String      | .NET format string (e.g., "N0", "N2", "dd MMM yyyy") |
| Visible            | Whether the placeholder is included in output |

### Column Line Types

| Type              | Description |
|-------------------|-------------|
| Value (from GI)   | Reads a GI column, applies key range + row filter, aggregates matching rows |
| Multi-Row Expand  | Expands top N rows into individual placeholders: `{{PREFIX_ALIAS_1_ColName}}`, `{{PREFIX_ALIAS_2_ColName}}`, etc. |
| Calculated        | Evaluates a formula referencing other Column Aliases in this data source |
| Heading           | Label only, no placeholder produced |

### Actions

| Button         | Description |
|----------------|-------------|
| Detect Columns | Fetches one row from the GI via OData and discovers available column names. Stores them for dropdown selection. |
| Test Fetch     | Opens a dialog for Year/Month/Branch/Org/Ledger, executes the full fetch + aggregation pipeline, and displays results in a dialog + trace log |

---

## 8. Generic Inquiries

Two Generic Inquiries are included for listing and searching:

- **AFS-Financial-Report (FR401000):** Lists all Financial Report records with their status, template name, year, month, and organization
- **AFS-Report-Definition (FR401002):** Lists all Report Definitions with their code, prefix, type, and status

These GIs provide quick access to records and can be used as data sources for dashboards.

---

## 9. Placeholder Reference

### Report Definition Placeholders

For each visible line item in a linked Report Definition, the system produces three placeholders:

| Placeholder Pattern          | Description |
|------------------------------|-------------|
| `{{PREFIX_LINECODE_CY}}`     | Current Year value |
| `{{PREFIX_LINECODE_PY}}`     | Previous Year (same month, prior year) value |
| `{{PREFIX_LINECODE_PM}}`     | Previous Month value |

**Examples:**
- `{{BS_TOTAL_ASSETS_CY}}` — Balance Sheet Total Assets for the current year
- `{{PL_NET_INCOME_PY}}` — Profit & Loss Net Income for the previous year
- `{{CF_OPERATING_CASH_PM}}` — Cash Flow Operating Cash for the previous month

### Special Placeholders

| Placeholder | Description |
|-------------|-------------|
| `{{CY}}`    | The current year number (e.g., "2026") |
| `{{PY}}`    | The previous year number (e.g., "2025") |

### Legacy Account-Code Placeholders

For backward compatibility, raw account-code placeholders are also supported:

| Pattern              | Description |
|----------------------|-------------|
| `{{A10100_CY}}`      | Ending balance of account A10100 for current year |
| `{{A10100_PY}}`      | Ending balance of account A10100 for previous year |
| `{{A10100_credit_CY}}`| Credit balance of account A10100 |
| `{{A10100:A10199_e_CY}}`| Sum of ending balances for account range A10100–A10199 |

### GI Data Source Placeholders

| Pattern                          | Description |
|----------------------------------|-------------|
| `{{PREFIX_ALIAS}}`               | Single aggregated value |
| `{{PREFIX_ALIAS_1_ColumnName}}`  | Multi-row: first row's column value |
| `{{PREFIX_ALIAS_2_ColumnName}}`  | Multi-row: second row's column value |

---

## 10. Word Template Authoring Guide

### Creating a Template

1. Create a standard Word document (.docx)
2. Design your financial report layout with tables, headers, formatting
3. Insert placeholders using double curly braces: `{{BS_TOTAL_ASSETS_CY}}`
4. Save the file with "FRTemplate" in the filename (e.g., `AnnualReport_FRTemplate.docx`)

### Placeholder Rules

- Placeholders are case-insensitive
- Placeholders can appear in the document body, headers, and footers
- Placeholders can appear inside table cells
- If a placeholder has no matching value, it resolves to "0"
- Maximum 1,000 placeholders per template

### Tips for Reliable Placeholder Detection

Word sometimes splits text across multiple XML runs (e.g., when spell-check or formatting changes
occur mid-placeholder). The system merges adjacent runs with the same formatting before scanning,
but for best results:

- Type each placeholder in one go without pausing
- Apply formatting to the entire placeholder at once (select all of `{{BS_CASH_CY}}` then bold it)
- If a placeholder isn't being replaced, try deleting it and retyping it
- Use **Paste as Plain Text** (Ctrl+Shift+V) when copying placeholders

### Example Template Structure

```
                    BALANCE SHEET
                    As at {{CY}}

                                    {{CY}}          {{PY}}
Current Assets
  Cash                          {{BS_CASH_CY}}    {{BS_CASH_PY}}
  Receivables                   {{BS_RECV_CY}}    {{BS_RECV_PY}}
  Total Current Assets          {{BS_CA_CY}}      {{BS_CA_PY}}

Fixed Assets                    {{BS_FA_CY}}      {{BS_FA_PY}}

TOTAL ASSETS                    {{BS_TA_CY}}      {{BS_TA_PY}}
```

---

## 11. Calculation Engine — How It Works

The ReportCalculationEngine is the core of the system. It processes all linked definitions in a
single pass using topological sort (Kahn's algorithm) to automatically determine the correct
calculation order.

### Processing Steps

1. **Load:** All line items from all linked definitions are loaded into a unified processing graph
2. **Dependency Resolution:** The engine builds a dependency graph:
   - Account Range lines have no dependencies
   - Subtotal lines depend on all lines whose Parent Line Code matches
   - Calculated lines depend on all Line Codes referenced in their formula
3. **Topological Sort:** Kahn's algorithm determines the order that satisfies all dependencies
4. **Circular Dependency Detection:** If a cycle is detected, a clear error message lists the involved line codes
5. **Calculation:** Each line is processed in dependency order:
   - Account Range: sums GL data for matching accounts, applies sign rule and balance type
   - Subtotal: sums all child lines from the global dictionary
   - Calculated: evaluates the formula using values already computed
6. **Formatting:** Values are rounded per the definition's rounding settings and formatted as strings

### Data Fetching Strategy

The system fetches multiple datasets in parallel:

| Dataset              | Purpose |
|----------------------|---------|
| CY (Current Year)    | Ending balance for the selected period |
| PY (Previous Year)   | Ending balance for the same month in the prior year |
| Prior Year Prior     | Ending balance 2 years ago (for PY opening balance) |
| Cumulative CY        | Full fiscal year range for YTD Debit/Credit/Movement |
| Cumulative PY        | Full fiscal year range for PY YTD values |
| Previous Month       | Single period for month-over-month comparison |

Optional datasets (Cumulative, PM) are skipped when no line items or template placeholders require them.

### Sign Correction

The engine applies automatic sign correction based on account type:

- Asset (A) and Expense (E) accounts: positive debit balance is positive
- Liability (L) and Income (I) accounts: positive credit balance is shown as positive

The Sign Rule field provides additional control:
- **As-Is:** Keep the sign-corrected value
- **Flip Sign:** Multiply by -1 (use when the natural GL sign is opposite to the desired presentation)

---

## 12. Presentation (Gamma AI) Integration

The system integrates with the Gamma API (public-api.gamma.app) to generate AI-powered presentations.

### How It Works

1. The system builds a structured markdown document containing:
   - Report context (organization, branch, period labels)
   - CFO-level instructions for the AI (customizable)
   - Raw financial figures for each visible line item (CY, PM, PY)
   - GI Data Source values
2. The markdown is submitted to Gamma's API
3. Gamma's AI generates a professional slide deck with:
   - Executive summary
   - Financial analysis by category
   - Trend analysis and percentage changes
   - Charts and visualizations
   - Strategic recommendations
4. The system polls until generation completes, then downloads the PPTX

### Two Generation Modes

- **Standard:** POST to `/generations` — Gamma creates the design from scratch
- **Template:** POST to `/generations/from-template` — Uses an existing Gamma template's design. Set the Template ID field to the gammaId from the template URL.

### Custom Prompts

The Presentation Description field on FR101003 allows you to replace the default CFO prompt with
custom instructions. This controls the AI's tone, structure, and focus areas.

---

## 13. Data Flow & Architecture

### Component Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Acumatica UI (ASPX Pages)                │
│  FR101000  FR101001  FR101002  FR101003  FR101004           │
└─────────┬───────────────────────────────────┬───────────────┘
          │                                   │
          ▼                                   ▼
┌─────────────────────┐         ┌─────────────────────────────┐
│  Graph (BLC) Layer  │         │  Graph (BLC) Layer          │
│  FLRTFinancialReport│         │  FLRTFinancialPresentation  │
│  Maint              │         │  Maint                      │
└─────────┬───────────┘         └──────────┬──────────────────┘
          │                                │
          ▼                                ▼
┌─────────────────────────────────────────────────────────────┐
│                    Service Layer                            │
│  AuthService          CredentialProvider                    │
│  FinancialDataService GIDataFetchService                    │
│  ReportDataPipeline   ReportCalculationEngine               │
│  WordTemplateService  MarkdownBuilderService                │
│  SlideGenerationService  GammaApiService                    │
│  FileService          TraceLogger                           │
└─────────┬──────────────────────────┬────────────────────────┘
          │                          │
          ▼                          ▼
┌──────────────────┐    ┌─────────────────────────┐
│  Acumatica OData │    │  Gamma API              │
│  (Trial Balance  │    │  (public-api.gamma.app)  │
│   & Generic      │    │                         │
│   Inquiries)     │    │                         │
└──────────────────┘    └─────────────────────────┘
```

### Database Tables

| Table                          | Purpose |
|--------------------------------|---------|
| FLRTFinancialReport            | Financial report header records |
| FLRTReportDefinition           | Report definition configurations |
| FLRTReportLineItem             | Line items within a definition |
| FLRTReportDefinitionLink       | Links definitions to financial reports (many-to-many) |
| FLRTPresentationGeneration     | Presentation header records |
| FLRTPresentationDefinitionLink | Links definitions to presentations |
| FLRTPresentationDataSourceLink | Links GI data sources to presentations |
| FLRTGIDataSource               | GI data source configurations |
| FLRTGIDataSourceColumn         | Column definitions within a GI data source |
| FLRTTenantCredentials          | Encrypted API credentials per tenant |

### OData API Communication

The system communicates with Acumatica's OData API using a fallback strategy:

1. Modern URL with Ledger filter: `{baseUrl}/odata/{tenant}/{GI}?$filter=...&LedgerID eq '{ledger}'`
2. Modern URL without Ledger filter
3. Legacy URL with Ledger filter: `{baseUrl}/t/{tenant}/api/odata/gi/{GI}?$filter=...`
4. Legacy URL without Ledger filter

This ensures compatibility across different Acumatica versions and configurations.

Data is fetched with pagination (5,000 rows per page) and URL-encoded filters.

---

## 14. Troubleshooting

### Common Issues

**Report stuck in "In Progress" status:**
- Click **Reset Status** to return to "Pending"
- Check the Acumatica trace log for error details
- Verify API credentials are correct in FR101001

**Placeholders not being replaced in Word document:**
- Ensure the placeholder is typed in one continuous action (no mid-placeholder formatting changes)
- Try deleting the placeholder and retyping it
- Check that the Line Code and Prefix match exactly (case-insensitive)
- Verify the line item is marked as "Visible in Report"

**"No files associated with this record" error:**
- Ensure a Word template is attached to the record via the Files panel
- The filename must contain "FRTemplate"

**Authentication failures:**
- Verify Base URL, Client ID, Client Secret, Username, and Password in FR101001
- Ensure the Connected Application is configured in Acumatica (System > Integration > Connected Applications)
- Check that the API user has appropriate access rights

**"Circular dependency detected" error:**
- Review the formula references listed in the error message
- Ensure no formula chain creates a loop (A depends on B, B depends on A)
- Cross-definition references are allowed but must not form cycles

**Presentation generation fails:**
- Verify the Gamma API Key is configured in FR101001
- Ensure all visible line items have descriptions filled in
- Check the Presentation Title is not empty
- Review the trace log for Gamma API error messages

**GI Data Source returns no data:**
- Use **Detect Columns** to verify the GI is accessible
- Use **Test Fetch** to verify filter configuration
- Check that the OData column names match (use the names shown by Detect Columns, not the GI field names)
- Verify the Period Template produces the correct filter value

### Trace Log

The system writes detailed trace information during report generation. Access the trace log via:
**System > Management > Trace** in Acumatica.

Key trace prefixes:
- `[Step N]` — Report generation pipeline steps
- `[Gamma]` — Presentation API communication
- `[Slide]` — Slide/markdown generation
- `[GIDataFetch]` — GI Data Source fetching
- `[Pipeline]` — Data pipeline context building
- `[Cache]` — Credential cache operations
- `[Decrypt]` — Credential loading from database

---

## 15. Glossary

| Term | Definition |
|------|------------|
| BLC (Business Logic Controller) | Acumatica term for a Graph — the server-side controller for a screen |
| CY | Current Year — the fiscal year selected on the report |
| DAC (Data Access Class) | Acumatica term for a data entity class mapped to a database table |
| Definition | A Report Definition — the structure of a financial statement |
| GI (Generic Inquiry) | Acumatica's configurable query tool that exposes data via OData |
| Graph | Acumatica's business logic controller class (PXGraph) |
| Line Code | The unique identifier for a line item within a definition |
| OData | Open Data Protocol — the REST API format used by Acumatica |
| Placeholder | A `{{TOKEN}}` in a Word template that gets replaced with a calculated value |
| PM | Previous Month — the month immediately before the selected financial month |
| Prefix | A short code (e.g., "BS") that namespaces all placeholders from a definition |
| PY | Previous Year — the same month in the prior fiscal year |
| Sign Rule | Controls whether a value's sign is kept as-is or flipped for presentation |
| Topological Sort | An algorithm that determines processing order based on dependencies |
| Trial Balance | A GL report showing all account balances for a period |

---

*End of User Manual*
