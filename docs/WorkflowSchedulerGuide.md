# Workflow Scheduling Guide

This document explains how to use the workflow scheduling system in the AccountingAgent application.

## Overview

The workflow scheduling system allows you to configure workflows to run automatically at specific times according to a schedule. This is useful for automating recurring tasks such as:

- Daily reports (e.g., morning reports at 9:00 AM)
- Weekly data synchronization
- Monthly invoice generation (e.g., first day of every month)
- Any task that needs to run at specific intervals or times

## How Scheduling Works

The system runs a background service that checks every minute for workflows that are scheduled to run. When a workflow's scheduled time arrives, the system automatically executes it.

Workflows are scheduled using CRON expressions, which provide a flexible way to specify recurring time patterns.

### CRON Expression Format

A CRON expression consists of 5 fields separated by spaces:

```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of week (0 - 6) (Sunday to Saturday)
│ │ │ │ │
* * * * *
```

### Common CRON Patterns

Here are some common CRON patterns:

- `0 9 * * *`: Every day at 9:00 AM
- `0 9 * * 1-5`: Every weekday (Monday through Friday) at 9:00 AM
- `0 */2 * * *`: Every 2 hours
- `0 0 1 * *`: At midnight on the first day of every month
- `0 9 1,15 * *`: At 9:00 AM on the 1st and 15th of every month
- `0 9 */2 * *`: At 9:00 AM every other day of the month

## Configuring a Scheduled Workflow in the UI

To configure a workflow to run on a schedule:

1. Open the WebChat interface
2. Navigate to the Workflow Scheduler page
3. Select the workflow you want to schedule
4. Enable scheduling for the workflow
5. Enter the CRON expression for when the workflow should run
6. Save the schedule

The UI includes a CRON expression helper that explains what your expression means in plain language.

## YAML Configuration

You can also configure workflow scheduling directly in the YAML file:

```yaml
id: daily-morning-report
name: Daily Morning Report
description: Generates a daily morning report
enabled: true
executionType: scheduled
schedule: 0 9 * * 1-5  # Run at 9:00 AM every weekday
systemPrompt: default
jobs:
  # Job configuration...
```

## Example Use Cases

### Daily Morning Report

```yaml
id: daily-morning-report
name: Daily Morning Report
description: Generates and sends a daily report every weekday at 9:00 AM
enabled: true
executionType: scheduled
systemPrompt: default
# CRON expression for 9:00 AM every weekday (Monday-Friday)
schedule: 0 9 * * 1-5
  
jobs:
  - name: generate-daily-report
    description: Generate and send a daily report
    order: 1
    enabled: true
    steps:
      - id: generate-report
        prompt: "Generate a daily report summarizing the current date, weather in Stockholm, and any scheduled meetings for today."
        order: 1
        actionType: prompt
        timeout: 60
        retries: 1
        onSuccess: send-report
        onFailure: report-failure
```

### Monthly Invoice Generation

```yaml
id: monthly-invoice
name: Monthly Invoice Generation
description: Generates invoices on the first of each month
enabled: true
executionType: scheduled
systemPrompt: default
# CRON expression for midnight on the 1st of every month
schedule: 0 0 1 * *
  
jobs:
  - name: generate-invoices
    description: Generate monthly invoices
    order: 1
    enabled: true
    steps:
      # Steps configuration...
```

## Monitoring Scheduled Workflows

The system tracks when each workflow was last executed and when it's scheduled to run next. You can view this information in the WebChat UI.

If a workflow fails to execute as scheduled, an error will be logged, and the system will attempt to run it during the next scheduled time.

## Testing Scheduled Workflows

To test a scheduled workflow without waiting for its scheduled time:

1. Temporarily change its CRON expression to run in the near future (e.g., `*/1 * * * *` to run every minute)
2. Monitor the logs to ensure it runs as expected
3. Reset the CRON expression to the desired schedule

## Troubleshooting

- If a workflow isn't running as scheduled, check that:
  - The `enabled` property is set to `true`
  - The `executionType` is set to `scheduled`
  - The `schedule` property contains a valid CRON expression
- Verify your CRON expression using an online CRON expression validator
- Check the application logs for errors related to workflow execution
- Ensure that the system time on the server is correct

## Best Practices

- Keep scheduled workflows focused on a single task
- Include error handling in your workflows
- Use descriptive names for scheduled workflows
- Add notifications for critical workflow failures
- Test workflows thoroughly before scheduling them for automatic execution
- Document the purpose and schedule of each workflow
