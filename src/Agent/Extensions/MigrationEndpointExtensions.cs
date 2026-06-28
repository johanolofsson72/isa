using Agent.Models.Migration;
using Agent.Models;
using Agent.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Extensions;

/// <summary>
/// Extension methods for workflow migration API endpoints
/// </summary>
public static class MigrationEndpointExtensions
{
    /// <summary>
    /// Maps workflow migration endpoints
    /// </summary>
    public static WebApplication MapMigrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/migration")
            .WithTags("Migration")
            .WithOpenApi();

        // GET: Discover workflows that can be migrated
        group.MapGet("/discover", DiscoverWorkflowsAsync)
            .WithName("DiscoverWorkflows")
            .WithSummary("Discover workflows for migration")
            .WithDescription("Discover all workflows that can be migrated to Quartz.NET");

        // POST: Migrate workflows to Quartz.NET
        group.MapPost("/migrate", MigrateWorkflowsAsync)
            .WithName("MigrateWorkflows")
            .WithSummary("Migrate workflows to Quartz.NET")
            .WithDescription("Migrate all discovered workflows to Quartz.NET scheduling");

        // POST: Dry run migration (preview only)
        group.MapPost("/migrate/dry-run", DryRunMigrationAsync)
            .WithName("DryRunMigration")
            .WithSummary("Preview workflow migration")
            .WithDescription("Preview what would happen during migration without making changes");

        // GET: Validate migration status
        group.MapGet("/validate", ValidateMigrationAsync)
            .WithName("ValidateMigration")
            .WithSummary("Validate migration status")
            .WithDescription("Validate that all workflows have been properly migrated to Quartz.NET");

        // POST: Create backup before migration
        group.MapPost("/backup/{backupName}", CreateBackupAsync)
            .WithName("CreateBackup")
            .WithSummary("Create workflow backup")
            .WithDescription("Create a backup of current workflows before migration");

        // GET: List available backups
        group.MapGet("/backups", ListBackupsAsync)
            .WithName("ListBackups")
            .WithSummary("List available backups")
            .WithDescription("List all available workflow backups");

        // POST: Rollback to a backup
        group.MapPost("/rollback/{backupName}", RollbackAsync)
            .WithName("RollbackMigration")
            .WithSummary("Rollback to backup")
            .WithDescription("Rollback workflows to a previous backup state");

        // GET: Validate schedule preservation
        group.MapGet("/validate/schedules", ValidateSchedulePreservationAsync)
            .WithName("ValidateSchedulePreservation")
            .WithSummary("Validate schedule preservation")
            .WithDescription("Validate that all workflow schedules have been preserved during migration");

        return app;
    }

    private static async Task<IResult> DiscoverWorkflowsAsync(
        [FromServices] WorkflowMigrationService migrationService,
        [FromServices] ILogger<WorkflowMigrationService> logger)
    {
        try
        {
            logger.LogInformation("🔍 API request: Discover workflows for migration");
            
            var result = await migrationService.DiscoverWorkflowsAsync();
            
            if (!result.IsSuccess)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error discovering workflows");
            return Results.Problem(
                detail: ex.Message,
                title: "Error discovering workflows",
                statusCode: 500);
        }
    }

    private static async Task<IResult> MigrateWorkflowsAsync(
        [FromServices] WorkflowMigrationService migrationService,
        [FromServices] ILogger<WorkflowMigrationService> logger)
    {
        try
        {
            logger.LogInformation("🔄 API request: Migrate workflows to Quartz.NET");
            
            var result = await migrationService.MigrateAllWorkflowsAsync(dryRun: false);
            
            if (!result.IsSuccess)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error migrating workflows");
            return Results.Problem(
                detail: ex.Message,
                title: "Error migrating workflows",
                statusCode: 500);
        }
    }

    private static async Task<IResult> DryRunMigrationAsync(
        [FromServices] WorkflowMigrationService migrationService,
        [FromServices] ILogger<WorkflowMigrationService> logger)
    {
        try
        {
            logger.LogInformation("🔍 API request: Dry run migration preview");
            
            var result = await migrationService.MigrateAllWorkflowsAsync(dryRun: true);
            
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error performing dry run migration");
            return Results.Problem(
                detail: ex.Message,
                title: "Error performing dry run migration",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ValidateMigrationAsync(
        [FromServices] WorkflowMigrationService migrationService,
        [FromServices] ILogger<WorkflowMigrationService> logger)
    {
        try
        {
            logger.LogInformation("✅ API request: Validate migration status");
            
            var result = await migrationService.ValidateMigrationAsync();
            
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error validating migration");
            return Results.Problem(
                detail: ex.Message,
                title: "Error validating migration",
                statusCode: 500);
        }
    }

    private static async Task<IResult> CreateBackupAsync(
        string backupName,
        [FromServices] WorkflowRollbackService rollbackService,
        [FromServices] ILogger<WorkflowRollbackService> logger)
    {
        try
        {
            logger.LogInformation("💾 API request: Create backup {BackupName}", backupName);
            
            var result = await rollbackService.CreateBackupAsync(backupName);
            
            if (!result.IsSuccess)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error creating backup");
            return Results.Problem(
                detail: ex.Message,
                title: "Error creating backup",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ListBackupsAsync(
        [FromServices] WorkflowRollbackService rollbackService,
        [FromServices] ILogger<WorkflowRollbackService> logger)
    {
        try
        {
            logger.LogInformation("📋 API request: List available backups");
            
            var backups = await rollbackService.ListBackupsAsync();
            
            return Results.Ok(backups);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error listing backups");
            return Results.Problem(
                detail: ex.Message,
                title: "Error listing backups",
                statusCode: 500);
        }
    }

    private static async Task<IResult> RollbackAsync(
        string backupName,
        [FromServices] WorkflowRollbackService rollbackService,
        [FromServices] ILogger<WorkflowRollbackService> logger)
    {
        try
        {
            logger.LogInformation("🔄 API request: Rollback to backup {BackupName}", backupName);

            var result = await rollbackService.RollbackAsync(backupName);

            if (!result.IsSuccess)
            {
                return Results.BadRequest(result);
            }

            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error performing rollback");
            return Results.Problem(
                detail: ex.Message,
                title: "Error performing rollback",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ValidateSchedulePreservationAsync(
        [FromServices] WorkflowRollbackService rollbackService,
        [FromServices] ILogger<WorkflowRollbackService> logger)
    {
        try
        {
            logger.LogInformation("📅 API request: Validate schedule preservation");
            
            var result = await rollbackService.ValidateSchedulePreservationAsync();
            
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error validating schedule preservation");
            return Results.Problem(
                detail: ex.Message,
                title: "Error validating schedule preservation",
                statusCode: 500);
        }
    }
}
