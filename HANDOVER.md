# Project Handover — AFSCP Financial Report

**Audience:** the developer taking over this customization.
**Purpose of this doc:** everything you need to build, deploy, configure, operate, and continue the project. For a line-by-line code explanation, read [Documentation/03-reference/CodeWalkthrough.md](Documentation/03-reference/CodeWalkthrough.md) — this handover deliberately does *not* repeat it.

> Fill in the **`TODO(handover)`** placeholders before you sign off — they are facts only the outgoing developer/owner knows (repo remote, owners, accounts, environments).

---

## 1. What this is, in one paragraph

An Acumatica ERP 2025 R2 customization (assembly **`FinancialReport.dll`**, namespace `FinancialReport`) that generates **Word financial reports** and **AI-generated PowerPoint presentations (via the Gamma API)**. Accountants define report structure as *data* (which GL accounts roll into which lines, sign rules, subtotals, formulas, rounding) on maintenance screens; at generation time the customization authenticates to an Acumatica tenant, pulls trial-balance data over OData, computes every line, and stamps the numbers into a `.docx` template or a markdown prompt for Gamma. No recompile is needed when the chart of accounts or report layout changes.

---

## 2. Tech stack & key facts

| Item | Value |
|------|-------|
| Platform | Acumatica ERP **2025 R2** customization |
| Language / runtime | C# 9, **.NET Framework 4.8** (`net48`) |
| Output | Class library `FinancialReport.dll` |
| Assembly version | `25.201.0213` (see `.csproj` `FileVersion`) |
| Nullable | `enable` (~300 pre-existing nullable-ref *warnings* are normal — only treat **Errors** as build failures) |
| UI | Classic ASPX screens (`Pages/FR/FR1010xx`) — the deployed UI. Modern-UI screen folders also exist (see §5). |
| Key NuGet deps | `DocumentFormat.OpenXml` 3.2.0 (Word generation), `Newtonsoft.Json` 13.0.3 (OData/Gamma JSON) |
| Acumatica refs | `PX.Common`, `PX.Common.Std`, `PX.CS.Contracts`, `PX.Data`, `PX.Data.BQL.Fluent`, `PX.DbServices`, `PX.Objects` — `HintPath` points at the runtime `Bin/` (`..\..\..\..\Bin`), `Private=False` (not copied locally) |
| External APIs | (1) Target Acumatica tenant OAuth2 + OData/REST; (2) **Gamma** `public-api.gamma.app/v1.0` |
| Source control | git, default branch `main`. **Remote:** `TODO(handover)` |

> **Critical environment note:** this project lives **inside the Acumatica install** at
> `C:\Program Files\Acumatica ERP\2025R2\App_Data\Projects\AFSCPFinancialReportv213032026`.
> The repository *is* the deployed source location. Building the DLL writes straight into the live site's `Bin/` (see §4). This is not a normal "clone anywhere" repo.

---

## 3. Repository layout

```
AFSCPFinancialReportv213032026/                 ← repo root (this folder)
├── AFSCPFinancialReportv213032026.sln          ← solution
├── AFSCPFinancialReportv213032026/             ← C# project
│   ├── AFSCPFinancialReportv213032026.csproj   ← build target (has PostBuild copy hook)
│   ├── DAC/        10 table classes (FLRT*)     ← DB schema
│   ├── Graph/      5 maintenance graphs         ← screen logic
│   ├── Services/   the engine (auth, fetch, calc, file, template, Gamma, markdown)
│   ├── Helper/     constants, messages, selectors, DTOs
│   └── Properties/ AssemblyInfo
├── Pages/FR/       FR101000–FR101004 .aspx (+ empty .aspx.cs stubs)
├── _project/       Acumatica customization package (SQL, sitemap, screen rights, GI defs)
├── _scripts/       script.sql (schema script used by the package)
├── Documentation/  user guides + reference (incl. CodeWalkthrough.md)
├── _dev/Solution.bat
└── HANDOVER.md     ← you are here
```

