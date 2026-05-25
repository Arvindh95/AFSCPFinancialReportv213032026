# Project Handover — AFSCP Financial Report (Code Guide)

**Audience:** the developer taking over this customization.
**Scope of this doc:** *how the code works* — every DAC, graph, service, and the functions inside them. It does **not** cover build/deploy/ops/config; for those and for end-user setup see the `Documentation/` folder.

---

## 1. What this is

An Acumatica ERP 2025 R2 customization (assembly **`FinancialReport.dll`**, namespace `FinancialReport`) that ships **two products on one shared engine**:

- **AFS Financial Report** — a **Word** statement (`.docx`). An accountant defines a statement's structure (which GL accounts roll into which line, sign rules, subtotals, formulas, rounding) on **FR101002 Report Definition**, then **FR101000 AFS Financial Report** pulls trial-balance data over OData, computes every line, and stamps the numbers into a `.docx` template's `{{…}}` placeholders.
- **MBR — Monthly Board Report** — an **AI-generated PowerPoint** deck (`.pptx`, via the Gamma API). A user defines **MBR Definitions** on **FR101004 (AFS MBR Config)** that pull *any* Generic Inquiry's columns as named values, then **FR101003 (AFS Monthly Board Report)** resolves them (and, optionally, Report Definitions) for the chosen period, builds a structured markdown prompt, and sends it to Gamma to produce the deck.

The defining idea behind both is **config-as-data**: structure lives as *rows in DB tables* edited through maintenance screens, not as code. The customization always authenticates **outbound** to an Acumatica tenant and reads over OData. No recompile is needed when the chart of accounts, report layout, or GI selection changes.

### Two products, one engine

| | **AFS Financial Report** | **MBR — Monthly Board Report** |
|---|---|---|
| Output | Word `.docx` | PowerPoint `.pptx` (Gamma) |
| Define on | FR101002 — Report Definition | FR101004 — *AFS MBR Config* (MBR Definition) |
| Generate on | FR101000 — *AFS Financial Report* | FR101003 — *AFS Monthly Board Report* |
| Data source | GL Trial-Balance GI (fixed concepts: Account/Ending/Debit/…) | **Any** Generic Inquiry, any columns (generic) |
| Header DAC | `FLRTFinancialReport` | `FLRTPresentationGeneration` |
| Definition DAC | `FLRTReportDefinition` + `FLRTReportLineItem` | `FLRTGIDataSource` + `FLRTGIDataSourceColumn` |
| Fetch service | `FinancialDataService` | `GIDataFetchService` (+ `FinancialDataService` for any linked Report Definitions) |
| Render service | `WordTemplateService` | `MarkdownBuilderService` → `GammaApiService` |
| Placeholders | `{{PREFIX_LINECODE_CY/PY}}` | `{{PREFIX_ALIAS}}`, `{{PREFIX_ALIAS_N_GICOL}}` |

> **Code-name ≠ screen-name.** The code predates the "MBR" branding, so the presentation classes keep generic names: `FLRTPresentationGeneration` / `FLRTFinancialPresentationMaint` **is** the MBR report (FR101003), and `FLRTGIDataSource` / `FLRTGIDataSourceMaint` **is** the MBR Definition / *AFS MBR Config* (FR101004). MBR can link **both** MBR Definitions (GI data) and Report Definitions (trial-balance data) on one deck.

### Data flow — AFS Financial Report (Word)

```
FR101002 define ─► FR101000 generate ─► .docx     (PXLongOperation, 15-min cap)

  ReportDataPipeline.BuildContext   → definitions + line items + FY periods
        │
  AuthService ──OAuth──► tenant /identity/connect/token
        │
  FinancialDataService ──OData──► Trial-Balance GI
        │     → FinancialApiData (accounts × balances)
  ReportCalculationEngine.CalculateAll
        │     → topo-sort, sum ranges, subtotals, formulas
        │     → { "BS_TOTAL_ASSETS_CY" : "1,234,567", ... }
  WordTemplateService.PopulateTemplate   → fill {{...}} in the .docx
        │
  FileService.SaveGeneratedDocument → Acumatica file storage → Download
```

### Data flow — MBR / Monthly Board Report (Gamma)

```
FR101004 define ─► FR101003 generate ─► .pptx     (PXLongOperation, 15-min cap)

  ReportDataPipeline.BuildContext   → linked definitions + FY periods
        │
  AuthService ──OAuth──► tenant
        │
  ├─ MBR Definitions (GI Data Sources) ─► GIDataFetchService.FetchAndAggregate
  │        ──OData──► any GI → {{PREFIX_ALIAS}} / {{PREFIX_ALIAS_N_GICOL}}
  │
  └─ (optional) Report Definitions ─► FinancialDataService
           → ReportCalculationEngine.CalculateAll → {{PREFIX_LINECODE_CY/PY}}
        │
  MarkdownBuilderService.Build   → structured markdown prompt (title, context, data)
        │
  GammaApiService ──REST──► public-api.gamma.app → poll → .pptx
        │
  FileService.SaveGeneratedDocument → Acumatica file storage → Download
```

### Naming & placeholder conventions

| Convention | Form |
|------------|------|
| DB-backed class prefix | `FLRT` (FinanciaL RepoRT) |
| Screen IDs | `FR1010xx` |
| Trial-balance placeholders | `{{PREFIX_LINECODE_CY}}` / `{{PREFIX_LINECODE_PY}}` |
| Year constants | `{{CY}}` / `{{PY}}` |
| GI-data-source placeholders | `{{PREFIX_ALIAS}}` |
| MULTIROW (rank-expanded) | `{{PREFIX_ALIAS_N_GICOL}}` |

---

## 2. Architecture: three layers

The code maps onto Acumatica's MVC-ish framework:

| Layer | Folder | Role |
|-------|--------|------|
| **DAC** | `DAC/` | The model — one C# class per DB table, schema declared via field attributes. |
| **Graph** | `Graph/` | The controller — one per maintenance screen. Views, events, action buttons. Runs on the UI / long-operation thread. |
| **Service** | `Services/` | The engine — plain C# business logic (auth, OData fetch, calculation, file/template/API), framework-agnostic where possible, called from graphs. |
| **Helper** | `Helper/` | Constants, messages, DTOs, custom selectors. |

The `Pages/FR/*.aspx.cs` files are **empty `PXPage` stubs** — all UI binding is declarative in the `.aspx` markup, and **all behavior lives in the graph**. Graphs themselves are thin: they validate and orchestrate, then hand the heavy lifting to `Services/` inside a `PXLongOperation` background task.

---

## 3. Screens → Graphs → primary DAC

