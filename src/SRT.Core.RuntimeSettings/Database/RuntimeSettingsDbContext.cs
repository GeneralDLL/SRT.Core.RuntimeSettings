using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRT.Core.Databases;
using SRT.Core.Domain;

namespace SRT.Core.RuntimeSettings.Database
{
    public sealed class RuntimeSettingsDbContext : DataBase
    {
        public RuntimeSettingsDbContext(DbContextOptions<RuntimeSettingsDbContext> options, AppConnectionString connectionStrings)
            : base(options, connectionStrings)
        {
        }

        public DbSet<RuntimeSettingRow> RuntimeSettings => Set<RuntimeSettingRow>();
        public DbSet<RuntimeSettingAuditRow> RuntimeSettingAudits => Set<RuntimeSettingAuditRow>();

        public override void BaseConfiguration(WebApplicationBuilder builder, AppConnectionString connectionStrings)
        {
            builder.Services.AddDbContext<RuntimeSettingsDbContext>(options =>
                options.UseSqlServer(
                    connectionStrings.DefaultConnection.GetConnectionString(),
                    sql => sql.MigrationsAssembly(typeof(RuntimeSettingsDbContext).Assembly.GetName().Name)
                        .MigrationsHistoryTable("__SRTRuntimeSettingsMigrations")));
        }

        public override Task InsertBaseData() => Task.CompletedTask;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RuntimeSettingRow>(entity =>
            {
                entity.ToTable("RuntimeSettings");
                entity.HasKey(row => row.key);
                entity.Property(row => row.key).HasMaxLength(160);
                entity.Property(row => row.valueJson).IsRequired();
                entity.Property(row => row.userType_AdminChange).HasMaxLength(20);
                entity.Property(row => row.fullName_AdminChange).HasMaxLength(200);
            });

            modelBuilder.Entity<RuntimeSettingAuditRow>(entity =>
            {
                entity.ToTable("RuntimeSettingAudits");
                entity.HasKey(row => row.id);
                entity.Property(row => row.id).UseIdentityColumn();
                entity.Property(row => row.key).HasMaxLength(160);
                entity.Property(row => row.action).HasMaxLength(10);
                entity.Property(row => row.userType).HasMaxLength(20);
                entity.Property(row => row.fullNameUser).HasMaxLength(200);
                entity.HasIndex(row => new { row.key, row.date_Create })
                    .HasDatabaseName("IX_RuntimeSettingAudits_key_date_Create");
            });
        }
    }
}