---

## 4. Build & deploy — **read this carefully**

This is the single biggest source of "I changed code but nothing happened" confusion. There are **three independent deploy paths** and they do not overlap:

### 4.1 C# code (DAC / Graph / Service / Helper) → `dotnet build`

**Publishing the customization project (CP) does NOT recompile C#.** The CP package contains SQL, ASPX, GI defs, and sitemap — not the compiled DLL. To deploy any `.cs` change:

```powershell
dotnet build "C:\Program Files\Acumatica ERP\2025R2\App_Data\Projects\AFSCPFinancialReportv213032026\AFSCPFinancialReportv213032026\AFSCPFinancialReportv213032026.csproj" -c Debug
```

- Builds in a few seconds.
- The `.csproj` has a **PostBuild hook** that copies the output DLL into the runtime `Bin/`:
  ```xml
  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="copy /Y &quot;$(TargetPath)&quot; &quot;$(ProjectDir)..\..\..\..\Bin\$(TargetFileName)&quot;" />
  </Target>
  ```
- Acumatica auto-recycles the AppDomain when `Bin/*.dll` changes — usually live in 10–20 s. If not, touch `web.config` or run `iisreset`.

**Diagnose a stale DLL** (symptom: new field/action/event not visible in UI) — compare timestamps:
```powershell
(Get-Item "C:\Program Files\Acumatica ERP\2025R2\Bin\FinancialReport.dll").LastWriteTime
(Get-Item "...\AFSCPFinancialReportv213032026\Graph\FLRTReportDefinitionMaint.cs").LastWriteTime
```
If the DLL is older than the `.cs`, rebuild.

### 4.2 SQL / ASPX / GI / sitemap / screen-rights → Publish Customization (CP)

The `_project/` package handles schema and screen deployment. Publishing the CP:
- runs the SQL migration scripts,
- deploys the classic ASPX pages,
- deploys Generic Inquiry definitions, site-map nodes, and screen access rights.

It does **not** recompile C# and does **not** build Modern UI bundles.

### 4.3 Modern UI (if/when used) → webpack

Modern-UI screen folders exist under `FrontendSources/screen/src/screens/FR/FR1010xx`, but the **deployed UI is the classic ASPX**. If you ever wire up Modern UI:

```powershell
cd "C:\Program Files\Acumatica ERP\2025R2\FrontendSources\screen"
node ./node_modules/webpack/bin/webpack.js --env production --env screenIds=FR101002
```
Add `--env tenant=<Tenant>` for a tenant-scoped build. Adding a DAC field that must appear in Modern UI requires editing the HTML `<field>` + three TS files (see [feedback memory on frontend build] / `Documentation`), or you'll hit *"cannot be bound to a FieldState"*. **For this project's classic-ASPX UI you normally won't touch this path.**

### 4.4 Decision table

| You changed… | dotnet build | Publish CP | webpack |
|--------------|:---:|:---:|:---:|
| DAC `.cs` (column/attribute) | ✅ | ✅ (for the SQL column) | only if Modern UI |
| Graph / Service / Helper `.cs` | ✅ | ❌ | ❌ |
| SQL schema | ❌ | ✅ | ❌ |
| Classic ASPX page | ❌ | ✅ | ❌ |
| GI / sitemap / rights | ❌ | ✅ | ❌ |
| Modern UI HTML/TS | ❌ | ❌ | ✅ |

> A new DAC field that needs a DB column + UI + server logic touches **all three**: edit DAC `.cs`, add the column to `script.sql` / `_project/Sql_AFSTables.xml`, add the ASPX selector, then `dotnet build` **and** Publish CP.

---

## 5. Database schema

10 tables, all prefixed `FLRT`, created/migrated by the idempotent script `_project/Sql_FullProject_Idempotent.sql` (safe on fresh, old, or current DBs).