| Screen | Screen title (UI) | Product | Graph | Primary DAC |
|--------|-------------------|---------|-------|-------------|
| FR101001 | Tenant Credentials | Shared | `FLRTTenantCredentialsMaint` | `FLRTTenantCredentials` |
| FR101002 | Report Definition | AFS | `FLRTReportDefinitionMaint` | `FLRTReportDefinition` |
| FR101000 | AFS Financial Report | AFS | `FLRTFinancialReportMaint` | `FLRTFinancialReport` |
| FR101004 | AFS MBR Config (MBR Definition) | MBR | `FLRTGIDataSourceMaint` | `FLRTGIDataSource` |
| FR101003 | AFS Monthly Board Report (MBR) | MBR | `FLRTFinancialPresentationMaint` | `FLRTPresentationGeneration` |

FR101001 feeds both products (outbound OAuth + Gamma key). FR101002/FR101000 are the **AFS** Word path; FR101004/FR101003 are the **MBR** presentation path — though an MBR deck may also link FR101002 Report Definitions to mix trial-balance numbers into the slides.

---

## 4. DAC layer (`DAC/`) — the tables

Each DAC declares schema via field attributes. The repeating per-field pattern is: a `[PXDB*]` attribute (column type), `[PXUIField]` (label/visibility), optional `[PXDefault]`/`[PXSelector]`/`[PXStringList]`, the property, and a matching `abstract class xxx : BqlField` for BQL queries. Every table also carries the standard audit block (`CreatedByID`, `LastModifiedDateTime`, `Tstamp`, …), omitted from the tables below.

### 4.1 `FLRTFinancialReport` — the Word-report header
One row = one report-generation job.

| Field | Purpose |
|-------|---------|
| `ReportID` | Identity, PK. |
| `ReportCD` | "Template Name". |
| `Description` | Free text. |
| `CurrYear` | Selector over distinct `FinPeriod.finYear`, descending. |
| `Branch`, `Organization`, `Ledger` | Selectors over GL master tables; become the OData dimension filters. |
| `FinancialMonth` | `01`–`12` string list, **interpreted as the fiscal-year START month**, default `12` (see §6 period math — the single most confusing field). |
| `DefinitionID` | **Legacy** single-definition link, `Visible=false`. Superseded by `FLRTReportDefinitionLink`; ignored when link rows exist, kept only for old records. |
| `GeneratedFileID` / `UploadedFileID` / `UploadedFileIDDisplay` | GUIDs for the produced file and the attached template. |
| `Status` | 1-char `N`/`P`/`C`/`F` (Pending/InProgress/Completed/Failed, `ReportStatus` constants); read-only; drives button enable state. |

### 4.2 `FLRTReportDefinition` — the reusable report blueprint
One row = a statement structure (Balance Sheet, P&L, …) reusable across many reports.

| Field | Purpose |
|-------|---------|
| `DefinitionID` | Identity. |
| `DefinitionCD` | Key code. |
| `Description` | Free text. |
| `DefinitionPrefix` | 2–10 alphanumeric chars. **The namespace for placeholders** (`BS` → `{{BS_*}}`). Unique; locked after first save. |
| `ReportType` | `BS`/`PL`/`CF`/`CU` (informational). |
| `GIName` | Which Generic Inquiry supplies trial-balance data (default `TrialBalance`); selector over `GIDesign.name`. |
| **Column-mapping block** | `AccountColumn`, `TypeColumn`, `BeginningBalColumn`, `EndingBalColumn`, `DebitColumn`, `CreditColumn`, `MovementColumn`, `PeriodColumn`, `SubaccountColumn`, `BranchColumn`, `OrganizationColumn`, `LedgerColumn` — map this definition's logical fields to the *actual OData column names* in the chosen GI. Each uses the `[GIColumnSelector]` attribute (§5.3). This is what makes the engine GI-agnostic. |
| **Rounding block** | `RoundingLevel` (`UNITS`/`THOUS`/`MILL`) and `DecimalPlaces` (0/1/2). |

Nested constants: `ReportDefinitionType`, `RoundingLevelType`.

### 4.3 `FLRTReportLineItem` — a single report line (child of definition)
The heart of report config. `[PXParent]` ties each line to its `FLRTReportDefinition`. One row = one statement line.

| Field | Purpose |
|-------|---------|
| `LineCode` | Unique within the definition; becomes the placeholder middle segment. |
| `LineType` | The engine's dispatch switch (see table below). |
| `AccountFrom` / `AccountTo` | Inclusive account-code range (ACCOUNT lines). |
| `AccountTypeFilter` | Optional `A`/`L`/`E`/`I` restriction. |
| `SignRule` | `ASIS` keeps raw GL sign; `FLIP` multiplies by −1. |
| `BalanceType` | `ENDING`/`BEGINNING`/`DEBIT`/`CREDIT`/`MOVEMENT`. The last three are full-fiscal-year YTD figures and trigger the cumulative range fetch. |
| `ParentLineCode` | Groups this line under a SUBTOTAL. |
| `Formula` | Arithmetic expression for CALCULATED lines (e.g. `REVENUE - TOTAL_EXPENSES`). |
| `IsVisible` | If false, the line is still computed (usable in formulas/subtotals) but its placeholder resolves to empty. |
| `SubaccountFilter`, `BranchFilter`, `OrganizationFilter`, `LedgerFilter` | Per-line dimension filters. **Setting any of these forces the engine onto the detail-row code path** (§7.6) instead of pre-aggregated data. |

`LineType` values:

| Value | Behavior |
|-------|----------|
| `ACCOUNT` | Sum all GL accounts in `AccountFrom`…`AccountTo`. |
| `SUBTOTAL` | Sum all lines whose `ParentLineCode` equals this line's `LineCode`. |
| `CALCULATED` | Evaluate `Formula` referencing other line codes. |
| `HEADING` | Display-only label, no value. |

Nested constants: `LineItemType`, `SignRuleValue`, `BalanceTypeValue`, `AccountTypeValue`.

### 4.4 `FLRTReportDefinitionLink` — many definitions per report
Child of `FLRTFinancialReport`. Each row links one `FLRTReportDefinition` (by `DefinitionID`) to a report, carrying its `DefinitionPrefix` (display-only, set by a `FieldSelecting` event) and a `DisplayOrder`. Multiple links enable **cross-definition formulas** (a CF line referencing `BS_RETAINED_EARNINGS`). `DisplayOrder` affects grid display only — calculation order comes from topological sort.

### 4.5 `FLRTGIDataSource` — generic (non-trial-balance) data source · **the MBR Definition**
This is the header behind **FR101004 (AFS MBR Config)** — what the UI/docs call an **MBR Definition**. Where `FLRTReportDefinition` is GL-specific, this is fully generic: point it at *any* GI, pull *any* columns as placeholders, for MBR presentations.

