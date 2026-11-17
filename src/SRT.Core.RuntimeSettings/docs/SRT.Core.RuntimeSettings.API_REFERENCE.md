# SRT.Core.RuntimeSettings — API reference

Reviewed against: package working tree on `30820f5` (uncommitted sources), Core `27dc4a1`, SQL Server package `a9bcc67`.  
Namespace root: `SRT.Core.RuntimeSettings`.  
This file describes the code, not a planned API.

## Plugin

```csharp
public sealed class RuntimeSettingsBasicConfigurations : ISRTBasicConfigurations
{
    public int Order => 60;
    public string FeatureName => "RuntimeSettings";
    public Task<WebApplicationBuilder> SRT_ConfigBuilder(WebApplicationBuilder builder, AppSetting configApp);
    public Task<WebApplication> SRT_ConfigApp(WebApplication app, AppSetting configApp);
}
```

Other plugin orders in the SRT tree: Logs 10, SQL Server 20, PostgreSQL 21, Redis 30, RabbitMQ 40, Swagger 50. 60 is unused and greater than SQL Server.

`SRT_ConfigBuilder` when the registry is missing: `StartupReport` section `runtime-settings`, status OFF, detail `استفاده نشده`.

`SRT_ConfigBuilder` when the registry exists: `SRT_AddGeneralConfig` for namespace `SRT.Core.RuntimeSettings.Database`, `IRuntimeSettings`, `RuntimeSettingsEditor`, both controllers through `RuntimeSettingsControllerFeatureProvider`, and `RuntimeSettingsReloadService`.

`SRT_ConfigApp` when used: `SRT_CheckDatabase` for that namespace, then an immediate reload, then startup detail `defined={count} overridden={count}`. No key names and no values.

## Host entry

```csharp
public static WebApplicationBuilder SRT_AddRuntimeSettings(
    this WebApplicationBuilder builder,
    AppSetting config,
    Action<RuntimeSettingsBuilder> register);
```

First call on a host:

- Stores one `RuntimeSettingsRegistry` singleton.
- Registers `RuntimeSettings:ReloadSeconds` (Int, min 5, max 600, audience DevOps, title `فاصله‌ی بازخوانی`).
- Builds a baseline `IConfigurationRoot` from `builder.Configuration.Sources` before adding this package’s source. Defaults are read from that baseline.
- Adds one `IConfigurationSource` at the highest priority and loads it synchronously (3 second cancel, SQL connect timeout 3). Failure logs a warning and leaves appsettings in effect.
- Registers `IRuntimeSettings` and `RuntimeSettingsEditor`.

Later calls add definitions to the same registry and reload the existing source. A second source is not added. Duplicate keys throw `InvalidOperationException` (`Runtime setting '{key}' is already registered.`).

An internal test overload accepts `Func<RuntimeSettingsDbContext>?`. Production passes null and opens SQL Server from `config.ConnectionStrings.DefaultConnection.GetConnectionString()`. Consumers must not call the internal overload.

## Builder

`RuntimeSettingsBuilder` methods are public and return the builder. A reserved, empty, or duplicate key throws `InvalidOperationException` immediately.

```csharp
public enum RuntimeSettingAudience { DevOps = 1, Admin = 2 }

RuntimeSettingsBuilder Bool(string key, string title, string description, RuntimeSettingAudience audience, bool requiresRestart = false);
RuntimeSettingsBuilder Int(string key, string title, string description, RuntimeSettingAudience audience, long min, long max, bool requiresRestart = false);
RuntimeSettingsBuilder Decimal(string key, string title, string description, RuntimeSettingAudience audience, decimal min, decimal max, bool requiresRestart = false);
RuntimeSettingsBuilder String(string key, string title, string description, RuntimeSettingAudience audience, int maxLength, bool requiresRestart = false);
RuntimeSettingsBuilder Enum(string key, string title, string description, RuntimeSettingAudience audience, IReadOnlyList<string> allowedValues, bool requiresRestart = false);
RuntimeSettingsBuilder StringList(string key, string title, string description, RuntimeSettingAudience audience, int maxItems, int maxItemLength, bool requiresRestart = false);
RuntimeSettingsBuilder Json(string key, string title, string description, RuntimeSettingAudience audience, int maxLength, Func<JsonElement, IRuntimeSettings, string?>? validate = null, bool requiresRestart = false);
RuntimeSettingsBuilder Rule(Func<IRuntimeSettings, string?> validate);
```

`min > max`, `maxLength < 1`, `maxItems < 1`, `maxItemLength < 1`, or an empty `allowedValues` list also throws `InvalidOperationException`. Enum matches are ordinal, not case-insensitive. A rule or JSON `validate` result that is null or whitespace means success.

