# Enterprise Observability, Metrics & Distributed Tracing Guide

## 1. Observability Architecture

**AgenticResearchHub** incorporates full-spectrum OpenTelemetry instrumentation across all agentic activities, LLM Gateway calls, tool executions, and security guardrail filters.

```
[Agentic .NET 9 Service]
   │
   ├── OTLP (gRPC / HTTP :4317) ──► [OpenTelemetry Collector] ──► [Jaeger UI :16686] (Distributed Tracing)
   │                                        │
   └── Prometheus Exporter (/metrics) ──────┴─────────────────► [Prometheus :9090] ──► [Grafana :3000] (Dashboards)
```

---

## 2. OpenTelemetry Tracing Spans & Traceability

Every research mission creates a root trace context and child activity spans with granular attributes:

| Activity / Span Name | Component | Key Tags & Attributes | Description |
|---|---|---|---|
| `ResearchWorkflow.Execute` | Orchestrator | `research.topic`, `user.context` | Root span measuring total end-to-end execution. |
| `SupervisorAgent.ProcessMessage` | Supervisor | `agent.role=Supervisor` | Sub-query decomposition and strategy planning. |
| `ResearcherAgent.ProcessMessage` | Researcher | `agent.role=Researcher` | Web search synthesis and evidence extraction. |
| `WebSearch.DuckDuckGo` / `WebSearch.Bing` | Search Tool | `search.query`, `results.count` | Latency and status of external search API calls. |
| `FactCheckerAgent.ProcessMessage` | Critic | `agent.role=FactChecker` | Peer review and hallucination verification. |
| `SynthesizerAgent.ProcessMessage` | Synthesizer | `agent.role=Synthesizer` | Structured report compilation in Markdown. |
| `LlmGateway.GenerateCompletion` | LLM Gateway | `llm.provider`, `llm.model` | Direct duration of LLM inference calls. |
| `SemanticReportStore.SaveReport` | Vector Store | `report.id`, `report.topic` | Vector embedding generation & indexing. |
| `SemanticReportStore.SemanticSearch` | Vector Store | `search.query`, `matches.count` | Cosine similarity ranking execution. |

---

## 3. Metrics Catalog (Prometheus & OpenTelemetry)

| Metric Name | Type | Unit | Description |
|---|---|---|---|
| `agentic.research.requests.total` | Counter | Requests | Total research missions initiated. |
| `agentic.agent.steps.total` | Counter | Steps | Total individual agent reasoning and handoff steps. |
| `agentic.search.queries.total` | Counter | Queries | Number of web searches / tool calls executed. |
| `agentic.guardrail.violations.total` | Counter | Violations | Count of blocked prompt injections and policy breaches. |
| `agentic.research.duration.seconds` | Histogram | Seconds | End-to-end mission latency distribution. |
| `agentic.llm.call.latency.ms` | Histogram | Milliseconds | Latency of cloud LLM gateway completion calls. |
| `agentic.llm.cost.usd` | Counter | USD ($) | Cumulative estimated financial cost of inference. |
| `agentic.llm.tokens.input` | Counter | Tokens | Total prompt tokens consumed. |
| `agentic.llm.tokens.output` | Counter | Tokens | Total completion tokens generated. |

---

## 4. Spin Up the Full Observability Stack

Launch the unified Docker stack with one command:
```bash
docker compose up -d
```

### Access Ports
- **Agentic Research Hub Web UI & API**: `http://localhost:5050`
- **Grafana Dashboards**: `http://localhost:3000` (User: `admin`, Password: `admin`)
- **Jaeger Tracing Explorer**: `http://localhost:16686`
- **Prometheus Metrics Browser**: `http://localhost:9090`
- **Application Prometheus Endpoint**: `http://localhost:5050/metrics`