| Table | Role |
|-------|------|
| `FLRTTenantCredentials` | Per-tenant OAuth creds + Gamma key (encrypted columns) |
| `FLRTReportDefinition` | Reusable report blueprint (GI name + column mapping + rounding) |
| `FLRTReportLineItem` | Lines of a definition (account ranges, subtotals, formulas) |
| `FLRTFinancialReport` | A Word-report generation job (header) |
| `FLRTReportDefinitionLink` | Many definitions ↔ one report |
| `FLRTPresentationGeneration` | A presentation generation job (header) |
| `FLRTPresentationDefinitionLink` | Many definitions ↔ one presentation |
| `FLRTPresentationDataSourceLink` | Many GI data sources ↔ one presentation |
| `FLRTGIDataSource` | Generic (any-GI) data source config |
| `FLRTGIDataSourceColumn` | Output columns of a GI data source |

**Migration scripts** (run once each; idempotent):
- `Sql_Migration_DecouplePresentation.sql` — splits presentation fields out of `FLRTFinancialReport` into `FLRTPresentationGeneration` (the project moved from one combined screen to separate Report and Presentation screens).
- `Sql_Migration_GIDataSource.sql` — adds the GI Data Source feature tables.
- `Sql_FullProject_CreateAll.sql` — non-idempotent full create (fresh DB only). Prefer the idempotent one.

> History matters here: the **legacy single `DefinitionID`** on `FLRTFinancialReport` was replaced by the link table, and presentations were decoupled from reports. New code uses the link tables; the legacy field is kept only for old rows.

---

## 6. Screens & where the logic lives

| Screen | Title | Graph (logic) | Primary table |
|--------|-------|---------------|---------------|
| FR101000 | Financial Report | `FLRTFinancialReportMaint` | `FLRTFinancialReport` |
| FR101001 | Tenant Credentials | `FLRTTenantCredentialsMaint` | `FLRTTenantCredentials` |
| FR101002 | Report Definition | `FLRTReportDefinitionMaint` | `FLRTReportDefinition` |
| FR101003 | Financial Presentation | `FLRTFinancialPresentationMaint` | `FLRTPresentationGeneration` |
| FR101004 | GI Data Source | `FLRTGIDataSourceMaint` | `FLRTGIDataSource` |

The `.aspx.cs` files are empty stubs — **all behavior is in the graph**. The heavy lifting (fetch/calculate/render) is in `Services/`; graphs only orchestrate via `PXLongOperation` background tasks with a 15-minute cancellation timeout.

---

## 7. External integrations & secrets

### 7.1 Target Acumatica tenant (data source)
- The customization authenticates **outbound** with OAuth2 (password grant, refresh-token aware) to `{BaseURL}/identity/connect/token`, then queries OData GIs.
- It tries two URL forms per request: modern `{BaseURL}/odata/{tenant}/{GI}` then legacy `{BaseURL}/t/{tenant}/api/odata/gi/{GI}`.
- Multi-tenant: the right credential row is found via the report's `CompanyID` → `FLRTTenantCredentials.CompanyNum` → `TenantName`.

### 7.2 Gamma API (presentations)
- `https://public-api.gamma.app/v1.0` — submit generation, poll up to 5 min, download the `.pptx` from a pre-signed export URL.
- Requires a **Gamma API key** stored per tenant in `FLRTTenantCredentials.GammaApiKey`.

### 7.3 Secret storage
- Username, Password, Client ID, Client Secret, and Gamma key are stored **encrypted** (`[PXRSACryptString]`) in `FLRTTenantCredentials` and entered via the FR101001 screen. They are never logged in plaintext.
- **No secrets are in source or config files** — they live in the DB. `TODO(handover):` hand over the actual credential values / who owns the Gamma account out-of-band (do not paste them into this doc or git).

---

## 8. First-run configuration (what a new environment needs)

To get a working report end-to-end:

