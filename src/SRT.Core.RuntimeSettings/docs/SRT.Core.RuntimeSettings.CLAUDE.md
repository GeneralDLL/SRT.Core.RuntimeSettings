# SRT.Core.RuntimeSettings — Claude entry

Package: **SRT.Core.RuntimeSettings**

## Required reading

- [SRT.Core.RuntimeSettings.AI_RULES.md](SRT.Core.RuntimeSettings.AI_RULES.md) — follow entirely
- [SRT.Core.RuntimeSettings.API_REFERENCE.md](SRT.Core.RuntimeSettings.API_REFERENCE.md) — signatures, JSON, and errors
- [SRT.Core.RuntimeSettings.COOKBOOK.md](SRT.Core.RuntimeSettings.COOKBOOK.md) — wire one service

## Working rule

Read docs first. Do not invent APIs. If ambiguous, inspect the code and update these docs.

Every registered key needs an appsettings value. Read it from `IOptionsMonitor` or `IRuntimeSettings` on each use unless `requiresRestart` is true. Do not register passwords or connection strings.

Keep the `SRT.Core.RuntimeSettings.` filename prefix when copying into a consumer. This entry is a pointer only. `AI_RULES.md` is authoritative.
