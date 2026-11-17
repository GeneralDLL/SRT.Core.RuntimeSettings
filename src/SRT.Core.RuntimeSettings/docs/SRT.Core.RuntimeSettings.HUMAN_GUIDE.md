# SRT.Core.RuntimeSettings — Human guide

A service registers the settings a panel may change. The first value comes from appsettings (and any other configuration source already on the host). A panel save stores JSON in that service’s main SQL Server database. The next read, including after a process restart, uses the stored value when it still matches the definition.

The package does not write appsettings. It does not keep a process-wide static registry: each host has its own registry in DI.

## What the service does

At the start of its own startup configuration, before `Build()`, the service calls `SRT_AddRuntimeSettings` and registers keys. The first call adds one configuration source at the highest priority and reads the database synchronously, with a connection timeout of 3 seconds. If the database or the table is missing, startup continues with appsettings and a warning is logged.

Calling `SRT_AddRuntimeSettings` again on the same host adds more keys. It does not add a second source. A duplicate, empty, or reserved key throws, so the service does not start.

## What the plugin does

`RuntimeSettingsBasicConfigurations` is discovered because the assembly name starts with `SRT.Core`. Its `Order` is **60** (after Logs 10, SQL Server 20, PostgreSQL 21, Redis 30, RabbitMQ 40, and Swagger 50).

If the service never called `SRT_AddRuntimeSettings`, the plugin records `استفاده نشده` and does nothing else.

If it did, the plugin registers `RuntimeSettingsDbContext` on the main connection, the reader, the editor, the two controllers, and a background reload. During app configuration it migrates that context (history table `__SRTRuntimeSettingsMigrations`), reloads immediately, and records how many keys are defined and how many are overridden. It does not record key names or values.

## How a value is chosen

For each registered key:

1. A stored row that still matches the definition (type, range, length, allowed values) replaces appsettings.
2. A row that does not match is ignored. The panel item has `hasInvalidStoredValue: true` and `isOverridden: false`. A warning is logged once per key until the row becomes valid.
3. A row whose key was not registered is ignored.
4. Otherwise the value is whatever the other configuration sources already had.

`StringList` and `Json` overrides replace the whole set of child keys. If appsettings has three list items and the panel saved one, bind returns one item.

`RuntimeSettings:ReloadSeconds` is registered by the package (integer 5–600, audience DevOps). Put `30` in appsettings. If that key is missing everywhere, reads and the reload loop use 30.

The background loop reloads on that interval. `Save` and `Reset` reload immediately. A reload raises `OnReload` only when the applied configuration data actually changed.

## Panels

| Route | Audience |
|-------|----------|
| `POST devops/runtime-settings/...` | DevOps keys only |
| `POST superadmin/runtime-settings/...` | Admin keys only |

Actions are `Search`, `Save`, `Reset`, and `History`. Access strings end in `View` or `Update`. Titles and descriptions are Persian and are shown by the panel. This package does not render a panel.

## What must never be registered

Connection strings, service identity, auth options, logging, Kestrel, and any key segment that is a secret, password, token, or key. `Tokens:DailyCap` is allowed. `Payment:GatewayToken` is not. See AI_RULES.

## Restart flag

`requiresRestart: true` tells the panel and the service that a consumer may have copied the value once at startup. The package still publishes the new value to configuration immediately. A consumer that needs the new value without a restart must read `IOptionsMonitor<T>` or `IRuntimeSettings` each time, and must not cache the value in a field.
