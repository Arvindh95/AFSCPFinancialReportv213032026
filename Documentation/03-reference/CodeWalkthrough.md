# AFSCP Financial Report — Code Walkthrough

A file-by-file, function-by-function explanation of how this Acumatica customization works.

This is a developer reference. For end-user setup/usage, see the other folders under `Documentation/`.

---

## 1. What the project does (the 30-second version)

This is an Acumatica ERP customization (assembly `FinancialReport.dll`, namespace `FinancialReport`) that produces two kinds of deliverables:

1. **Word financial reports** — Take a `.docx` template containing placeholders like `{{BS_TOTAL_ASSETS_CY}}`, pull GL trial-balance data from a remote Acumatica tenant over OData, compute every line value defined by an accountant in a "Report Definition", and stamp the numbers into the template. Output is a downloadable `.docx`.

2. **AI presentations (Gamma)** — Take the same computed data (plus optional Generic-Inquiry data), build a structured markdown prompt, send it to the Gamma API, and get back a `.pptx` slide deck.

The defining idea: **accountants configure report structure as data, not code.** Which accounts roll into which line, sign rules, subtotals, formulas, rounding — all live in DB tables edited through maintenance screens. No recompile when the chart of accounts changes.

### Data flow at a glance

```
                  Maintenance Screens (FR101000–FR101004)
                              │  (config saved to DB)
                              ▼
   ┌─────────────────────────────────────────────────────────────┐
   │  GENERATE (long operation, background thread)                 │
   │                                                               │
   │  ReportDataPipeline.BuildContext                              │
   │    → load definitions + line items, compute FY periods        │
   │                                                               │
   │  AuthService ──OAuth──► remote tenant /identity/connect/token │
   │                                                               │
   │  FinancialDataService ──OData──► remote GI (Trial Balance)    │
   │    → FinancialApiData (accounts × balances)                   │
   │                                                               │
   │  ReportCalculationEngine.CalculateAll                         │
   │    → topological sort, sum ranges, subtotals, formulas        │
   │    → { "BS_TOTAL_ASSETS_CY" : "1,234,567" , ... }             │
   │                                                               │
   │   ┌── Word path ──────────┐   ┌── Slide path ──────────────┐  │
   │   │ WordTemplateService   │   │ MarkdownBuilderService     │  │
   │   │  fill {{...}} in docx │   │  → markdown prompt         │  │
   │   │                       │   │ GammaApiService → .pptx    │  │
   │   └───────────────────────┘   └────────────────────────────┘  │
   │                                                               │
   │  FileService.SaveGeneratedDocument → Acumatica file storage   │
   └─────────────────────────────────────────────────────────────┘
```

### Naming convention

Every DB-backed class is prefixed `FLRT` (FinanciaL RepoRT). Screens are `FR1010xx`. Placeholder keys follow `PREFIX_LINECODE_CY` / `PREFIX_LINECODE_PY` for trial-balance lines and `PREFIX_ALIAS` for GI data-source values.

---

## 2. Project layout

```
AFSCPFinancialReportv213032026/
├── DAC/        Data Access Classes — one C# class per DB table
├── Graph/      Business Logic Controllers — one per maintenance screen
├── Services/   The engine: auth, OData fetch, calculation, file/template/API
├── Helper/     Constants, messages, DTOs, custom selectors
├── Properties/ AssemblyInfo
Pages/FR/       ASPX screen definitions (FR101000–FR101004)
_project/       Acumatica customization package (SQL, sitemap, screen XML)
Documentation/  User + reference docs
```

The three layers map to Acumatica's MVC-ish framework:

- **DAC** = the model (tables + field attributes).
- **Graph** = the controller (views, events, action buttons). Runs on the UI/long-operation thread.
- **Service** = plain C# business logic, framework-agnostic where possible, called from graphs.

---

## 3. Screens → Graphs → Primary DAC

| Screen | Title | Graph | Primary DAC |
|--------|-------|-------|-------------|
| FR101000 | Financial Report | `FLRTFinancialReportMaint` | `FLRTFinancialReport` |
| FR101001 | Tenant Credentials | `FLRTTenantCredentialsMaint` | `FLRTTenantCredentials` |
| FR101002 | Report Definition | `FLRTReportDefinitionMaint` | `FLRTReportDefinition` |
| FR101003 | Financial Presentation | `FLRTFinancialPresentationMaint` | `FLRTPresentationGeneration` |
| FR101004 | GI Data Source | `FLRTGIDataSourceMaint` | `FLRTGIDataSource` |

The `Pages/FR/*.aspx.cs` files are empty `PXPage` stubs — all UI binding is declarative in the `.aspx` markup; all behavior is in the graph.

---

## 4. DAC layer (`DAC/`)

DACs declare the table schema via field attributes. The repeating pattern per field is:
a `[PXDB*]` attribute (column type), `[PXUIField]` (label/visibility), optional `[PXDefault]`/`[PXSelector]`/`[PXStringList]`, then the property and a matching `abstract class xxx : BqlField` used in BQL queries. Every table also carries the standard audit block (`CreatedByID`, `LastModifiedDateTime`, `Tstamp`, etc.).

### 4.1 `FLRTFinancialReport.cs` — the Word-report header

