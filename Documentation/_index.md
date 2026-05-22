# AFSCPFinancialReport — Documentation Index

All documentation for the AFS Financial Report module (Acumatica 2025 R2 customization, v2.1.3).

## Folders

| Folder | Index | Contents |
|---|---|---|
| `01-setup/` | [_index](01-setup/_index.md) | Connected Application, Tenant Credentials, Report Definition, MBR Definition — everything you configure before generating. |
| `02-generation/` | [_index](02-generation/_index.md) | Financial Report Generation (FR101000) and MBR Report Generation (FR101003). |
| `03-reference/` | [_index](03-reference/_index.md) | Placeholder catalogue and troubleshooting. |
| `04-testing/` | [_index](04-testing/_index.md) | Manual test cases for FR101002. |
| `05-Credentials/` | [_index](05-Credentials/_index.md) | ⚠️ Local dev credentials — gitignore before pushing public. |
| `archive/` | [_index](archive/_index.md) | Superseded manuals and historical refactor plans. |
| `images/` | [_index](images/_index.md) | Screenshots referenced inline by the guides. |
| `templates/` | [_index](templates/_index.md) | Reference Word templates (`*FRTemplate*.docx`). |

## Recommended reading order

1. [Connected Application Setup](01-setup/ConnectedApplication_Setup.md) — Acumatica OAuth2 client.
2. [Tenant Credentials Setup](01-setup/TenantCredentials_Setup.md) — store API credentials (FR101001).
3. [Report Definition Setup](01-setup/ReportDefinition_Setup.md) — define statements (FR101002).
4. [MBR Definition Setup](01-setup/MBRDefinition_Setup.md) — Management Business Report definitions backed by any Generic Inquiry (FR101004).
5. [Financial Report Generation](02-generation/FinancialReport_Generation.md) — produce `.docx` (FR101000).
6. [MBR Report Generation](02-generation/MBRReport_Generation.md) — produce the AI-driven Monthly Board Report `.pptx` / `.docx` (FR101003).
7. [Placeholder Reference](03-reference/Placeholder_Reference.md) and [Troubleshooting](03-reference/Troubleshooting.md) as needed.
8. [Report Definition Test Cases](04-testing/ReportDefinition_TestCases.md) — manual QA catalogue.

## Project-truth notes (v2.1.x model)

Two facts changed in the v2.1.x refactor — keep these in mind when reading any older `.docx` / `.pptx` template you inherit, or when peeking at pages in `archive/old-manuals/`:

- **Balance types.** Five FY-to-date types only — `ENDING`, `BEGINNING`, `DEBIT`, `CREDIT`, `MOVEMENT`. The legacy single-period types (`PDEBIT`, `PCREDIT`, `PMOVEMENT`) were removed. *Authoritative reference:* [01-setup/ReportDefinition_Setup.md § Balance Type values](01-setup/ReportDefinition_Setup.md#balance-type-values).
- **Placeholder periods.** Two suffixes only — `_CY` (current FY-to-date) and `_PY` (previous FY same window). The legacy `_PM` (previous-month) suffix was removed. Compute month-only deltas inside the Word template (`{{X_X_CY}} − {{X_X_PY}}`), not inside a Definition formula. *Authoritative reference:* [03-reference/Placeholder_Reference.md](03-reference/Placeholder_Reference.md).

All current-folder guides (`01-setup/`, `02-generation/`, `03-reference/`, `04-testing/`) are written against the v2.1.x code. Files under `archive/old-manuals/` are historical only — `00_README.md`, `02_*`, `03_*`, `PresentationGeneration_Steps.md`, `UserManual.md`. Don't trust them for current behaviour.
