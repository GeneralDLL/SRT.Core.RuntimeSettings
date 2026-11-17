using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SRT.Core.Domain;
using SRT.Core.RuntimeSettings.Database;

namespace SRT.Core.RuntimeSettings
{
    public static class RuntimeSettingsExtensions
    {
        /// <summary>
        /// Registers runtime settings and, on the first call, adds the highest-priority configuration source.
        /// Later calls add more definitions to the same registry and do not add another source.
        /// </summary>
        public static WebApplicationBuilder SRT_AddRuntimeSettings(
            this WebApplicationBuilder builder,
            AppSetting config,
            Action<RuntimeSettingsBuilder> register)
            => SRT_AddRuntimeSettings(builder, config, register, contextFactory: null);

        internal static WebApplicationBuilder SRT_AddRuntimeSettings(
            this WebApplicationBuilder builder,
            AppSetting config,
            Action<RuntimeSettingsBuilder> register,
            Func<RuntimeSettingsDbContext>? contextFactory)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(register);

            var registry = Find(builder.Services);
            var created = registry is null;
            registry ??= new RuntimeSettingsRegistry();
            if (created)
            {
                registry.ContextFactory = contextFactory ?? CreateSqlFactory(config);
                RegisterBuiltIn(registry);
                registry.Baseline = CaptureBaseline(builder.Configuration);
                builder.Services.AddSingleton(registry);
                builder.Services.AddSingleton<IRuntimeSettings>(sp =>
                    new RuntimeSettingsStore(sp.GetRequiredService<IConfiguration>(), registry));
                builder.Services.AddSingleton(sp =>
                    new RuntimeSettingsEditor(registry, sp.GetRequiredService<IRuntimeSettings>()));
            }
            else if (contextFactory is not null)
            {
                registry.ContextFactory = contextFactory;
            }

            register(new RuntimeSettingsBuilder(registry));

            if (registry.Provider is null)
                ((IConfigurationBuilder)builder.Configuration).Add(new RuntimeSettingsConfigurationSource(registry, Logger));
            else
                registry.Provider.Load();

            return builder;
        }

        internal static RuntimeSettingsRegistry? Find(IServiceCollection services)
        {
            for (var index = services.Count - 1; index >= 0; index--)
            {
                var descriptor = services[index];
                if (descriptor.ServiceType == typeof(RuntimeSettingsRegistry) && descriptor.ImplementationInstance is RuntimeSettingsRegistry registry)
                    return registry;
            }

            return null;
        }

        private static void RegisterBuiltIn(RuntimeSettingsRegistry registry)
        {
            registry.Add(new RuntimeSettingDefinition
            {
                Key = RuntimeSettingsRegistry.ReloadSecondsKey,
                Title = "فاصله‌ی بازخوانی",
                Description = "هر چند ثانیه یک‌بار تنظیم‌های ذخیره‌شده دوباره خوانده می‌شوند.",
                Audience = RuntimeSettingAudience.DevOps,
                Type = RuntimeSettingType.Int,
                IntMin = 5,
                IntMax = 600
            });
        }

        private static readonly ILogger Logger = LoggerFactory.Create(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddConsole();
        }).CreateLogger("SRT.Core.RuntimeSettings");

        private static IConfigurationRoot CaptureBaseline(ConfigurationManager configuration)
        {
            try
            {
                var baseline = new ConfigurationBuilder();
                foreach (var source in configuration.Sources)
                    baseline.Add(source);
                return baseline.Build();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    "Runtime settings baseline was copied from current values ({ExceptionType}).",
                    ex.GetType().Name);
                var snapshot = configuration.AsEnumerable()
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                return new ConfigurationBuilder().AddInMemoryCollection(snapshot).Build();
            }
        }

        private static Func<RuntimeSettingsDbContext> CreateSqlFactory(AppSetting config)
        {
            var raw = config.ConnectionStrings.DefaultConnection.GetConnectionString();
            var connectionString = raw;
            try
            {
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var sql = new SqlConnectionStringBuilder(raw) { ConnectTimeout = 3 };
                    connectionString = sql.ConnectionString;
                }
            }
            catch (ArgumentException)
            {
                connectionString = raw;
            }

            var appConnection = config.ConnectionStrings;
            return () =>
            {
                var options = new DbContextOptionsBuilder<RuntimeSettingsDbContext>()
                    .UseSqlServer(connectionString, sql => sql
                        .MigrationsAssembly(typeof(RuntimeSettingsDbContext).Assembly.GetName().Name)
                        .MigrationsHistoryTable("__SRTRuntimeSettingsMigrations")
                        .CommandTimeout(3))
                    .Options;
                return new RuntimeSettingsDbContext(options, appConnection);
            };
        }

    }
}
