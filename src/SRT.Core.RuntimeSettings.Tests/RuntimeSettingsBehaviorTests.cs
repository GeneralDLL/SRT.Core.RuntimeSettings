using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SRT.Core.Domain;
using SRT.Core.Domain.RequestsDomqins;
using SRT.Core.Json;
using SRT.Core.RuntimeSettings.Controllers;
using SRT.Core.RuntimeSettings.Database;

namespace SRT.Core.RuntimeSettings.Tests;

public sealed class RuntimeSettingsBehaviorTests
{
    private static readonly RuntimeSettingActor Actor = new("DevOps", 4, "Panel User");

    [Fact]
    public void Missing_row_returns_appsettings_and_is_not_overridden()
    {
        using var scope = RuntimeSettingsScope.Open(PollSettings("5"), RegisterPoll);
        Assert.Equal("5", scope.Config["Events:Outbox:PollSeconds"]);
        Assert.Equal(5, scope.Settings.Get<int>("Events:Outbox:PollSeconds"));
        Assert.False(scope.Settings.IsOverridden("Events:Outbox:PollSeconds"));
        Assert.Equal(30, scope.Settings.Get<int>(RuntimeSettingsRegistry.ReloadSecondsKey));
    }

    [Fact]
    public async Task Save_is_visible_from_configuration_options_and_runtime_settings()
    {
        using var scope = RuntimeSettingsScope.Open(PollSettings("5"), RegisterPoll);
        scope.Builder.Services.Configure<PollSection>(scope.Builder.Configuration.GetSection("Events:Outbox"));
        var saved = await scope.Editor.SaveAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingSaveRequest
        {
            key = "Events:Outbox:PollSeconds",
            valueJson = "10"
        }, Actor, CancellationToken.None);