One row = one report-generation job. Key fields:

- `ReportID` (identity, PK), `ReportCD` ("Template Name"), `Description`.
- `CurrYear` — selector over distinct `FinPeriod.finYear`, descending.
- `Branch`, `Organization`, `Ledger` — selectors over GL master tables. These become the OData dimension filters.
- `FinancialMonth` — `01`–`12` string list, **interpreted as the fiscal-year START month**, defaulting to `12`. (See §7 on period math — this is the single most confusing field in the project.)
- `DefinitionID` — **legacy** single-definition link, `Visible=false`. Superseded by the `FLRTReportDefinitionLink` child table. Ignored if any link rows exist; kept only for old records.
- `GeneratedFileID` / `UploadedFileID` / `UploadedFileIDDisplay` — GUIDs for the produced file and the attached template.
- `Status` — 1-char: `N` Pending / `P` InProgress / `C` Completed / `F` Failed (`ReportStatus` constants), read-only, drives button enable state.

### 4.2 `FLRTReportDefinition.cs` — the reusable report blueprint

One row = a financial-statement structure (a Balance Sheet, a P&L, etc.) that can be reused across many reports.

- `DefinitionID` (identity), `DefinitionCD` (key code), `Description`.
- `DefinitionPrefix` — 2–10 alphanumeric chars. **This is the namespace for placeholders.** Prefix `BS` → placeholders `{{BS_*}}`. Must be unique; locked after first save.
- `ReportType` — `BS`/`PL`/`CF`/`CU` (Balance Sheet / P&L / Cash Flow / Custom). Informational.
- `GIName` — which Generic Inquiry supplies the trial-balance data (default `TrialBalance`); selector over `GIDesign.name`.
- **Column-mapping block** (`AccountColumn`, `TypeColumn`, `BeginningBalColumn`, `EndingBalColumn`, `DebitColumn`, `CreditColumn`, `MovementColumn`, `PeriodColumn`, `SubaccountColumn`, `BranchColumn`, `OrganizationColumn`, `LedgerColumn`) — map this definition's logical fields to the actual OData column names in the chosen GI. Each uses the `[GIColumnSelector]` custom attribute (§6.3). This is what makes the engine GI-agnostic.
- **Rounding block** — `RoundingLevel` (`UNITS`/`THOUS`/`MILL`) and `DecimalPlaces` (0/1/2).
- Nested constant classes: `ReportDefinitionType`, `RoundingLevelType`.

### 4.3 `FLRTReportLineItem.cs` — a single report line (child of definition)

This is the heart of report configuration. `[PXParent]` ties each line to its `FLRTReportDefinition`. One row = one line on the statement.

- `LineCode` — unique code within the definition; becomes the placeholder middle segment (`PREFIX_LINECODE_CY`).
- `LineType` — the dispatch switch for the engine:
  - `ACCOUNT` — sum all GL accounts in `AccountFrom`…`AccountTo`.
  - `SUBTOTAL` — sum all lines whose `ParentLineCode` equals this line's `LineCode`.
  - `CALCULATED` — evaluate `Formula` referencing other line codes.
  - `HEADING` — display-only label, no value.
- `AccountFrom` / `AccountTo` — inclusive account-code range (ACCOUNT lines).
- `AccountTypeFilter` — optional `A`/`L`/`E`/`I` restriction.
- `SignRule` — `ASIS` keeps raw GL sign; `FLIP` multiplies by −1 for presentation.
- `BalanceType` — `ENDING` / `BEGINNING` / `DEBIT` / `CREDIT` / `MOVEMENT`. The last three are full-fiscal-year YTD figures and trigger the "cumulative" range fetch.
- `ParentLineCode` — groups this line under a SUBTOTAL.
- `Formula` — arithmetic expression for CALCULATED lines (e.g. `REVENUE - TOTAL_EXPENSES`).
- `IsVisible` — if false, the line is still computed (usable in formulas/subtotals) but its placeholder resolves to empty.
- Per-line dimension filters: `SubaccountFilter`, `BranchFilter`, `OrganizationFilter`, `LedgerFilter`. **Setting any of these forces the engine onto the detail-row code path** (§8.6) instead of pre-aggregated account data.
- Nested constants: `LineItemType`, `SignRuleValue`, `BalanceTypeValue`, `AccountTypeValue`.

### 4.4 `FLRTReportDefinitionLink.cs` — many definitions per report

Child of `FLRTFinancialReport`. Each row links one `FLRTReportDefinition` (by `DefinitionID`) to a report, carrying its `DefinitionPrefix` (display-only, populated by a `FieldSelecting` event) and a `DisplayOrder`. Multiple links per report enable **cross-definition formulas** (a CF line referencing `BS_RETAINED_EARNINGS`). The doc-comment notes `DisplayOrder` affects grid display only — calculation order is decided by topological sort.

### 4.5 `FLRTGIDataSource.cs` — generic (non-trial-balance) data source

Where `FLRTReportDefinition` is GL-specific, this is fully generic: point it at *any* GI and pull *any* columns as placeholders, for use in presentations.