1. **Publish the customization** (deploys tables, screens, GIs, rights).
2. **FR101001 Tenant Credentials** — add a row: CompanyNum, TenantName, BaseURL, OAuth client id/secret + username/password, and Gamma API key.
3. Ensure the **Trial Balance GI** (or whichever GI you target) is published and accessible in the target tenant. The default expected GI name is `TrialBalance`.
4. **FR101002 Report Definition** — create a definition, set a unique 2–10 char alphanumeric **Prefix**, confirm the column mapping matches your GI's OData column names (use **Detect Columns**), and add line items.
5. **FR101000 Financial Report** — create a record, attach a `.docx` template whose filename contains **`FRTemplate`** and which holds `{{PREFIX_LINECODE_CY}}` / `{{..._PY}}` placeholders, link the definition(s), set Year/Month/Branch/Org/Ledger, click **Generate Report**, then **Download Report**.
6. For presentations: **FR101004** (optional GI data sources) → **FR101003** (link definitions/data sources, set title, optionally a Gamma template id, **Preview Markdown**, then **Generate Presentation**).

> **`FinancialMonth` gotcha:** it is the **fiscal-year START month**, and `CurrYear` is the FY **end** year. e.g. `FinancialMonth=Aug, CurrYear=2025` ⇒ FY2025 = Aug 2024 → Jul 2025. See CodeWalkthrough §7. Misreading this is the most common configuration mistake.

---

## 9. Known issues, recent fixes & gotchas

### 9.1 Recently fixed (numbered review findings, on `main` as of the last merge)
The last work cycle (`fix-review-findings-22052026`) closed a batch of correctness issues — worth knowing because they describe the system's failure modes:

- **#2** `FetchRangeApiData` now populates `AccountType`/`DetailRows` (YTD Debit/Credit/Movement sign-flip works).
- **#3** Generate Report is blocked when no definitions are linked (otherwise every placeholder silently became `0`).
- **#4** Ledger filter is no longer dropped on URL retry (prevented silently blending ACTUAL with BUDGET ledgers).
- **#5** Single quotes are escaped in all OData filters (values like `O'Brien` no longer 500).
- **#6** `RowFilter` columns are added to the OData `$select` (client-side filter no longer reads empty columns).
- **#7** Unknown formula line codes now **throw** instead of defaulting to 0 (a plausible-but-wrong financial number is worse than a hard failure).
- **#8** Gamma export URL is no longer logged at Info (it is the only auth on the file).
- **#9** Cancellation tokens threaded through Gamma + `WhenAll`.
- **GI probe** — report generation now validates the GI exists (`ValidateGIExists`) before fanning out, so a wrong GI name fails fast with a clear message.

