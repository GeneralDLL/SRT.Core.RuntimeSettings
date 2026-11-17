using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SRT.Core.RuntimeSettings.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialRuntimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuntimeSettingAudits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    key = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    oldValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    newValueJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    action = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    userType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    idUser = table.Column<int>(type: "int", nullable: false),
                    fullNameUser = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    date_Create = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeSettingAudits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeSettings",
                columns: table => new
                {
                    key = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    valueJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    date_Update = table.Column<DateTime>(type: "datetime2", nullable: false),
                    userType_AdminChange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    id_AdminChange = table.Column<int>(type: "int", nullable: false),
                    fullName_AdminChange = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeSettings", x => x.key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeSettingAudits_key_date_Create",
                table: "RuntimeSettingAudits",
                columns: new[] { "key", "date_Create" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuntimeSettingAudits");

            migrationBuilder.DropTable(
                name: "RuntimeSettings");
        }
    }
}
