---
name: observability-and-cost
description: OpenTelemetry distributed tracing, Prometheus metrics, Grafana dashboards, and real-time LLM financial cost accounting in .NET 9.
---

# Observability & Cost Management Skill

Use this skill when configuring metrics, distributed traces, Grafana panels, and token cost tracking in C#.

## Observability Stack Flow

```
[.NET 9 App] ──► OTLP / Prometheus ──► [OTel Collector / Prometheus] ──► [Jaeger UI / Grafana]
```

## Telemetry Components

### 1. Activity Tracing (`ActivitySource`)
```csharp
using var activity = AgentDiagnostics.ActivitySource.StartActivity("AgentName.ProcessMessage");
activity?.SetTag("agent.role", "Supervisor");
activity?.SetTag("research.topic", topic);
```

### 2. Metrics & Counters (`Meter`)
- `agentic.research.requests.total`: Total missions launched.
- `agentic.agent.steps.total`: Agent reasoning steps executed.
- `agentic.llm.cost.usd`: Cumulative USD cost calculated.
- `agentic.llm.tokens.input` & `agentic.llm.tokens.output`: Token counters.

### 3. Cost Calculation Standard
```csharp
var (inTokens, outTokens) = TokenCostCalculator.EstimateTokenCount(prompt, response);
var costRecord = TokenCostCalculator.Calculate(modelId, inTokens, outTokens);
// Pricing: gpt-4o ($2.50/1M in, $10.00/1M out), gpt-4o-mini ($0.15/1M in, $0.60/1M out)
```