- `DataSourceCD` (key), `Prefix` (placeholder namespace), `Description`, `IsActive`, `GIName`.
- `KeyColumn` — the row-identifier column (analogous to Account), used for `KeyFrom`/`KeyTo` range filtering by child columns.
- **Filter-column mapping** — `PeriodFilterColumn` + `PeriodFilterType` + `PeriodFilterTemplate` + `PeriodScope`, plus `Branch/Org/Ledger` filter columns each with a type. The template substitutes `{YEAR}`/`{MONTH}` tokens (e.g. `{MONTH}{YEAR}` → `012025`). `PeriodScope` (`Exact`/`Monthly`/`Yearly`) controls whether Date columns use an `eq` or a `ge…lt` range.
- `DetectedColumns` — comma-separated cache of real OData column names, filled by the "Detect Columns" action (§5.5) so dropdowns can show them.
- Nested constants: `PeriodScopeType`, `GIColumnType`.

### 4.6 `FLRTGIDataSourceColumn.cs` — one output value of a GI data source (child)

`[PXParent]` to `FLRTGIDataSource`. Produces `{{PREFIX_ALIAS}}`.

- `ColumnAlias` — placeholder key; `Description`; `SortOrder`.
- `LineType` — `VALUE` (read+aggregate a GI column), `MULTIROW` (expand top-N rows into `PREFIX_ALIAS_N_COLUMN` placeholders), `CALCULATED` (formula over other aliases), `HEADING`.
- VALUE fields: `GIColumn`, `ColumnType` (Decimal/Integer/Boolean/Date/String), `AggregateFunction` (Sum/First/Max/Min/Avg/Count), `KeyFrom`/`KeyTo` (range against parent `KeyColumn`), `RowFilter` (extra OData-style predicate applied client-side).
- CALCULATED: `Formula`.
- MULTIROW: `OrderByColumn`, `OrderByDirection`, `RowLimit`, `DisplayColumns` (markdown-only column subset).
- `FormatString` (.NET format), `IsVisible`.
- Nested constants: `ColumnLineType`, `OrderByDirectionType`, `AggregateFunctionType`.

### 4.7 `FLRTPresentationGeneration.cs` — the presentation header

Mirror of `FLRTFinancialReport` for the slide path. Adds:

- `PresentationTitle`, `PresentationDescription` — fed to Gamma.
- `GammaTemplateId` — optional Gamma template gammaId; if set, generation goes through the from-template endpoint.
- `PresentationMarkdown` — `PXDBText`; the generated/previewable markdown prompt, cached on the record so "Generate" can reuse "Preview" output.
- `SlideStatus` (same `N/P/C/F`), `SlideGeneratedFileID`.

### 4.8 `FLRTPresentationDefinitionLink.cs` & `FLRTPresentationDataSourceLink.cs`

Children of `FLRTPresentationGeneration`. The former links Report Definitions (trial-balance data); the latter links GI Data Sources (generic data). Both carry a display-only prefix and `DisplayOrder`. The data-source-link selector restricts to `IsActive = true` sources.

### 4.9 `FLRTTenantCredentials.cs` — per-tenant secrets

Keyed by `CompanyNum`. Holds `TenantName`, `BaseURL`, and **encrypted** (`[PXRSACryptString]`) `UsernameNew`, `PasswordNew`, `ClientIDNew`, `ClientSecretNew`, and `GammaApiKey`. This is how the customization authenticates *outbound* to the (possibly same, possibly remote) Acumatica tenant's OData/REST endpoints and to Gamma.

### 4.10 `GLHistoryEnqFilter.cs` — a cache extension

`GLHistoryEnqFilterExt : PXCacheExtension<GLHistoryEnqFilter>` adds an unbound `UsrSumEndingBalance` decimal to Acumatica's stock GL History Inquiry filter. Standalone helper, not part of the main generation flow.

---

## 5. Graph layer (`Graph/`)

### 5.1 `FLRTFinancialReportMaint.cs` — the Word-report screen

**Views:** `FinancialReport` (primary) and `DefinitionLinks` (the linked-definitions grid, ordered by `DisplayOrder`).

**Events:**
- `FieldSelecting<…definitionPrefix>` — populates the read-only Prefix column on each link row by selecting the parent definition.
- `RowPersisting<FLRTReportDefinitionLink>` — validates a definition is chosen and that its prefix is **unique among the report's links** (walks `DefinitionLinks.Cache.Cached`).
- `FLRTFinancialReport_RowSelected` — disables all fields and the Generate/Download buttons while `Status == InProgress`.
- `FLRTFinancialReport_RowPersisting` — when a template file is attached (matched by the `FRTemplate` filename filter), captures its `FileID` into `UploadedFileID`.

**Actions:**
- `generateReport` — the orchestration entry point. On the UI thread it validates (record selected, has a NoteID/template, not already running, **at least one linked definition** — else every placeholder would silently fill with `0`), resolves tenant + credentials, builds an `AuthService`, flips status to `InProgress`, saves, then kicks off `PXLongOperation.StartOperation`. Inside the background op it re-loads the record on a fresh graph, wraps work in a **15-minute `CancellationTokenSource`**, authenticates, runs `ReportGenerationService.Execute(token)`, and on success writes `GeneratedFileID` + `Completed`. On timeout/exception it sets `Failed` and re-throws. `finally` logs out the auth session.
- `downloadReport` — throws `PXRedirectToFileException` on `GeneratedFileID` to stream the file to the browser.
- `resetStatus` — confirmation dialog, then clears status back to `Pending` and nulls `GeneratedFileID` so stale output isn't downloadable.

