using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsureChatBot.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ThemeBrand = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Persona = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SalesApproach = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Compliance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Style = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LlmEnabled = table.Column<bool>(type: "bit", nullable: true),
                    LlmBaseUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LlmModel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LlmTemperature = table.Column<double>(type: "float", nullable: true),
                    LlmMaxTokens = table.Column<int>(type: "int", nullable: true),
                    LlmTimeoutSeconds = table.Column<int>(type: "int", nullable: true),
                    EmailUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailTo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CustCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
