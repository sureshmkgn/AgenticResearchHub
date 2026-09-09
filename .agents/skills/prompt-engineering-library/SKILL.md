---
name: prompt-engineering-library
description: Prompt templating, dynamic variable interpolation, versioning, and hot-reloadable prompt registries for C# Semantic Kernel agents.
---

# Prompt Engineering & Library Skill

Use this skill when defining, editing, or versioning system/user prompts across multi-agent workflows.

## Prompt Architecture

### 1. Template Engine
- Use `{{VariableName}}` tags for variable placeholders.
- Always validate that all `RequiredVariables` are present before sending requests to the LLM Gateway.

### 2. Registering a New Prompt
```csharp
var template = new PromptTemplate(
    Name: "SupervisorPlanner",
    Version: "1.0.0",
    Description: "Deconstructs topic into sub-queries",
    RequiredVariables: ["Topic", "UserContext"],
    SystemPromptTemplate: "You are the Chief Research Supervisor...",
    UserPromptTemplate: "Research Topic: \"{{Topic}}\"\nContext: {{UserContext}}"
);
promptRegistry.Register(template);
```

### 3. Rendering Prompts in Agents
```csharp
var template = _promptRegistry.Get("SupervisorPlanner", "latest");
var (sys, user) = _templateEngine.RenderPrompts(template, new Dictionary<string, object?>
{
    ["Topic"] = topic,
    ["UserContext"] = context ?? "None"
});
```
