# SRT.Core.RuntimeSettings — Documentation Index

Package: **SRT.Core.RuntimeSettings**  
Target: .NET 10 / ASP.NET Core / EF Core SQL Server  
Reviewed against: this package working tree on `30820f5` (uncommitted), Core `27dc4a1`, SQL Server package `a9bcc67`

The GitHub homepage is the unprefixed repo-root `README.md`. This file is the **package docs index**.

All filenames in this folder start with `SRT.Core.RuntimeSettings.` so you can copy the whole set into a consumer project without colliding with other packages’ docs.

## Reading order

| Audience | Start here | Then |
|----------|------------|------|
| Humans | [SRT.Core.RuntimeSettings.HUMAN_GUIDE.md](SRT.Core.RuntimeSettings.HUMAN_GUIDE.md) | [COOKBOOK](SRT.Core.RuntimeSettings.COOKBOOK.md), [API_REFERENCE](SRT.Core.RuntimeSettings.API_REFERENCE.md) |
| AI (all tools) | [SRT.Core.RuntimeSettings.AI_RULES.md](SRT.Core.RuntimeSettings.AI_RULES.md) | [API_REFERENCE](SRT.Core.RuntimeSettings.API_REFERENCE.md), [COOKBOOK](SRT.Core.RuntimeSettings.COOKBOOK.md) |
| Codex | [SRT.Core.RuntimeSettings.AGENTS.md](SRT.Core.RuntimeSettings.AGENTS.md) | AI_RULES |
| Claude | [SRT.Core.RuntimeSettings.CLAUDE.md](SRT.Core.RuntimeSettings.CLAUDE.md) | AI_RULES |
| Cursor | Install [SRT.Core.RuntimeSettings.CURSOR_RULE.mdc](SRT.Core.RuntimeSettings.CURSOR_RULE.mdc) | AI_RULES |
| Copy paths per AI | [SRT.Core.RuntimeSettings.AI_INSTALL.md](SRT.Core.RuntimeSettings.AI_INSTALL.md) | — |

## Files in this set

1. `SRT.Core.RuntimeSettings.README.md` — this index
2. `SRT.Core.RuntimeSettings.HUMAN_GUIDE.md` — how the package works
3. `SRT.Core.RuntimeSettings.API_REFERENCE.md` — exact contracts
4. `SRT.Core.RuntimeSettings.COOKBOOK.md` — wire one service
5. `SRT.Core.RuntimeSettings.AI_RULES.md` — canonical MUST / MUST NOT
6. `SRT.Core.RuntimeSettings.AGENTS.md` — Codex entry
7. `SRT.Core.RuntimeSettings.CLAUDE.md` — Claude entry
8. `SRT.Core.RuntimeSettings.CURSOR_RULE.mdc` — Cursor rule **template**
9. `SRT.Core.RuntimeSettings.AI_INSTALL.md` — which files to copy where

## Copy into a consumer project

Follow [SRT.Core.RuntimeSettings.AI_INSTALL.md](SRT.Core.RuntimeSettings.AI_INSTALL.md). Files in this folder are not auto-applied.

## Session bootstrap

```text
You are working with SRT.Core.RuntimeSettings.
Before changing or consuming this package:
1) Read SRT.Core.RuntimeSettings.AI_RULES.md
2) Use SRT.Core.RuntimeSettings.API_REFERENCE.md for signatures and JSON
3) Follow SRT.Core.RuntimeSettings.COOKBOOK.md to wire a service
Rules: read docs first; do not invent APIs; if ambiguous, inspect the code and update these docs.
Every registered key needs an appsettings value. Read it from IOptionsMonitor or IRuntimeSettings on each use unless requiresRestart is true. Do not register passwords or connection strings.
```
