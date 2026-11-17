using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SRT.Core.Authentication;
using SRT.Core.Domain;

namespace SRT.Core.RuntimeSettings.Tests;

public sealed class RuntimeSettingsControllerRegistrationTests
{
    [Fact]
    public async Task Feature_provider_exposes_both_controllers()
    {
        var endpoints = await EndpointsAsync(useRuntimeSettings: true);
        var routes = endpoints.Select(endpoint => endpoint.RoutePattern.RawText).ToList();

        Assert.Contains("devops/runtime-settings/Search", routes);
        Assert.Contains("devops/runtime-settings/Save", routes);
        Assert.Contains("devops/runtime-settings/Reset", routes);
        Assert.Contains("devops/runtime-settings/History", routes);
        Assert.Contains("superadmin/runtime-settings/Search", routes);
        Assert.Contains("superadmin/runtime-settings/Save", routes);
        Assert.Contains("superadmin/runtime-settings/Reset", routes);
        Assert.Contains("superadmin/runtime-settings/History", routes);

        AssertAccess(endpoints, "devops/runtime-settings/Search", "DevOps", RuntimeSettingsAccess.DevOpsView);
        AssertAccess(endpoints, "devops/runtime-settings/History", "DevOps", RuntimeSettingsAccess.DevOpsView);
        AssertAccess(endpoints, "devops/runtime-settings/Save", "DevOps", RuntimeSettingsAccess.DevOpsUpdate);
        AssertAccess(endpoints, "devops/runtime-settings/Reset", "DevOps", RuntimeSettingsAccess.DevOpsUpdate);
        AssertAccess(endpoints, "superadmin/runtime-settings/Search", "Admin", RuntimeSettingsAccess.AdminView);
        AssertAccess(endpoints, "superadmin/runtime-settings/Save", "Admin", RuntimeSettingsAccess.AdminUpdate);
    }

    [Fact]
    public async Task Without_registration_no_runtime_settings_route_exists()
    {
        Assert.Empty(await EndpointsAsync(useRuntimeSettings: false));
    }

    private static async Task<List<RouteEndpoint>> EndpointsAsync(bool useRuntimeSettings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Services.AddControllers();
        var setting = new AppSetting
        {
            ConnectionStrings = new AppConnectionString
            {
                DefaultConnection = new GeneralConnectionString
                {
                    connectionType = "sql",
                    host = "localhost",
                    port = "1433",
                    dbName = "Unused"
                }
            }
        };

        if (useRuntimeSettings)
        {
            using var connection = RuntimeSettingsScope.OpenConnection();
            var factory = RuntimeSettingsScope.FactoryFor(connection, createSchema: true);
            builder.SRT_AddRuntimeSettings(setting, register =>
                register.Bool("Feature:Enabled", "فعال", "کلید نمونه", RuntimeSettingAudience.DevOps), factory);
        }

        await new RuntimeSettingsBasicConfigurations().SRT_ConfigBuilder(builder, setting);
        var app = builder.Build();
        app.MapControllers();
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("runtime-settings", StringComparison.Ordinal) == true)
            .ToList();
    }

    private static void AssertAccess(IEnumerable<RouteEndpoint> endpoints, string route, string userType, string access)
    {
        var endpoint = Assert.Single(endpoints, item => item.RoutePattern.RawText == route);
        var attribute = endpoint.Metadata.GetMetadata<CheckAuthMiddleWareAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(userType, Read(attribute, "userType"));
        Assert.Equal(access, Read(attribute, "requiredAccess"));
    }

    private static string? Read(CheckAuthMiddleWareAttribute attribute, string name)
        => typeof(CheckAuthMiddleWareAttribute).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(attribute) as string;
}