There is no public static registry.

## Reserved keys

The key is split on `:`. Registration throws if:

- The key is empty, longer than 160 characters, or has an empty segment.
- The first segment, ignoring case, is `ConnectionDB`, `ConnectionStrings`, `Service`, `urlData`, `ServiceAuth`, `ServiceTokenCache`, `AuthSettings`, `SeedPasswords`, `Logging`, `Kestrel`, or `AllowedHosts`, or starts with `AUTH_` or `S2S_`.
- Any segment contains, ignoring case: `secret`, `password`, `passwd`, `pepper`, `credential`, `connectionstring`, `privatekey`, `apikey`, `certificate`.
- Any segment ends with, ignoring case: `pass`, `pwd`, `token`, or `key`.

`Tokens:DailyCap` is allowed. `X:GatewayToken` is rejected.

## Reader

```csharp
public interface IRuntimeSettings
{
    T Get<T>(string key);
    bool IsOverridden(string key);
}
```

`Get<T>` throws `InvalidOperationException` when the key was not registered. Scalars use `IConfiguration.GetValue<T>`. `StringList` and `Json` use `GetSection(key).Get<T>()` (`string[]`, `List<string>`, dictionaries, or a POCO). `IsOverridden` is true only when a valid row is applied.

`RuntimeSettings:ReloadSeconds` returns 30 when no other source defines it. The reload loop uses the same fallback and clamps to 5–600.

Stored JSON is flattened like `JsonConfigurationProvider`. A `StringList` or object/array `Json` override records its path. `GetChildKeys` drops earlier keys for that path and for paths under it, so a shorter list or object replaces appsettings children completely.

Load checks type, range, string length, list size, enum membership, and JSON length. It does not re-run `Rule` or the JSON `validate` callback. Those run on `Save`, against an overlay that shows the candidate value for the key being saved and current effective values for other keys.

## Database

`RuntimeSettingsDbContext : SRT.Core.Databases.DataBase` in `SRT.Core.RuntimeSettings.Database`.

`BaseConfiguration` uses `UseSqlServer(connectionStrings.DefaultConnection.GetConnectionString(), sql => sql.MigrationsAssembly(<this assembly>).MigrationsHistoryTable("__SRTRuntimeSettingsMigrations"))`.

`InsertBaseData` completes immediately. `RunIdentityGapFix` stays the base `false`. There is no `OnConfiguring` password fallback.

`RuntimeSettingsDbContextFactory` reads `SRT_DESIGNTIME_CONNECTION`. If that variable is missing it uses `Server=localhost;Database=SRT_DesignTime;Trusted_Connection=True;TrustServerCertificate=True`.

| Table | Columns |
|-------|---------|
| `RuntimeSettings` | `key` nvarchar(160) PK, `valueJson` nvarchar(max), `date_Update` datetime2, `userType_AdminChange` nvarchar(20), `id_AdminChange` **int**, `fullName_AdminChange` nvarchar(200) |
| `RuntimeSettingAudits` | `id` bigint identity, `key` nvarchar(160), `oldValueJson` nvarchar(max) null, `newValueJson` nvarchar(max) null, `action` nvarchar(10), `userType` nvarchar(20), `idUser` **int**, `fullNameUser` nvarchar(200), `date_Create` datetime2, index `IX_RuntimeSettingAudits_key_date_Create` on (`key`, `date_Create`) |

`id_AdminChange` and `idUser` are `int` because `IUserInfo` extends `IDatabaseEntryID<int>`. They are not `bigint`.

Initial migration class: `InitialRuntimeSettings` in `Database/Migrations`. `action` is `Save` or `Reset`. User type and full name are truncated to 20 and 200 characters.

## Access

```csharp
public static class RuntimeSettingsAccess
{
    public const string DevOpsView = "devops.runtimesettings.View";
    public const string DevOpsUpdate = "devops.runtimesettings.Update";
    public const string AdminView = "settings.runtime.View";
    public const string AdminUpdate = "settings.runtime.Update";
}
```

## Controllers

Both are `internal sealed`, discovered only by `RuntimeSettingsControllerFeatureProvider`, and derive from `SRTBaseControllerSystem`. Every action is POST. The body is inside the standard `ResponseData.data`.

| Route | Auth | Audience |
|-------|------|----------|
| `devops/runtime-settings` | `[CheckAuthMiddleWare("DevOps", requiredAccess: ...)]` | DevOps |
| `superadmin/runtime-settings` | `[CheckAuthMiddleWare("Admin", requiredAccess: ...)]` | Admin |

