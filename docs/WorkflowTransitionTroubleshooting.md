# Workflow Transition Configuration Troubleshooting Guide

## Overview
This guide addresses issues with configuring workflow step transitions, particularly when transition fields appear as read-only in the workflow editor interface.

## Problem Analysis

### Why Transition Fields May Appear Read-Only

1. **Browser Caching Issues**
   - The workflow editor interface may be cached with old data
   - Solution: Hard refresh (Ctrl+F5 or Cmd+Shift+R)

2. **Workflow State Constraints**
   - Some workflows may be locked during execution
   - Solution: Ensure workflow is stopped before editing

3. **JavaScript Binding Issues**
   - Blazor component binding may not be working correctly
   - Solution: Check browser console for JavaScript errors

4. **Data Loading Issues**
   - Workflow data may not be loading correctly from the YAML file
   - Solution: Verify YAML file syntax and structure

## Solution Methods

### Method 1: Direct YAML File Editing (Recommended)

The most reliable way to configure workflow transitions is to edit the YAML file directly:

```yaml
steps:
  - id: step1
    prompt: "Your step prompt here"
    order: 1
    actionType: tool
    toolName: YourPlugin
    timeout: 300
    retries: 2
    onSuccess: step2    # Next step on success
    onFailure: step8    # Error handler step on failure
```

**Transition Configuration Rules:**
- `onSuccess`: Defines where to go when step succeeds
  - Use `step2`, `step3`, etc. for next steps
  - Use `completed` for workflow completion
- `onFailure`: Defines where to go when step fails
  - Use `step8` for error handling step
  - Use `failed` for immediate workflow failure
  - Use `completed` to end workflow gracefully

### Method 2: Workflow Editor Interface

If the interface is working correctly:

1. Navigate to Workflow Manager in WebChat
2. Select the workflow to edit
3. Ensure workflow is not currently running
4. Click on the step you want to modify
5. Update the "Vid framgång" (On Success) field
6. Update the "Vid fel" (On Failure) field
7. Save the workflow

### Method 3: API-Based Updates

Use the workflow service API to update transitions programmatically:

```csharp
var workflow = await WorkflowService.GetWorkflowAsync("workflow-id");
foreach (var step in workflow.Steps)
{
    if (step.Id != "step8") // Don't modify error handler
    {
        step.OnFailure = "step8"; // Route failures to error handler
    }
}
await WorkflowService.UpdateWorkflowAsync(workflow);
```

## Implemented Solution for daily-morning-report-workflow

I've updated your daily-morning-report-workflow with the desired transition logic:

```yaml
# Step 1-7: All failures route to step8 (error handler)
step1 → success: step2, failure: step8
step2 → success: step3, failure: step8  
step3 → success: step4, failure: step8
step4 → success: step5, failure: step8
step5 → success: step6, failure: step8
step6 → success: step7, failure: step8
step7 → success: completed, failure: step8

# Step 8: Error handler - always completes workflow
step8 → success: completed, failure: completed
```

## Validation and Testing

### 1. Verify YAML Syntax
```bash
# Check YAML syntax
yamllint src/Agent/Workflows/daily-morning-report-workflow.yaml
```

### 2. Test Workflow Execution
```bash
# Test workflow execution
curl -X POST http://localhost:5000/api/workflows/daily-morning-report-workflow/execute
```

### 3. Monitor Workflow Logs
Check the application logs for workflow execution details and any transition errors.

## Common Transition Patterns

### Error Handling Pattern
```yaml
onSuccess: next-step
onFailure: error-handler
```

### Retry Pattern
```yaml
onSuccess: next-step
onFailure: retry-step
```

### Conditional Branching
```yaml
onSuccess: conditional-step
onFailure: alternative-step
```

### Graceful Completion
```yaml
onSuccess: completed
onFailure: completed
```

## Troubleshooting Checklist

- [ ] Workflow is not currently executing
- [ ] YAML file syntax is valid
- [ ] Browser cache has been cleared
- [ ] No JavaScript errors in browser console
- [ ] Workflow service is running correctly
- [ ] File permissions allow writing to workflow directory
- [ ] Transition target steps exist in the workflow

## Best Practices

1. **Always include an error handler step** (like step8)
2. **Use descriptive step IDs** (avoid generic names like "step1")
3. **Test transitions thoroughly** before deploying
4. **Document complex transition logic** in workflow description
5. **Use consistent naming conventions** for transition targets
6. **Validate YAML syntax** before saving changes

## Support

If you continue to experience issues with workflow transitions:

1. Check the browser developer console for errors
2. Verify the workflow YAML file structure
3. Test with a simple workflow first
4. Contact the development team with specific error messages
