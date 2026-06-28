using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Linq;
using AnalysisPlugin.Models;
using AnalysisPlugin.Components;
using OfficeOpenXml;

namespace AnalysisPlugin.Services;

/// <summary>
/// Factory for creating report components with JSON schema validation and dynamic component creation
/// </summary>
public class ComponentFactory
{
    private readonly ILogger<ComponentFactory> _logger;
    private readonly Dictionary<string, JSchema> _componentSchemas;

    public ComponentFactory(ILogger<ComponentFactory> logger)
    {
        _logger = logger;
        _componentSchemas = InitializeSchemas();
    }

    /// <summary>
    /// Creates a report component with configuration validation
    /// </summary>
    /// <param name="componentType">Type of component to create</param>
    /// <param name="title">Component title</param>
    /// <param name="configuration">Component configuration</param>
    /// <returns>Created component or null if validation fails</returns>
    public IReportComponent? CreateComponent(string componentType, string title, Dictionary<string, object> configuration)
    {
        try
        {
            _logger.LogDebug("Creating component of type {ComponentType} with title {Title}", componentType, title);

            // Validate configuration against schema (lenient for now)
            if (!ValidateConfiguration(componentType, configuration))
            {
                _logger.LogWarning("Configuration validation failed for component type {ComponentType}, proceeding anyway", componentType);
                // Continue anyway for now - validation warnings only
            }

            // Create component based on type
            _logger.LogError("🔥🔥🔥 ComponentFactory skapar komponent: {ComponentType}", componentType);
            Console.WriteLine($"🔥🔥🔥 ComponentFactory skapar komponent: {componentType}");
            IReportComponent? component = componentType.ToLowerInvariant() switch
            {
                "frontpage" => new Components.FrontPageComponent(),
                "chart" => new Components.ChartComponent(),
                "table" => new Components.TableComponent(),
                "pivot" => new Components.PivotComponent(),
                "mlanalysis" => new Components.MLAnalysisComponent(),
                "advancedchart" => new Components.AdvancedChartComponent(),
                "financialanalysis" => new Components.FinancialAnalysisComponent(),
                "vatreport" => new Components.VATReportComponent("2025Q3"),
                "balancesheet" => new Components.BalanceSheetComponent(),
                "incomestatement" => new Components.IncomeStatementComponent(),
                _ => null
            };

            if (component != null)
            {
                component.Title = title;
                component.Configuration = configuration;
                _logger.LogInformation("Successfully created {ComponentType} component: {Title} (Type: {ActualType})",
                    componentType, title, component.GetType().Name);
            }
            else
            {
                _logger.LogWarning("Unknown component type: {ComponentType}", componentType);
            }

            return component;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create component of type {ComponentType}", componentType);
            return null;
        }
    }