**Helpers (public, reused by the service):**
- `GetCompanyIDFromDB(reportID)` — raw `PXDatabase.SelectSingle` for the row's `CompanyID` (the tenant discriminator).
- `MapCompanyIDToTenantName(companyID)` — looks up `FLRTTenantCredentials.TenantName` by `CompanyNum`.

### 5.2 `FLRTFinancialPresentationMaint.cs` — the presentation screen

**Views:** `PresentationRecord` (primary), `DefinitionLinks`, `DataSourceLinks`.

**Events:** prefix-display `FieldSelecting` for both link types; duplicate-prefix validation on definition links; `RowSelected` toggles field/button enable on `InProgress` and only enables Download when a file exists.

**Actions:**
- `previewMarkdown` — long op: builds markdown via `SlideGenerationService.BuildMarkdownPreview`, stores it in `PresentationMarkdown`, saves. Lets the user inspect the prompt before spending a Gamma call.
- `generateGamma` — long op: validates title + that a `GammaApiKey` exists. Reuses stored markdown if present, else builds it. Then calls `GammaApiService` — `GeneratePresentationFromTemplate` if `GammaTemplateId` is set, else `GeneratePresentation`. Saves the returned `.pptx` via `FileService`, sets `SlideGeneratedFileID` + `Completed`. Same 15-min timeout/Failed/logout pattern.
- `downloadPresentation`, `resetStatus` — mirror the report screen.

**Helpers:** its own `GetCompanyIDFromDB` / `MapCompanyIDToTenantName` (same shape, against `FLRTPresentationGeneration`).

### 5.3 `FLRTReportDefinitionMaint.cs` — the definition + line-items screen

**Views:** `ReportDefinition` (primary), `LineItems` (child grid, `[PXImport]` enabled for Excel paste, ordered by `SortOrder`).

**Definition events:**
- `RowSelected` — `DefinitionCD` and `DefinitionPrefix` editable only on insert (locking prefix protects existing templates).
- `RowPersisting` — validates: code required; prefix required, **alphanumeric only** (regex `^[A-Za-z0-9]+$`), and unique; code unique.

**Line-item events:**
- `RowSelected` — shows/hides fields by `LineType` (account-range fields only for ACCOUNT, formula only for CALCULATED, etc.).
- `FieldUpdated<…lineType>` — auto-clears now-irrelevant fields when the type changes (e.g. switching to HEADING nulls account range and sets `IsVisible=false`).
- `RowPersisting` — `LineCode` required; ACCOUNT lines need from/to; CALCULATED needs a formula; `LineCode` unique within the definition.

### 5.4 `FLRTGIDataSourceMaint.cs` — the generic-data-source screen

**Views:** `DataSource` (primary), `Columns` (child).

**Events:** mirror the definition screen — lock `DataSourceCD`/`Prefix` after insert, validate prefix alphanumeric + globally unique; per-column field enabling by `LineType`, auto-clear on type change, alias-required + GIColumn/Formula required + alias-unique validation.

**Actions:**
- `detectColumns` — authenticates with tenant creds, calls `FinancialDataService.FetchGIColumns(GIName)`, stores the discovered names into `DetectedColumns`, and shows them in a dialog so the user can map columns to real OData property names.
- `testFetch` — opens a `GITestFetchFilter` dialog (year/month/branch/org/ledger), runs the full `GIDataFetchService.FetchAndAggregate`, and renders the resulting `{{PREFIX_ALIAS}} = value` pairs in a dialog + trace log. Lets the user verify a data source before wiring it into a presentation.

`GITestFetchFilter` is a nested non-persisted (`[PXHidden]`) DAC for the dialog inputs.

### 5.5 `FLRTTenantCredentialsMaint.cs` — credentials screen

Simple maintenance graph. `RowPersisting` requires `CompanyNum` and `TenantName` and enforces unique `TenantName`. `RowPersisted` logs success/failure. A custom `save()` action wraps `Cache.Persist` + `Actions.PressSave` in try/catch with tracing. (Note: this graph does *not* call `CredentialProvider.ClearCache` on save — the cache instead relies on its 10-minute TTL, and report generation clears it on completion.)

---

## 6. Helper layer (`Helper/`)

### 6.1 `Constants.cs`
- `ReportStatus` — the `N/P/C/F` string constants plus BQL `Constant<>` wrappers.
- `Constants` — `TemplateFileFilter = "FRTemplate"` (substring that identifies an uploaded template file), `CurrentYearSuffix = "CY"`, `PreviousYearSuffix = "PY"`, `MaxPlaceholdersPerTemplate = 1000`.

### 6.2 `Messages.cs`
`[PXLocalizable]` static class — every user-facing/error string in one place, grouped by area (auth, OData, file, generation, validation, multi-definition, slide). Format-string messages take `{0}`-style args.

