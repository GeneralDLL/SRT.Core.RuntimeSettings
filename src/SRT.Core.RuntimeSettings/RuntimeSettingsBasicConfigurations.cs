using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SRT.Core.Database.SQLServer;
using SRT.Core.Domain;
using SRT.Core.Domain.Diagnostics;
using SRT.Core.Interfaces;
using SRT.Core.RuntimeSettings.Controllers;
using SRT.Core.RuntimeSettings.Database;

namespace SRT.Core.RuntimeSettings
{
    /// <summary>
    /// Discovers itself from <c>SRT.Core.RuntimeSettings.dll</c>. Does nothing until the service calls <c>SRT_AddRuntimeSettings</c>.
    /// Order 60 is free and runs after SQL Server (20), PostgreSQL (21), Redis (30), RabbitMQ (40), and Swagger (50).
    /// </summary>
    public sealed class RuntimeSettingsBasicConfigurations : ISRTBasicConfigurations
    {
        public int Order => 60;
        public string FeatureName => "RuntimeSettings";

        public Task<WebApplicationBuilder> SRT_ConfigBuilder(WebApplicationBuilder builder, AppSetting configApp)
        {
            var registry = RuntimeSettingsExtensions.Find(builder.Services);
            if (registry is null)
            {
                configApp.StartupReport.Set("runtime-settings", SRTStartupReport.Status.Off, "استفاده نشده");
                return Task.FromResult(builder);
            }

            builder.SRT_AddGeneralConfig(configApp.ConnectionStrings, typeof(RuntimeSettingsDbContext).Namespace!);
            builder.Services.TryAddSingleton<IRuntimeSettings>(sp =>
                new RuntimeSettingsStore(sp.GetRequiredService<IConfiguration>(), registry));
            builder.Services.TryAddSingleton(sp =>
                new RuntimeSettingsEditor(registry, sp.GetRequiredService<IRuntimeSettings>()));
            builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
                manager.FeatureProviders.Add(new RuntimeSettingsControllerFeatureProvider()));
            builder.Services.AddSingleton<IHostedService>(sp => new RuntimeSettingsReloadService(
                registry,
                sp.GetRequiredService<IRuntimeSettings>()));
            return Task.FromResult(builder);
        }

        public async Task<WebApplication> SRT_ConfigApp(WebApplication app, AppSetting configApp)
        {
            var registry = app.Services.GetService<RuntimeSettingsRegistry>();
            if (registry is null)
                return app;

            await app.SRT_CheckDatabase(typeof(RuntimeSettingsDbContext).Namespace!).ConfigureAwait(false);
            registry.Provider?.Load();
            var defined = registry.Count;
            var overridden = registry.Provider?.OverriddenCount ?? 0;
            configApp.StartupReport.Ok("runtime-settings", $"defined={defined} overridden={overridden}");
            return app;
        }
    }
}
