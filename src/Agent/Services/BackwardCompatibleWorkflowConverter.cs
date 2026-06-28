using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Agent.Models;

namespace Agent.Services;

/// <summary>
/// Custom YAML type converter that provides enhanced backward compatibility for WorkflowDefinition
/// Handles both PascalCase and camelCase properties, missing properties, and legacy formats
/// </summary>
public class BackwardCompatibleWorkflowConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(WorkflowDefinition);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer nestedObjectDeserializer)
    {
        var workflow = new WorkflowDefinition();
        
        parser.Consume<MappingStart>();
        
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>();
            var keyName = key.Value.ToLowerInvariant();
            
            switch (keyName)
            {
                case "id":
                    workflow.Id = ReadStringValue(parser);
                    break;
                case "name":
                    workflow.Name = ReadStringValue(parser);
                    break;
                case "description":
                    workflow.Description = ReadStringValue(parser);
                    break;
                case "enabled":
                    workflow.Enabled = ReadBoolValue(parser);
                    break;
                case "executiontype":
                    workflow.ExecutionType = ReadStringValue(parser);
                    break;
                case "systemprompt":
                    workflow.SystemPrompt = ReadStringValue(parser);
                    break;
                case "schedule":
                    workflow.Schedule = ReadStringValue(parser);
                    break;
                case "lastexecuted":
                    workflow.LastExecuted = ReadDateTimeValue(parser);
                    break;
                case "nextexecutiontime":
                    workflow.NextExecutionTime = ReadDateTimeValue(parser);
                    break;
                case "timezone":
                    workflow.Timezone = ReadStringValue(parser);
                    break;
                case "misfirehandling":
                    workflow.MisfireHandling = ReadStringValue(parser);
                    break;
                case "scheduledescription":
                    workflow.ScheduleDescription = ReadStringValue(parser);
                    break;
                case "jobs":
                    workflow.Jobs = ReadJobsList(parser);
                    break;
                default:
                    // Skip unknown properties for forward compatibility
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
        
        // Apply backward compatibility fixes
        ApplyBackwardCompatibilityFixes(workflow);
        
        return workflow;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer nestedObjectSerializer)
    {
        // Use default serialization for writing
        throw new NotImplementedException("Use default serializer for writing");
    }

    private string ReadStringValue(IParser parser)
    {
        if (parser.TryConsume<Scalar>(out var scalar))
        {
            return scalar.Value ?? string.Empty;
        }
        
        // Handle null values
        parser.Consume<NodeEvent>();
        return string.Empty;
    }

    private bool ReadBoolValue(IParser parser)
    {
        var value = ReadStringValue(parser);
        return bool.TryParse(value, out var result) && result;
    }

    private DateTime? ReadDateTimeValue(IParser parser)
    {
        var value = ReadStringValue(parser);
        if (string.IsNullOrEmpty(value) || value == "null")
        {
            return null;
        }
        
        return DateTime.TryParse(value, out var result) ? result : null;
    }

    private List<WorkflowJob> ReadJobsList(IParser parser)
    {
        var jobs = new List<WorkflowJob>();
        
        if (!parser.TryConsume<SequenceStart>(out _))
        {
            // Not a sequence, skip
            parser.SkipThisAndNestedEvents();
            return jobs;
        }
        
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            var job = ReadJob(parser);
            if (job != null)
            {
                jobs.Add(job);
            }
        }
        
        return jobs;
    }

    private WorkflowJob? ReadJob(IParser parser)
    {
        if (!parser.TryConsume<MappingStart>(out _))
        {
            parser.SkipThisAndNestedEvents();
            return null;
        }
        
        var job = new WorkflowJob();
        
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>();
            var keyName = key.Value.ToLowerInvariant();
            
            switch (keyName)
            {
                case "name":
                    job.Name = ReadStringValue(parser);
                    break;
                case "description":
                    job.Description = ReadStringValue(parser);
                    break;
                case "order":
                    job.Order = int.TryParse(ReadStringValue(parser), out var order) ? order : 0;
                    break;
                case "enabled":
                    job.Enabled = ReadBoolValue(parser);
                    break;
                case "steps":
                    job.Steps = ReadStepsList(parser);
                    break;
                default:
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
        
        return job;
    }

    private List<WorkflowStep> ReadStepsList(IParser parser)
    {
        var steps = new List<WorkflowStep>();
        
        if (!parser.TryConsume<SequenceStart>(out _))
        {
            parser.SkipThisAndNestedEvents();
            return steps;
        }
        
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            var step = ReadStep(parser);
            if (step != null)
            {
                steps.Add(step);
            }
        }
        
        return steps;
    }

    private WorkflowStep? ReadStep(IParser parser)
    {
        if (!parser.TryConsume<MappingStart>(out _))
        {
            parser.SkipThisAndNestedEvents();
            return null;
        }
        
        var step = new WorkflowStep();
        
        while (!parser.TryConsume<MappingEnd>(out _))
        {
            var key = parser.Consume<Scalar>();
            var keyName = key.Value.ToLowerInvariant();
            
            switch (keyName)
            {
                case "id":
                    step.Id = ReadStringValue(parser);
                    break;
                case "prompt":
                    step.Prompt = ReadStringValue(parser);
                    break;
                case "order":
                    step.Order = int.TryParse(ReadStringValue(parser), out var order) ? order : 0;
                    break;
                case "actiontype":
                    // ActionType is no longer used - skip this field
                    ReadStringValue(parser);
                    break;
                case "toolname":
                    // ToolName is no longer used - skip this field
                    ReadStringValue(parser);
                    break;
                case "timeout":
                    step.Timeout = int.TryParse(ReadStringValue(parser), out var timeout) ? timeout : 300;
                    break;
                case "retries":
                    step.Retries = int.TryParse(ReadStringValue(parser), out var retries) ? retries : 2;
                    break;
                case "onsuccess":
                    step.OnSuccess = ReadStringValue(parser);
                    break;
                case "onfailure":
                    step.OnFailure = ReadStringValue(parser);
                    break;
                default:
                    parser.SkipThisAndNestedEvents();
                    break;
            }
        }
        
        return step;
    }

    /// <summary>
    /// Apply backward compatibility fixes to the workflow
    /// </summary>
    private void ApplyBackwardCompatibilityFixes(WorkflowDefinition workflow)
    {
        // Ensure schedule is not null
        if (workflow.Schedule == null)
        {
            workflow.Schedule = string.Empty;
        }
        
        // Set default execution type if missing
        if (string.IsNullOrEmpty(workflow.ExecutionType))
        {
            workflow.ExecutionType = "manual";
        }
        
        // Set default timezone if missing
        if (string.IsNullOrEmpty(workflow.Timezone))
        {
            workflow.Timezone = "Europe/Stockholm";
        }
        
        // Set default misfire handling if missing
        if (string.IsNullOrEmpty(workflow.MisfireHandling))
        {
            workflow.MisfireHandling = "Skip";
        }
        
        // Ensure jobs list is not null
        if (workflow.Jobs == null)
        {
            workflow.Jobs = new List<WorkflowJob>();
        }
        
        // Fix job and step properties
        foreach (var job in workflow.Jobs)
        {
            if (job.Steps == null)
            {
                job.Steps = new List<WorkflowStep>();
            }
            
            foreach (var step in job.Steps)
            {
                // Set default values for missing properties
                if (step.Timeout <= 0)
                {
                    step.Timeout = 300;
                }
                
                if (step.Retries < 0)
                {
                    step.Retries = 2;
                }
                
                // ActionType is no longer used - all steps use natural language instructions
                
                if (string.IsNullOrEmpty(step.OnSuccess))
                {
                    step.OnSuccess = "completed";
                }
                
                if (string.IsNullOrEmpty(step.OnFailure))
                {
                    step.OnFailure = "failed";
                }
            }
        }
    }
}
