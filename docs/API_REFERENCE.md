# Quartz.NET Workflow System - API Reference

## 🔗 Base URL

```
Production: https://workflow.company.com/api
Development: https://localhost:7001/api
```

## 🔐 Authentication

Currently, the API uses basic authentication. In production, implement JWT or OAuth2.

```http
Authorization: Bearer your-jwt-token
```

## 📋 Workflow Management API

### Schedule Workflow

Creates and schedules a new workflow from YAML definition.

```http
POST /workflows/schedule
Content-Type: application/json
```

**Request Body:**
```json
{
  "workflowYaml": "id: example-workflow\nname: Example Workflow\nschedule: '0 0 9 * * ?'\njobs:\n  - name: main-job\n    steps:\n      - id: step1\n        prompt: 'Process data'\n        actionType: 'AI'"
}
```

**Response:**
```json
{
  "isSuccess": true,
  "jobKey": "example-workflow.workflows",
  "triggerKey": "example-workflow.workflows",
  "scheduleType": "Cron",
  "nextFireTime": "2024-01-15T09:00:00Z",
  "errorMessage": null
}
```

**Status Codes:**
- `200 OK` - Workflow scheduled successfully
- `400 Bad Request` - Invalid YAML or schedule format
- `409 Conflict` - Workflow with same ID already exists
- `500 Internal Server Error` - Scheduling failed

### Get Scheduled Workflows

Retrieves all currently scheduled workflows.

```http
GET /workflows
```

**Response:**
```json
[
  {
    "id": "example-workflow",
    "name": "Example Workflow",
    "description": "Processes daily data",
    "schedule": "0 0 9 * * ?",
    "nextFireTime": "2024-01-15T09:00:00Z",
    "previousFireTime": "2024-01-14T09:00:00Z",
    "state": "Normal"
  }
]
```

### Trigger Workflow

Executes a workflow immediately, regardless of schedule.

```http
POST /workflows/{workflowId}/trigger
```

**Parameters:**
- `workflowId` (path) - Unique workflow identifier

**Response:**
```json
{
  "message": "Workflow triggered successfully",
  "executionId": "exec-12345",
  "triggeredAt": "2024-01-15T10:30:00Z"
}
```

### Pause Workflow

Pauses a scheduled workflow.

```http
POST /workflows/{workflowId}/pause
```

**Response:**
```json
{
  "message": "Workflow paused successfully",
  "workflowId": "example-workflow",
  "pausedAt": "2024-01-15T10:30:00Z"
}
```

### Resume Workflow

Resumes a paused workflow.

```http
POST /workflows/{workflowId}/resume
```

**Response:**
```json
{
  "message": "Workflow resumed successfully",
  "workflowId": "example-workflow",
  "resumedAt": "2024-01-15T10:30:00Z"
}
```

### Unschedule Workflow

Removes a workflow from the scheduler.

```http
DELETE /workflows/{workflowId}
```

**Response:**
```json
{
  "message": "Workflow unscheduled successfully",
  "workflowId": "example-workflow",
  "unscheduledAt": "2024-01-15T10:30:00Z"
}
```

## 📊 Monitoring API

### Get All Workflow Metrics

Retrieves performance metrics for all workflows.

```http
GET /monitoring/metrics
```

**Response:**
```json
{
  "example-workflow": {
    "workflowId": "example-workflow",
    "workflowName": "Example Workflow",
    "totalExecutions": 150,
    "totalSuccessfulExecutions": 145,
    "totalFailedExecutions": 5,
    "successRate": 96.67,
    "averageExecutionTime": "00:02:30",
    "consecutiveFailures": 0,
    "lastExecutionTime": "2024-01-15T09:00:00Z",
    "lastSuccessTime": "2024-01-15T09:00:00Z",
    "lastFailureTime": "2024-01-13T09:00:00Z",
    "lastErrorMessage": null
  }
}
```

### Get Specific Workflow Metrics

Retrieves metrics for a specific workflow.

```http
GET /monitoring/metrics/{workflowId}
```

**Response:**
```json
{
  "workflowId": "example-workflow",
  "workflowName": "Example Workflow",
  "totalExecutions": 150,
  "totalSuccessfulExecutions": 145,
  "totalFailedExecutions": 5,
  "successRate": 96.67,
  "averageExecutionTime": "00:02:30",
  "consecutiveFailures": 0,
  "lastExecutionTime": "2024-01-15T09:00:00Z",
  "lastSuccessTime": "2024-01-15T09:00:00Z",
  "lastFailureTime": "2024-01-13T09:00:00Z",
  "lastErrorMessage": null,
  "executionHistory": [
    {
      "executionId": "exec-12345",
      "startTime": "2024-01-15T09:00:00Z",
      "endTime": "2024-01-15T09:02:30Z",
      "isSuccess": true,
      "errorMessage": null,
      "duration": "00:02:30"
    }
  ]
}
```

### Get Performance Summary

Retrieves overall system performance summary.

```http
GET /monitoring/summary
```

**Response:**
```json
{
  "totalWorkflows": 10,
  "totalExecutions": 1500,
  "totalSuccessfulExecutions": 1450,
  "totalFailedExecutions": 50,
  "overallSuccessRate": 96.67,
  "averageExecutionTime": "00:02:15",
  "generatedAt": "2024-01-15T10:30:00Z",
  "topPerformingWorkflows": [
    {
      "workflowId": "fast-workflow",
      "averageExecutionTime": "00:00:30",
      "successRate": 100.0
    }
  ],
  "problematicWorkflows": [
    {
      "workflowId": "slow-workflow",
      "consecutiveFailures": 3,
      "lastErrorMessage": "Timeout occurred"
    }
  ]
}
```

