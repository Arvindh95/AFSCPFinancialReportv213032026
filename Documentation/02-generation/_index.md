# 02 — Generation

Produce the Word `.docx` (financial report) or PowerPoint `.pptx` (presentation) from configured Definitions.

| File | Screen | Status | Contents |
|---|---|---|---|
| [FinancialReport_Generation.md](FinancialReport_Generation.md) | FR101000 / FR401000 | **Current** | Word merge engine — header lifecycle, status flow (`N`/`P`/`C`/`F`), Generate / Download / Reset Status actions, multi-definition reports, worked examples. |
| [MBRReport_Generation.md](MBRReport_Generation.md) | FR101003 | **Current** | AI-driven Monthly Board Report (`.pptx` + merged `.docx`) generation via Gamma. Consumes the placeholders emitted by Report Definitions and MBR Definitions. Was `archive/old-manuals/05_Presentation_Generation.md`. |

Both screens read placeholders produced by the linked **Report Definitions** (and, for MBR Reports, the **MBR Definitions** linked through the *GI Data Sources* tab on FR101003) — see [01-setup/](../01-setup/_index.md).
