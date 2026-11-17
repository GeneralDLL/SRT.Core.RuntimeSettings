# SRT.Core.RuntimeSettings — Cookbook

One recipe: connect a generic Orders service (id `9002`) so a DevOps panel can change how often its outbox is polled. Names and the id are examples. Do not copy a real host, password, or connection string into this package.

## 1. Reference the package

```xml
<ProjectReference Include="path/to/SRT.Core.RuntimeSettings/src/SRT.Core.RuntimeSettings/SRT.Core.RuntimeSettings.csproj" />
```

The host must already reference `SRT.Core` and `SRT.Core.Database.SQLServer`. The runtime-settings assembly is loaded by the SRT plugin loader because its file name starts with `SRT.Core`.

## 2. Put the initial values in appsettings

Do this for every environment file. The package will not create these values for you, except that a missing `RuntimeSettings:ReloadSeconds` is read as 30.

```json
{
  "Service": { "Id": 9002, "Name": "Orders" },
  "RuntimeSettings": { "ReloadSeconds": 30 },
  "Events": { "Outbox": { "PollSeconds": 5 } }
}
```

`Service` is a normal host section. Do not register it as a runtime setting.

## 3. Register the key at the start of the service configuration

Call this before any other startup work that reads the key, and before `Build()`.

```csharp
using SRT.Core.RuntimeSettings;

public Task<WebApplicationBuilder> SRT_ConfigBuilder(WebApplicationBuilder builder, AppSetting config)
{
    builder.SRT_AddRuntimeSettings(config, settings =>
    {
        settings.Int(
            "Events:Outbox:PollSeconds",
            "فاصله‌ی خواندن صف",
            "هر چند ثانیه یک‌بار صف خروجی بررسی می‌شود.",
            RuntimeSettingAudience.DevOps,
            min: 1,
            max: 300);
    });

    return Task.FromResult(builder);
}
```

More keys can be added in the same callback or in a later `SRT_AddRuntimeSettings` call. Do not register the same key twice.

Do not call `SRT_AddGeneralConfig` for `RuntimeSettingsDbContext`. `RuntimeSettingsBasicConfigurations` (Order 60) registers the context, migrates `__SRTRuntimeSettingsMigrations`, and reloads after migration.

## 4. Read the effective value every time

```csharp
public sealed class OutboxOptions
{
    public int PollSeconds { get; set; }
}

builder.Services.Configure<OutboxOptions>(builder.Configuration.GetSection("Events:Outbox"));
```

```csharp
public sealed class OutboxWorker
{
    private readonly IOptionsMonitor<OutboxOptions> _options;

    public OutboxWorker(IOptionsMonitor<OutboxOptions> options)
    {
        _options = options;
    }

    public Task PollAsync(CancellationToken cancellationToken)
    {
        var seconds = _options.CurrentValue.PollSeconds;
        return Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
    }
}
```

`IRuntimeSettings.Get<int>("Events:Outbox:PollSeconds")` returns the same number. Do not store `PollSeconds` in a field at startup. After a panel save, `IOptionsMonitor.CurrentValue` and `IRuntimeSettings` both see the stored value without a restart.

Use `requiresRestart: true` only when the consumer really copies the value once. The flag does not freeze configuration. It tells the panel that a running copy will not notice the change.

## 5. Do not register secrets

These calls throw and the service does not start:

```csharp
settings.String("ConnectionStrings:Default", "...", "...", RuntimeSettingAudience.DevOps, 200);
settings.String("Payment:GatewayToken", "...", "...", RuntimeSettingAudience.Admin, 200);
```

This call is legal:

```csharp
settings.Int("Tokens:DailyCap", "سقف روزانه", "بیشترین تعداد در روز.", RuntimeSettingAudience.Admin, 1, 100000);
```

## 6. What the panel calls

DevOps search and history require `devops.runtimesettings.View`. Save and reset require `devops.runtimesettings.Update`.

```http
POST /devops/runtime-settings/Save
```

```json
{ "key": "Events:Outbox:PollSeconds", "valueJson": "10", "expectedDate_Update": null }
```

`valueJson` is raw JSON. An integer is `10`, not `"\"10\""`. The response `data` is one item. `date_Update` comes back as host-local `yyyy-MM-dd'T'HH:mm:ss` with no `Z`. Send that same string back as `expectedDate_Update` to detect a conflicting update.

Reset:

```json
{ "key": "Events:Outbox:PollSeconds" }
```

After reset, configuration returns the appsettings value `5`.