### 6.3 `GIColumnSelectorAttribute.cs`
Contains:
- `FLRTGIColumnItem` — a `[PXVirtual]` projection DAC (one field `ColumnName`) used as the selector's return type.
- `GIResult` — a `[PXHidden]` read-only stub mapping Acumatica's GI-definition table (`DesignID`, `LineNbr`, `ObjectName`, `Field`, `Caption`, `IsVisible`).
- `GIColumnSelectorAttribute` — the dropdown behind every column-mapping field on the Report Definition screen. `GetRecords()` finds the current GI name (from the `FLRTReportDefinition` or `FLRTGIDataSource` cache), then **prefers live OData column names** via `TryGetODataColumns` (uses stored credentials + `FinancialDataService.FetchGIColumns`, cached 5 min per GI), falling back to the `GIResult` table. `ValidateValue=false` lets users type values not in the list.
- `GIDataSourceColumnSelectorAttribute` — sibling selector for the GI Data Source screen; prefers the `DetectedColumns` cache, then `GIResult`.

### 6.4 `GIColumnMapping.cs`
Plain DTO carrying the GI name + all 12 column-name mappings from a definition into the services. `BuildSelectColumns()` joins them into the OData `$select`. `FromDefinition(def)` builds one from a DAC (with sensible defaults / null-coalescing to the stock TrialBalance names).

### 6.5 `RoundingSettings.cs`
DTO with `RoundingLevel` + `DecimalPlaces`; `FromDefinition(def)` factory. Consumed by the engine's formatting step.

### 6.6 `TraceLogger.cs` (in `Services/` folder but `Helper` namespace)
File logger writing to `…\App_Data\Logs\FinancialReports\Overview Trace\`. `Info`/`Error` append timestamped lines. Note: the codebase predominantly uses Acumatica's `PXTrace` instead; `TraceLogger` is a secondary/legacy facility.

---

## 7. Period math (read this before the services)

`FinancialMonth` is the **fiscal-year START month**; `CurrYear` is the **FY end year**. `ReportDataPipeline.BuildContext` derives every period string from these two:

- `fyEndMonth = FinancialMonth − 1` (wraps Jan→Dec).
- Example: `FinancialMonth=Aug, CurrYear=2025` → FY2025 runs **Aug 2024 → Jul 2025**.
- Example: `FinancialMonth=Jan, CurrYear=2025` → FY2025 is the calendar year **Jan→Dec 2025**.

Periods produced (format `MMYYYY`, e.g. `072025`):

| Field | Meaning |
|-------|---------|
| `SelectedPeriod` | FY-end month of CY (point-in-time for Ending/Beginning) |
| `PrevYearPeriod` | FY-end of CY−1 |
| `PrevYearPriorPeriod` | FY-end of CY−2 (source of PY opening balance) |
| `CyFyStartPeriod` | FY-start month of CY (YTD range start) |
| `PyFyStartPeriod` | FY-start of CY−1 |

The opening-balance trick: a fiscal year's **beginning** balance = the prior FY-end's **ending** balance. So CY-opening is fetched from `PrevYearPeriod`, and PY-opening from `PrevYearPriorPeriod`.

---

## 8. Service layer (`Services/`) — the engine

### 8.1 `AuthService.cs` — OAuth client

Wraps the remote tenant's `/identity/connect/token` (OAuth2 password grant) and `/entity/auth/logout`.

- Static shared `HttpClient` (proxy off, 3-min timeout) to avoid socket exhaustion.
- `AuthenticateAndGetTokenAsync()` — fast path returns the cached token if unexpired; otherwise a `SemaphoreSlim(1,1)` ensures only one thread fetches. Tries **refresh-token grant** first, falls back to **password grant**. Buffers expiry by 60 s. `AuthenticateAndGetToken()` is the sync wrapper (`Task.Run(...).GetResult()` to dodge sync-context deadlocks).
- `RefreshAccessTokenAsync` — refresh-token flow.
- `IsAuthenticated`, `Logout`/`LogoutAsync`, `Dispose`.
- Warns (via `PXTrace`) if `BaseURL` is plain HTTP.

### 8.2 `CredentialProvider.cs` — credential cache

`AcumaticaCredentials` POCO + static `CredentialProvider`. `GetCredentials(tenant)` returns from a 10-minute TTL cache, else decrypts the `FLRTTenantCredentials` row (the `[PXRSACryptString]` fields auto-decrypt on read) into the POCO. `ClearCache()` / `ClearCache(tenant)` invalidate it. Logs only non-sensitive confirmations, never credential values.

### 8.3 `FinancialDataService.cs` — trial-balance OData fetch

The GL-specific data layer. Constructed with an `AuthService`, tenant name, and `GIColumnMapping`. Static shared `HttpClient` (5-min timeout, max 10 conns/server). `OEsc()` doubles single-quotes to keep OData filters from breaking on values like `O'Brien`.

Fetch methods:
- `FetchAllApiData(branch, org, ledger, period, includeDetail, token)` — point-in-time fetch for one period. Async core streams rows; a `rowConsumer` callback aggregates per account into `FinancialPeriodData` (begin/end/debit/credit) **and**, when `includeDetail`, keeps raw per-row detail (with sub/branch/org/ledger). Returns `FinancialApiData`.
- `FetchRangeApiData(…fromPeriod, toPeriod…)` — YTD range fetch (`Period ge from and le to`); accumulates debit/credit/ending for the DEBIT/CREDIT/MOVEMENT balance types.
- `FetchCompositeKeyData`, `FetchEndingBalance` — narrower helpers (composite-key map; single precise balance).
- `ValidateGIExists(token)` — **probes the GI with `$top=1` before the parallel fan-out**, so a misconfigured GI name fails fast with a clear message (`GIDataSourceNotFound`) instead of five generic 404s.
- `FetchGIColumns(giName)` / `TryFetchColumnsFromUrl` — grab one row and return its JSON property names (powers Detect Columns + the column selector).

