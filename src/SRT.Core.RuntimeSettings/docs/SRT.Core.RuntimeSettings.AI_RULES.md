# SRT.Core.RuntimeSettings — AI rules

These rules are mandatory for every agent that consumes or changes **SRT.Core.RuntimeSettings**.

Companion docs in this folder:

- [SRT.Core.RuntimeSettings.API_REFERENCE.md](SRT.Core.RuntimeSettings.API_REFERENCE.md)
- [SRT.Core.RuntimeSettings.COOKBOOK.md](SRT.Core.RuntimeSettings.COOKBOOK.md)
- [SRT.Core.RuntimeSettings.HUMAN_GUIDE.md](SRT.Core.RuntimeSettings.HUMAN_GUIDE.md)

## Working rule

1. Read these docs before using or changing the package.
2. Do not invent APIs, routes, JSON fields, or access strings.
3. If the docs and the code disagree, inspect the code and update the docs in the same change.

## Registration

MUST:

- Put the initial value of every registered key in appsettings (or another configuration source added before `SRT_AddRuntimeSettings`). The package never writes appsettings.
- Call `SRT_AddRuntimeSettings` at the start of the service’s own `SRT_ConfigBuilder`, before `Build()`.
- Include `RuntimeSettings:ReloadSeconds` in appsettings as `30` unless a different value inside 5–600 is intended.
- Keep `title` and `description` in Persian. They are panel text.
- Use explicit audience `DevOps` or `Admin`.

MUST NOT:

- Register a password, secret, token, API key, certificate, credential, or connection string. The package rejects those keys at startup. `Tokens:DailyCap` is a legal business key. `GatewayToken` is not.
- Register `ConnectionStrings`, `ConnectionDB`, `Service`, `urlData`, `ServiceAuth`, `ServiceTokenCache`, `AuthSettings`, `SeedPasswords`, `Logging`, `Kestrel`, or `AllowedHosts`, or a first segment that starts with `AUTH_` or `S2S_`.
- Cache a registered value in a field during startup. Read it from `IOptionsMonitor<T>` or `IRuntimeSettings` on each use. Copying once at startup is allowed only when that key was registered with `requiresRestart: true`.
- Add a second configuration source for the same host. Repeated `SRT_AddRuntimeSettings` calls only add definitions.
- Call `SRT_AddGeneralConfig` for `RuntimeSettingsDbContext` yourself. The plugin does that once.
- Put a real password, username, connection string, host name, IP, or person name in this package’s code, tests, or docs.

## Runtime

MUST:

- Treat a valid database row as the effective value and appsettings as the fallback.
- Treat `hasInvalidStoredValue` as “a row exists but was not applied”.
- Expect `OnReload` only when applied data changed. `Save` and `Reset` reload immediately when they change a row.
- Keep user ids as `int`. That is `IUserInfo.id` in SRT.Core (`IDatabaseEntryID<int>`).

MUST NOT:

- Assume the JSON date has a `Z` or an offset. SRT.Core’s `LocalDateTimeJsonConverter` writes host-local `yyyy-MM-dd'T'HH:mm:ss`.
- Show a DevOps key on the Admin controller, or the reverse. Unknown keys and the other audience return 404.
- Log stored values. Warnings may include the key.
