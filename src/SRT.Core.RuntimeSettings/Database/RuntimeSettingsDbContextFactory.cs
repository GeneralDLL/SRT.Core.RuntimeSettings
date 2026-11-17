using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRT.Core.Domain;

namespace SRT.Core.RuntimeSettings.Database
{
    /// <summary>
    /// Design-time factory for EF tools. Reads <c>SRT_DESIGNTIME_CONNECTION</c> and otherwise uses a local trusted connection.
    /// Not used at runtime.
    /// </summary>
    public sealed class RuntimeSettingsDbContextFactory : IDesignTimeDbContextFactory<RuntimeSettingsDbContext>
    {
        public const string FallbackConnection =
            "Server=localhost;Database=SRT_DesignTime;Trusted_Connection=True;TrustServerCertificate=True";

        public RuntimeSettingsDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("SRT_DESIGNTIME_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
                connectionString = FallbackConnection;

            var options = new DbContextOptionsBuilder<RuntimeSettingsDbContext>()
                .UseSqlServer(
                    connectionString,
                    sql => sql.MigrationsAssembly(typeof(RuntimeSettingsDbContext).Assembly.GetName().Name)
                        .MigrationsHistoryTable("__SRTRuntimeSettingsMigrations"))
                .Options;

            return new RuntimeSettingsDbContext(options, new AppConnectionString());
        }
    }
}
