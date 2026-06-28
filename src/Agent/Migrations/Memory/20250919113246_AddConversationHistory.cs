using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agent.Migrations.Memory
{
    /// <inheritdoc />
    public partial class AddConversationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationHistory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MessageOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessingTimeMs = table.Column<long>(type: "INTEGER", nullable: true),
                    HasError = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationHistory_CreatedAt",
                table: "ConversationHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationHistory_DeviceFingerprint",
                table: "ConversationHistory",
                column: "DeviceFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationHistory_ExpiresAt",
                table: "ConversationHistory",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationHistory_SessionId_MessageOrder",
                table: "ConversationHistory",
                columns: new[] { "SessionId", "MessageOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationHistory");
        }
    }
}
