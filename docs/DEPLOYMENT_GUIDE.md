# Quartz.NET Workflow System - Deployment Guide

## 🚀 Production Deployment

### Prerequisites

- .NET 9.0 Runtime
- SQL Server (for persistent job store)
- Redis (optional, for distributed caching)
- SMTP Server (for email notifications)
- Slack/Teams webhooks (for notifications)

### Environment Configuration

#### 1. Production appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Quartz": "Information"
    }
  },
  "AllowedHosts": "*",
  
  "Quartz": {
    "quartz.scheduler.instanceName": "ProductionWorkflowScheduler",
    "quartz.scheduler.instanceId": "AUTO",
    "quartz.threadPool.type": "Quartz.Simpl.SimpleThreadPool, Quartz",
    "quartz.threadPool.threadCount": "20",
    "quartz.threadPool.threadPriority": "Normal",
    
    "quartz.jobStore.type": "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
    "quartz.jobStore.dataSource": "default",
    "quartz.jobStore.tablePrefix": "QRTZ_",
    "quartz.jobStore.clustered": "true",
    "quartz.jobStore.clusterCheckinInterval": "20000",
    "quartz.jobStore.maxMisfiresToHandleAtATime": "1",
    "quartz.jobStore.misfireThreshold": "60000",
    "quartz.jobStore.selectWithLockSQL": "SELECT * FROM {0}LOCKS WHERE SCHED_NAME = @schedulerName AND LOCK_NAME = @lockName FOR UPDATE",
    
    "quartz.dataSource.default.provider": "SqlServer",
    "quartz.dataSource.default.connectionString": "Server=prod-sql-server;Database=WorkflowScheduler;Integrated Security=true;TrustServerCertificate=true;",
    
    "quartz.serializer.type": "json"
  },
  
  "WorkflowNotifications": {
    "EnableFailureNotifications": true,
    "EnableConsecutiveFailureAlerts": true,
    "EnableVetoNotifications": true,
    "EnableRecoveryNotifications": true,
    "EnableDailySummary": true,
    
    "SlackWebhookUrl": "https://hooks.slack.com/services/YOUR/PRODUCTION/WEBHOOK",
    "TeamsWebhookUrl": "https://your-org.webhook.office.com/webhookb2/YOUR-WEBHOOK",
    "EmailRecipients": "ops-team@company.com,alerts@company.com",
    "SmsRecipients": "+1234567890,+0987654321",
    
    "ConsecutiveFailureThreshold": 3,
    "CriticalFailureThreshold": 5,
    "EnableRateLimiting": true,
    "RateLimitMinutes": 15,
    "CriticalWorkflows": ["critical-backup", "financial-reports"],
    "QuietHours": ["22:00-06:00", "12:00-13:00"]
  },
  
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-sql-server;Database=WorkflowScheduler;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

#### 2. Environment Variables

```bash
# Database
CONNECTIONSTRINGS__DEFAULTCONNECTION="Server=prod-sql-server;Database=WorkflowScheduler;User Id=workflow_user;Password=secure_password;"

# Notifications
WORKFLOWNOTIFICATIONS__SLACKWEBHOOKURL="https://hooks.slack.com/services/YOUR/PRODUCTION/WEBHOOK"
WORKFLOWNOTIFICATIONS__EMAILRECIPIENTS="ops-team@company.com"

# Security
ASPNETCORE_ENVIRONMENT="Production"
ASPNETCORE_URLS="http://+:8080"
```

### Database Setup

#### 1. Create Quartz.NET Tables

Download and run the SQL Server scripts from Quartz.NET:

```sql
-- Create database
CREATE DATABASE WorkflowScheduler;
GO

USE WorkflowScheduler;
GO

-- Run Quartz.NET table creation scripts
-- Download from: https://github.com/quartznet/quartznet/tree/main/database/tables
-- Execute: tables_sqlServer.sql
```

#### 2. Create Application User

```sql
USE WorkflowScheduler;
GO

CREATE LOGIN workflow_user WITH PASSWORD = 'SecurePassword123!';
CREATE USER workflow_user FOR LOGIN workflow_user;

-- Grant necessary permissions
ALTER ROLE db_datareader ADD MEMBER workflow_user;
ALTER ROLE db_datawriter ADD MEMBER workflow_user;
ALTER ROLE db_ddladmin ADD MEMBER workflow_user;
```

### Docker Deployment

#### 1. Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Agent/Agent.csproj", "src/Agent/"]
COPY ["src/Shared/Shared.csproj", "src/Shared/"]
RUN dotnet restore "src/Agent/Agent.csproj"
COPY . .
WORKDIR "/src/src/Agent"
RUN dotnet build "Agent.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Agent.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "Agent.dll"]
```

#### 2. Docker Compose

```yaml
version: '3.8'