| Field | Purpose |
|-------|---------|
| `DataSourceCD` | Key. |
| `Prefix` | Placeholder namespace. |
| `Description`, `IsActive`, `GIName` | Description, active flag, source GI. |
| `KeyColumn` | The row-identifier column (like Account), used for `KeyFrom`/`KeyTo` range filtering by child columns. |
| **Filter-column mapping** | `PeriodFilterColumn` + `PeriodFilterType` + `PeriodFilterTemplate` + `PeriodScope`, plus Branch/Org/Ledger filter columns each with a type. The template substitutes `{YEAR}`/`{MONTH}` tokens (`{MONTH}{YEAR}` → `012025`). `PeriodScope` (`Exact`/`Monthly`/`Yearly`) controls whether Date columns use `eq` or a `ge…lt` range. |
| `DetectedColumns` | Comma-separated cache of real OData column names, filled by the **Detect Columns** action. |

Nested constants: `PeriodScopeType`, `GIColumnType`.

### 4.6 `FLRTGIDataSourceColumn` — one output value of a GI data source (child)
`[PXParent]` to `FLRTGIDataSource`. Produces `{{PREFIX_ALIAS}}`.

| Field | Purpose |
|-------|---------|
| `ColumnAlias` | Placeholder key. |
| `Description`, `SortOrder` | Label and ordering. |
| `LineType` | `VALUE` / `MULTIROW` / `CALCULATED` / `HEADING` (see table below). |
| `GIColumn` | (VALUE) source GI column. |
| `ColumnType` | (VALUE) Decimal/Integer/Boolean/Date/String. |
| `AggregateFunction` | (VALUE) Sum/First/Max/Min/Avg/Count. |
| `KeyFrom` / `KeyTo` | (VALUE) range against parent `KeyColumn`. |
| `RowFilter` | (VALUE) extra OData-style predicate applied client-side. |
| `Formula` | (CALCULATED) formula over other aliases. |
| `OrderByColumn`, `OrderByDirection`, `RowLimit`, `DisplayColumns` | (MULTIROW) sort, top-N, and markdown-only column subset. |
| `FormatString` | .NET format string. |
| `IsVisible` | Output visibility. |

`LineType` values:

| Value | Behavior |
|-------|----------|
| `VALUE` | Read + aggregate a GI column. |
| `MULTIROW` | Expand top-N rows into `PREFIX_ALIAS_N_GICOL`. |
| `CALCULATED` | Formula over other aliases. |
| `HEADING` | Display-only. |

Nested constants: `ColumnLineType`, `OrderByDirectionType`, `AggregateFunctionType`.

### 4.7 `FLRTPresentationGeneration` — the presentation header · **the MBR report**
The header behind **FR101003 (AFS Monthly Board Report)** — one row = one MBR generation run. Mirror of `FLRTFinancialReport` for the slide path. Adds:

| Field | Purpose |
|-------|---------|
| `PresentationTitle`, `PresentationDescription` | Fed to Gamma. |
| `GammaTemplateId` | Optional Gamma template id; if set, generation uses the from-template endpoint. |
| `PresentationMarkdown` | `PXDBText`; the generated/previewable prompt, cached on the record so **Generate** can reuse **Preview** output. |
| `SlideStatus` | `N`/`P`/`C`/`F`. |
| `SlideGeneratedFileID` | GUID of the produced `.pptx`. |

### 4.8 `FLRTPresentationDefinitionLink` & `FLRTPresentationDataSourceLink`
Children of `FLRTPresentationGeneration`. The former links Report Definitions (trial-balance data); the latter links GI Data Sources (generic data). Both carry a display-only prefix and `DisplayOrder`. The data-source-link selector restricts to `IsActive = true`.

### 4.9 `FLRTTenantCredentials` — per-tenant secrets
Keyed by `CompanyNum`. This is how the customization authenticates *outbound* to the target tenant's OData/REST endpoints and to Gamma. The encrypted fields auto-decrypt on read.

| Field | Purpose |
|-------|---------|
| `CompanyNum` | Key (the tenant discriminator). |
| `TenantName`, `BaseURL` | Target tenant name + base URL. |
| `UsernameNew`, `PasswordNew`, `ClientIDNew`, `ClientSecretNew` | **Encrypted** (`[PXRSACryptString]`) OAuth credentials. |
| `GammaApiKey` | **Encrypted** Gamma API key. |

### 4.10 `GLHistoryEnqFilter` — a cache extension
`GLHistoryEnqFilterExt : PXCacheExtension<GLHistoryEnqFilter>` adds an unbound `UsrSumEndingBalance` decimal to Acumatica's stock GL History Inquiry filter. Standalone helper, not part of the generation flow.

---

## 5. Helper layer (`Helper/`)

| File | Contents |
|------|----------|
| `Constants.cs` | `ReportStatus` (the `N`/`P`/`C`/`F` constants + BQL `Constant<>` wrappers); `Constants` (`TemplateFileFilter = "FRTemplate"`, `CurrentYearSuffix = "CY"`, `PreviousYearSuffix = "PY"`). |
| `Messages.cs` | `[PXLocalizable]` static class — every user-facing/error string in one place, grouped by area (auth, OData, file, generation, validation, multi-definition, slide). Format messages take `{0}`-style args. |
| `GIColumnMapping.cs` | Plain DTO carrying the GI name + all 12 column-name mappings. `BuildSelectColumns()` joins them into the OData `$select`. `FromDefinition(def)` builds one from a DAC (null-coalescing to stock TrialBalance names). |
| `RoundingSettings.cs` | DTO with `RoundingLevel` + `DecimalPlaces`; `FromDefinition(def)` factory. Consumed by the engine's formatting step. |

### 5.3 `GIColumnSelectorAttribute.cs`
The column-name dropdowns. Contains:

| Member | Role |
|--------|------|
| `FLRTGIColumnItem` | A `[PXVirtual]` projection DAC (single field `ColumnName`) used as the selector's return type. |
| `GIResult` | A `[PXHidden]` read-only stub mapping Acumatica's GI-definition table (`DesignID`, `LineNbr`, `ObjectName`, `Field`, `Caption`, `IsVisible`). |
| `GIColumnSelectorAttribute` | The dropdown behind every column-mapping field on Report Definition. `GetRecords()` finds the current GI name, then **prefers live OData column names** via `TryGetODataColumns` (stored credentials + `FinancialDataService.FetchGIColumns`, cached 5 min per GI), falling back to the `GIResult` table. `ValidateValue=false` lets users type values not in the list. |
| `GIDataSourceColumnSelectorAttribute` | Sibling selector for GI Data Source; prefers the `DetectedColumns` cache, then `GIResult`. |