| Action | Access |
|--------|--------|
| `Search`, `History` | `DevOpsView` or `AdminView` |
| `Save`, `Reset` | `DevOpsUpdate` or `AdminUpdate` |

The changing user is `dmUser` (`userType`, `id`, `GetFullName()`).

### Search

Request: `text`, `group`, `onlyOverridden`, `page`, `pageSize`, `sortBy`, `sortDir`.

Omitted `page` is 1. Omitted `pageSize` is 20. `page < 1` or `pageSize` outside 1–100 returns 400 `صفحه‌بندی نامعتبر است.` `sortBy` is `key`, `group`, or `date_Update`. `sortDir` is `asc` or `desc`. Anything else returns 400 `مرتب‌سازی نامعتبر است.` `text` matches key, title, and description. `group` is the first key segment. `groups` in the response is every group for that audience, ignoring filters. `date_Update` nulls sort last.

Item fields: `key`, `group`, `title`, `description`, `type`, `min`, `max`, `maxLength`, `maxItems`, `allowedValues`, `requiresRestart`, `defaultValueJson`, `valueJson`, `isOverridden`, `hasInvalidStoredValue`, `date_Update`, `fullName_AdminChange`.

`type` is `Bool`, `Int`, `Decimal`, `String`, `Enum`, `StringList`, or `Json`. `min` and `max` are JSON numbers for Int and Decimal, otherwise null. `maxItemLength` is not a response field. `allowedValues` is null except for Enum. `valueJson` and `defaultValueJson` are raw JSON text (`10`, `true`, `"hello"`, `["a"]`). When a row is applied, `valueJson` is the stored text. Baseline JSON for a `Json` value is rebuilt from configuration strings. A leaf whose text is a canonical JSON number (`8012`, `-3`, `1.5`) is written as a number, so a string that only looks like a canonical number is a number in `defaultValueJson`. `007`, `1.0`, `+5`, `1e3`, and an empty string stay strings. `true` and `false`, in any letter case, are written as booleans. A present empty object is `{}`. A missing key stays `null`. `IConfiguration` stores a JSON `null` the same way as an empty object, so that null is written as `{}`. It stores `[]` the same way as an empty string, so an empty array is written as `""` rather than `[]`.

Response envelope: `items`, `total`, `page`, `pageSize`, `groups`.

### Save

Request: `key`, `valueJson`, `expectedDate_Update`. Response: one item, not wrapped in `items`.

| Case | Status | Message |
|------|--------|---------|
| Unknown key or other audience | 404 | `کلید پیدا نشد.` |
| Invalid JSON | 400 | `مقدار JSON نامعتبر است.` |
| Wrong JSON kind | 400 | `نوع مقدار درست نیست.` |
| Outside min/max | 400 | `مقدار خارج از بازه است.` |
| String or JSON too long | 400 | `طول مقدار بیش از حد است.` |
| Too many list items | 400 | `تعداد عضوهای فهرست بیش از حد است.` |
| List item too long | 400 | `طول عضو فهرست بیش از حد است.` |
| Enum value not allowed | 400 | `مقدار در فهرست مجاز نیست.` |
| `validate` or `Rule` returns text | 400 | that text |
| `expectedDate_Update` sent and it does not match the current row’s second | 409 | `این مقدار جای دیگر عوض شده است.` |

The row and the audit row commit in one transaction, then the provider reloads. `expectedDate_Update` null skips the check. A missing row plus a non-null expected timestamp is 409.

### Reset

Request: `key`. Deletes the row when one exists, writes an audit with `newValueJson: null` and `action: "Reset"`, reloads, and returns the item. No row: the same item, no audit, no error.

### History

Request: `key`, `page`, `pageSize`. Same paging rules as Search. `key` null returns every audit for this audience. Newest `date_Create` first, then `id`. A key from the other audience returns an empty page.

Item: `id`, `key`, `oldValueJson`, `newValueJson`, `action`, `userType`, `idUser`, `fullNameUser`, `date_Create`.

## JSON dates

SRT.Core registers `LocalDateTimeJsonConverter` on MVC JSON. It writes `yyyy-MM-dd'T'HH:mm:ss` with no `Z` and no offset. `DateTimeKind.Utc` is converted with `ToLocalTime()` first. Stored instants are UTC. On a host in Iran Standard Time (UTC+03:30), `2026-11-16T07:00:00Z` is sent as `2026-11-16T10:30:00`.

`expectedDate_Update` is compared as that same wall-clock second. Send back the `date_Update` string the previous response contained.

## Errors at startup

Registration failures are English `InvalidOperationException` messages for the developer. Panel failures are the short Persian strings above.