services:
  workflow-agent:
    build: .
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - CONNECTIONSTRINGS__DEFAULTCONNECTION=Server=sql-server;Database=WorkflowScheduler;User Id=sa;Password=YourStrong@Passw0rd;
    depends_on:
      - sql-server
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/ready"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Passw0rd
      - MSSQL_PID=Express
    ports:
      - "1433:1433"
    volumes:
      - sql_data:/var/opt/mssql
    restart: unless-stopped

volumes:
  sql_data:
```

### Kubernetes Deployment

#### 1. Deployment Manifest

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: workflow-agent
  labels:
    app: workflow-agent
spec:
  replicas: 2
  selector:
    matchLabels:
      app: workflow-agent
  template:
    metadata:
      labels:
        app: workflow-agent
    spec:
      containers:
      - name: workflow-agent
        image: your-registry/workflow-agent:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: CONNECTIONSTRINGS__DEFAULTCONNECTION
          valueFrom:
            secretKeyRef:
              name: workflow-secrets
              key: connection-string
        livenessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 60
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: workflow-agent-service
spec:
  selector:
    app: workflow-agent
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: LoadBalancer
```

#### 2. ConfigMap and Secrets

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: workflow-config
data:
  appsettings.Production.json: |
    {
      "Quartz": {
        "quartz.scheduler.instanceName": "K8sWorkflowScheduler",
        "quartz.scheduler.instanceId": "AUTO",
        "quartz.threadPool.threadCount": "10",
        "quartz.jobStore.clustered": "true"
      }
    }
---
apiVersion: v1
kind: Secret
metadata:
  name: workflow-secrets
type: Opaque
stringData:
  connection-string: "Server=sql-server;Database=WorkflowScheduler;User Id=workflow_user;Password=SecurePassword123!;"
  slack-webhook: "https://hooks.slack.com/services/YOUR/PRODUCTION/WEBHOOK"
```

### Monitoring Setup

#### 1. Prometheus Metrics

Add to `Program.cs`:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(builder =>
    {
        builder.AddPrometheusExporter();
        builder.AddMeter("WorkflowSystem");
    });

app.MapPrometheusScrapingEndpoint();
```

#### 2. Grafana Dashboard

Import the provided Grafana dashboard configuration for workflow monitoring.

### Security Configuration

#### 1. HTTPS Configuration

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://+:8080"
      },
      "Https": {
        "Url": "https://+:8443",
        "Certificate": {
          "Path": "/app/certificates/workflow-agent.pfx",
          "Password": "certificate-password"
        }
      }
    }
  }
}
```

#### 2. Authentication (if required)

```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://your-identity-server";
        options.RequireHttpsMetadata = true;
        options.Audience = "workflow-api";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("WorkflowAdmin", policy =>
        policy.RequireClaim("role", "workflow-admin"));
});
```

### Load Balancing

#### 1. Nginx Configuration

```nginx
upstream workflow_backend {
    server workflow-agent-1:8080;
    server workflow-agent-2:8080;
}

server {
    listen 80;
    server_name workflow.company.com;

    location / {
        proxy_pass http://workflow_backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /health {
        proxy_pass http://workflow_backend;
        access_log off;
    }
}
```

### Backup Strategy

#### 1. Database Backup

```sql
-- Daily backup script
BACKUP DATABASE WorkflowScheduler 
TO DISK = 'C:\Backups\WorkflowScheduler_' + 
    CONVERT(VARCHAR(8), GETDATE(), 112) + '.bak'
WITH COMPRESSION, CHECKSUM;
```

#### 2. Configuration Backup

```bash
#!/bin/bash
# Backup configuration files
tar -czf /backups/workflow-config-$(date +%Y%m%d).tar.gz \
    /app/appsettings.Production.json \
    /app/workflows/*.yaml
```

### Disaster Recovery

#### 1. Recovery Procedures

1. **Database Recovery**
   - Restore from latest backup
   - Verify Quartz.NET tables
   - Test scheduler connectivity

2. **Application Recovery**
   - Deploy latest container image
   - Restore configuration files
   - Verify health endpoints

3. **Workflow Recovery**
   - Re-schedule active workflows
   - Verify notification channels
   - Check monitoring systems

### Performance Tuning

#### 1. Quartz.NET Optimization

```json
{
  "Quartz": {
    "quartz.threadPool.threadCount": "20",
    "quartz.jobStore.acquireTriggersWithinLock": "true",
    "quartz.jobStore.txIsolationLevelSerializable": "false",
    "quartz.jobStore.maxMisfiresToHandleAtATime": "5"
  }
}
```

#### 2. Application Optimization

- Enable response compression
- Configure connection pooling
- Implement caching strategies
- Optimize database queries

### Maintenance

#### 1. Regular Tasks

- Monitor disk space
- Review error logs
- Update dependencies
- Test backup procedures
- Verify notification channels

#### 2. Health Monitoring

- Set up alerts for health check failures
- Monitor resource usage
- Track workflow success rates
- Review performance metrics

This deployment guide ensures a robust, scalable, and maintainable production environment for the Quartz.NET workflow system.