---

## 6. Period math (read before the services)

`FinancialMonth` is the **fiscal-year START month**; `CurrYear` is the **FY end year**. `ReportDataPipeline.BuildContext` derives every period string from these two:
- `fyEndMonth = FinancialMonth − 1` (wraps Jan→Dec).
- `FinancialMonth=Aug, CurrYear=2025` → FY2025 runs **Aug 2024 → Jul 2025**.
- `FinancialMonth=Jan, CurrYear=2025` → FY2025 is calendar **Jan→Dec 2025**.

Periods produced (format `MMYYYY`, e.g. `072025`):

| Field | Meaning |
|-------|---------|
| `SelectedPeriod` | FY-end month of CY (point-in-time for Ending/Beginning) |
| `PrevYearPeriod` | FY-end of CY−1 |
| `PrevYearPriorPeriod` | FY-end of CY−2 (source of PY opening balance) |
| `CyFyStartPeriod` | FY-start month of CY (YTD range start) |
| `PyFyStartPeriod` | FY-start of CY−1 |

The opening-balance trick: a fiscal year's **beginning** balance = the prior FY-end's **ending** balance. So CY-opening is fetched from `PrevYearPeriod`, PY-opening from `PrevYearPriorPeriod`.

---

## 7. Service layer (`Services/`) — the engine, function by function

### 7.1 `AuthService.cs` — OAuth client
Wraps the remote tenant's `/identity/connect/token` (OAuth2 password grant) and `/entity/auth/logout`. Static shared `HttpClient` (proxy off, 3-min timeout) to avoid socket exhaustion. Warns via `PXTrace` if `BaseURL` is plain HTTP.

| Member | What it does |
|--------|--------------|
| `AuthenticateAndGetTokenAsync()` | Fast path returns the cached token if unexpired; otherwise a `SemaphoreSlim(1,1)` ensures only one thread fetches. Tries **refresh-token grant** first, falls back to **password grant**. Buffers expiry by 60 s. |
| `AuthenticateAndGetToken()` | Sync wrapper (`Task.Run(...).GetResult()` to dodge sync-context deadlocks). |
| `RefreshAccessTokenAsync` | Refresh-token flow. |
| `IsAuthenticated`, `Logout` / `LogoutAsync`, `Dispose` | Session state + teardown. |

### 7.2 `CredentialProvider.cs` — credential cache
`AcumaticaCredentials` POCO + static `CredentialProvider`. Logs only non-sensitive confirmations, never credential values.

| Member | What it does |
|--------|--------------|
| `GetCredentials(tenant)` | Returns from a **10-minute TTL cache**, else decrypts the `FLRTTenantCredentials` row into the POCO. |
| `ClearCache()` / `ClearCache(tenant)` | Invalidate the cache. |

### 7.3 `FinancialDataService.cs` — trial-balance OData fetch
The GL-specific data layer. Constructed with an `AuthService`, tenant name, and `GIColumnMapping`. Static shared `HttpClient` (5-min timeout, max 10 conns/server). `OEsc()` doubles single-quotes so OData filters survive values like `O'Brien`.

Public methods:

| Method | What it does |
|--------|--------------|
| `FetchAllApiData(branch, org, ledger, period, includeDetail, token)` | Point-in-time fetch for one period. Streams rows; a `rowConsumer` callback aggregates per account into `FinancialPeriodData` (begin/end/debit/credit) **and**, when `includeDetail`, keeps raw per-row detail (sub/branch/org/ledger). Returns `FinancialApiData`. |
| `FetchRangeApiData(…fromPeriod, toPeriod…)` | YTD range fetch (`Period ge from and le to`); accumulates debit/credit/ending for the DEBIT/CREDIT/MOVEMENT balance types, and populates `AccountType`/`DetailRows` so YTD sign-flips work. |
| `ValidateGIExists(token)` | **Probes the GI with `$top=1` before the parallel fan-out**, so a wrong GI name fails fast with `GIDataSourceNotFound` instead of five generic 404s. |
| `FetchGIColumns(giName)` | Grabs one row and returns its JSON property names (powers Detect Columns + the column selector); delegates to `TryFetchColumnsFromUrl`. |

Fetch plumbing (private, streaming-only):

| Method | What it does |
|--------|--------------|
| `ExecuteFetchStreamWithFallbackAsync` | Tries **modern URL** (`/odata/{tenant}/{gi}`) then **legacy URL** (`/t/{tenant}/api/odata/gi/{gi}`), **always preserving the ledger filter** across retries (a past bug dropped it and silently blended ACTUAL with BUDGET). Calls `resetConsumer()` before each retry so aggregation state isn't double-counted. |
| `PaginatedFetchStreamAsync` | Pages in 10 000-row chunks via `$top`/`$skip` up to a 100 000-row cap, streaming JSON with `JsonTextReader` and handing one row at a time to the consumer (peak memory = a single page). Bearer token set **per-request** so parallel calls never race on shared headers. |

DTOs (bottom of file):

| DTO | Contents |
|-----|----------|
| `FinancialPeriodData` | Account, subaccount, type, dimension IDs, and the four balances. |
| `FinancialApiData` | `AccountData` (aggregated) + `DetailRows` (raw). |

### 7.4 `ReportDataPipeline.cs` — shared setup + period math
Static helper removing duplication between the report and slide paths.

| Member | What it does |
|--------|--------------|
| `Context` | Bundles definition links, definitions-with-line-items, column mapping, and all period strings from §6. |
| `BuildContext(graph, FLRTFinancialReport)` | Loads `FLRTReportDefinitionLink` rows (join to definitions), or falls back to the legacy single `DefinitionID`; builds `ReportCalculationEngine.DefinitionLink` objects (id + prefix + rounding); loads each definition's line items; computes periods. A parallel overload takes `FLRTPresentationGeneration` and reads `FLRTPresentationDefinitionLink`. |
| `FetchAndCalculate(ctx, …)` | The **simple** pipeline used by slides: fires five parallel fetches (CY, PY, Prior point-in-time + CY/PY YTD ranges), waits, then runs `ReportCalculationEngine.CalculateAll`. (The report path does its own conditional fetch instead — see §7.5.) |

### 7.5 `ReportGenerationService.cs` — Word orchestration
`Execute(token)` is the end-to-end Word flow:

