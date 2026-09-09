---
name: csharp-agent-architect
description: Enterprise C# 13 and .NET 9 Agentic AI Architect skill. Use this skill to scaffold new custom agents, review code for Microsoft patterns and practices, verify OpenTelemetry tracing, and inspect Polly resilience and guardrail pipelines.
---

# C# Agent Architect Skill

Use this skill when developing, refactoring, or auditing multi-agent systems in this C# codebase.

## Workflows

### 1. Scaffolding a New Agent
When asked to create a new agent:
1. Define the role in [`AgentRole.cs`](../../src/AgenticResearchHub.Core/Models/AgentRole.cs).
2. Register a prompt template in [`PromptTemplates.cs`](../../src/AgenticResearchHub.Core/Prompts/PromptTemplates.cs) with versioning and parameter validation.
3. Implement the `IAgent` interface in [`AgentImplementations.cs`](../../src/AgenticResearchHub.Agents/Implementations/AgentImplementations.cs) with `TokenCostCalculator` and `AgentDiagnostics` span recording.
4. Register the agent in `Program.cs` and create a dedicated endpoint under `/api/agents/<agent-name>/process`.
5. Add unit tests covering the new agent under `tests/AgenticResearchHub.Tests/`.

### 2. Code Quality Checklist
- [ ] Targets `net9.0` with C# 13 syntax.
- [ ] `CancellationToken` forwarded across all async call chains.
- [ ] Uses collection expressions (`[]`), primary constructors, and file-scoped namespaces.
- [ ] Protected by `GuardrailPipeline`.
- [ ] Traced with `AgentDiagnostics.ActivitySource.StartActivity`.
- [ ] Token and USD cost calculated and reported.
- [ ] Zero build warnings / errors (`dotnet test AgenticResearchHub.sln` passes).
