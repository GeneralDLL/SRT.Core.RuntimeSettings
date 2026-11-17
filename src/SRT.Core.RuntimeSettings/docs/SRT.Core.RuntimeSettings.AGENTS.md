# SRT.Core.RuntimeSettings — Codex / Agents entry

Package: **SRT.Core.RuntimeSettings**

Before any change that uses or modifies this package, read:

1. [SRT.Core.RuntimeSettings.AI_RULES.md](SRT.Core.RuntimeSettings.AI_RULES.md)
2. [SRT.Core.RuntimeSettings.API_REFERENCE.md](SRT.Core.RuntimeSettings.API_REFERENCE.md)
3. [SRT.Core.RuntimeSettings.COOKBOOK.md](SRT.Core.RuntimeSettings.COOKBOOK.md) when wiring a service

Working rule: read docs first; do not invent APIs; if ambiguous, inspect the code and update the docs.

Every registered key needs an appsettings value. Read it from `IOptionsMonitor` or `IRuntimeSettings` unless `requiresRestart` is true. Do not register passwords or connection strings.

Keep the `SRT.Core.RuntimeSettings.` filename prefix when copying into a consumer. This file does not replace `AI_RULES.md`.