### 9.2 Standing gotchas / things to watch
- **Build path confusion** — see §4. The #1 time-sink. Always check the DLL timestamp.
- **Placeholder keying** — Word placeholders must be `PREFIX_LINECODE_CY/PY`. The prefix is locked after a definition is first saved (changing it would break existing templates).
- **Unknown placeholders default to `0`** in the Word output (by design), so a typo in a template silently shows `0` rather than erroring — double-check template keys against the definition.
- **Long operations** hard-cap at **15 minutes**; large trial balances rely on streaming + a 3-way concurrency gate to stay within memory. Row fetches cap at 100,000.
- **`TraceLogger`** writes to `…\App_Data\Logs\FinancialReports\Overview Trace\` but most logging goes through Acumatica `PXTrace` (visible in the Trace screen / request trace). Use `PXTrace` for diagnostics.
- **Credential cache** has a 10-minute TTL and is cleared after each report generation; the credentials screen does not explicitly invalidate it on save, so a credential change can take up to 10 min to take effect (or restart the AppDomain).

### 9.3 Tech debt / candidates for cleanup
- Two separate recursive-descent formula evaluators exist (`ReportCalculationEngine` and `GIDataFetchService`) — intentional (different token semantics) but a consolidation candidate.
- Legacy `FLRTFinancialReport.DefinitionID` and the legacy single-definition `Calculate()` path remain for backward compatibility — can be removed once no old records depend on them.
- `FetchCompositeKeyData` / `FetchEndingBalance` in `FinancialDataService` appear to be narrower helpers not on the main path — verify usage before relying on or removing them.

---

## 10. Testing & verification

- There is **no automated test project** in the solution. `TODO(handover):` confirm whether any external test harness exists.
- Manual verification tools built into the app:
  - **FR101004 → Test Fetch** runs a GI data source against live data and shows the resulting placeholder values in a dialog — use it to validate a data source before wiring it into a presentation.
  - **FR101004 → Detect Columns** confirms the GI is reachable and lists its real OData column names.
  - **Preview Markdown** (FR101003) lets you inspect the exact Gamma prompt without spending a generation.
- End-to-end smoke test: generate a Word report with a known definition and confirm the numbers tie to the source GI/Trial Balance for the same period/branch/ledger.
- Test case material lives in `Documentation/04-testing/`.

---

## 11. Operations & troubleshooting

| Symptom | Likely cause / action |
|---------|----------------------|
| Code change not reflected | Stale DLL — `dotnet build` (§4.1); check DLL vs `.cs` timestamp |
| "Failed to fetch OData" | Wrong GI name / GI not published in tenant / bad credentials. The GI probe message names the GI + tenant |
| Report full of `0`s | No definitions linked (now blocked) or template placeholders don't match `PREFIX_LINECODE_*` |
| "Failed to authenticate" | Bad OAuth client/secret/user/pass in FR101001, or BaseURL wrong |
| Presentation fails | Missing/invalid Gamma API key; check trace for the Gamma poll status |
| Stuck "In Progress" | A long op died — use **Reset Status** on the screen to return to Pending, then regenerate |
| Numbers wrong by a sign | Check line item `SignRule` (ASIS/FLIP) and `AccountTypeFilter`; remember Liability/Income are auto-negated |
| Numbers wrong by period | The `FinancialMonth` = FY-start-month gotcha (§8) |
| Logs | Acumatica Trace screen (`PXTrace`), and the file log under `App_Data\Logs\FinancialReports\` |

---

## 12. Handover checklist

- [ ] New developer can open the solution and run `dotnet build` successfully (warnings OK, no errors).
- [ ] New developer understands the **three deploy paths** (§4) and the DLL-timestamp diagnostic.
- [ ] Git remote / access transferred — `TODO(handover)`.
- [ ] Tenant credentials + Gamma API key handed over securely (not via this doc) — `TODO(handover)`.
- [ ] Target tenant(s), BaseURLs, and the expected GI name(s) documented — `TODO(handover)`.
- [ ] Owner of the Gamma account / billing identified — `TODO(handover)`.
- [ ] Walked through one full report generation and one presentation generation together.
- [ ] Reviewed [CodeWalkthrough.md](Documentation/03-reference/CodeWalkthrough.md) and the `Documentation/` setup guides.

---

## 13. Reference map

- **Deep code reference:** [Documentation/03-reference/CodeWalkthrough.md](Documentation/03-reference/CodeWalkthrough.md)
- **Setup guides:** `Documentation/01-setup/` (Connected App, Tenant Credentials, Report/MBR Definition)
- **Generation guides:** `Documentation/02-generation/`
- **Placeholder reference & troubleshooting:** `Documentation/03-reference/`
- **Test cases:** `Documentation/04-testing/`
- **Credentials guide:** `Documentation/05-Credentials/`
- **User manual:** `Documentation/AFSCPFinancialReport_UserManual.md` (+ `.docx`)
- **Schema scripts:** `_project/Sql_*.sql`, `_scripts/script.sql`

---

## 14. Owners & contacts

| Role | Name / contact |
|------|----------------|
| Outgoing developer | `TODO(handover)` |
| Incoming developer | `TODO(handover)` |
| Product / business owner | `TODO(handover)` |
| Acumatica admin (target tenant) | `TODO(handover)` |
| Gamma account owner | `TODO(handover)` |

*Document prepared 2026-05-25.*