    /// <summary>
    /// Validates component configuration against JSON schema
    /// </summary>
    /// <param name="componentType">Component type</param>
    /// <param name="configuration">Configuration to validate</param>
    /// <returns>True if valid</returns>
    public bool ValidateConfiguration(string componentType, Dictionary<string, object> configuration)
    {
        try
        {
            if (!_componentSchemas.TryGetValue(componentType.ToLowerInvariant(), out var schema))
            {
                _logger.LogWarning("No schema found for component type {ComponentType}", componentType);
                return true; // Allow unknown types for flexibility
            }

            var configJson = JObject.FromObject(configuration);
            var isValid = configJson.IsValid(schema, out IList<string> errorMessages);

            if (!isValid)
            {
                _logger.LogWarning("Configuration validation failed for {ComponentType}: {Errors}", 
                    componentType, string.Join(", ", errorMessages));
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration for {ComponentType}", componentType);
            return false;
        }
    }

    private Dictionary<string, JSchema> InitializeSchemas()
    {
        var schemas = new Dictionary<string, JSchema>();

        // Front Page Component Schema
        schemas["frontpage"] = JSchema.Parse(@"{
            'type': 'object',
            'properties': {
                'subtitle': { 'type': 'string' },
                'companyName': { 'type': 'string' },
                'logoPath': { 'type': 'string' },
                'reportDate': { 'type': 'string', 'format': 'date' }
            },
            'required': ['subtitle', 'companyName']
        }");

        // Chart Component Schema
        schemas["chart"] = JSchema.Parse(@"{
            'type': 'object',
            'properties': {
                'chartType': { 'type': 'string', 'enum': ['revenue_trend', 'expense_breakdown', 'profit_margin', 'forecast_chart'] },
                'dataSource': { 'type': 'string' },
                'dateRange': { 'type': 'object' },
                'colors': { 'type': 'array', 'items': { 'type': 'string' } }
            },
            'required': ['chartType', 'dataSource']
        }");

        // Table Component Schema
        schemas["table"] = JSchema.Parse(@"{
            'type': 'object',
            'properties': {
                'tableType': { 'type': 'string', 'enum': ['transactions', 'monthly_summary', 'account_summary'] },
                'sortable': { 'type': 'boolean' },
                'filterable': { 'type': 'boolean' },
                'columns': { 'type': 'array', 'items': { 'type': 'string' } },
                'pageSize': { 'type': 'integer', 'minimum': 10, 'maximum': 1000 }
            },
            'required': ['tableType']
        }");

        // Pivot Component Schema
        schemas["pivot"] = JSchema.Parse(@"{
            'type': 'object',
            'properties': {
                'pivotType': { 'type': 'string', 'enum': ['monthly_revenue', 'project_costs', 'account_breakdown'] },
                'groupBy': { 'type': 'string' },
                'aggregateBy': { 'type': 'string' },
                'filters': { 'type': 'object' }
            },
            'required': ['pivotType', 'groupBy', 'aggregateBy']
        }");

        // ML Analysis Component Schema
        schemas["mlanalysis"] = JSchema.Parse(@"{
            'type': 'object',
            'properties': {
                'analysisType': { 'type': 'string', 'enum': ['trend_analysis', 'forecast', 'anomaly_detection'] },
                'confidenceLevel': { 'type': 'number', 'minimum': 0.5, 'maximum': 1.0 },
                'timeHorizon': { 'type': 'integer', 'minimum': 1, 'maximum': 36 }
            },
            'required': ['analysisType']
        }");

        // Advanced Chart Component Schema
        schemas["advancedchart"] = JSchema.Parse(@"{
            'type': 'object',
            'properties': {
                'chartType': { 'type': 'string', 'enum': ['waterfall', 'gantt', 'combo', 'area', 'scatter', 'funnel', 'treemap', 'boxwhisker', 'stock'] },
                'dataSource': { 'type': 'string' },
                'data': { 'type': 'array' },
                'colors': { 'type': 'array', 'items': { 'type': 'string' } }
            },
            'required': ['chartType']
        }");

        // Financial Analysis Component Schema
        schemas["financialanalysis"] = JSchema.Parse(@"{
            'type': 'object',
            'properties': {
                'analysisType': { 'type': 'string', 'enum': ['cashflow', 'budgetvsactual', 'breakeven', 'roi', 'ratios', 'variance', 'trend'] },
                'dataSource': { 'type': 'string' },
                'data': { 'type': 'array' },
                'timeHorizon': { 'type': 'integer', 'minimum': 1, 'maximum': 36 }
            },
            'required': ['analysisType']
        }");

        return schemas;
    }
}

// Stub component implementations (keeping for backward compatibility)
public class FrontPageComponentStub : ReportComponentBase
{
    public override string ComponentType => "FrontPage";
    
    public override Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        // Stub implementation
        worksheet.Cells[startRow, 1].Value = Title;
        worksheet.Cells[startRow + 1, 1].Value = Configuration.GetValueOrDefault("subtitle", "").ToString();
        worksheet.Cells[startRow + 2, 1].Value = Configuration.GetValueOrDefault("companyName", "").ToString();

        ApplyProfessionalStyling(worksheet.Cells[startRow, 1, startRow + 2, 2], true);
        
        return Task.FromResult(ComponentResult.CreateSuccess(5));
    }
}

public class ChartComponentStub : ReportComponentBase
{
    public override string ComponentType => "Chart";

    public override Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        // Stub implementation - would create actual charts in production
        worksheet.Cells[startRow, 1].Value = $"Chart: {Title}";
        worksheet.Cells[startRow + 1, 1].Value = $"Type: {Configuration.GetValueOrDefault("chartType", "unknown")}";

        return Task.FromResult(ComponentResult.CreateSuccess(10));
    }
}

public class TableComponentStub : ReportComponentBase
{
    public override string ComponentType => "Table";

    public override Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        // Stub implementation
        worksheet.Cells[startRow, 1].Value = $"Table: {Title}";
        worksheet.Cells[startRow + 1, 1].Value = $"Type: {Configuration.GetValueOrDefault("tableType", "unknown")}";

        return Task.FromResult(ComponentResult.CreateSuccess(15));
    }
}

public class PivotComponentStub : ReportComponentBase
{
    public override string ComponentType => "Pivot";

    public override Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        // Stub implementation
        worksheet.Cells[startRow, 1].Value = $"Pivot Table: {Title}";

        return Task.FromResult(ComponentResult.CreateSuccess(20));
    }
}

public class MLAnalysisComponentStub : ReportComponentBase
{
    public override string ComponentType => "MLAnalysis";

    public override Task<ComponentResult> RenderAsync(ExcelWorksheet worksheet, int startRow)
    {
        // Stub implementation
        worksheet.Cells[startRow, 1].Value = $"ML Analysis: {Title}";

        return Task.FromResult(ComponentResult.CreateSuccess(12));
    }
}