| Step | Action |
|------|--------|
| 1 | Resolve tenant; `ReportDataPipeline.BuildContext`; build a `FinancialDataService`. |
| 2 | `ValidateGIExists` (fail fast). |
| 3 | Pull the template `.docx` from Acumatica (`FileService`), write to a temp path, free the bytes. |
| 4 | **Scan line items to decide which fetches are needed:** `needsDetail` (any per-line dimension filter) and `needsCumulative` (any DEBIT/CREDIT/MOVEMENT balance type) — avoids unnecessary API calls. |
| 5 | Reuse the context's period strings. |
| 6 | **Parallel fetch gated by `SemaphoreSlim(3)`** (caps peak memory at ~3 result sets): CY, PY, Prior, and — only if `needsCumulative` — CY-range + PY-range. Unneeded YTD tasks become `Task.FromResult(null)`. |
| 7 | `ReportCalculationEngine.CalculateAll`, mapping opening/cumulative args precisely (CY-opening = PY data, PY-opening = Prior data). |
| 8 | Add `CY`/`PY` year constants; null out the big API objects to free memory. |
| 9 | `WordTemplateService.PopulateTemplate` → read output bytes → `FileService.SaveGeneratedDocument` → return `FileID`. |
| 10 | `finally`: delete temp files, `CredentialProvider.ClearCache()`, dispose the semaphore. Whole run timed via `Stopwatch`. |

### 7.6 `ReportCalculationEngine.cs` — the calculator
Stateless per run (new instance each time). Two global dicts `_cyGlobal`/`_pyGlobal` keyed by `PREFIX_LINECODE` hold computed decimals; a compiled `FormulaTokenRegex` tokenizes formulas.

`CalculateAll(links, cyData, pyData, cyOpeningData, pyOpeningData, cyCumulativeData, pyCumulativeData)` — the entry point:

| Step | Action |
|------|--------|
| 1 | Collect known prefixes (for formula token resolution). |
| 2 | `LoadAllLineItems` — flatten every definition's lines into `LineNode`s (each remembers prefix, definitionID, rounding; `GlobalKey = PREFIX_LINECODE`). |
| 3 | `BuildChildrenMap` — parent→children index (keyed `DefinitionID_PARENTCODE`), built **once** and shared by dependency analysis + subtotal summation (no per-subtotal DB queries). |
| 4 | `BuildAndSort` — build a dependency graph and topologically sort with **Kahn's algorithm**. Dependencies: SUBTOTAL→children; CALCULATED→referenced line codes; ACCOUNT/HEADING→none. Detects **duplicate global keys** and **circular dependencies**, throwing clear `Messages.*` errors. HEADINGs/unreferenced nodes appended last. |
| 5 | Walk nodes in sorted order, computing CY and PY per `LineType`: ACCOUNT→`CalculateAccountLine`, SUBTOTAL→`CalculateSubtotal`, CALCULATED→`EvaluateFormula`. |
| 6 | `BuildPlaceholderMap` — emit `PREFIX_LINECODE_CY/PY` formatted strings (empty for HEADING/invisible lines). |

`Calculate(definitionID, cy, py)` — legacy single-definition shim; derives a prefix and delegates to `CalculateAll`.

Key internals:

