using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agent.Migrations.Memory
{
    /// <inheritdoc />
    public partial class InitialMemoryDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PrimaryUserId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "MemoryEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    IsAuthoritative = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Provenance = table.Column<int>(type: "INTEGER", nullable: false),
                    Confirmation = table.Column<int>(type: "INTEGER", nullable: false),
                    Confidentiality = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    SubjectUserId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    EmbeddingProvider = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EmbeddingModelId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EmbeddingDim = table.Column<int>(type: "INTEGER", nullable: false),
                    EmbeddingVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    L2Normed = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmbeddingVector = table.Column<string>(type: "TEXT", nullable: true),
                    RedactionMask = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TombstonedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReplacesMemoryId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Locale = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Consents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Granted = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceUsers_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRuntimeStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    NamePromptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastNamePromptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastOnlineSearchAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastOnlineSearchDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OnlineSearchCount = table.Column<int>(type: "INTEGER", nullable: false),
                    OnlineSearchIsStarted = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRuntimeStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRuntimeStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Consents_Kind",
                table: "Consents",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_Consents_UserId",
                table: "Consents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Consents_UserId_Kind",
                table: "Consents",
                columns: new[] { "UserId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_FirstSeen",
                table: "Devices",
                column: "FirstSeen");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LastSeen",
                table: "Devices",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PrimaryUserId",
                table: "Devices",
                column: "PrimaryUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUsers_Confidence",
                table: "DeviceUsers",
                column: "Confidence");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUsers_DeviceId",
                table: "DeviceUsers",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUsers_DeviceId_UserId",
                table: "DeviceUsers",
                columns: new[] { "DeviceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUsers_LastSeen",
                table: "DeviceUsers",
                column: "LastSeen");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUsers_UserId",
                table: "DeviceUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Confidentiality",
                table: "MemoryEntries",
                column: "Confidentiality");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Confidentiality_Type",
                table: "MemoryEntries",
                columns: new[] { "Confidentiality", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_CreatedAt",
                table: "MemoryEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_DeviceFingerprint",
                table: "MemoryEntries",
                column: "DeviceFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_ExpiresAt",
                table: "MemoryEntries",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_OwnerUserId",
                table: "MemoryEntries",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_OwnerUserId_Type",
                table: "MemoryEntries",
                columns: new[] { "OwnerUserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Scope",
                table: "MemoryEntries",
                column: "Scope");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Scope_Type",
                table: "MemoryEntries",
                columns: new[] { "Scope", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_SubjectUserId",
                table: "MemoryEntries",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_TombstonedAt",
                table: "MemoryEntries",
                column: "TombstonedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Type",
                table: "MemoryEntries",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_UserRuntimeStates_LastNamePromptAt",
                table: "UserRuntimeStates",
                column: "LastNamePromptAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserRuntimeStates_LastOnlineSearchAt",
                table: "UserRuntimeStates",
                column: "LastOnlineSearchAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserRuntimeStates_UserId",
                table: "UserRuntimeStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedAt",
                table: "Users",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DisplayName",
                table: "Users",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastSeenAt",
                table: "Users",
                column: "LastSeenAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Consents");

            migrationBuilder.DropTable(
                name: "DeviceUsers");

            migrationBuilder.DropTable(
                name: "MemoryEntries");

            migrationBuilder.DropTable(
                name: "UserRuntimeStates");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
