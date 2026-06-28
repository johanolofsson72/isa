# Example Workflow Configurations for Scheduled Reports

This document provides ready-to-use Workflow configurations for common financial reporting schedules using the AnalysisPlugin.

## Daily Reports

### Daily Cash Flow Report
**Purpose**: Monitor daily cash position and transactions
**Schedule**: Every weekday at 8:00 AM
**Recipients**: Finance team, CFO

```yaml
name: "Daily Cash Flow Report"
description: "Automated daily cash flow analysis for weekdays"
trigger:
  cron: "0 8 * * 1-5"  # 8 AM Monday-Friday
  timezone: "Europe/Stockholm"

parameters:
  report_config:
    report_type: "Financial"
    format: "Excel"
    language: "sv"
    date_range: "yesterday"  # Previous business day
    
  components:
    - type: "Chart"
      title: "Daily Cash Flow"
      chart_type: "Line"
      data_source: "Transactions"
      filters:
        account_categories: ["Assets"]
        account_codes: ["1930", "1940", "1950"]  # Bank accounts
        
    - type: "Table"
      title: "Large Transactions"
      data_source: "Transactions"
      filters:
        amount_threshold: 50000
      sorting:
        - field: "Amount"
          direction: "Desc"
      limit: 20

  email_config:
    recipients: 
      - "finance@company.com"
      - "cfo@company.com"
    subject: "Daily Cash Flow Report - {{date}}"
    priority: "Normal"
    
  error_handling:
    retry_attempts: 2
    retry_delay_minutes: 15
    fallback_recipients: ["it-support@company.com"]
```

### Daily Transaction Summary
**Purpose**: Quick overview of daily transaction volume and categories
**Schedule**: Every day at 7:30 AM
**Recipients**: Accounting team

```yaml
name: "Daily Transaction Summary"
description: "Daily transaction volume and category breakdown"
trigger:
  cron: "30 7 * * *"  # 7:30 AM daily
  timezone: "Europe/Stockholm"

parameters:
  report_config:
    report_type: "Financial"
    format: "PDF"
    language: "en"
    date_range: "yesterday"
    
  components:
    - type: "Chart"
      title: "Transaction Volume by Category"
      chart_type: "Pie"
      data_source: "Transactions"
      group_by: "AccountCategory"
      aggregation: "Count"
      
    - type: "Table"
      title: "Category Summary"
      data_source: "Transactions"
      columns:
        - field: "AccountCategory"
          title: "Category"
        - field: "TransactionCount"
          title: "Count"
        - field: "TotalAmount"
          title: "Total Amount"
          format: "Currency"

  email_config:
    recipients: ["accounting@company.com"]
    subject: "Daily Transaction Summary - {{date}}"
    body_template: |
      Daily transaction summary for {{date}}.
      
      Total transactions processed: {{transaction_count}}
      Report generation time: {{generation_time}}ms
      
      Please find the detailed report attached.
```

## Weekly Reports

### Weekly Financial Summary
**Purpose**: Comprehensive weekly financial overview
**Schedule**: Every Monday at 9:00 AM
**Recipients**: Management team

```yaml
name: "Weekly Financial Summary"
description: "Comprehensive weekly financial analysis and trends"
trigger:
  cron: "0 9 * * 1"  # 9 AM every Monday
  timezone: "Europe/Stockholm"

parameters:
  report_config:
    report_type: "Financial"
    format: "Excel"
    language: "en"
    date_range: "last_week"
    
  components:
    - type: "Chart"
      title: "Weekly Revenue Trend"
      chart_type: "Bar"
      data_source: "Transactions"
      filters:
        account_categories: ["Revenue"]
      group_by: "Date"
      aggregation: "Sum"
      
    - type: "Chart"
      title: "Expense Breakdown"
      chart_type: "Pie"
      data_source: "Transactions"
      filters:
        account_categories: ["Expenses"]
      group_by: "AccountSubCategory"
      aggregation: "Sum"
      
    - type: "Table"
      title: "Top Revenue Accounts"
      data_source: "Transactions"
      filters:
        account_categories: ["Revenue"]
      columns:
        - field: "AccountCode"
        - field: "Description"
        - field: "Amount"
          format: "Currency"
      sorting:
        - field: "Amount"
          direction: "Desc"
      limit: 10
      
    - type: "PivotTable"
      title: "Weekly Analysis by Project"
      data_source: "Transactions"
      row_fields: ["ProjectId"]
      column_fields: ["AccountCategory"]
      value_fields: ["Amount"]
      filters:
        exclude_null_projects: true

  email_config:
    recipients: 
      - "management@company.com"
      - "finance@company.com"
      - "ceo@company.com"
    subject: "Weekly Financial Summary - Week {{week_number}}"
    priority: "High"
    
  advanced_config:
    include_currency_info: true
    archive_retention_days: 365
    memory_limit_mb: 512
```