        Assert.Equal(200, saved.Status);
        var item = Assert.IsType<RuntimeSettingItem>(saved.Body);
        Assert.True(item.isOverridden);
        Assert.Equal("10", item.valueJson);
        Assert.Equal("5", item.defaultValueJson);
        Assert.Equal("10", scope.Config["Events:Outbox:PollSeconds"]);
        Assert.Equal(10, scope.Settings.Get<int>("Events:Outbox:PollSeconds"));
        Assert.Equal(10, scope.App.Services.GetRequiredService<IOptionsMonitor<PollSection>>().CurrentValue.PollSeconds);
    }

    [Fact]
    public async Task Rebuilding_the_source_uses_the_stored_value()
    {
        var connection = RuntimeSettingsScope.OpenConnection();
        var factory = RuntimeSettingsScope.FactoryFor(connection, createSchema: true);
        await using var first = await SaveOnNewHost(connection, factory, "10");
        await using var second = OpenHost(PollSettings("5"), RegisterPoll, factory);
        Assert.Equal("10", second.Config["Events:Outbox:PollSeconds"]);
        Assert.True(second.Settings.IsOverridden("Events:Outbox:PollSeconds"));
    }

    [Fact]
    public async Task Reset_removes_the_row_and_restores_appsettings()
    {
        using var scope = RuntimeSettingsScope.Open(PollSettings("5"), RegisterPoll);
        var before = await scope.Editor.ResetAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingResetRequest
        {
            key = "Events:Outbox:PollSeconds"
        }, Actor, CancellationToken.None);
        Assert.Equal(200, before.Status);
        Assert.Equal(0, await CountAudits(scope));

        await scope.Editor.SaveAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingSaveRequest
        {
            key = "Events:Outbox:PollSeconds",
            valueJson = "10"
        }, Actor, CancellationToken.None);
        var reset = await scope.Editor.ResetAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingResetRequest
        {
            key = "Events:Outbox:PollSeconds"
        }, Actor, CancellationToken.None);

        Assert.Equal(200, reset.Status);
        var item = Assert.IsType<RuntimeSettingItem>(reset.Body);
        Assert.False(item.isOverridden);
        Assert.Equal("5", scope.Config["Events:Outbox:PollSeconds"]);
        Assert.False(scope.Settings.IsOverridden("Events:Outbox:PollSeconds"));
        await using var db = scope.Registry.CreateContext();
        Assert.Equal(0, await db.RuntimeSettings.CountAsync());
        Assert.Equal(2, await db.RuntimeSettingAudits.CountAsync());
    }

    [Fact]
    public async Task Unregistered_key_is_rejected_and_its_row_is_ignored()
    {
        var settings = PollSettings("5");
        settings["NotRegistered:Value"] = "1";
        using var scope = RuntimeSettingsScope.Open(settings, RegisterPoll);
        await using (var db = scope.Registry.CreateContext())
        {
            db.RuntimeSettings.Add(Row("NotRegistered:Value", "9"));
            await db.SaveChangesAsync();
        }

        scope.Registry.Provider!.Load();
        Assert.Equal("1", scope.Config["NotRegistered:Value"]);
        var save = await scope.Editor.SaveAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingSaveRequest
        {
            key = "NotRegistered:Value",
            valueJson = "2"
        }, Actor, CancellationToken.None);
        Assert.Equal(404, save.Status);
    }

    [Fact]
    public void Reserved_keys_are_rejected_and_near_keys_are_allowed()
    {
        using var scope = RuntimeSettingsScope.Open(new Dictionary<string, string?>(), register =>
        {
            register.Bool("Tokens:DailyCap", "سقف", "سقف روزانه", RuntimeSettingAudience.DevOps);
            register.Bool("Author:Mode", "حالت", "حالت نویسنده", RuntimeSettingAudience.DevOps);
            register.Bool("S2Sample:On", "نمونه", "نمونه مجاز", RuntimeSettingAudience.DevOps);
            register.Bool("Mail:Passphrase", "عبارت", "عبارت مجاز", RuntimeSettingAudience.DevOps);
            register.Int("Catalog:Keystone", "شاخص", "شاخص مجاز", RuntimeSettingAudience.Admin, 1, 5);
        });

        string[] forbidden =
        [
            "ConnectionStrings:Main",
            "ConnectionDB:Main",
            "Service:Name",
            "urlData:Base",
            "ServiceAuth:Client",
            "ServiceTokenCache:Minutes",
            "AuthSettings:Issuer",
            "SeedPasswords:One",
            "Logging:Level",
            "Kestrel:Limits",
            "AllowedHosts:Name",
            "AUTH_Mode:On",
            "S2S_Mode:On",
            "Mail:ClientSecret",
            "Mail:Password",
            "Mail:Passwd",
            "Hash:Pepper",
            "Login:Credential",
            "Data:ConnectionString",
            "Sign:PrivateKey",
            "Mail:ApiKey",
            "Tls:Certificate",
            "X:MyPass",
            "X:DbPwd",
            "X:GatewayToken",
            "X:PublicKey",
            ""
        ];

        foreach (var key in forbidden)
        {
            var rejected = key;
            Assert.Throws<InvalidOperationException>(() => scope.Builder.SRT_AddRuntimeSettings(
                new AppSetting(),
                register => register.Bool(rejected, "عنوان", "توضیح", RuntimeSettingAudience.DevOps),
                scope.Factory));
        }

        Assert.True(scope.Registry.TryGet("Tokens:DailyCap", out _));
        Assert.False(scope.Registry.TryGet("X:GatewayToken", out _));
    }

    [Fact]
    public async Task Invalid_values_are_rejected()
    {
        using var scope = RuntimeSettingsScope.Open(new Dictionary<string, string?>
        {
            ["Limits:Count"] = "1",
            ["Limits:Ratio"] = "1.5",
            ["Limits:Mode"] = "On",
            ["Limits:Names:0"] = "a"
        }, register =>
        {
            register.Int("Limits:Count", "تعداد", "تعداد مجاز", RuntimeSettingAudience.DevOps, 1, 3);
            register.Decimal("Limits:Ratio", "نسبت", "نسبت مجاز", RuntimeSettingAudience.DevOps, 0m, 2m);
            register.Enum("Limits:Mode", "حالت", "حالت مجاز", RuntimeSettingAudience.DevOps, ["On", "Off"]);
            register.StringList("Limits:Names", "نام‌ها", "فهرست نام", RuntimeSettingAudience.DevOps, 2, 4);
        });

        Assert.Equal(400, (await Save(scope, "Limits:Count", "\"5\"")).Status);
        Assert.Equal(400, (await Save(scope, "Limits:Count", "9")).Status);
        Assert.Equal(400, (await Save(scope, "Limits:Ratio", "3.5")).Status);
        Assert.Equal(400, (await Save(scope, "Limits:Mode", "\"Side\"")).Status);
        Assert.Equal(400, (await Save(scope, "Limits:Names", "[\"a\",\"b\",\"c\"]")).Status);
        Assert.Equal("1", scope.Config["Limits:Count"]);
    }

    [Fact]
    public async Task Overridden_list_and_json_replace_appsettings_children()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Catalog:Names:0"] = "one",
            ["Catalog:Names:1"] = "two",
            ["Catalog:Names:2"] = "three",
            ["Catalog:Map:a"] = "1",
            ["Catalog:Map:b"] = "2",
            ["Catalog:Map:c"] = "3"
        };
        using var scope = RuntimeSettingsScope.Open(settings, register =>
        {
            register.StringList("Catalog:Names", "نام‌ها", "فهرست نام", RuntimeSettingAudience.DevOps, 10, 20);
            register.Json("Catalog:Map", "نگاشت", "نگاشت مجاز", RuntimeSettingAudience.DevOps, 2000);
        });

        Assert.Equal(200, (await Save(scope, "Catalog:Names", "[\"only\"]")).Status);
        Assert.Equal(200, (await Save(scope, "Catalog:Map", "{\"a\":\"1\"}")).Status);

        var names = scope.Config.GetSection("Catalog:Names").Get<string[]>();
        Assert.NotNull(names);
        Assert.Equal(["only"], names);
        var map = scope.Config.GetSection("Catalog:Map").Get<Dictionary<string, string>>();
        Assert.NotNull(map);
        Assert.Single(map);
        Assert.Equal("1", map["a"]);
    }

    [Fact]
    public void Unavailable_database_or_missing_table_keeps_appsettings()
    {
        using var down = RuntimeSettingsScope.Open(PollSettings("5"), RegisterPoll, RuntimeSettingsScope.DatabaseMode.Unavailable);
        Assert.Equal("5", down.Config["Events:Outbox:PollSeconds"]);

        using var missing = RuntimeSettingsScope.Open(PollSettings("5"), RegisterPoll, RuntimeSettingsScope.DatabaseMode.MissingTable);
        Assert.Equal("5", missing.Config["Events:Outbox:PollSeconds"]);
    }

    [Fact]
    public void Invalid_stored_row_is_not_applied()
    {
        using var scope = RuntimeSettingsScope.Open(PollSettings("5"), RegisterPoll);
        using (var db = scope.Registry.CreateContext())
        {
            db.RuntimeSettings.Add(Row("Events:Outbox:PollSeconds", "\"nope\""));
            db.SaveChanges();
        }

        scope.Registry.Provider!.Load();
        Assert.Equal("5", scope.Config["Events:Outbox:PollSeconds"]);
        Assert.False(scope.Settings.IsOverridden("Events:Outbox:PollSeconds"));
        var item = Item(scope, "Events:Outbox:PollSeconds");
        Assert.True(item.hasInvalidStoredValue);
        Assert.False(item.isOverridden);
    }

    [Fact]
    public async Task Save_and_reset_each_write_an_audit_row()
    {
        using var scope = RuntimeSettingsScope.Open(PollSettings("5"), RegisterPoll);
        await Save(scope, "Events:Outbox:PollSeconds", "10");
        await scope.Editor.ResetAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingResetRequest
        {
            key = "Events:Outbox:PollSeconds"
        }, Actor, CancellationToken.None);

        await using var db = scope.Registry.CreateContext();
        var actions = await db.RuntimeSettingAudits.OrderBy(row => row.id).Select(row => row.action).ToListAsync();
        Assert.Equal([RuntimeSettingsEditor.SaveAction, RuntimeSettingsEditor.ResetAction], actions);
    }

    [Fact]
    public async Task Stale_expected_timestamp_returns_conflict()
    {
        using var scope = RuntimeSettingsScope.Open(PollSettings("5"), RegisterPoll);
        var saved = await Save(scope, "Events:Outbox:PollSeconds", "10");
        var item = Assert.IsType<RuntimeSettingItem>(saved.Body);
        var stale = await scope.Editor.SaveAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingSaveRequest
        {
            key = "Events:Outbox:PollSeconds",
            valueJson = "11",
            expectedDate_Update = item.date_Update!.Value.AddHours(-1)
        }, Actor, CancellationToken.None);

        Assert.Equal(409, stale.Status);
        Assert.Equal("10", scope.Config["Events:Outbox:PollSeconds"]);

        var controller = DevOpsController(scope);
        var http = Assert.IsType<ObjectResult>(await controller.Save(new RuntimeSettingSaveRequest
        {
            key = "Events:Outbox:PollSeconds",
            valueJson = "12",
            expectedDate_Update = item.date_Update.Value.AddHours(-1)
        }, CancellationToken.None));
        Assert.Equal(409, http.StatusCode);
        Assert.Equal("10", scope.Config["Events:Outbox:PollSeconds"]);
    }

    [Fact]
    public async Task Reload_is_not_raised_when_data_is_unchanged()
    {
        using var scope = RuntimeSettingsScope.Open(PollSettings("5"), RegisterPoll);
        await Save(scope, "Events:Outbox:PollSeconds", "10");
        var calls = 0;
        using var subscription = scope.Config.GetReloadToken().RegisterChangeCallback(_ => calls++, null);
        scope.Registry.Provider!.Load();
        Assert.Equal(0, calls);

        await using (var db = scope.Registry.CreateContext())
        {
            var row = await db.RuntimeSettings.SingleAsync();
            row.valueJson = "15";
            await db.SaveChangesAsync();
        }

        scope.Registry.Provider.Load();
        Assert.Equal(1, calls);
        Assert.Equal("15", scope.Config["Events:Outbox:PollSeconds"]);
    }

    [Fact]
    public void Repeated_registration_accumulates_and_adds_one_source()
    {
        var connection = RuntimeSettingsScope.OpenConnection();
        var factory = RuntimeSettingsScope.FactoryFor(connection, createSchema: true);
        var builder = RuntimeSettingsScope.CreateBuilder(PollSettings("5"));
        var before = builder.Configuration.Sources.Count;
        builder.SRT_AddRuntimeSettings(new AppSetting(), register =>
            register.Bool("Feature:First", "اول", "کلید اول", RuntimeSettingAudience.DevOps), factory);
        Assert.Throws<InvalidOperationException>(() => builder.SRT_AddRuntimeSettings(new AppSetting(), register =>
            register.Bool("Feature:First", "اول", "کلید اول", RuntimeSettingAudience.DevOps), factory));
        builder.SRT_AddRuntimeSettings(new AppSetting(), register =>
            register.Bool("Feature:Second", "دوم", "کلید دوم", RuntimeSettingAudience.Admin), factory);

        Assert.Equal(before + 1, builder.Configuration.Sources.Count);
        var registry = RuntimeSettingsExtensions.Find(builder.Services);
        Assert.NotNull(registry);
        Assert.True(registry.TryGet("Feature:First", out _));
        Assert.True(registry.TryGet("Feature:Second", out _));
        Assert.True(registry.TryGet(RuntimeSettingsRegistry.ReloadSecondsKey, out _));
        connection.Dispose();
    }

    [Fact]
    public async Task Cross_key_rule_rejects_a_breaking_save()
    {
        using var scope = RuntimeSettingsScope.Open(new Dictionary<string, string?>
        {
            ["Limits:Min"] = "1",
            ["Limits:Max"] = "10"
        }, register =>
        {
            register.Int("Limits:Min", "کمینه", "حد پایین", RuntimeSettingAudience.DevOps, 0, 100);
            register.Int("Limits:Max", "بیشینه", "حد بالا", RuntimeSettingAudience.DevOps, 0, 100);
            register.Rule(settings => settings.Get<int>("Limits:Min") <= settings.Get<int>("Limits:Max")
                ? null
                : "کمینه از بیشینه بزرگ‌تر است.");
        });

        var rejected = await Save(scope, "Limits:Min", "50");
        Assert.Equal(400, rejected.Status);
        Assert.Equal("کمینه از بیشینه بزرگ‌تر است.", rejected.Message);
        Assert.Equal("1", scope.Config["Limits:Min"]);
    }

    [Fact]
    public async Task Each_controller_shows_only_its_audience()
    {
        using var scope = RuntimeSettingsScope.Open(new Dictionary<string, string?>
        {
            ["Ops:Enabled"] = "true",
            ["Shop:Enabled"] = "false"
        }, register =>
        {
            register.Bool("Ops:Enabled", "عملیات", "کلید عملیات", RuntimeSettingAudience.DevOps);
            register.Bool("Shop:Enabled", "فروشگاه", "کلید فروشگاه", RuntimeSettingAudience.Admin);
        });

        var devops = DataOf(Assert.IsType<ObjectResult>(await DevOpsController(scope).Search(new RuntimeSettingSearchRequest(), CancellationToken.None)));
        var admin = DataOf(Assert.IsType<ObjectResult>(await AdminController(scope).Search(new RuntimeSettingSearchRequest(), CancellationToken.None)));
        Assert.Contains(devops.items, item => item.key == "Ops:Enabled");
        Assert.DoesNotContain(devops.items, item => item.key == "Shop:Enabled");
        Assert.Contains(admin.items, item => item.key == "Shop:Enabled");
        Assert.DoesNotContain(admin.items, item => item.key == "Ops:Enabled");
        Assert.DoesNotContain(admin.items, item => item.key == RuntimeSettingsRegistry.ReloadSecondsKey);

        var hidden = Assert.IsType<ObjectResult>(await DevOpsController(scope).Save(new RuntimeSettingSaveRequest
        {
            key = "Shop:Enabled",
            valueJson = "true"
        }, CancellationToken.None));
        Assert.Equal(404, hidden.StatusCode);
    }

    [Fact]
    public void Response_dates_use_the_local_wall_clock_without_an_offset()
    {
        var utc = new DateTime(2026, 11, 16, 7, 0, 0, DateTimeKind.Utc);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new LocalDateTimeJsonConverter());
        var json = JsonSerializer.Serialize(utc, options);
        var expected = utc.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal("\"" + expected + "\"", json);
        Assert.DoesNotContain("Z", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Json_baseline_writes_canonical_numbers()
    {
        using var doc = JsonDocument.Parse(BaselineFromJson(
            """
            {
              "Targets": {
                "Orders": 9002,
                "Neg": -3,
                "Frac": 1.5,
                "Pad": "007",
                "One": 1.0,
                "Plus": "+5",
                "Sci": "1e3",
                "Blank": ""
              }
            }
            """,
            "Targets"));
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Number, root.GetProperty("Orders").ValueKind);
        Assert.Equal(9002, root.GetProperty("Orders").GetInt32());
        Assert.Equal(-3, root.GetProperty("Neg").GetInt32());
        Assert.Equal(1.5, root.GetProperty("Frac").GetDouble());
        Assert.Equal("007", root.GetProperty("Pad").GetString());
        Assert.Equal("1.0", root.GetProperty("One").GetString());
        Assert.Equal("+5", root.GetProperty("Plus").GetString());
        Assert.Equal("1e3", root.GetProperty("Sci").GetString());
        Assert.Equal("", root.GetProperty("Blank").GetString());
    }

    [Fact]
    public void Json_baseline_writes_booleans()
    {
        using var doc = JsonDocument.Parse(BaselineFromJson(
            """
            {
              "Flags": { "On": true, "Off": false, "Text": "true", "Word": "FALSE" }
            }
            """,
            "Flags"));
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.True, root.GetProperty("On").ValueKind);
        Assert.Equal(JsonValueKind.False, root.GetProperty("Off").ValueKind);
        Assert.True(root.GetProperty("Text").GetBoolean());
        Assert.False(root.GetProperty("Word").GetBoolean());
    }

    [Fact]
    public async Task Json_baseline_keeps_empty_object_and_reports_missing_as_null()
    {
        Assert.Equal("{}", BaselineFromJson("""{ "Subscriptions": { } }""", "Subscriptions"));
        Assert.Equal("null", BaselineFromJson("""{ "Subscriptions": { } }""", "Missing"));
        Assert.Equal("\"\"", BaselineFromJson("""{ "Items": [] }""", "Items"));

        using var scope = RuntimeSettingsScope.Open(new Dictionary<string, string?>
        {
            ["Catalog:Map"] = null
        }, register => register.Json("Catalog:Map", "نگاشت", "نگاشت", RuntimeSettingAudience.DevOps, 2000));
        var outcome = await scope.Editor.SearchAsync(
            RuntimeSettingAudience.DevOps,
            new RuntimeSettingSearchRequest { text = "Catalog:Map", pageSize = 50 },
            CancellationToken.None);
        Assert.Equal(200, outcome.Status);
        var body = Assert.IsType<RuntimeSettingSearchResult>(outcome.Body);
        var item = Assert.Single(body.items, entry => entry.key == "Catalog:Map");
        Assert.Equal("{}", item.defaultValueJson);
    }

    [Fact]
    public void Json_baseline_number_shaped_string_is_a_number()
    {
        using var doc = JsonDocument.Parse(BaselineFromJson(
            """{ "Codes": { "Port": "8012" } }""",
            "Codes"));
        Assert.Equal(JsonValueKind.Number, doc.RootElement.GetProperty("Port").ValueKind);
        Assert.Equal(8012, doc.RootElement.GetProperty("Port").GetInt32());
    }

    private static string BaselineFromJson(string json, string key)
    {
        var dir = Path.Combine(Path.GetTempPath(), "srt-rs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "appsettings.json");
        File.WriteAllText(file, json);
        try
        {
            var config = new ConfigurationBuilder().SetBasePath(dir).AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();
            var definition = new RuntimeSettingDefinition
            {
                Key = key,
                Title = "title",
                Description = "description",
                Audience = RuntimeSettingAudience.DevOps,
                Type = RuntimeSettingType.Json,
                MaxLength = 4000
            };
            return RuntimeSettingsValues.BaselineJson(definition, config);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static void RegisterPoll(RuntimeSettingsBuilder register)
        => register.Int("Events:Outbox:PollSeconds", "فاصله", "فاصله‌ی خواندن صف", RuntimeSettingAudience.DevOps, 1, 300);

    private static Dictionary<string, string?> PollSettings(string seconds) => new()
    {
        ["Events:Outbox:PollSeconds"] = seconds
    };

    private static async Task<EditorOutcome> Save(RuntimeSettingsScope scope, string key, string valueJson)
        => await scope.Editor.SaveAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingSaveRequest
        {
            key = key,
            valueJson = valueJson
        }, Actor, CancellationToken.None);

    private static RuntimeSettingItem Item(RuntimeSettingsScope scope, string key)
    {
        var outcome = scope.Editor.SearchAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingSearchRequest
        {
            text = key,
            pageSize = 100
        }, CancellationToken.None).GetAwaiter().GetResult();
        var page = Assert.IsType<RuntimeSettingSearchResult>(outcome.Body);
        return Assert.Single(page.items, item => item.key == key);
    }

    private static async Task<int> CountAudits(RuntimeSettingsScope scope)
    {
        await using var db = scope.Registry.CreateContext();
        return await db.RuntimeSettingAudits.CountAsync();
    }

    private static RuntimeSettingRow Row(string key, string valueJson) => new()
    {
        key = key,
        valueJson = valueJson,
        date_Update = DateTime.UtcNow,
        userType_AdminChange = "DevOps",
        id_AdminChange = 4,
        fullName_AdminChange = "Panel User"
    };

    private static DevOpsRuntimeSettingsController DevOpsController(RuntimeSettingsScope scope)
    {
        var controller = new DevOpsRuntimeSettingsController(scope.Editor);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static AdminRuntimeSettingsController AdminController(RuntimeSettingsScope scope)
    {
        var controller = new AdminRuntimeSettingsController(scope.Editor);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static RuntimeSettingSearchResult DataOf(ObjectResult result)
    {
        Assert.Equal(200, result.StatusCode);
        var envelope = Assert.IsType<ResponseData>(result.Value);
        return Assert.IsType<RuntimeSettingSearchResult>(envelope.data);
    }

    private static async Task<HostSession> SaveOnNewHost(SqliteConnection connection, Func<RuntimeSettingsDbContext> factory, string value)
    {
        var session = OpenHost(PollSettings("5"), RegisterPoll, factory);
        await session.Editor.SaveAsync(RuntimeSettingAudience.DevOps, new RuntimeSettingSaveRequest
        {
            key = "Events:Outbox:PollSeconds",
            valueJson = value
        }, Actor, CancellationToken.None);
        return session;
    }

    private static HostSession OpenHost(
        Dictionary<string, string?> settings,
        Action<RuntimeSettingsBuilder> register,
        Func<RuntimeSettingsDbContext> factory)
    {
        var builder = RuntimeSettingsScope.CreateBuilder(settings);
        builder.SRT_AddRuntimeSettings(new AppSetting(), register, factory);
        return new HostSession(builder);
    }

    private sealed class PollSection
    {
        public int PollSeconds { get; set; }
    }

    private sealed class HostSession : IAsyncDisposable
    {
        private readonly WebApplicationBuilder _builder;
        private WebApplication? _app;

        public HostSession(WebApplicationBuilder builder) => _builder = builder;

        public WebApplication App => _app ??= _builder.Build();
        public IConfiguration Config => App.Configuration;
        public IRuntimeSettings Settings => App.Services.GetRequiredService<IRuntimeSettings>();
        public RuntimeSettingsEditor Editor => App.Services.GetRequiredService<RuntimeSettingsEditor>();

        public async ValueTask DisposeAsync()
        {
            if (_app is not null)
                await _app.DisposeAsync();
        }
    }
}
