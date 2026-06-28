using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agent.Migrations.Agent
{
    /// <inheritdoc />
    public partial class InitialAgentDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true),
                    UserInput = table.Column<string>(type: "TEXT", nullable: true),
                    AIResponse = table.Column<string>(type: "TEXT", nullable: true),
                    WorkflowId = table.Column<string>(type: "TEXT", nullable: true),
                    PluginName = table.Column<string>(type: "TEXT", nullable: true),
                    FunctionName = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    JobName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    JobGroup = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TriggerKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TriggerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TriggerGroup = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    StackTrace = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    WorkflowId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ScheduledTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobStatistics",
                columns: table => new
                {
                    JobKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    JobName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    JobGroup = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    WorkflowId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TotalExecutions = table.Column<int>(type: "INTEGER", nullable: false),
                    SuccessfulExecutions = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedExecutions = table.Column<int>(type: "INTEGER", nullable: false),
                    CancelledExecutions = table.Column<int>(type: "INTEGER", nullable: false),
                    VetoedExecutions = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageExecutionTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    MinExecutionTimeMs = table.Column<long>(type: "INTEGER", nullable: false),
                    MaxExecutionTimeMs = table.Column<long>(type: "INTEGER", nullable: false),
                    LastExecutionTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastExecutionStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    NextScheduledTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobStatistics", x => x.JobKey);
                });

            migrationBuilder.CreateTable(
                name: "OpenAICache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResponseContent = table.Column<string>(type: "TEXT", nullable: false),
                    RequestData = table.Column<string>(type: "TEXT", nullable: true),
                    ResponseData = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenAICache", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PluginStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PluginId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    PluginName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExecutionId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    WorkflowName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WorkflowPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TriggeredBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TotalSteps = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedSteps = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedSteps = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutionLog = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowExecutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventType",
                table: "AuditLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_PluginName",
                table: "AuditLogs",
                column: "PluginName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SessionId",
                table: "AuditLogs",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Status",
                table: "AuditLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_WorkflowId",
                table: "AuditLogs",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_JobKey",
                table: "JobExecutions",
                column: "JobKey");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_JobKey_StartTime",
                table: "JobExecutions",
                columns: new[] { "JobKey", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_StartTime",
                table: "JobExecutions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_Status",
                table: "JobExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_WorkflowId",
                table: "JobExecutions",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_JobStatistics_JobName",
                table: "JobStatistics",
                column: "JobName");

            migrationBuilder.CreateIndex(
                name: "IX_JobStatistics_LastExecutionTime",
                table: "JobStatistics",
                column: "LastExecutionTime");

            migrationBuilder.CreateIndex(
                name: "IX_JobStatistics_NextScheduledTime",
                table: "JobStatistics",
                column: "NextScheduledTime");

            migrationBuilder.CreateIndex(
                name: "IX_JobStatistics_WorkflowId",
                table: "JobStatistics",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenAICache_CreatedAt",
                table: "OpenAICache",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OpenAICache_ExpiresAt",
                table: "OpenAICache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OpenAICache_LastAccessedAt",
                table: "OpenAICache",
                column: "LastAccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OpenAICache_RequestHash",
                table: "OpenAICache",
                column: "RequestHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginStates_IsEnabled",
                table: "PluginStates",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_PluginStates_LastModified",
                table: "PluginStates",
                column: "LastModified");

            migrationBuilder.CreateIndex(
                name: "IX_PluginStates_PluginId",
                table: "PluginStates",
                column: "PluginId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginStates_PluginName",
                table: "PluginStates",
                column: "PluginName");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_ExecutionId",
                table: "WorkflowExecutions",
                column: "ExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_StartedAt",
                table: "WorkflowExecutions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_Status",
                table: "WorkflowExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_Trigger",
                table: "WorkflowExecutions",
                column: "Trigger");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_WorkflowName",
                table: "WorkflowExecutions",
                column: "WorkflowName");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_WorkflowName_StartedAt",
                table: "WorkflowExecutions",
                columns: new[] { "WorkflowName", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "JobExecutions");

            migrationBuilder.DropTable(
                name: "JobStatistics");

            migrationBuilder.DropTable(
                name: "OpenAICache");

            migrationBuilder.DropTable(
                name: "PluginStates");

            migrationBuilder.DropTable(
                name: "WorkflowExecutions");
        }
    }
}
