using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Agent.Models;
using Agent.Services.Interfaces;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Agent.Services;

public class WorkflowExecutor
{
    private readonly WorkflowService _workflowService;
    private readonly HistoryService _historyService;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IChatHistoryFactory _chatHistoryFactory;
    private readonly ILogger<WorkflowExecutor> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAuditLogService _auditLogService;
    private readonly IOpenAICacheService _cacheService;
    private readonly IExecutionContextService _executionContextService;
    private readonly IDynamicSystemPromptGenerator _systemPromptGenerator;
    private readonly WorkflowValidationService _validationService;
    private readonly WorkflowNavigationService _navigationService;
    private readonly IChatExecutionService _chatExecutionService;

    // Static dictionary to track running workflows and prevent duplicates (legacy backup)
    private static readonly ConcurrentDictionary<string, DateTime> _runningWorkflows = new();

    public WorkflowExecutor(
        WorkflowService workflowService,
        HistoryService historyService,
        Kernel kernel,
        IChatHistoryFactory chatHistoryFactory,
        ILogger<WorkflowExecutor> logger,
        IConfiguration configuration,
        IAuditLogService auditLogService,
        IOpenAICacheService cacheService,
        IExecutionContextService executionContextService,
        IDynamicSystemPromptGenerator systemPromptGenerator,
        WorkflowValidationService validationService,
        WorkflowNavigationService navigationService,
        IChatExecutionService chatExecutionService
    )
    {
        _workflowService = workflowService;
        _historyService = historyService;
        _kernel = kernel;
        _chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        _chatHistoryFactory = chatHistoryFactory;
        _logger = logger;
        _configuration = configuration;
        _auditLogService = auditLogService;
        _cacheService = cacheService;
        _executionContextService = executionContextService;
        _systemPromptGenerator = systemPromptGenerator;
        _validationService = validationService;
        _navigationService = navigationService;
        _chatExecutionService = chatExecutionService;
    }

    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(string workflowId)
    {
        var stopwatch = Stopwatch.StartNew();
        // CRITICAL: Use consistent sessionId for entire workflow to maintain shared memory
        var sessionId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "🚀 STARTING WORKFLOW EXECUTION: WorkflowId={WorkflowId}, SessionId={SessionId}",
            workflowId,
            sessionId
        );

