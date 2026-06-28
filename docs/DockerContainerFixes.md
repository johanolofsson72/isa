# Docker Container Startup Fixes for GitHub Actions

## 🔍 **Problem Analysis**

The nightly test pipeline was failing with "Service container agent failed" due to several critical issues:

### **Root Causes Identified:**

1. **Port Mismatch**
   - GitHub Actions expected port 5000
   - Agent actually runs on port 80/443
   - Health checks failed due to wrong port

2. **Wrong Container Image**
   - Using base `mcr.microsoft.com/dotnet/aspnet:9.0` instead of built Agent image
   - Missing application code and dependencies

3. **Missing Configuration**
   - No Azure OpenAI credentials in container
   - Missing required environment variables
   - No proper health check setup

4. **Service Container Limitations**
   - Limited control over container configuration
   - Difficult to debug startup issues
   - No access to build context

## 🛠️ **Solutions Implemented**

### **1. Fixed GitHub Actions Workflow (.github/workflows/nightly-tests.yml)**

**Changes Made:**
- ✅ Removed problematic service container configuration
- ✅ Added proper Docker image build step
- ✅ Created manual container startup with full control
- ✅ Fixed port mapping (80:80 instead of 5000:5000)
- ✅ Added proper configuration mounting
- ✅ Improved health check logic with better timeout
- ✅ Added comprehensive logging and debugging

**Key Improvements:**
```yaml
# Before: Service container (limited control)
services:
  agent:
    image: mcr.microsoft.com/dotnet/aspnet:9.0
    ports:
      - 5000:5000

# After: Manual container management (full control)
- name: Build Docker Image
  run: docker build -t agent-test:latest -f src/Agent/Dockerfile .

- name: Start Agent Service Container
  run: |
    docker run -d \
      --name agent-service \
      --network host \
      -p 80:80 \
      -v ${{ github.workspace }}/test-data/config:/config:ro \
      agent-test:latest
```

### **2. Enhanced Dockerfile (src/Agent/Dockerfile)**

**Changes Made:**
- ✅ Simplified ASPNETCORE_URLS to HTTP only for CI
- ✅ Added curl installation for health checks
- ✅ Added built-in Docker HEALTHCHECK
- ✅ Added DOTNET_EnableDiagnostics=0 for better performance
- ✅ Improved port exposure configuration

**Key Improvements:**
```dockerfile
# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Add health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1

# Simplified environment variables
ENV ASPNETCORE_URLS="http://+:80" \
    DOTNET_EnableDiagnostics=0
```

### **3. Improved Agent Startup Resilience (src/Agent/Program.cs)**

**Changes Made:**
- ✅ Better error handling for Azure OpenAI configuration
- ✅ Fallback values for testing environments
- ✅ More informative startup logging
- ✅ Graceful degradation when services unavailable

**Key Improvements:**
```csharp
// Fallback configuration for CI/testing
if (string.IsNullOrEmpty(deploymentName) || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("⚠️ [STARTUP] Azure OpenAI configuration incomplete - using test configuration");
    deploymentName ??= "gpt-4o";
    endpoint ??= "https://test.openai.azure.com/";
    apiKey ??= "test-key";
}
```

### **4. Created Test Scripts**

**run-all-tests.sh:**
- ✅ Comprehensive test runner for all projects
- ✅ Proper test configuration and environment setup
- ✅ Detailed logging and result collection
- ✅ Summary reporting

**test-docker-agent.sh:**
- ✅ Local Docker testing script
- ✅ Simulates GitHub Actions environment
- ✅ Validates container startup and health checks
- ✅ Comprehensive debugging output

## 🎯 **Expected Results**

### **Before Fixes:**
```
Starting agent service container
Waiting for all services to be ready
Service container agent failed.
Error: One or more containers failed to start.
```

### **After Fixes:**
```
🐳 Building Agent Docker image...
🚀 Starting Agent service container for workflow testing...
⏳ Waiting for Agent container to start...
📦 Container is running, checking health...
✅ Agent service is healthy and responding
🎉 Agent service is ready for testing!
```

## 🔧 **Testing the Fixes**

### **Local Testing:**
```bash
# Test Docker container locally
./test-docker-agent.sh

# Run complete test suite
./run-all-tests.sh
```

### **Manual Verification:**
```bash
# Build and test manually
docker build -t agent-test:latest -f src/Agent/Dockerfile .
docker run -d --name agent-test -p 8080:80 agent-test:latest

# Check health
curl http://localhost:8080/health
curl http://localhost:8080/info

# Check logs
docker logs agent-test
```

## 📊 **Configuration for Different Environments**

### **CI/CD Environment (GitHub Actions):**
- ✅ Minimal configuration with test values
- ✅ External communications disabled
- ✅ Fast startup and health checks
- ✅ Comprehensive logging for debugging

### **Development Environment:**
- ✅ Full configuration with real API keys
- ✅ All services enabled
- ✅ Debug logging enabled
- ✅ Hot reload support

### **Production Environment:**
- ✅ Secure configuration management
- ✅ All services operational
- ✅ Performance optimizations
- ✅ Health monitoring

## 🚀 **Next Steps**

1. **Deploy the fixes** to the repository
2. **Test the nightly workflow** manually using workflow_dispatch
3. **Monitor the results** and adjust timeouts if needed
4. **Consider adding** more comprehensive integration tests
5. **Document** any additional configuration requirements

## 🔍 **Monitoring and Debugging**

### **Health Check Endpoints:**
- `GET /health` - Basic health status
- `GET /info` - Detailed service information

### **Container Logs:**
```bash
# View real-time logs
docker logs -f agent-service

# Check health status
docker inspect --format='{{.State.Health.Status}}' agent-service
```

### **GitHub Actions Debugging:**
- All logs are collected in test artifacts
- Container logs are saved for analysis
- Detailed step-by-step output for troubleshooting

## ✅ **Success Criteria**

The fixes are successful when:
1. ✅ Docker image builds without errors
2. ✅ Container starts and becomes healthy within 60 seconds
3. ✅ Health endpoint responds with "healthy" status
4. ✅ No critical errors in container logs
5. ✅ Tests can run against the containerized service
6. ✅ Nightly workflow completes without container failures

These fixes address all identified issues and provide a robust, debuggable container setup for CI/CD environments.
