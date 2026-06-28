# Workflow Scheduling System

This document explains how to use the workflow scheduling system to create automated tasks that run on a schedule.

## Overview

The AccountingAgent includes a robust scheduling system for workflows, allowing them to run automatically at specified times using CRON expressions. This feature enables various automation scenarios like:

- Daily reports generated and sent at 9:00 AM
- Weekly summaries sent every Friday at 4:00 PM
- Monthly accounting tasks run on the first day of each month
- Hourly monitoring or data collection tasks

## CRON Expressions

Scheduling uses standard CRON expressions with five fields:
```
┌───────────── minute (0 - 59)
│ ┌───────────── hour (0 - 23)
│ │ ┌───────────── day of month (1 - 31)
│ │ │ ┌───────────── month (1 - 12)
│ │ │ │ ┌───────────── day of week (0 - 6) (Sunday to Saturday)
│ │ │ │ │
│ │ │ │ │
* * * * *
```

### Common Examples

- `0 9 * * *` - Run at 9:00 AM every day
- `0 9 * * 1-5` - Run at 9:00 AM every weekday (Monday to Friday)
- `0 */2 * * *` - Run every 2 hours
- `0 9 1 * *` - Run at 9:00 AM on the first day of every month
- `0 9 * * 1` - Run at 9:00 AM every Monday

## Creating a Scheduled Workflow

### Using the Web Interface

1. Navigate to the Workflow Manager in WebChat
2. Create a new workflow or select an existing one
3. Enable scheduling
4. Enter a CRON expression or choose from common presets
5. Save the workflow

### Manually Creating the Workflow YAML

Add a `schedule` field to your workflow YAML file:

```yaml
id: my-scheduled-workflow
name: My Scheduled Workflow
description: A workflow that runs on schedule
enabled: true
executionType: scheduled
systemPrompt: default
# CRON expression for 9:00 AM every weekday
schedule: 0 9 * * 1-5

jobs:
  # Your workflow jobs here
```

## Implementation Details

The system uses:

1. A background service (`WorkflowSchedulingService`) that checks every minute for workflows that need to be executed
2. CRON expression parsing using the Cronos library
3. Workflow state tracking to manage execution times
4. Error handling and retry mechanisms
5. Logging of all scheduled executions

## Best Practices

1. Use descriptive workflow names and descriptions that identify their schedule
2. Consider system resource usage when scheduling multiple workflows
3. Workflows that run frequently should be lightweight and quick to execute
4. Use the `onSuccess` and `onFailure` properties in your workflow steps to handle errors gracefully
5. Configure notifications for workflow failures
6. Schedule resource-intensive workflows during off-peak hours

## Troubleshooting

If your scheduled workflow is not running as expected:

1. Check the workflow is enabled (`enabled: true`)
2. Verify the execution type is set to "scheduled" (`executionType: scheduled`)
3. Validate your CRON expression using a CRON validator
4. Check application logs for any errors related to the workflow
5. Ensure the system time is correctly set on the server