        try
        {
            var workflows = await _workflowService.LoadWorkflowsAsync();
            var workflow = workflows.FirstOrDefault(w => w.Id == workflowId);

            if (workflow == null)
            {
                throw new ArgumentException($"Workflow with ID '{workflowId}' not found");
            }

            if (!workflow.Enabled)
            {
                throw new InvalidOperationException($"Workflow '{workflow.Name}' is disabled");
            }

            // Validate workflow structure and step references
            var validationResult = _validationService.ValidateWorkflow(workflow);
            if (!validationResult.IsValid)
            {
                var errorMessage = $"Workflow validation failed: {string.Join(", ", validationResult.Errors)}";
                _logger.LogError("❌ WORKFLOW VALIDATION FAILED: {WorkflowId} - {Errors}", workflowId, errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            if (validationResult.Warnings.Any())
            {
                _logger.LogWarning("⚠️ WORKFLOW VALIDATION WARNINGS: {WorkflowId} - {Warnings}",
                    workflowId, string.Join(", ", validationResult.Warnings));
            }

            // 🚨 CRITICAL: Start execution context and acquire workflow lock
            var executionId = await _executionContextService.StartWorkflowExecutionAsync(
                workflowId,
                workflow.Name,
                "Manual"
            );

            if (
                !await _executionContextService.TryAcquireWorkflowLockAsync(workflowId, executionId)
            )
            {
                await _executionContextService.CompleteWorkflowExecutionAsync(
                    executionId,
                    "Cancelled",
                    "Workflow is already executing"
                );
                throw new InvalidOperationException(
                    $"Workflow '{workflow.Name}' is already executing. Duplicate execution prevented."
                );
            }

            try
            {
                // Legacy duplicate check as backup - reduced cooldown for scheduled workflows
                var now = DateTime.UtcNow;
                var cooldownPeriod = TimeSpan.FromSeconds(5); // Reduced from 30 to 5 seconds

                if (_runningWorkflows.TryGetValue(workflowId, out var lastStartTime))
                {
                    if (now - lastStartTime < cooldownPeriod)
                    {
                        _logger.LogWarning(
                            "🚨 LEGACY DUPLICATE CHECK: Workflow {WorkflowId} was recently executed. Ignoring duplicate request.",
                            workflowId
                        );
                        await _executionContextService.CompleteWorkflowExecutionAsync(
                            executionId,
                            "Cancelled",
                            "Legacy duplicate check prevented execution"
                        );
                        throw new InvalidOperationException(
                            $"Workflow '{workflowId}' was recently executed. Please wait before trying again."
                        );
                    }
                }

                // Mark workflow as running in legacy system
                _runningWorkflows[workflowId] = now;

                // Start history tracking
                var historyEntry = await _historyService.StartExecutionAsync(
                    workflowId,
                    workflow.Name,
                    "Manual"
                );

                var result = new WorkflowExecutionResult
                {
                    WorkflowId = workflowId,
                    WorkflowName = workflow.Name,
                    StartTime = System.DateTime.UtcNow,
                    Status = "Running",
                };

                try
                {
                    await _historyService.AddExecutionLogAsync(
                        historyEntry.Id,
                        $"🚀 Starting execution of workflow: {workflow.Name} (ExecutionId: {executionId})"
                    );

                    // Execute jobs in order with execution context
                    var enabledJobs = workflow
                        .Jobs.Where(j => j.Enabled)
                        .OrderBy(j => j.Order)
                        .ToList();

                    foreach (var job in enabledJobs)
                    {
                        _logger.LogInformation(
                            "▶️ EXECUTING JOB: {JobName} for workflow: {WorkflowName} (ExecutionId: {ExecutionId})",
                            job.Name,
                            workflow.Name,
                            executionId
                        );

                        await _historyService.AddExecutionLogAsync(
                            historyEntry.Id,
                            $"▶️ Executing job: {job.Name}"
                        );

                        var jobResult = await ExecuteJobAsync(
                            workflow,
                            job,
                            historyEntry.Id,
                            executionId,
                            sessionId
                        );
                        result.JobResults.Add(jobResult);

                        if (jobResult.Status == "Failed")
                        {
                            _logger.LogError(
                                "❌ JOB FAILED: {JobName} failed, stopping workflow execution (ExecutionId: {ExecutionId})",
                                job.Name,
                                executionId
                            );
                            await _historyService.AddExecutionLogAsync(
                                historyEntry.Id,
                                $"❌ Job {job.Name} failed: {jobResult.ErrorMessage}"
                            );
                            result.Status = "Failed";
                            break;
                        }
                        else
                        {
                            await _historyService.AddExecutionLogAsync(
                                historyEntry.Id,
                                $"✅ Job {job.Name} completed successfully"
                            );
                        }
                    }

                    if (result.Status == "Running")
                    {
                        result.Status = "Completed";
                    }

                    // Handle workflow-level navigation (OnSuccess/OnError)
                    await HandleWorkflowLevelNavigation(workflow, result, historyEntry.Id, executionId, sessionId);

                    // 🚨 KRITISK FIX: Uppdatera LastExecuted för workflow
                    workflow.LastExecuted = DateTime.UtcNow;
                    await _workflowService.SaveWorkflowAsync(workflow);

                    await _executionContextService.CompleteWorkflowExecutionAsync(
                        executionId,
                        result.Status,
                        result.ErrorMessage
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "❌ ERROR EXECUTING WORKFLOW: {WorkflowName} (ExecutionId: {ExecutionId})",
                        workflow.Name,
                        executionId
                    );
                    result.Status = "Failed";
                    result.ErrorMessage = ex.Message;
                    await _executionContextService.CompleteWorkflowExecutionAsync(
                        executionId,
                        "Failed",
                        ex.Message
                    );
                }
                finally
                {
                    result.EndTime = System.DateTime.UtcNow;
                    result.Duration = result.EndTime.Value - result.StartTime;
                    stopwatch.Stop();

                    // Complete history tracking
                    await _historyService.CompleteExecutionAsync(
                        historyEntry.Id,
                        result.Status == "Completed",
                        result.ErrorMessage
                    );

                    // Log workflow execution
                    await _auditLogService.LogWorkflowExecutionAsync(
                        sessionId,
                        workflowId,
                        result.Status,
                        result.ErrorMessage,
                        stopwatch.ElapsedMilliseconds
                    );
                }

                _logger.LogInformation(
                    "🏁 WORKFLOW EXECUTION COMPLETED: {WorkflowName}, Status: {Status}, Duration: {Duration}ms (ExecutionId: {ExecutionId})",
                    workflow.Name,
                    result.Status,
                    result.Duration?.TotalMilliseconds,
                    executionId
                );

                return result;
            }
            finally
            {
                // Always release the workflow lock
                await _executionContextService.ReleaseWorkflowLockAsync(workflowId, executionId);
            }
        }
        finally
        {
            // Remove the workflow from legacy running list immediately after completion
            _runningWorkflows.TryRemove(workflowId, out _);
        }
    }

    private async Task<JobExecutionResult> ExecuteJobAsync(
        WorkflowDefinition workflow,
        WorkflowJob job,
        string historyEntryId,
        string executionId,
        string sessionId
    )
    {
        var jobResult = new JobExecutionResult
        {
            JobName = job.Name,
            StartTime = System.DateTime.UtcNow,
            Status = "Running",
        };

        try
        {
            // Create a new isolated ChatHistory for this workflow execution with fresh system prompt
            // 🎯 CRITICAL FIX: Use unique GUID that doesn't match any user in users.json to avoid loading passwords
            var workflowDeviceFingerprint = "workflow-system-00000000-0000-0000-0000-000000000000";
            var workflowHistory = await _chatHistoryFactory.CreateWorkflowHistoryAsync(workflowDeviceFingerprint);

            _logger.LogInformation("🔒 WORKFLOW ISOLATION: Created new ChatHistory for workflow {WorkflowId} execution {ExecutionId}",
                workflow.Id, executionId);

            // Execute steps following onSuccess/onFailure flow, starting with order 1
            var allSteps = job.Steps.Where(s => s.Order > 0).ToList();
            var currentStep = allSteps.OrderBy(s => s.Order).FirstOrDefault();

            while (currentStep != null)
            {
                _logger.LogInformation(
                    "Executing step: {StepId} for job: {JobName}",
                    currentStep.Id,
                    job.Name
                );

                await _historyService.AddExecutionLogAsync(
                    historyEntryId,
                    $"  → Executing step: {currentStep.Id}"
                );

                var stepResult = await ExecuteStepAsync(
                    workflow,
                    currentStep,
                    workflowHistory,
                    executionId,
                    sessionId
                );
                jobResult.StepResults.Add(stepResult);

                // Add the step result to the workflow history for the next step
                if (!string.IsNullOrEmpty(stepResult.Output))
                {
                    await _chatHistoryFactory.AddAssistantMessageAsync(workflowHistory, stepResult.Output);
                }

                // Use navigation service to determine next step
                var navigationResult = _navigationService.GetNextStep(
                    workflow,
                    currentStep.Id,
                    stepResult.Status == "Completed",
                    stepResult.ErrorMessage
                );

                if (stepResult.Status == "Failed")
                {
                    _logger.LogError("Step {StepId} failed: {ErrorMessage}", currentStep.Id, stepResult.ErrorMessage);
                    await _historyService.AddExecutionLogAsync(
                        historyEntryId,
                        $"  ✗ Step {currentStep.Id} failed: {stepResult.ErrorMessage}"
                    );
                }
                else if (stepResult.Status == "Completed")
                {
                    _logger.LogInformation("Step {StepId} completed successfully", currentStep.Id);
                    await _historyService.AddExecutionLogAsync(
                        historyEntryId,
                        $"  ✓ Step {currentStep.Id} completed successfully"
                    );
                }

                // Handle navigation result
                switch (navigationResult.Action)
                {
                    case NavigationAction.Complete:
                        _logger.LogInformation(
                            "Job {JobName} completed - navigation result: {Reason}",
                            job.Name,
                            navigationResult.Reason
                        );
                        await _historyService.AddExecutionLogAsync(
                            historyEntryId,
                            $"  → Job flow completed: {navigationResult.Reason}"
                        );
                        jobResult.Status = stepResult.Status == "Failed" ? "Failed" : "Completed";
                        break;

                    case NavigationAction.Continue:
                        await _historyService.AddExecutionLogAsync(
                            historyEntryId,
                            $"  → Following flow to next step: {navigationResult.NextStepId}"
                        );
                        currentStep = allSteps.FirstOrDefault(s => s.Id == navigationResult.NextStepId);

                        if (currentStep == null)
                        {
                            _logger.LogError(
                                "Next step '{NextStepId}' not found in job {JobName}",
                                navigationResult.NextStepId,
                                job.Name
                            );
                            await _historyService.AddExecutionLogAsync(
                                historyEntryId,
                                $"  ❌ Navigation error: Step '{navigationResult.NextStepId}' not found"
                            );
                            jobResult.Status = "Failed";
                            jobResult.ErrorMessage = $"Navigation error: Step '{navigationResult.NextStepId}' not found";
                        }
                        break;

                    case NavigationAction.Error:
                        _logger.LogError("Navigation error: {ErrorMessage}", navigationResult.ErrorMessage);
                        await _historyService.AddExecutionLogAsync(
                            historyEntryId,
                            $"  ❌ Navigation error: {navigationResult.ErrorMessage}"
                        );
                        jobResult.Status = "Failed";
                        jobResult.ErrorMessage = navigationResult.ErrorMessage;
                        break;
                }

                // Break the loop if we're not continuing
                if (navigationResult.Action != NavigationAction.Continue || currentStep == null)
                {
                    break;
                }
            }

            if (jobResult.Status == "Running")
            {
                jobResult.Status = "Completed";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing job: {JobName}", job.Name);
            jobResult.Status = "Failed";
            jobResult.ErrorMessage = ex.Message;
        }
        finally
        {
            jobResult.EndTime = System.DateTime.UtcNow;
            jobResult.Duration = jobResult.EndTime.Value - jobResult.StartTime;
        }

        return jobResult;
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(
        WorkflowDefinition workflow,
        WorkflowStep step,
        ChatHistory workflowHistory,
        string executionId,
        string sessionId
    )
    {
        var stepResult = new StepExecutionResult
        {
            StepId = step.Id,
            StartTime = System.DateTime.UtcNow,
            Status = "Running",
        };

        try
        {
            // All steps are now natural language instructions - let AI determine which tools to use
            await HandlePromptAction(workflow, step, stepResult, workflowHistory, executionId, sessionId);

            if (stepResult.Status == "Running")
            {
                stepResult.Status = "Completed";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing step: {StepId}", step.Id);
            stepResult.Status = "Failed";
            stepResult.ErrorMessage = ex.Message;
        }
        finally
        {
            stepResult.EndTime = System.DateTime.UtcNow;
            stepResult.Duration = stepResult.EndTime.Value - stepResult.StartTime;
        }

        return stepResult;
    }

    private async Task HandleWaitAction(WorkflowStep step, StepExecutionResult stepResult)
    {
        _logger.LogInformation("Executing wait action for step: {StepId}", step.Id);

        // Process template variables in the prompt
        var processedPrompt = ProcessPromptTemplate(step.Prompt);

        // Extract wait time from processed prompt (default to 5 seconds)
        var waitTimeSeconds = ExtractWaitTimeFromPrompt(processedPrompt);
        await Task.Delay(TimeSpan.FromSeconds(waitTimeSeconds));

        stepResult.Output = $"Waited for {waitTimeSeconds} seconds - {processedPrompt}";
        _logger.LogInformation("Wait completed for step: {StepId}", step.Id);
    }

    private async Task HandlePluginAction(
        WorkflowDefinition workflow,
        WorkflowStep step,
        StepExecutionResult stepResult,
        ChatHistory workflowHistory,
        string executionId
    )
    {
        var stopwatch = Stopwatch.StartNew();
        // CRITICAL: Use executionId as sessionId to maintain shared memory within workflow
        // This ensures all steps in same workflow share the same session context
        var sessionId = executionId;

        _logger.LogInformation(
            "🔧 EXECUTING PLUGIN ACTION: StepId={StepId}, Plugin={Plugin}, ExecutionId={ExecutionId}",
            step.Id,
            "AI-Selected",
            executionId
        );

        // Add the current step's prompt to the workflow history (sanitized)
        var processedPrompt = ProcessPromptTemplate(step.Prompt);

        // Add execution context to make each workflow run unique for cache purposes
        var uniquePrompt = $"[ExecutionId: {executionId}] {processedPrompt}";
        await _chatHistoryFactory.AddUserMessageAsync(workflowHistory, uniquePrompt);

        // 🚨 CRITICAL: Create action hash for duplicate prevention
        var actionData = $"AI-Selected:{uniquePrompt}:{step.Id}";
        var actionHash = ComputeHash(actionData);

        // 🚨 CRITICAL: Prevent duplicate step execution
        if (
            !await _executionContextService.TryAcquirePluginLockAsync(
                executionId,
                step.Id,
                "AI-Selected",
                actionHash
            )
        )
        {
            _logger.LogWarning(
                "🚨 DUPLICATE STEP PREVENTED: StepId={StepId}, ExecutionId={ExecutionId}, ActionHash={ActionHash}",
                step.Id,
                executionId,
                actionHash
            );

            stepResult.Output =
                $"⚠️ DUPLICATE PREVENTION: This step was already executed in this workflow run.";
            stepResult.Status = "Completed"; // Mark as completed to avoid breaking workflow
            return;
        }

        _logger.LogInformation(
            "✅ STEP LOCK ACQUIRED: StepId={StepId}, ExecutionId={ExecutionId}, ActionHash={ActionHash}",
            step.Id,
            executionId,
            actionHash
        );

        // Use the PERFECT execution settings that work 100% in WebChat
        var settings = ChatExecutionService.CreatePerfectExecutionSettings();

        try
        {
            // Check cache first - AI will determine if communication tools are needed
            var cachedResponse = await _cacheService.GetCachedResponseAsync(
                workflowHistory,
                settings
            );

            if (cachedResponse != null)
            {
                stepResult.Output = cachedResponse;
                stopwatch.Stop();

                // Log cached plugin execution
                await _auditLogService.LogPluginExecutionAsync(
                    sessionId,
                    "AI-Selected (Cached)", // AI automatically selected tools
                    step.Id,
                    "Success",
                    null,
                    stopwatch.ElapsedMilliseconds,
                    new
                    {
                        WorkflowId = workflow.Id,
                        ExecutionId = executionId,
                        StepId = step.Id,
                        Prompt = processedPrompt,
                        Cached = true,
                    }
                );

                _logger.LogInformation(
                    "💾 USING CACHED RESPONSE: StepId={StepId}, ExecutionId={ExecutionId}",
                    step.Id,
                    executionId
                );
            }
            else
            {
                // Use the PERFECT ChatExecutionService that works 100% in WebChat
                // 🎯 CRITICAL FIX: Use unique GUID that doesn't match any user in users.json to avoid loading passwords
                var workflowDeviceFingerprint = "workflow-system-00000000-0000-0000-0000-000000000000";
                stepResult.Output = await _chatExecutionService.ExecuteChatCompletionAsync(
                    workflowHistory,
                    sessionId,
                    useCache: true,
                    executionSettings: ChatExecutionService.CreatePerfectExecutionSettings(),
                    deviceFingerprint: workflowDeviceFingerprint
                );

                stopwatch.Stop();

                // Log plugin execution
                await _auditLogService.LogPluginExecutionAsync(
                    sessionId,
                    "AI-Selected", // AI automatically selected tools
                    step.Id,
                    "Success",
                    null,
                    stopwatch.ElapsedMilliseconds,
                    new
                    {
                        WorkflowId = workflow.Id,
                        ExecutionId = executionId,
                        StepId = step.Id,
                        Prompt = processedPrompt,
                        ActionHash = actionHash,
                        AISelected = true,
                    }
                );

                _logger.LogInformation(
                    "✅ PLUGIN ACTION COMPLETED: StepId={StepId}, Plugin={Plugin}, ExecutionId={ExecutionId}",
                    step.Id,
                    "AI-Selected",
                    executionId
                );
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log failed plugin execution
            await _auditLogService.LogPluginExecutionAsync(
                sessionId,
                "AI-Selected", // AI automatically selected tools
                step.Id,
                "Failed",
                ex.Message,
                stopwatch.ElapsedMilliseconds,
                new
                {
                    WorkflowId = workflow.Id,
                    ExecutionId = executionId,
                    StepId = step.Id,
                    Prompt = processedPrompt,
                    ActionHash = actionHash,
                    AISelected = true,
                }
            );

            _logger.LogWarning(
                ex,
                "❌ PLUGIN ACTION FAILED: StepId={StepId}, Plugin={Plugin}, ExecutionId={ExecutionId}, continuing with prompt-only execution",
                step.Id,
                "AI-Selected",
                executionId
            );

            // Fallback to prompt-only execution if tool calling fails
            var cachedFallbackResponse = await _cacheService.GetCachedResponseAsync(
                workflowHistory
            );
            if (cachedFallbackResponse != null)
            {
                stepResult.Output = cachedFallbackResponse;
                _logger.LogInformation(
                    "💾 USING CACHED FALLBACK RESPONSE: StepId={StepId}, ExecutionId={ExecutionId}",
                    step.Id,
                    executionId
                );
            }
            else
            {
                var result = await _chatCompletionService.GetChatMessageContentAsync(
                    workflowHistory,
                    kernel: _kernel
                );

                stepResult.Output = result.Content ?? "";

                // Cache the fallback response
                await _cacheService.CacheResponseAsync(workflowHistory, stepResult.Output);

                _logger.LogInformation(
                    "🔄 FALLBACK PROMPT EXECUTION COMPLETED: StepId={StepId}, ExecutionId={ExecutionId}",
                    step.Id,
                    executionId
                );
            }
        }
    }

    private async Task HandlePromptAction(
        WorkflowDefinition workflow,
        WorkflowStep step,
        StepExecutionResult stepResult,
        ChatHistory workflowHistory,
        string executionId,
        string sessionId
    )
    {
        _logger.LogInformation("Executing prompt action for step: {StepId}", step.Id);

        // Add the current step's prompt to the workflow history (sanitized)
        var processedPrompt = ProcessPromptTemplate(step.Prompt);

        // Add execution context to make each workflow run unique for cache purposes
        var uniquePrompt = $"[ExecutionId: {executionId}] {processedPrompt}";
        await _chatHistoryFactory.AddUserMessageAsync(workflowHistory, uniquePrompt);

        // Check cache first
        var cachedResponse = await _cacheService.GetCachedResponseAsync(workflowHistory);
        if (cachedResponse != null)
        {
            stepResult.Output = cachedResponse;
            _logger.LogInformation("Using cached response for step: {StepId}", step.Id);
        }
        else
        {
            // Use the PERFECT ChatExecutionService that works 100% in WebChat
            // This ensures plugins are called exactly like in WebChat
            // CRITICAL: Use sessionId to maintain shared memory within workflow
            // 🎯 CRITICAL FIX: Use unique GUID that doesn't match any user in users.json to avoid loading passwords
            var workflowDeviceFingerprint = "workflow-system-00000000-0000-0000-0000-000000000000";
            stepResult.Output = await _chatExecutionService.ExecuteChatCompletionAsync(
                workflowHistory,
                sessionId, // This ensures all steps in same workflow share memory!
                useCache: true,
                executionSettings: ChatExecutionService.CreatePerfectExecutionSettings(),
                deviceFingerprint: workflowDeviceFingerprint
            );

            // Log AI response
            _logger.LogInformation("AI Response for step {StepId}: {Response}", step.Id, stepResult.Output);

            // Cache the response
            await _cacheService.CacheResponseAsync(workflowHistory, stepResult.Output);

            _logger.LogInformation("Prompt action completed for step: {StepId}", step.Id);
        }
    }

    private async Task<string?> GetSystemPromptAsync(WorkflowDefinition workflow)
    {
        if (string.IsNullOrWhiteSpace(workflow.SystemPrompt))
        {
            return null;
        }

        // If systemPrompt is "default", use dynamic system prompt generator
        if (string.Equals(workflow.SystemPrompt, "default", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                _logger.LogDebug("Generating dynamic system prompt for workflow");
                return await _systemPromptGenerator.GenerateSystemPromptAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate dynamic system prompt for workflow");
                return null;
            }
        }

        // Use the workflow's custom system prompt
        return workflow.SystemPrompt;
    }

    private string ProcessPromptTemplate(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return prompt;

        // Simple template processing
        var processedPrompt = prompt;

        // Replace {{ now }} with current timestamp
        processedPrompt = processedPrompt.Replace(
            "{{ now }}",
            System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        );

        // Replace {{ date }} with current date
        processedPrompt = processedPrompt.Replace(
            "{{ date }}",
            System.DateTime.Now.ToString("yyyy-MM-dd")
        );

        // Replace {{ time }} with current time
        processedPrompt = processedPrompt.Replace(
            "{{ time }}",
            System.DateTime.Now.ToString("HH:mm:ss")
        );

        return processedPrompt;
    }

    /// <summary>
    /// Computes a hash of the action data to create a unique identifier for duplicate prevention
    /// </summary>
    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes)[..12]; // Use first 12 characters for uniqueness
    }

    private int ExtractWaitTimeFromPrompt(string prompt)
    {
        // Simple extraction - look for numbers in the prompt
        var matches = System.Text.RegularExpressions.Regex.Matches(prompt, @"\d+");
        if (matches.Count > 0 && int.TryParse(matches[0].Value, out var seconds))
        {
            return seconds;
        }
        return 5; // Default to 5 seconds
    }

    /// <summary>
    /// Handles workflow-level navigation (OnSuccess/OnError) after all jobs are completed
    /// </summary>
    private async Task HandleWorkflowLevelNavigation(
        WorkflowDefinition workflow,
        WorkflowExecutionResult result,
        string historyEntryId,
        string executionId,
        string sessionId)
    {
        try
        {
            string? targetStepId = null;

            if (result.Status == "Completed" && !string.IsNullOrWhiteSpace(workflow.OnSuccess))
            {
                targetStepId = workflow.OnSuccess;
                _logger.LogInformation("Workflow completed successfully, executing OnSuccess step: {StepId}", targetStepId);
                await _historyService.AddExecutionLogAsync(
                    historyEntryId,
                    $"🎯 Workflow OnSuccess: Executing step '{targetStepId}'"
                );
            }
            else if (result.Status == "Failed" && !string.IsNullOrWhiteSpace(workflow.OnError))
            {
                targetStepId = workflow.OnError;
                _logger.LogInformation("Workflow failed, executing OnError step: {StepId}", targetStepId);
                await _historyService.AddExecutionLogAsync(
                    historyEntryId,
                    $"🎯 Workflow OnError: Executing step '{targetStepId}'"
                );
            }

            if (!string.IsNullOrWhiteSpace(targetStepId))
            {
                // Find the target step in any job
                var targetStep = _validationService.FindStepById(workflow, targetStepId);
                if (targetStep != null)
                {
                    _logger.LogInformation("Executing workflow-level navigation step: {StepId}", targetStepId);

                    // Create a new isolated ChatHistory for this navigation step
                    // 🎯 CRITICAL FIX: Use unique GUID that doesn't match any user in users.json to avoid loading passwords
                    var workflowDeviceFingerprint = "workflow-system-00000000-0000-0000-0000-000000000000";
                    var navigationHistory = await _chatHistoryFactory.CreateWorkflowHistoryAsync(workflowDeviceFingerprint);

                    var stepResult = await ExecuteStepAsync(workflow, targetStep, navigationHistory, executionId, sessionId);

                    // Add the navigation step result to the workflow result
                    var navigationJobResult = new JobExecutionResult
                    {
                        JobName = "Workflow Navigation",
                        StartTime = stepResult.StartTime,
                        EndTime = stepResult.EndTime,
                        Duration = stepResult.Duration,
                        Status = stepResult.Status,
                        ErrorMessage = stepResult.ErrorMessage
                    };
                    navigationJobResult.StepResults.Add(stepResult);
                    result.JobResults.Add(navigationJobResult);

                    await _historyService.AddExecutionLogAsync(
                        historyEntryId,
                        $"✅ Workflow navigation step '{targetStepId}' completed with status: {stepResult.Status}"
                    );

                    // Update overall workflow status if navigation step failed
                    if (stepResult.Status == "Failed")
                    {
                        result.Status = "Failed";
                        result.ErrorMessage = $"Workflow navigation step failed: {stepResult.ErrorMessage}";
                    }
                }
                else
                {
                    _logger.LogError("Workflow navigation step '{StepId}' not found", targetStepId);
                    await _historyService.AddExecutionLogAsync(
                        historyEntryId,
                        $"❌ Workflow navigation error: Step '{targetStepId}' not found"
                    );
                    result.Status = "Failed";
                    result.ErrorMessage = $"Workflow navigation step '{targetStepId}' not found";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling workflow-level navigation");
            await _historyService.AddExecutionLogAsync(
                historyEntryId,
                $"❌ Workflow navigation error: {ex.Message}"
            );
            result.Status = "Failed";
            result.ErrorMessage = $"Workflow navigation error: {ex.Message}";
        }
    }
}