Fetch plumbing (the resilient core):
- `ExecuteFetchWithFallbackAsync` / `…StreamWithFallbackAsync` — try **modern URL** (`/odata/{tenant}/{gi}`) then **legacy URL** (`/t/{tenant}/api/odata/gi/{gi}`), always preserving the ledger filter (a past bug that dropped it on retry silently blended ACTUAL with BUDGET ledgers — explicitly fixed). The streaming variant calls `resetConsumer()` before each retry so aggregation state isn't double-counted.
- `PaginatedFetchAsync` — pages in 10 000-row chunks via `$top`/`$skip` up to a 100 000-row cap; bearer token set **per-request** (`HttpRequestMessage`) so parallel calls never race on shared headers.
- `PaginatedFetchStreamAsync` — same, but streams JSON with `JsonTextReader` and hands one row at a time to the consumer, keeping peak memory to a single page.

DTOs at the bottom of the file:
- `FinancialPeriodData` — account, subaccount, type, dimension IDs, and the four balances.
- `FinancialApiData` — `AccountData` (aggregated), `CompositeKeyData`, `DetailRows` (raw).

### 8.4 `ReportDataPipeline.cs` — shared setup + period math

Static helper that removes duplication between the report and slide paths.

- `Context` — bundles the definition links, definitions-with-line-items, column mapping, and all the period strings from §7.
- `BuildContext(graph, FLRTFinancialReport)` — loads `FLRTReportDefinitionLink` rows (join to definitions), or falls back to the legacy single `DefinitionID`; builds `ReportCalculationEngine.DefinitionLink` objects (id + prefix + rounding); loads each definition's line items; computes periods. There's a parallel overload taking `FLRTPresentationGeneration` that reads `FLRTPresentationDefinitionLink` instead.
- `FetchAndCalculate(ctx, …)` — the **simple** pipeline used by slides: fires five parallel fetches (CY, PY, Prior point-in-time + CY/PY YTD ranges), waits, then runs `ReportCalculationEngine.CalculateAll`. (The report path doesn't use this — it does its own conditional fetch; see next.)

### 8.5 `ReportGenerationService.cs` — Word orchestration

`Execute(token)` is the end-to-end Word flow:
1. Resolve tenant; `ReportDataPipeline.BuildContext`; build a `FinancialDataService`.
2. `ValidateGIExists` (fail fast).
3. Pull the template `.docx` from Acumatica (`FileService`), write it to a temp path, free the bytes.
4. **Scan line items to decide which fetches are needed:** `needsDetail` (any per-line dimension filter) and `needsCumulative` (any DEBIT/CREDIT/MOVEMENT balance type). This avoids unnecessary API calls.
5. Reuse period strings from the context.
6. **Parallel fetch, gated by `SemaphoreSlim(3)`** to cap peak memory at ~3 concurrent result sets: CY, PY, Prior, and (only if `needsCumulative`) CY-range + PY-range. YTD tasks become `Task.FromResult(null)` when not needed.
7. Run `ReportCalculationEngine.CalculateAll`, mapping the opening/cumulative arguments precisely (CY-opening = PY data, PY-opening = Prior data).
8. Add `CY`/`PY` year constants. Null out the big API objects to free memory.
9. `WordTemplateService.PopulateTemplate` → read the output bytes → `FileService.SaveGeneratedDocument` → return the `FileID`.
10. `finally`: delete temp files, `CredentialProvider.ClearCache()`, dispose the semaphore. Times the whole thing via `Stopwatch`.

### 8.6 `ReportCalculationEngine.cs` — the calculator

Stateless per run (new instance each time). Two global dicts `_cyGlobal`/`_pyGlobal` keyed by `PREFIX_LINECODE` hold computed decimals; a compiled `FormulaTokenRegex` tokenizes formulas.

**`CalculateAll(links, cyData, pyData, cyOpeningData, pyOpeningData, cyCumulativeData, pyCumulativeData)`** is the entry point:
1. Collect known prefixes (for formula token resolution).
2. `LoadAllLineItems` — flatten every definition's lines into `LineNode`s (each remembers its prefix, definitionID, rounding; `GlobalKey = PREFIX_LINECODE`).
3. `BuildChildrenMap` — parent→children index (keyed `DefinitionID_PARENTCODE`), built **once** and shared by both dependency analysis and subtotal summation (no per-subtotal DB queries).
4. `BuildAndSort` — build a dependency graph and topologically sort with **Kahn's algorithm**. Dependencies: SUBTOTAL depends on its children; CALCULATED depends on the line codes its formula references; ACCOUNT/HEADING depend on nothing. Detects **duplicate global keys** and **circular dependencies**, throwing clear `Messages.*` errors. HEADINGs and unreferenced nodes are appended last.
5. Walk nodes in sorted order, computing CY and PY values per `LineType`:
   - **ACCOUNT** → `CalculateAccountLine`.
   - **SUBTOTAL** → `CalculateSubtotal` (sum children from the global dict).
   - **CALCULATED** → `EvaluateFormula`.
6. `BuildPlaceholderMap` — emit `PREFIX_LINECODE_CY/PY` formatted strings (empty for HEADING/invisible lines).

**`Calculate(definitionID, cy, py)`** — legacy single-definition shim; derives a prefix and delegates to `CalculateAll`.

Key internals:
- `CalculateAccountLine` — if the line has dimension filters and detail rows exist, defer to `CalculateAccountLineFromDetail`; otherwise iterate `AccountData`. For BEGINNING, read the **opening** dataset's EndingBalance; for DEBIT/CREDIT/MOVEMENT, read the **cumulative** (YTD) dataset; else the point-in-time period. Applies `ApplyAccountTypeSign` then the line's `SignRule`.
- `CalculateAccountLineFromDetail` — same logic per raw row, honoring sub/branch/org/ledger filters; builds an O(1) `BuildDetailIndex` for opening lookups; logs sample rows when nothing matches.
- `ApplyAccountTypeSign` — Liability/Income negated, Asset/Expense as-is (normalizes GL's natural signs).
- `GetBalanceByType` — maps the `BalanceType` enum to the right `FinancialPeriodData` field (`Movement = Debit − Credit`).
- `EvaluateFormula` / `Tokenize` / `Parse{Expression,Term,Factor}` — a recursive-descent arithmetic evaluator (`+ - * /`, parentheses, unary minus) with correct precedence. `ResolveToken` distinguishes **explicit** cross-definition refs (`BS_TOTAL_ASSETS`, token starts with a known prefix — longest-prefix-first to avoid `PL` shadowing `PLS`) from **implicit** own-definition refs (`TOTAL_ASSETS` → `CURRENTPREFIX_TOTAL_ASSETS`). An unknown line code **throws** rather than defaulting to 0 — a silent zero in a financial report is worse than a hard failure. Division by zero logs and yields 0.
- `IsAccountInRange` / `CompareAccountCodes` — natural/segmented account-code comparison (numeric segments compared as numbers, handles dashed codes like `10100-01`).
- `FormatFinancialValue` / `ApplyRounding` / `BuildFormatString` — scale by Units/Thousands/Millions, round away-from-zero, format with thousands separators; zero renders as `-`, negatives as `(1,234)`.

### 8.7 `GIDataFetchService.cs` — generic-GI fetch + aggregate

The engine for GI Data Sources (presentations). Static shared `HttpClient`.

**`FetchAndAggregate(ds, columns, year, month, branch, org, ledger, token)`** is the whole pipeline:
1. Authenticate; `BuildHeaderFilter` (period/branch/org/ledger predicates, typed per column).
2. Build `$select` from VALUE columns + any columns referenced by their `RowFilter` + the key column — **unless** a MULTIROW column is present, in which case all columns are fetched.
3. Fetch with a **4-way fallback** (modern/legacy URL × with/without `$select`), capturing errors as strings to log on the main thread.
4. **VALUE columns** → `FilterRowsByKey` (range on key column) → `ApplyRowFilter` (client-side `eq/ne/gt/lt/ge/le/contains`) → `AggregateColumn`.
5. **CALCULATED columns** → `TopoSortCalculated` (Kahn's again, so cross-references work regardless of SortOrder; circular = throw) → `EvaluateFormula`.
6. **MULTIROW columns** → key/row filter → sort by `OrderByColumn` (numeric/date/string aware via `GetSortValue`) → take top `RowLimit` → emit `ALIAS_rank_PROPERTY` keys (optionally narrowed by `DisplayColumns`).
7. Format every visible result with `FormatValue` and prefix with the data source's `Prefix` → `{PREFIX_ALIAS}` (or expanded multi-row keys).

Aggregation helpers — `AggregateNumeric` (Sum/Max/Min/Avg/First), `AggregateBoolean` (Sum=count-of-true), `AggregateDate` (Min/Max/First), `AggregateString` (First/Count/Max/Min), `GetDefaultValue`. Filter builders — `BuildTypedFilter` (per-type OData `eq`, with quote-escaping for strings and `datetime'…'` for dates), `BuildDateRangeFilter` (Monthly/Yearly `ge…lt`). Formula evaluator — a second recursive-descent arithmetic parser (`ParseExpression/Term/Factor`, `SkipSpaces`), simpler than the engine's because tokens are pre-substituted with numeric values. `PaginatedFetchAsync` — same paging/cap shape as `FinancialDataService`, but accumulates per-call error strings.

### 8.8 `MarkdownBuilderService.cs` — prompt builder

Turns computed data into the markdown prompt for Gamma. Two `Build` overloads (one for `FLRTFinancialReport`, one for `FLRTPresentationGeneration` with optional GI data sources). It assembles: a title + **Report Context** (period/org/branch/ledger), an **Your Task** instruction block (audience, tone, recommended slide flow, formatting rules — basically a CFO-presentation system prompt), a **Data** section, and a footer.

- `DeriveLabels` — builds `FY{year} ({Month} {year})` CY/PY labels.
- `AppendTBLineItems` — for each visible trial-balance line, a small `Period | Value` table with CY and PY rows, keyed `PREFIX_LINECODE_CY/PY`.
- `AppendGIDataSources` — per data source, bullet each visible column; MULTIROW columns expand into per-rank bullets pulled from the `ALIAS_rank_PROPERTY` keys.
- `GetValue` — missing/empty → `"0"`.

### 8.9 `GammaApiService.cs` — slide API client

Calls `public-api.gamma.app/v1.0`. `GammaGenerationOptions` carries defaults (12 cards, executive tone, etc.).

- `GeneratePresentation(markdown, title, token, options)` — `SubmitGeneration` (POST `/generations`, `exportAs:"pptx"`, CFO-level `additionalInstructions`) → `PollUntilCompleted` → `DownloadFile`.
- `GeneratePresentationFromTemplate(markdown, gammaTemplateId, token)` — `SubmitGenerationFromTemplate` (POST `/generations/from-template`) → same poll/download.
- `CreateRequest` — sets `X-API-KEY` **per request** (never mutates the shared client).
- `PollUntilCompleted` — polls GET `/generations/{id}` every 5 s up to 60 times (5 min). 5xx = transient, retry; 4xx = fail immediately; `completed` returns `exportUrl`; `failed` throws. The `exportUrl` (the only auth on the file) is deliberately never logged.
- `DownloadFile` — GETs the pre-signed export URL (no key header) → bytes.

### 8.10 `SlideGenerationService.cs` — presentation orchestration

`BuildMarkdownPreview(token)`:
1. `ReportDataPipeline.BuildContext` for the presentation record.
2. Load `FLRTPresentationDataSourceLink` rows. Require at least one definition or one data source.
3. If definitions exist: validate visible lines have descriptions (`VisibleLineItemsMissingDescriptions`), then `ReportDataPipeline.FetchAndCalculate` for the trial-balance numbers.
4. If data sources exist: for each active one, load its columns and call `GIDataFetchService.FetchAndAggregate`, merging results.
5. `MarkdownBuilderService.Build(...)` → store on `LastGeneratedMarkdown` (the graph then persists it / hands it to Gamma).

### 8.11 `FileService.cs` — Acumatica file storage

- `GetFileContentAndName(noteID, record)` — finds the latest uploaded file on the record whose name matches the `FRTemplate` filter (join `UploadFile`+`NoteDoc`), reads the newest `UploadFileRevision.BlobData`, returns (bytes, name). Throws if none usable.
- `SaveGeneratedDocument(name, bytes, record)` — two overloads (report / presentation). Saves via `UploadFileMaintenance.SaveFile` (public), attaches the file to the record's note (`PXNoteAttribute.SetFileNotes`), inserts a `NoteDoc` link if absent, returns the file `UID`.

---

## 9. End-to-end: generating one Word report

1. User opens **FR101000**, picks a template-name record (with a `.docx` containing `{{…}}` placeholders attached), sets Year/Month/Branch/Org/Ledger, adds one or more **Report Definitions** on the links grid, clicks **Generate Report**.
2. `generateReport` validates, resolves the tenant via `CompanyID → FLRTTenantCredentials`, builds an `AuthService`, marks the row `InProgress`, and starts a 15-min background long-operation.
3. `ReportGenerationService.Execute`: build context (definitions + line items + FY periods), validate the GI exists, fetch the template, decide which API calls are needed, fan out CY/PY/Prior/(YTD) OData fetches gated to 3 at a time.
4. `ReportCalculationEngine.CalculateAll`: topologically sort all lines across all definitions, compute account ranges (sign-normalized), subtotals, and formulas (including cross-definition refs), and emit `{ PREFIX_LINECODE_CY/PY : formatted }` plus `CY`/`PY` constants.
5. `WordTemplateService.PopulateTemplate`: extract placeholders, default unknowns to `0`, merge split runs, replace `{{…}}` in body/headers/footers, flag "update fields on open", save.
6. `FileService.SaveGeneratedDocument` stores the output; the row flips to `Completed` with a `GeneratedFileID`; **Download Report** streams it.

The presentation flow (FR101003) is the same up through calculation, then branches to `MarkdownBuilderService` → `GammaApiService` → `.pptx` instead of the Word template.

---

## 10. Cross-cutting design notes

- **Multi-tenant by design.** The customization can run in one tenant but fetch data from another; everything routes through `FLRTTenantCredentials` keyed by `CompanyNum`, with encrypted secrets and a TTL'd credential cache.
- **Resilient OData.** Every fetch tries modern then legacy URL forms, pages large result sets, caps rows, escapes quotes, and sets auth per-request for safe parallelism.
- **Memory discipline.** Streaming JSON parse, a semaphore-gated fan-out, and explicit nulling of large objects keep peak memory bounded for big trial balances.
- **Fail loud, not silent.** Missing definitions, unknown formula codes, circular dependencies, duplicate keys, and missing GIs all raise clear errors — because a plausible-looking report full of wrong/zero numbers is the worst outcome.
- **Config-as-data.** Report structure, column mappings, rounding, and GI data sources are all editable in the UI; code changes are only needed for genuinely new behavior.
