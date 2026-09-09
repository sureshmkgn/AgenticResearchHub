---
name: agent-orchestrator-a2a
description: Guide and patterns for designing and implementing Agent-to-Agent (A2A) collaboration, Supervisor hierarchical planning, and Blackboard memory systems in C# .NET 9.
---

# Agent-to-Agent (A2A) Orchestration Skill

Use this skill when building, modifying, or auditing multi-agent workflows, handoffs, and coordination protocols in C#.

## Key Architectural Patterns

### 1. Supervisor Decomposition Pattern
- Deconstruct complex queries into atomic, specialized sub-queries.
- Generate structured execution plans before invoking specialist agents.
- Supervise transitions and synthesize intermediate states.

### 2. Blackboard Shared Memory Pattern
- Store shared state in `AgentBlackboard` (discovered citations, verified claims, working drafts).
- Keep agents decoupled by having them read and write to the blackboard rather than tightly coupling agent classes.

### 3. Structured A2A Messaging Standard
- Every message across agents must use `AgentMessage`:
  - `MessageId`: Unique UUID string.
  - `Sender`: `AgentRole` (Supervisor, Researcher, FactChecker, Synthesizer).
  - `Recipient`: `AgentRole?` (Explicit destination or null for broadcast).
  - `Content`: Structured text or JSON payload.
  - `Metadata`: Contextual data, token costs, and prompt template versions.

## Code Blueprint
```csharp
// Publish message to A2A Message Bus
var message = new AgentMessage(
    MessageId: Guid.NewGuid().ToString(),
    Sender: AgentRole.Supervisor,
    Recipient: AgentRole.Researcher,
    Content: planOutput,
    TimestampUtc: DateTime.UtcNow,
    Metadata: new() { ["Cost"] = costRecord }
);
await _messageBus.PublishAsync(message, cancellationToken);
```
