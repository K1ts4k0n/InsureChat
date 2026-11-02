using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsureChatBot.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_ConversationsAndOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CarBrand = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CarModel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CarYear = table.Column<int>(type: "int", nullable: true),
                    InsuranceClass = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    SumInsure = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrentStep = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SpinReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProspectName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ProspectPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ProspectEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProspectIdCard = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ProspectLicensePlate = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    OutcomeStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: true),
                    CustName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CustPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CustEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CustIdCard = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LicensePlate = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false),
                    InsuranceClass = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    PackageId = table.Column<int>(type: "int", nullable: false),
                    InsurerName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SumInsure = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Premium = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    EmailStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    EmailSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationNeeds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    Need = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationNeeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationNeeds_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationTurns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    Seq = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationTurns_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationNeeds_ConversationId",
                table: "ConversationNeeds",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_SessionKey",
                table: "Conversations",
                column: "SessionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTurns_ConversationId_Seq",
                table: "ConversationTurns",
                columns: new[] { "ConversationId", "Seq" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNo",
                table: "Orders",
                column: "OrderNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationNeeds");

            migrationBuilder.DropTable(
                name: "ConversationTurns");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Conversations");
        }
    }
}