## Monthly Reports

### Monthly Financial Report
**Purpose**: Comprehensive monthly financial analysis
**Schedule**: First day of each month at 10:00 AM
**Recipients**: Board, management, finance team

```yaml
name: "Monthly Financial Report"
description: "Comprehensive monthly financial analysis with year-over-year comparison"
trigger:
  cron: "0 10 1 * *"  # 10 AM on first day of month
  timezone: "Europe/Stockholm"

parameters:
  report_config:
    report_type: "Financial"
    format: "Excel"
    language: "en"
    date_range: "last_month"
    
  components:
    - type: "Chart"
      title: "Monthly Revenue vs Expenses"
      chart_type: "Bar"
      data_source: "Transactions"
      group_by: "AccountCategory"
      aggregation: "Sum"
      comparison_period: "previous_month"
      
    - type: "Chart"
      title: "Cash Flow Trend"
      chart_type: "Line"
      data_source: "Transactions"
      filters:
        account_codes: ["1930"]  # Main bank account
      group_by: "Date"
      aggregation: "RunningSum"
      
    - type: "Table"
      title: "Profit & Loss Summary"
      data_source: "Calculated"
      rows:
        - label: "Total Revenue"
          calculation: "SUM(Revenue)"
        - label: "Total Expenses"
          calculation: "SUM(Expenses)"
        - label: "Gross Profit"
          calculation: "Revenue - COGS"
        - label: "Net Profit"
          calculation: "Revenue - Expenses"
      format: "Currency"
      
    - type: "PivotTable"
      title: "Monthly Analysis by Department"
      data_source: "Transactions"
      row_fields: ["ProjectId", "AccountCategory"]
      column_fields: ["Month"]
      value_fields: ["Amount"]
      
    - type: "Forecast"
      title: "Next Month Forecast"
      model_type: "LinearRegression"
      data_source: "Transactions"
      forecast_periods: 1
      confidence_interval: 0.95

  email_config:
    recipients: 
      - "board@company.com"
      - "management@company.com"
      - "finance@company.com"
      - "ceo@company.com"
      - "cfo@company.com"
    subject: "Monthly Financial Report - {{month_name}} {{year}}"
    priority: "High"
    body_template: |
      Monthly Financial Report for {{month_name}} {{year}}
      
      Key Highlights:
      - Total Revenue: {{total_revenue}}
      - Total Expenses: {{total_expenses}}
      - Net Profit: {{net_profit}}
      - Transaction Count: {{transaction_count}}
      
      Report generated in {{generation_time}}ms
      
      Please find the comprehensive analysis attached.
      
  advanced_config:
    include_currency_info: true
    include_comparative_analysis: true
    archive_retention_days: 2555  # 7 years
    memory_limit_mb: 1024
    timeout_seconds: 600
```

### Quarterly Board Report
**Purpose**: Executive summary for board meetings
**Schedule**: First day of quarter at 11:00 AM
**Recipients**: Board members, C-suite

