# Archive — Planning

Internal record of v2.1.x refactor decisions. Not user-facing; kept for traceability.

| File | Subject |
|---|---|
| [PLAN_MemoryOptimization.md](PLAN_MemoryOptimization.md) | Memory profile of the report generation pipeline; plan for nulling API datasets after the engine consumes them. |
| [PLAN_RemoveLegacyPlaceholders.md](PLAN_RemoveLegacyPlaceholders.md) | Removal plan for the legacy raw-account-code resolution path (`{{A74101_CY}}` style). Engine-only path is canonical now; legacy code was deleted in commit `86111f8`. |

> Both plans are landed. Notes retained for context — do not re-implement without re-reviewing the engine code first; current behaviour may already differ from the plan text.
