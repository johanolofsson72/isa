# Workflow Integration for Scheduled Reports

## Overview

The AnalysisPlugin integrates with AI agent orchestration through Workflows to provide automated, scheduled financial reporting. This document outlines the integration patterns and configuration required for scheduled report generation.

## Architecture

### Components
- **Workflows**: Trigger AI agent at scheduled intervals
- **AI Agent**: Orchestrates report generation and email delivery
- **AnalysisPlugin**: Generates financial reports from SIE4 data
- **MailPlugin**: Delivers generated reports via email
- **FortnoxPlugin**: Provides SIE4 financial data

### Flow Diagram
```
┌─────────────┐    ┌─────────────┐    ┌─────────────────┐
│  Workflows  │───▶│  AI Agent   │───▶│ AnalysisPlugin │
│ (Scheduler) │    │(Orchestrator)│    │ (Report Gen)   │
└─────────────┘    └─────────────┘    └─────────────────┘
                           │                     │
                           ▼                     ▼
                   ┌─────────────┐    ┌─────────────────┐
                   │ MailPlugin  │    │ FortnoxPlugin   │
                   │(Email Send) │    │ (SIE4 Data)     │
                   └─────────────┘    └─────────────────┘
```

## Integration Pattern

### 1. Workflow Trigger
Workflows are configured with cron expressions to trigger at specific times:
- Daily reports: `0 8 * * 1-5` (8 AM weekdays)
- Weekly reports: `0 9 * * 1` (9 AM Mondays)
- Monthly reports: `0 10 1 * *` (10 AM first day of month)

### 2. AI Agent Orchestration
When triggered, the AI agent performs the following sequence:

```python
# Pseudo-code for AI agent orchestration
async def generate_scheduled_report(schedule_config):
    try:
        # Step 1: Retrieve SIE4 data from FortnoxPlugin
        sie4_data = await fortnox_plugin.get_sie_async(
            financial_year=schedule_config.year
        )
        
        # Step 2: Generate report using AnalysisPlugin
        report_result = await analysis_plugin.generate_financial_report_async(
            sie4_data=sie4_data,
            report_type=schedule_config.report_type,
            format=schedule_config.format,
            start_date=schedule_config.start_date,
            end_date=schedule_config.end_date,
            language=schedule_config.language
        )
        
        # Step 3: Handle errors with structured error codes
        if not report_result.is_successful:
            await handle_report_error(report_result)
            return
        
        # Step 4: Send report via MailPlugin
        await mail_plugin.send_email_async(
            to=schedule_config.recipients,
            subject=f"Scheduled Financial Report - {schedule_config.period}",
            body=generate_email_body(report_result),
            attachments=report_result.report_files
        )
        
        # Step 5: Log success
        logger.info(f"Scheduled report sent successfully: {report_result.transaction_count} transactions")
        
    except Exception as ex:
        await handle_orchestration_error(ex, schedule_config)
```

### 3. Error Handling
The AI agent handles different error scenarios based on structured error codes:

#### Recoverable Errors
- `REQUEST_VALIDATION_FAILED`: Adjust parameters and retry
- `NO_TRANSACTIONS_FOUND`: Notify recipients of empty period
- `COMPONENT_CONFIGURATION_INVALID`: Use default configurations

#### Non-Recoverable Errors  
- `TIMEOUT_EXCEEDED`: Alert system administrators
- `INSUFFICIENT_MEMORY`: Scale resources or reduce scope
- `UNEXPECTED_ERROR`: Log for investigation

## Configuration

### Workflow Configuration
Workflows should be configured with the following parameters:

```yaml
name: "Monthly Financial Report"
trigger:
  cron: "0 10 1 * *"  # 10 AM on first day of month
parameters:
  report_type: "Financial"
  format: "Excel"
  period: "monthly"
  recipients: ["finance@company.com", "ceo@company.com"]
  language: "en"
```

### AI Agent Configuration
The AI agent requires access to:
- AnalysisPlugin functions via Semantic Kernel
- FortnoxPlugin for SIE4 data retrieval
- MailPlugin for email delivery
- Configuration for report parameters and recipients

## Security Considerations

### Authentication
- AI agent must have valid API keys for all plugins
- Workflow triggers should be authenticated and authorized
- Email recipients should be validated against allowed domains

### Data Protection
- SIE4 data contains sensitive financial information
- Reports should be encrypted in transit and at rest
- Access logs should be maintained for audit purposes

### Error Information
- Error details should not expose sensitive data in logs
- Failed report attempts should be monitored and alerted

## Monitoring and Alerting

### Success Metrics
- Report generation time
- Email delivery success rate
- Recipient engagement (if tracking enabled)

### Failure Alerts
- Failed report generation
- Email delivery failures
- Data retrieval errors from FortnoxPlugin

### Performance Monitoring
- Memory usage during large report generation
- Processing time for different report types
- Archive storage growth

## Best Practices

### Scheduling
- Avoid peak business hours for large reports
- Stagger multiple scheduled reports to prevent resource conflicts
- Consider time zones for international recipients

### Report Optimization
- Use appropriate date ranges to balance completeness and performance
- Configure memory limits based on expected data volume
- Implement retry logic for transient failures

### Maintenance
- Regularly review and update recipient lists
- Monitor archive storage and implement retention policies
- Test scheduled reports in staging environment before production deployment
