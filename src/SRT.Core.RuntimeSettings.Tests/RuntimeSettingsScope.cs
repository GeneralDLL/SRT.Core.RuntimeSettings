using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SRT.Core.Domain;
using SRT.Core.RuntimeSettings.Database;

namespace SRT.Core.RuntimeSettings.Tests;

internal sealed class RuntimeSettingsScope : IDisposable
{
    public enum DatabaseMode
    {
        Ready,
        MissingTable,
        Unavailable
    }

    private readonly SqliteConnection _connection;
    private WebApplication? _app;

    private RuntimeSettingsScope(SqliteConnection connection, WebApplicationBuilder builder, RuntimeSettingsRegistry registry, Func<RuntimeSettingsDbContext> factory)
    {
        _connection = connection;
        Builder = builder;
        Registry = registry;
        Factory = factory;
    }

    public WebApplicationBuilder Builder { get; }
    public RuntimeSettingsRegistry Registry { get; }
    public Func<RuntimeSettingsDbContext> Factory { get; }
    public WebApplication App => _app ??= Builder.Build();
    public IConfiguration Config => App.Configuration;
    public IRuntimeSettings Settings => App.Services.GetRequiredService<IRuntimeSettings>();
    public RuntimeSettingsEditor Editor => App.Services.GetRequiredService<RuntimeSettingsEditor>();

    public static RuntimeSettingsScope Open(
        Dictionary<string, string?> settings,
        Action<RuntimeSettingsBuilder> register,
        DatabaseMode mode = DatabaseMode.Ready)
    {
        var connection = OpenConnection();
        var factory = mode == DatabaseMode.Unavailable
            ? () => throw new InvalidOperationException("database is down")
            : FactoryFor(connection, mode == DatabaseMode.Ready);
        var builder = CreateBuilder(settings);
        builder.SRT_AddRuntimeSettings(new AppSetting(), register, factory);
        var registry = RuntimeSettingsExtensions.Find(builder.Services)
            ?? throw new InvalidOperationException("Registry was not registered.");
        return new RuntimeSettingsScope(connection, builder, registry, factory);
    }

    public static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();
        return connection;
    }

    public static Func<RuntimeSettingsDbContext> FactoryFor(SqliteConnection connection, bool createSchema)
    {
        var appConnection = new AppConnectionString();
        var options = new DbContextOptionsBuilder<RuntimeSettingsDbContext>().UseSqlite(connection).Options;
        if (createSchema)
        {
            using var db = new RuntimeSettingsDbContext(options, appConnection);
            db.Database.EnsureCreated();
        }

        return () => new RuntimeSettingsDbContext(options, appConnection);
    }

    public static WebApplicationBuilder CreateBuilder(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Configuration.AddInMemoryCollection(settings);
        return builder;
    }

    public void Dispose()
    {
        if (_app is IAsyncDisposable asyncDisposable)
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _connection.Dispose();
    }
}