| Member | Role |
|--------|------|
| `CalculateAccountLine` | If the line has dimension filters and detail rows exist, defer to `CalculateAccountLineFromDetail`; else iterate `AccountData`. For BEGINNING read the **opening** dataset's EndingBalance; for DEBIT/CREDIT/MOVEMENT read the **cumulative** (YTD) dataset; else the point-in-time period. Applies `ApplyAccountTypeSign` then the line's `SignRule`. |
| `CalculateAccountLineFromDetail` | Same logic per raw row, honoring sub/branch/org/ledger filters; builds an O(1) `BuildDetailIndex` for opening lookups; logs sample rows when nothing matches. |
| `ApplyAccountTypeSign` | Liability/Income negated, Asset/Expense as-is (normalizes GL's natural signs). |
| `GetBalanceByType` | Maps `BalanceType` to the right `FinancialPeriodData` field (`Movement = Debit − Credit`). |
| `EvaluateFormula` / `TokenizeFormula` / `EvaluateTokens` / `ParseExpression`/`Term`/`Factor` | A recursive-descent arithmetic evaluator (`+ − * /`, parentheses, unary minus, correct precedence). Division by zero logs and yields 0. |
| `ResolveToken` | Distinguishes **explicit** cross-definition refs (`BS_TOTAL_ASSETS`, token starts with a known prefix — longest-prefix-first so `PL` can't shadow `PLS`) from **implicit** own-definition refs (`TOTAL_ASSETS` → `CURRENTPREFIX_TOTAL_ASSETS`). An unknown line code **throws** (a silent zero in a financial report is the worst outcome). |
| `ExtractFormulaDependencies` | Pulls referenced line codes out of a formula for the dependency graph. |
| `IsAccountInRange` / `CompareAccountCodes` | Natural/segmented account-code comparison (numeric segments compared as numbers; handles dashed codes like `10100-01`). |
| `FormatFinancialValue` / `ApplyRounding` / `BuildFormatString` | Scale by Units/Thousands/Millions, round away-from-zero, format with thousands separators; zero renders as `-`, negatives as `(1,234)`. |

> Most of these helpers are `internal static` (no `PXGraph` needed), which is what makes them unit-testable — see `FinancialReport.Tests/`.

### 7.7 `GIDataFetchService.cs` — generic-GI fetch + aggregate · **the MBR engine**
The engine behind **MBR Definitions** (GI Data Sources) — it powers both the FR101004 **Test Fetch** action and the FR101003 MBR generation run. Static shared `HttpClient`.

`FetchAndAggregate(ds, columns, year, month, branch, org, ledger, token)` — the whole pipeline:

| Step | Action |
|------|--------|
| 1 | Authenticate; `BuildHeaderFilter` (period/branch/org/ledger predicates, typed per column). |
| 2 | Build `$select` from VALUE columns + columns referenced by their `RowFilter` + the key column — **unless** a MULTIROW column is present, in which case all columns are fetched. |
| 3 | Fetch with a **4-way fallback** (modern/legacy URL × with/without `$select`), capturing errors as strings to log on the main thread. |
| 4 | **VALUE columns** → `FilterRowsByKey` (range on key column) → `ApplyRowFilter` (client-side `eq/ne/gt/lt/ge/le/contains`) → `AggregateColumn`. |
| 5 | **CALCULATED columns** → `TopoSortCalculated` (Kahn's again, so cross-refs work regardless of SortOrder; circular = throw) → `EvaluateFormula`. |
| 6 | **MULTIROW columns** → key/row filter → sort by `OrderByColumn` (numeric/date/string aware via `GetSortValue`) → take top `RowLimit` → emit `ALIAS_rank_PROPERTY` keys (optionally narrowed by `DisplayColumns`). |
| 7 | Format every visible result with `FormatValue`, prefix with the data source's `Prefix` → `{PREFIX_ALIAS}` (or expanded multi-row keys). |

Supporting helpers:

| Group | Members |
|-------|---------|
| Aggregation | `AggregateNumeric` (Sum/Max/Min/Avg/First), `AggregateBoolean` (Sum=count-of-true), `AggregateDate` (Min/Max/First), `AggregateString` (First/Count/Max/Min), `GetDefaultValue`, `GetSortValue`, `ConvertToDecimal`. |
| Filter builders | `BuildTypedFilter` (per-type OData `eq`, quote-escaping for strings, `datetime'…'` for dates), `BuildDateRangeFilter` (Monthly/Yearly `ge…lt`), `ExtractRowFilterColumns`. |
| Formula evaluator | A second recursive-descent parser (`EvaluateArithmeticExpression`, `ParseExpression`/`Term`/`Factor`, `SkipSpaces`) — simpler than the engine's because tokens are pre-substituted with numeric values. |
| Paging | `PaginatedFetchAsync` — same paging/cap shape as `FinancialDataService`, accumulating per-call error strings. |

### 7.8 `MarkdownBuilderService.cs` — prompt builder
Turns computed data into the markdown prompt for Gamma. Two `Build` overloads (one for `FLRTFinancialReport`, one for `FLRTPresentationGeneration` with optional GI data sources). Assembles a title + **Report Context** (period/org/branch/ledger), an instruction block (audience, tone, recommended slide flow, formatting rules — effectively a CFO-presentation system prompt), a **Data** section, and a footer.

| Member | What it does |
|--------|--------------|
| `DeriveLabels` | Builds `FY{year} ({Month} {year})` CY/PY labels. |
| `AppendTBLineItems` | For each visible trial-balance line, a small `Period | Value` table with CY and PY rows. |
| `AppendGIDataSources` | Per data source, bullet each visible column; MULTIROW columns expand into per-rank bullets from the `ALIAS_rank_PROPERTY` keys. |
| `GetValue` | Missing/empty → `"0"`. |

### 7.9 `GammaApiService.cs` — slide API client
Calls `public-api.gamma.app/v1.0`. `GammaGenerationOptions` carries defaults (12 cards, executive tone, …).

| Member | What it does |
|--------|--------------|
| `GeneratePresentation(markdown, title, token, options)` | `SubmitGeneration` (POST `/generations`, `exportAs:"pptx"`, CFO-level `additionalInstructions`) → `PollUntilCompleted` → `DownloadFile`. |
| `GeneratePresentationFromTemplate(markdown, gammaTemplateId, token)` | `SubmitGenerationFromTemplate` (POST `/generations/from-template`) → same poll/download. |
| `CreateRequest` | Sets `X-API-KEY` **per request** (never mutates the shared client). |
| `PollUntilCompleted` | Polls GET `/generations/{id}` every 5 s up to 60 times (5 min). 5xx = transient retry; 4xx = fail immediately; `completed` returns `exportUrl`; `failed` throws. The `exportUrl` (the only auth on the file) is deliberately never logged. |
| `DownloadFile` | GETs the pre-signed export URL (no key header) → bytes. |

### 7.10 `SlideGenerationService.cs` — presentation orchestration
`BuildMarkdownPreview(token)`:

| Step | Action |
|------|--------|
| 1 | `ReportDataPipeline.BuildContext` for the presentation record. |
| 2 | Load `FLRTPresentationDataSourceLink` rows. Require at least one definition or one data source. |
| 3 | If definitions exist: validate visible lines have descriptions (`VisibleLineItemsMissingDescriptions`), then `ReportDataPipeline.FetchAndCalculate` for the trial-balance numbers. |
| 4 | If data sources exist: for each active one, load its columns and call `GIDataFetchService.FetchAndAggregate`, merging results. |
| 5 | `MarkdownBuilderService.Build(...)` → expose on `LastGeneratedMarkdown` (the graph persists it / hands it to Gamma). |

### 7.11 `WordTemplateService.cs` — docx placeholder fill
Uses `DocumentFormat.OpenXml`. A compiled `PlaceholderRegex` matches `{{…}}`.

| Member | What it does |
|--------|--------------|
| `PopulateTemplate(templatePath, outputPath, data)` | Entry point: opens the doc, processes the main body + headers + footers, ensures fields update on open, saves to `outputPath`. Unknown placeholders default to `0` by design. |
| `ProcessDocumentPart` | Runs the replace pass over one part (body/header/footer). |
| `ExtractPlaceholders` / `…FromPart` / `…FromElement` / `ExtractTextRecursive` | Collect the distinct placeholder keys actually present in the template. |
| `ReplacePlaceholdersInRuns` | The core: replaces `{{…}}` within a paragraph's runs. |
| `MergeRunsWithSameFormatting` / `IsLikelyPartOfPlaceholder` / `HaveSameFormatting` | Word often splits `{{TOKEN}}` across multiple runs (spell-check, formatting); these merge adjacent same-format runs so the regex can match a whole placeholder. |
| `EnsureUpdateFieldsOnOpen` | Sets the doc flag so Word recalculates fields on open. |
| `GetConfigValue` | Reads a Web.config value (template behavior config). |

### 7.12 `FileService.cs` — Acumatica file storage

| Member | What it does |
|--------|--------------|
| `GetFileContentAndName(noteID, record)` | Finds the latest uploaded file on the record whose name matches the `FRTemplate` filter (join `UploadFile`+`NoteDoc`), reads the newest `UploadFileRevision.BlobData`, returns (bytes, name). Throws if none usable. |
| `SaveGeneratedDocument(name, bytes, record)` | Two overloads (report / presentation). Saves via `UploadFileMaintenance.SaveFile`, attaches the file to the record's note (`PXNoteAttribute.SetFileNotes`), inserts a `NoteDoc` link if absent, returns the file `UID`. |

---

## 8. Graph layer (`Graph/`) — the screen controllers

### 8.1 `FLRTFinancialReportMaint` — Word-report screen (FR101000)
**Views:** `FinancialReport` (primary), `DefinitionLinks` (linked-definitions grid, ordered by `DisplayOrder`).

Events:

| Event | Behavior |
|-------|----------|
| `FieldSelecting<…definitionPrefix>` | Populates the read-only Prefix column per link row. |
| `RowPersisting<FLRTReportDefinitionLink>` | Validates a definition is chosen and its prefix is **unique among the report's links** (walks `DefinitionLinks.Cache.Cached`). |
| `FLRTFinancialReport_RowSelected` | Disables all fields + Generate/Download while `Status == InProgress`. |
| `FLRTFinancialReport_RowPersisting` | When a `FRTemplate`-matching file is attached, captures its `FileID` into `UploadedFileID`. |

Actions:

| Action | Behavior |
|--------|----------|
| `generateReport` | Orchestration entry point. On the UI thread: validate (record selected, has NoteID/template, not already running, **≥1 linked definition** — else every placeholder silently fills `0`), resolve tenant + credentials, build `AuthService`, flip status to InProgress, save, then `PXLongOperation.StartOperation`. Inside the background op: re-load the record on a fresh graph, wrap work in a **15-minute `CancellationTokenSource`**, authenticate, run `ReportGenerationService.Execute(token)`, on success write `GeneratedFileID` + Completed. On timeout/exception set Failed and re-throw. `finally` logs out the auth session. |
| `downloadReport` | Throws `PXRedirectToFileException` on `GeneratedFileID` to stream the file. |
| `resetStatus` | Confirmation dialog, then clears status to Pending and nulls `GeneratedFileID` so stale output isn't downloadable. |

Helpers (public, reused by the service):

| Helper | What it does |
|--------|--------------|
| `GetCompanyIDFromDB(reportID)` | Raw `PXDatabase.SelectSingle` for the row's `CompanyID` (the tenant discriminator). |
| `MapCompanyIDToTenantName(companyID)` | Looks up `FLRTTenantCredentials.TenantName` by `CompanyNum`. |

### 8.2 `FLRTFinancialPresentationMaint` — MBR screen (FR101003, *AFS Monthly Board Report*)
**Views:** `PresentationRecord` (primary), `DefinitionLinks`, `DataSourceLinks`.
**Events:** prefix-display `FieldSelecting` for both link types; duplicate-prefix validation on definition links; `RowSelected` toggles field/button enable on InProgress and only enables Download when a file exists.

Actions:

| Action | Behavior |
|--------|----------|
| `previewMarkdown` | Long op: build markdown via `SlideGenerationService.BuildMarkdownPreview`, store in `PresentationMarkdown`, save. Lets the user inspect the prompt before spending a Gamma call. |
| `generateGamma` | Long op: validate title + that a `GammaApiKey` exists. Reuse stored markdown if present, else build it. Then call `GammaApiService` — `GeneratePresentationFromTemplate` if `GammaTemplateId` is set, else `GeneratePresentation`. Save the returned `.pptx` via `FileService`, set `SlideGeneratedFileID` + Completed. Same 15-min timeout/Failed/logout pattern. |
| `downloadPresentation`, `resetStatus` | Mirror the report screen. |

**Helpers:** its own `GetCompanyIDFromDB` / `MapCompanyIDToTenantName` against `FLRTPresentationGeneration`.

### 8.3 `FLRTReportDefinitionMaint` — definition + line-items screen (FR101002)
**Views:** `ReportDefinition` (primary), `LineItems` (child grid, `[PXImport]`-enabled for Excel paste, ordered by `SortOrder`).

Events:

| Event | Behavior |
|-------|----------|
| `ReportDefinition_RowSelected` | `DefinitionCD` and `DefinitionPrefix` editable only on insert (locking the prefix protects existing templates). |
| `ReportDefinition_RowPersisting` | Code required; prefix required, **alphanumeric only** (regex `^[A-Za-z0-9]+$`), unique; code unique. |
| `LineItem_RowSelected` | Show/hide fields by `LineType` (account-range only for ACCOUNT, formula only for CALCULATED, …). |
| `FieldUpdated<…lineType>` | Auto-clears now-irrelevant fields on type change (HEADING nulls account range, sets `IsVisible=false`). |
| `LineItem_RowPersisting` | `LineCode` required; ACCOUNT needs from/to; CALCULATED needs a formula; `LineCode` unique within the definition. |

### 8.4 `FLRTGIDataSourceMaint` — MBR Definition screen (FR101004, *AFS MBR Config*)
**Views:** `DataSource` (primary), `Columns` (child).
**Events:** mirror the definition screen — lock `DataSourceCD`/`Prefix` after insert, validate prefix alphanumeric + globally unique; per-column field enabling by `LineType`, auto-clear on type change, alias-required + GIColumn/Formula required + alias-unique validation.

Actions:

| Action | Behavior |
|--------|----------|
| `detectColumns` | Authenticate with tenant creds, call `FinancialDataService.FetchGIColumns(GIName)`, store the discovered names into `DetectedColumns`, show them in a dialog. |
| `testFetch` | Open a `GITestFetchFilter` dialog (year/month/branch/org/ledger), run the full `GIDataFetchService.FetchAndAggregate`, render the resulting `{{PREFIX_ALIAS}} = value` pairs in a dialog + trace. Verifies a data source before wiring it into a presentation. |

`GITestFetchFilter` is a nested non-persisted (`[PXHidden]`) DAC for the dialog inputs.

### 8.5 `FLRTTenantCredentialsMaint` — credentials screen (FR101001)
Simple maintenance graph. `RowPersisting` requires `CompanyNum` + `TenantName` and enforces unique `TenantName`. `RowPersisted` logs success/failure. A custom `save()` action wraps `Cache.Persist` + `Actions.PressSave` in try/catch with tracing. (Note: it does *not* call `CredentialProvider.ClearCache` on save — the cache relies on its 10-minute TTL, and report generation clears it on completion, so a credential change can take up to 10 min to take effect unless the AppDomain restarts.)

---

## 9. End-to-end walkthroughs

### 9.1 AFS — generating one Word report

1. User opens **FR101000 (AFS Financial Report)**, picks a template-name record (with a `.docx` containing `{{…}}` placeholders attached), sets Year/Month/Branch/Org/Ledger, adds one or more **Report Definitions** on the links grid, clicks **Generate Report**.
2. `generateReport` validates, resolves the tenant via `CompanyID → FLRTTenantCredentials`, builds an `AuthService`, marks the row InProgress, starts a 15-min background long-operation.
3. `ReportGenerationService.Execute`: build context (definitions + line items + FY periods), validate the GI exists, fetch the template, decide which API calls are needed, fan out CY/PY/Prior/(YTD) OData fetches gated to 3 at a time.
4. `ReportCalculationEngine.CalculateAll`: topologically sort all lines across all definitions, compute account ranges (sign-normalized), subtotals, and formulas (incl. cross-definition refs), emit `{ PREFIX_LINECODE_CY/PY : formatted }` plus `CY`/`PY`.
5. `WordTemplateService.PopulateTemplate`: extract placeholders, default unknowns to `0`, merge split runs, replace `{{…}}` in body/headers/footers, flag "update fields on open", save.
6. `FileService.SaveGeneratedDocument` stores the output; the row flips to Completed with a `GeneratedFileID`; **Download Report** streams it.

### 9.2 MBR — generating one Monthly Board Report

Prerequisite setup happens on **FR101004 (AFS MBR Config)**: create one or more **MBR Definitions**, each bound to a GI; run **Detect Columns** (captures live OData column names) and **Test Fetch** (verifies the resolved `{{PREFIX_ALIAS}} = value` pairs against a chosen period) before relying on it.

1. User opens **FR101003 (AFS Monthly Board Report)**, sets a **Presentation Title**/description and Year/Month/Branch/Org/Ledger, links one or more **MBR Definitions** (GI data sources) and/or **Report Definitions** (trial-balance data), optionally sets a **Gamma Template Id**, and clicks **Preview Markdown** or **Generate Presentation**.
2. The graph validates (title present, **Gamma API key** configured on FR101001, ≥1 linked definition/data source), resolves tenant + credentials, marks the row InProgress, starts a 15-min background long-operation.
3. `SlideGenerationService.BuildMarkdownPreview`: `ReportDataPipeline.BuildContext` for the record; if Report Definitions are linked, validate visible lines have descriptions and `FetchAndCalculate` the trial-balance numbers; for each active MBR Definition, `GIDataFetchService.FetchAndAggregate` resolves its columns (VALUE/MULTIROW/CALCULATED, key+row filters, aggregation) into placeholders.
4. `MarkdownBuilderService.Build`: assemble the title, **Report Context**, the CFO-presentation instruction block, the **Data** section (TB line items + GI-data-source bullets, MULTIROW rows expanded), and a footer. **Preview Markdown** stops here and stores the prompt on `PresentationMarkdown` so the user can inspect it before spending a Gamma call.
5. **Generate Presentation**: reuse the stored markdown if present, else build it; call `GammaApiService` — `GeneratePresentationFromTemplate` if a `GammaTemplateId` is set, else `GeneratePresentation` — which submits to `public-api.gamma.app`, polls up to 5 min, and downloads the `.pptx` from the pre-signed export URL.
6. `FileService.SaveGeneratedDocument` stores the deck; the row flips to Completed with a `SlideGeneratedFileID`; **Download Presentation** streams it.

The two paths **share** auth, the period/context build, the calculation engine (for any linked Report Definitions), and file storage; they **differ** in the data source (Trial-Balance GI vs any GI) and the render step (`WordTemplateService` → `.docx` vs `MarkdownBuilderService` + `GammaApiService` → `.pptx`).

---

## 10. Cross-cutting design notes

- **Multi-tenant by design.** The customization can run in one tenant but fetch data from another; everything routes through `FLRTTenantCredentials` keyed by `CompanyNum`, with encrypted secrets and a TTL'd credential cache.
- **Resilient OData.** Every fetch tries modern then legacy URL forms, pages large result sets, caps rows at 100 000, escapes quotes, and sets auth per-request for safe parallelism.
- **Memory discipline.** Streaming JSON parse, a semaphore-gated fan-out, and explicit nulling of large objects keep peak memory bounded for big trial balances.
- **Fail loud, not silent.** Missing definitions, unknown formula codes, circular dependencies, duplicate keys, and missing GIs all raise clear errors — a plausible-looking report full of wrong/zero numbers is the worst outcome.
- **Config-as-data.** Report structure, column mappings, rounding, and GI data sources are all editable in the UI; code changes are only needed for genuinely new behavior.

---

## 11. Quick reference — every code file

| File | One-line role |
|------|---------------|
| `DAC/FLRTFinancialReport.cs` | Word-report job header |
| `DAC/FLRTReportDefinition.cs` | Reusable statement blueprint (GI + column map + rounding) |
| `DAC/FLRTReportLineItem.cs` | One statement line (account range / subtotal / formula / heading) |
| `DAC/FLRTReportDefinitionLink.cs` | Many definitions ↔ one report |
| `DAC/FLRTGIDataSource.cs` | MBR Definition — generic any-GI data source config (FR101004) |
| `DAC/FLRTGIDataSourceColumn.cs` | One output value of an MBR Definition |
| `DAC/FLRTPresentationGeneration.cs` | MBR report job header (FR101003) |
| `DAC/FLRTPresentationDefinitionLink.cs` | Many definitions ↔ one presentation |
| `DAC/FLRTPresentationDataSourceLink.cs` | Many GI data sources ↔ one presentation |
| `DAC/FLRTTenantCredentials.cs` | Per-tenant encrypted secrets |
| `DAC/GLHistoryEnqFilter.cs` | Cache extension on stock GL History filter (standalone) |
| `Graph/FLRTFinancialReportMaint.cs` | FR101000 controller |
| `Graph/FLRTFinancialPresentationMaint.cs` | FR101003 controller — MBR report |
| `Graph/FLRTReportDefinitionMaint.cs` | FR101002 controller |
| `Graph/FLRTGIDataSourceMaint.cs` | FR101004 controller — MBR Definition |
| `Graph/FLRTTenantCredentialsMaint.cs` | FR101001 controller |
| `Services/AuthService.cs` | OAuth client to target tenant |
| `Services/CredentialProvider.cs` | Decrypt + TTL-cache credentials |
| `Services/FinancialDataService.cs` | Trial-balance OData fetch (streaming) |
| `Services/ReportDataPipeline.cs` | Shared context build + period math |
| `Services/ReportGenerationService.cs` | Word end-to-end orchestration |
| `Services/ReportCalculationEngine.cs` | The calculator (ranges, subtotals, formulas, formatting) |
| `Services/GIDataFetchService.cs` | Generic-GI fetch + aggregate — the MBR engine |
| `Services/MarkdownBuilderService.cs` | Build the Gamma markdown prompt |
| `Services/GammaApiService.cs` | Gamma slide-API client |
| `Services/SlideGenerationService.cs` | Presentation orchestration |
| `Services/WordTemplateService.cs` | Fill `{{…}}` in the docx |
| `Services/FileService.cs` | Acumatica file read/save |
| `Helper/Constants.cs` | Status + filename + suffix constants |
| `Helper/Messages.cs` | All localizable strings |
| `Helper/GIColumnSelectorAttribute.cs` | Column-name dropdowns (live OData + GI fallback) |
| `Helper/GIColumnMapping.cs` | GI + 12 column-name mapping DTO |
| `Helper/RoundingSettings.cs` | Rounding DTO |

For a still-finer file-by-file reference (and the test suite notes), see [Documentation/03-reference/CodeWalkthrough.md](Documentation/03-reference/CodeWalkthrough.md).

*Document updated 2026-05-25.*