```yaml
name: "Quarterly Board Report"
description: "Executive financial summary for board meetings"
trigger:
  cron: "0 11 1 1,4,7,10 *"  # 11 AM on Jan 1, Apr 1, Jul 1, Oct 1
  timezone: "Europe/Stockholm"

parameters:
  report_config:
    report_type: "Executive"
    format: "PDF"
    language: "en"
    date_range: "last_quarter"
    
  components:
    - type: "Chart"
      title: "Quarterly Performance Overview"
      chart_type: "Column"
      data_source: "Calculated"
      metrics: ["Revenue", "Expenses", "Profit"]
      comparison_periods: ["current_quarter", "previous_quarter", "same_quarter_last_year"]
      
    - type: "Chart"
      title: "Revenue by Business Unit"
      chart_type: "Pie"
      data_source: "Transactions"
      filters:
        account_categories: ["Revenue"]
      group_by: "ProjectId"
      aggregation: "Sum"
      
    - type: "Table"
      title: "Key Financial Metrics"
      data_source: "Calculated"
      rows:
        - label: "Quarterly Revenue"
          current: "{{q_revenue}}"
          previous: "{{prev_q_revenue}}"
          change: "{{revenue_change_pct}}"
        - label: "Quarterly Expenses"
          current: "{{q_expenses}}"
          previous: "{{prev_q_expenses}}"
          change: "{{expense_change_pct}}"
        - label: "Net Profit Margin"
          current: "{{profit_margin}}"
          previous: "{{prev_profit_margin}}"
          change: "{{margin_change}}"

  email_config:
    recipients: 
      - "board@company.com"
      - "ceo@company.com"
      - "cfo@company.com"
    subject: "Quarterly Board Report - Q{{quarter}} {{year}}"
    priority: "Urgent"
    
  advanced_config:
    executive_summary: true
    include_forecasting: true
    professional_styling: true
    confidential: true
```

## Special Purpose Reports

### Year-End Closing Report
**Purpose**: Annual financial summary for tax preparation and auditing
**Schedule**: January 2nd at 2:00 PM
**Recipients**: Accounting firm, CFO, auditors

```yaml
name: "Year-End Closing Report"
description: "Comprehensive annual financial report for tax and audit purposes"
trigger:
  cron: "0 14 2 1 *"  # 2 PM on January 2nd
  timezone: "Europe/Stockholm"

parameters:
  report_config:
    report_type: "Annual"
    format: "Excel"
    language: "sv"  # Swedish for tax compliance
    date_range: "previous_year"

  components:
    - type: "Table"
      title: "BAS Account Summary"
      data_source: "Transactions"
      group_by: "AccountCode"
      columns:
        - field: "AccountCode"
          title: "Konto"
        - field: "AccountName"
          title: "Kontonamn"
        - field: "OpeningBalance"
          title: "Ingående balans"
          format: "Currency"
        - field: "Debit"
          title: "Debet"
          format: "Currency"
        - field: "Credit"
          title: "Kredit"
          format: "Currency"
        - field: "ClosingBalance"
          title: "Utgående balans"
          format: "Currency"
      sorting:
        - field: "AccountCode"
          direction: "Asc"

  email_config:
    recipients:
      - "accounting-firm@company.com"
      - "cfo@company.com"
      - "auditor@company.com"
    subject: "Year-End Closing Report - {{year}}"
    priority: "High"

  advanced_config:
    include_all_transactions: true
    bas_compliance: true
    audit_trail: true
    retention_permanent: true
```

## Configuration Guidelines

### Cron Expression Reference
- `0 8 * * 1-5`: 8 AM weekdays
- `30 7 * * *`: 7:30 AM daily
- `0 9 * * 1`: 9 AM every Monday
- `0 10 1 * *`: 10 AM first day of month
- `0 11 1 1,4,7,10 *`: 11 AM first day of quarters

### Best Practices
1. **Timing**: Schedule reports during off-peak hours
2. **Recipients**: Use distribution lists for easier management
3. **Error Handling**: Always include fallback recipients
4. **Resource Limits**: Set appropriate memory and timeout limits
5. **Retention**: Configure retention based on regulatory requirements

### Testing Workflows
Before deploying to production:
1. Test with small date ranges
2. Verify recipient lists
3. Check error handling scenarios
4. Validate report content and formatting
5. Monitor resource usage during generation