### Send Daily Summary

Triggers manual daily summary notification.

```http
POST /monitoring/daily-summary
```

**Response:**
```json
{
  "message": "Daily summary sent successfully",
  "sentAt": "2024-01-15T10:30:00Z",
  "recipients": ["ops-team@company.com"]
}
```

## 🔔 Notification API

### Get Notification Statistics

Retrieves notification rate limiting and delivery statistics.

```http
GET /monitoring/notifications/statistics
```

**Response:**
```json
{
  "totalTrackers": 25,
  "totalNotificationsSent": 150,
  "totalNotificationsRateLimited": 30,
  "rateLimitEffectiveness": 16.67,
  "generatedAt": "2024-01-15T10:30:00Z"
}
```

### Clear Rate Limits

Clears all notification rate limiting data.

```http
POST /monitoring/notifications/clear-rate-limits
```

**Response:**
```json
{
  "message": "Notification rate limiting data cleared successfully",
  "clearedAt": "2024-01-15T10:30:00Z"
}
```

### Send Test Notification

Sends a test notification to verify channel configuration.

```http
POST /notifications/test
Content-Type: application/json
```

**Request Body:**
```json
{
  "channel": "slack",
  "message": "Test notification from workflow system"
}
```

**Response:**
```json
{
  "isSuccess": true,
  "channel": "slack",
  "message": "Test notification sent successfully",
  "sentAt": "2024-01-15T10:30:00Z",
  "errorMessage": null
}
```

## 🏥 Health Check API

### Basic Health Check

Simple health status endpoint.

```http
GET /health/ready
```

**Response:**
```
Healthy
```

**Status Codes:**
- `200 OK` - System is healthy
- `503 Service Unavailable` - System is unhealthy

### Detailed Health Check

Comprehensive health information with detailed component status.

```http
GET /health
```

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "version": "1.0.0",
  "environment": "Production",
  "checks": [
    {
      "name": "quartz-scheduler",
      "status": "Healthy",
      "description": "All workflow systems are operating normally",
      "duration": 45.2,
      "data": {
        "scheduler": {
          "schedulerName": "WorkflowScheduler",
          "schedulerInstanceId": "AUTO",
          "isStarted": true,
          "isShutdown": false,
          "inStandbyMode": false,
          "numberOfJobsExecuted": 1500,
          "threadPoolSize": 10
        },
        "jobs": {
          "totalJobs": 10,
          "runningJobs": 2,
          "pausedJobs": 0,
          "triggersCount": 10
        },
        "metrics": {
          "totalWorkflows": 10,
          "totalExecutions": 1500,
          "successRate": 96.67,
          "averageExecutionTime": 135.5
        },
        "resources": {
          "memoryUsageMB": 256,
          "threadCount": 45
        }
      }
    }
  ],
  "totalDuration": 45.2
}
```

## 🔧 System API

### Get System Information

Retrieves system configuration and status.

```http
GET /system/info
```

**Response:**
```json
{
  "version": "1.0.0",
  "environment": "Production",
  "startTime": "2024-01-15T08:00:00Z",
  "uptime": "02:30:00",
  "schedulerInfo": {
    "name": "WorkflowScheduler",
    "instanceId": "AUTO",
    "version": "3.8.0",
    "threadPoolSize": 10,
    "jobStoreType": "AdoJobStore"
  },
  "features": {
    "notificationsEnabled": true,
    "healthChecksEnabled": true,
    "metricsEnabled": true,
    "rateLimitingEnabled": true
  }
}
```

## 📝 Error Responses

All API endpoints return consistent error responses:

```json
{
  "error": "BadRequest",
  "message": "Invalid workflow YAML format",
  "details": "Line 5: Invalid schedule expression",
  "timestamp": "2024-01-15T10:30:00Z",
  "traceId": "trace-12345"
}
```

**Common Error Codes:**
- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Authentication required
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `409 Conflict` - Resource already exists
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error
- `503 Service Unavailable` - Service temporarily unavailable

## 📊 Rate Limiting

API endpoints are rate limited to prevent abuse:

- **Workflow operations**: 100 requests per minute
- **Monitoring endpoints**: 200 requests per minute
- **Health checks**: 1000 requests per minute

Rate limit headers are included in responses:

```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1642248600
```

## 🔍 Filtering and Pagination

List endpoints support filtering and pagination:

```http
GET /workflows?status=active&page=1&size=20&sort=name
GET /monitoring/metrics?from=2024-01-01&to=2024-01-31
```

**Query Parameters:**
- `page` - Page number (default: 1)
- `size` - Page size (default: 20, max: 100)
- `sort` - Sort field and direction (e.g., `name`, `-created`)
- `status` - Filter by status
- `from`/`to` - Date range filtering

## 📚 SDK and Examples

### cURL Examples

```bash
# Schedule a workflow
curl -X POST "https://workflow.company.com/api/workflows/schedule" \
  -H "Content-Type: application/json" \
  -d '{"workflowYaml": "id: test\nname: Test\nschedule: \"0 0 9 * * ?\"\njobs: []"}'

# Get workflow metrics
curl "https://workflow.company.com/api/monitoring/metrics/test-workflow"

# Health check
curl "https://workflow.company.com/health"
```

### PowerShell Examples

```powershell
# Schedule workflow
$body = @{
    workflowYaml = "id: test`nname: Test`nschedule: '0 0 9 * * ?'`njobs: []"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://workflow.company.com/api/workflows/schedule" `
    -Method POST -Body $body -ContentType "application/json"
```

This API reference provides comprehensive documentation for integrating with the Quartz.NET workflow system.
