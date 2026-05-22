# Templates — Reference Word Templates

Starter templates for the FR101000 Financial Report generation pipeline. Filename **must contain `FRTemplate`** for the loader to pick it up — see [../02-generation/FinancialReport_Generation.md § Step 7](../02-generation/FinancialReport_Generation.md#step-7--attach-the-word-template).

## Contents

| File | Purpose |
|---|---|
| [DemoTemplate_FRTemplate.docx](DemoTemplate_FRTemplate.docx) | Original demo Word template — references the `{{PREFIX_LINECODE_CY|PY}}` placeholder pattern. Drop into the Files panel of an FR101000 record. |
| [DemoTemplate_v2_FRTemplate.docx](DemoTemplate_v2_FRTemplate.docx) | Updated demo template (v2). Use this one for new reports. |
| [DemoTemplate_FRTemplate.md](DemoTemplate_FRTemplate.md) | Markdown source-of-truth for the demo template — paste into Word and save as `.docx` if the binary copy is lost. |
| [ConnectedApplication_Setup.docx](ConnectedApplication_Setup.docx) | Word version of [../01-setup/ConnectedApplication_Setup.md](../01-setup/ConnectedApplication_Setup.md). |

> Placeholders inside a template must match the **Prefix + Line Code** of a Definition linked to the report record. See [../01-setup/ReportDefinition_Setup.md § Placeholder Key Format](../01-setup/ReportDefinition_Setup.md#placeholder-key-format).
