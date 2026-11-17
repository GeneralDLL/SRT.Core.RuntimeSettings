# SRT.Core.RuntimeSettings — Where to copy files for each AI

Source folder (inside this package):

`SRT.Core.RuntimeSettings/src/SRT.Core.RuntimeSettings/docs/`

Copy **with the same filenames** (keep the `SRT.Core.RuntimeSettings.` prefix).  
`<project>` = root of the consumer solution that referenced this package.

---

## Shared (all AIs)

| Copy from `docs/` | Paste into consumer |
|-------------------|---------------------|
| `SRT.Core.RuntimeSettings.README.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.HUMAN_GUIDE.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.API_REFERENCE.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.COOKBOOK.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.AI_RULES.md` | `<project>/docs/` |

Optional: also keep `SRT.Core.RuntimeSettings.AI_INSTALL.md` in `<project>/docs/`.

---

## Cursor

| Copy from `docs/` | Paste into consumer |
|-------------------|---------------------|
| `SRT.Core.RuntimeSettings.CURSOR_RULE.mdc` | `<project>/.cursor/rules/` |
| `SRT.Core.RuntimeSettings.AI_RULES.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.API_REFERENCE.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.COOKBOOK.md` | `<project>/docs/` |

The `.mdc` under `.cursor/rules/` is what Cursor can apply. The rule points at `docs/`. Those three files must exist there too.

---

## Claude

| Copy from `docs/` | Paste into consumer |
|-------------------|---------------------|
| `SRT.Core.RuntimeSettings.CLAUDE.md` | `<project>/` or `<project>/docs/` |
| `SRT.Core.RuntimeSettings.AI_RULES.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.API_REFERENCE.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.COOKBOOK.md` | `<project>/docs/` |

If the repo already has `CLAUDE.md`, add one pointer line. Do not replace it by renaming this file to `CLAUDE.md`.

---

## Codex

| Copy from `docs/` | Paste into consumer |
|-------------------|---------------------|
| `SRT.Core.RuntimeSettings.AGENTS.md` | `<project>/` or `<project>/docs/` |
| `SRT.Core.RuntimeSettings.AI_RULES.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.API_REFERENCE.md` | `<project>/docs/` |
| `SRT.Core.RuntimeSettings.COOKBOOK.md` | `<project>/docs/` |

If the repo already has `AGENTS.md`, add one pointer line. Do not replace it by renaming this file to `AGENTS.md`.

---

## Quick matrix

| AI | Entry file | Knowledge files |
|----|------------|-----------------|
| Cursor | `.cursor/rules/SRT.Core.RuntimeSettings.CURSOR_RULE.mdc` | `docs/SRT.Core.RuntimeSettings.{AI_RULES,API_REFERENCE,COOKBOOK}.md` |
| Claude | `SRT.Core.RuntimeSettings.CLAUDE.md` | same `docs/` trio |
| Codex | `SRT.Core.RuntimeSettings.AGENTS.md` | same `docs/` trio |

Canonical rules: `docs/SRT.Core.RuntimeSettings.AI_RULES.md`
