# Tool Name Enforcement in Workflows

This document explains how tool restrictions are enforced in workflow steps using the `toolName` field.

## Overview

The `toolName` field in workflow step definitions allows explicit control over which tools are available to the agent during each step of a workflow. This ensures that the agent can only use the specified tools, providing better control over the workflow execution.

## How It Works

1. **Definition in Workflow YAML**:
   - Each step in a workflow YAML file includes a `toolName` field
   - This field specifies which tool or tools the agent is allowed to use for that step
   - Example:
     
     ```yaml
     steps:
       - id: "step1"
         order: 1
         prompt: "Skicka ett mail"
         toolName: "Tool.Mailjet"  # Endast Tool.Mailjet är tillåtet för detta steg
     ```
     
   - For multiple tools in priority order:
     
     ```yaml
     steps:
       - id: "step2"
         order: 2
         prompt: "Skicka ett SMS"
         toolName: "Tool.Twilio,Tool.46elks"  # Använd Tool.Twilio först, om det misslyckas använd Tool.46elks
     ```
     

2. **Special Value `"System"`**:
   - The special value `"System"` can be used to indicate that the agent should not use any external tools
   - Example:
     
     ```yaml
     steps:
       - id: "step3"
         order: 3
         prompt: "Analysera data utan att anropa några externa verktyg"
         toolName: "System"  # Inga externa verktyg tillåts
     ```
     

3. **Technical Implementation**:
   - When a workflow step is executed, the `toolName` value is parsed
   - Available tools are filtered based on the names specified in `toolName`
   - Only these tools are presented to the AI when generating function definitions
   - If the AI attempts to call a tool not in the allowed list, the call will be blocked

4. **Priority Order**:
   - Tools are presented to the AI in the order specified in the `toolName` field
   - The system prompt instructs the AI to try tools in the specified order
   - If a tool fails, the AI should try the next tool in the list

## Fallback Mechanism

When multiple tools are specified in a comma-separated list, they represent a fallback chain:

1. The AI should attempt to use the first tool
2. If the first tool fails (returns an error), the AI should try the second tool
3. And so on through the list
4. If all specified tools fail, the AI should report the failure

## System Prompt Instructions

The workflow's system prompt includes instructions about how to interpret and respect the `toolName` field:

```text
VERKTYGSSPECI I WORKFLOW:
- Varje steg i workflowen har ett 'toolName'-fält som explicit anger vilket verktyg eller vilka verktyg som ska användas
- Format för ett verktyg: 'Tool.Mailjet'
- Format för flera verktyg i prioritetsordning: 'Tool.Twilio,Tool.46elks'
- Du FÅR ENDAST använda de verktyg som är listade i toolName-fältet för det specifika steget
- Följ prioritetsordningen exakt - verktyg listade efter det första används endast om föregående verktyg misslyckats
```

## Technical Details

The enforcement mechanism uses both AI instructions and technical code enforcement:

1. **AI Instructions**: The system prompt instructs the AI to only use specified tools
2. **Technical Enforcement**: The code filters available plugins based on the `toolName` value before sending function definitions to the AI
3. **Double Check**: Even if the AI somehow tries to call a non-allowed plugin, the code will block the call

This combination ensures that tools are used according to the specifications in the workflow definition.
