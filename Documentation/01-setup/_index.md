# 01 — Setup

Pre-generation configuration. Work through these in order; each step depends on the previous.

| File | Screen | Status | Contents |
|---|---|---|---|
| [ConnectedApplication_Setup.md](ConnectedApplication_Setup.md) | SM303010 | **Current** | OAuth2 Connected Application — Client ID + Shared Secret (stock Acumatica screen). |
| [TenantCredentials_Setup.md](TenantCredentials_Setup.md) | FR101001 | **Current** | Store API credentials (Acumatica + Gamma) per tenant. Encrypted-at-rest fields, run-time decrypt flow, validation errors. Was `archive/old-manuals/01_TenantCredentials_Setup.md`. |
| [ReportDefinition_Setup.md](ReportDefinition_Setup.md) | FR101002 | **Current** | Define a statement — header, GI column mappings, line items (5 FY-to-date balance types, `_CY`/`_PY` placeholders, cross-definition formulas). |
| [MBRDefinition_Setup.md](MBRDefinition_Setup.md) | FR101004 | **Current** | Management Business Report data sources backed by any Generic Inquiry. Drives the [MBR Report Generation](../02-generation/MBRReport_Generation.md) pipeline (FR101003). Was `archive/old-manuals/04_GIDataSource_Setup.md`. |

> Connected Application is a Microsoft / Acumatica stock screen — no project code involved. Everything else is part of the AFSCPFinancialReport customization.
