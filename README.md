# Agentic Research Hub (.NET 9 / C# 13)

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Semantic Kernel](https://img.shields.io/badge/Semantic%20Kernel-Agentic%20AI-blue)](https://github.com/microsoft/semantic-kernel)
[![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-Traces%20%26%20Metrics-orange?logo=opentelemetry&logoColor=white)](https://opentelemetry.io/)
[![Docker Compose](https://img.shields.io/badge/Docker%20Compose-Orchestration-2496ED?logo=docker&logoColor=white)](https://docs.docker.com/compose/)
[![Grafana](https://img.shields.io/badge/Grafana-Dashboard%203000-F46800?logo=grafana&logoColor=white)](https://grafana.com/)
[![Jaeger](https://img.shields.io/badge/Jaeger-Distributed%20Tracing-00A98F?logo=jaeger&logoColor=white)](https://www.jaegertracing.io/)

> **Enterprise Autonomous Multi-Agent Research Platform built strictly with Microsoft .NET 9, C# 13, Microsoft Semantic Kernel, Model Context Protocol (MCP), Agent-to-Agent (A2A) Mesh, Hardware-Accelerated SIMD Vector Search, OpenTelemetry Observability, and Prometheus/Grafana Stack.**

---

## 📑 Table of Contents
- [Architecture Overview](#-architecture-overview)
- [Multi-Agent Collaboration Workflow](#-multi-agent-collaboration-workflow)
- [Key Features](#-key-features)
- [Project Structure & Clean Architecture](#-project-structure--clean-architecture)
- [Quick Start](#-quick-start)
- [Observability & Monitoring Stack](#-observability--monitoring-stack)
- [Deployment Modes](#-deployment-modes)
- [REST & SSE Stream API Reference](#-rest--sse-stream-api-reference)
- [Automated Testing](#-automated-testing)
- [Configuration](#-configuration)

---

## 🏛️ Architecture Overview

The platform uses a layered Clean Architecture pattern with loose coupling, dependency inversion, and co-located domain prompts:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Web UI & REST / SSE API                          │
│                (ASP.NET Core Minimal APIs / Web Root)                   │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
┌────────────────────────────────────▼────────────────────────────────────┐
│                    Research Orchestrator (Supervisor)                   │
├─────────────────────────────────────────────────────────────────────────┤
│   A2A In-Memory Message Bus  │  Guardrail Pipeline (Input & Output)     │
├─────────────────────────────────────────────────────────────────────────┤
│  [SupervisorAgent]  │  [ResearcherAgent]  │  [FactChecker]  │ [Synthesizer]│
│  (Deconstruction)   │  (Web Intelligence) │  (Peer-Review)  │  (Reporting) │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
┌────────────────────────────────────▼────────────────────────────────────┐
│                       Infrastructure Services                           │
├─────────────────────────────────────────────────────────────────────────┤
│  • LLM Gateway (OpenAI / Azure / Ollama / Local Fallback Reasoner)     │
│  • Web Search Service (DuckDuckGo / Bing Search v7 / Custom MCP)        │
│  • SIMD Vector Store (Normalized Cosine Similarity & Indexing)          │
│  • OpenTelemetry Tracing (ActivitySource) & Meter Metrics               │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🤖 Multi-Agent Collaboration Workflow

1. **Supervisor Agent (`SupervisorAgent`)**:
   - Deconstructs research queries into 3–4 targeted sub-hypotheses and produces a 4-phase execution roadmap.
2. **Researcher Agent (`ResearcherAgent`)**:
   - Executes multi-query web intelligence retrieval via DuckDuckGo, Bing v7, or external Model Context Protocol (MCP) servers and extracts grounded citations.
3. **Fact-Checker & Critic (`FactCheckerAgent`)**:
   - Validates findings against retrieved citations, eliminates hallucinations, checks bias, and calculates a confidence score (0–100%).
4. **Executive Synthesizer (`SynthesizerAgent`)**:
   - Compiles peer-reviewed findings, architectural trade-offs, and citations into an executive markdown briefing.

---

## 🌟 Key Features

- **Strictly .NET 9 & C# 13**: High-performance asynchronous streaming with `IAsyncEnumerable<AgentStep>`, modern pattern matching, records, and primary constructors.
- **Enterprise Guardrail Pipeline**:
  - `InputSanitizerGuardrail`: Strips control characters and normalizes Unicode.
  - `PromptInjectionGuardrail`: Regex and heuristic detection against prompt injection and jailbreak payloads.
  - `TopicPolicyGuardrail`: Verifies research queries conform to organization safety standards.
  - `OutputFactualityGuardrail`: Prevents system prompt leakage and ungrounded statements.
- **Dynamic Prompt Templating & Prompt Library**:
  - Abstract rendering engine in `Core` (`{{Variable}}` interpolation).
  - Specialist agents in `Agents` encapsulate and self-register their own default prompts.
  - Live REST endpoints (`GET /api/prompts`, `POST /api/prompts`) for runtime prompt updates.
- **SIMD Vector Store & Semantic Search**:
  - Hardware-accelerated SIMD cosine similarity.
  - Real-time indexing of synthesized research reports with vector embeddings.
- **Real-Time Token & Cost Accounting**:
  - Calculates prompt tokens, completion tokens, and real-time USD cost based on model pricing catalogs.
  - Metrics exported directly to OpenTelemetry & Prometheus.
- **Full Observability Suite**:
  - Pre-configured Grafana operational dashboard (`http://localhost:3000`).
  - Distributed request tracing via Jaeger (`http://localhost:16686`).
  - Prometheus time-series metrics (`http://localhost:9090`).
- **Interactive Dark Glassmorphism Web UI**:
  - Live Server-Sent Events (SSE) agent execution stream.
  - Interactive multi-agent topology visualizer.
  - Markdown report reader and vector search explorer.

---

## 📁 Project Structure & Clean Architecture

```
AgenticResearchHub/
├── src/
│   ├── AgenticResearchHub.Core/             # Pure abstractions, interfaces, domain models,
│   │                                        # guardrails, token cost calculator, prompt engine
│   ├── AgenticResearchHub.Agents/           # Agent implementations, co-located prompts,
│   │                                        # A2A message bus, supervisor orchestrator
│   ├── AgenticResearchHub.Infrastructure/   # LLM gateway, MCP client, SIMD vector store,
│   │                                        # web search providers (DuckDuckGo, Bing)
│   └── AgenticResearchHub.Web/              # ASP.NET Core host, Minimal APIs, SSE streaming,
│                                            # static web UI assets (HTML/CSS/JS)
├── tests/
│   └── AgenticResearchHub.Tests/            # Unit & integration tests for orchestrator,
│                                            # vector search, guardrails, and prompt library
├── monitoring/
│   ├── grafana/                             # Grafana datasources and pre-built dashboard
│   ├── prometheus/                          # Prometheus scrape configuration
│   └── otel-collector/                      # OpenTelemetry collector configuration
├── docs/                                    # Detailed technical specifications
├── docker-compose.yml                       # Consolidated deployment stack
├── docker-compose.microservices.yml         # Independent micro-agent cluster deployment
└── Dockerfile                               # Multi-stage .NET 9 container build
```

---

## 🚀 Quick Start

### Prerequisites
- [Docker](https://www.docker.com/) & Docker Compose

### 1. Launch the Stack
```bash
docker compose up -d --build
```

### 2. Access the Applications
| Service | URL | Notes / Credentials |
| :--- | :--- | :--- |
| **Research Hub Web UI** | [http://localhost:5050](http://localhost:5050) | Interactive Agentic Workspace & Chat |
| **Swagger API Explorer** | [http://localhost:5050/swagger](http://localhost:5050/swagger) | OpenAPI Specifications |
| **Grafana Dashboard** | [http://localhost:3000](http://localhost:3000) | `admin` / `admin` |
| **Jaeger Tracing** | [http://localhost:16686](http://localhost:16686) | Service: `AgenticResearchHub` |
| **Prometheus Metrics** | [http://localhost:9090](http://localhost:9090) | Time-series query interface |

---

## 📊 Observability & Monitoring Stack

The platform includes production-ready telemetry:
- **Distributed Tracing**: All agent step executions, LLM gateway calls, and web search requests create OpenTelemetry spans tracked via Jaeger.
- **Custom Metrics**:
  - `agents.workflow.duration`: Execution latency across workflow phases.
  - `agents.step.count`: Execution counts grouped by agent role.
  - `agents.tokens.used`: Token volume per model.
  - `agents.cost.usd`: Estimated LLM inference cost in USD.
- **Grafana Dashboard**: Pre-provisioned dashboard displaying active agents, token rate, query latency, and total operational cost.

---

## 🧩 Deployment Modes

### Mode A: Consolidated Stack (`docker-compose.yml`)
Runs the full Agentic Hub and observability stack in a unified container setup on port `5050`.

```bash
docker compose up -d
```

### Mode B: Independent Micro-Agent Cluster (`docker-compose.microservices.yml`)
Runs each specialist agent as an independently scalable microservice behind an API Gateway:
- **Gateway Orchestrator**: Port `5060`
- **Supervisor Service**: Port `5061`
- **Researcher Service**: Port `5062`
- **Fact-Checker Service**: Port `5063`
- **Synthesizer Service**: Port `5064`

```bash
docker compose -f docker-compose.microservices.yml up -d
```

---

## 🔌 REST & SSE Stream API Reference

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `POST` | `/api/research/stream` | Real-time Server-Sent Events (SSE) stream of agent steps |
| `POST` | `/api/research/execute` | Synchronous research workflow execution |
| `GET` | `/api/reports` | List all synthesized research reports |
| `POST` | `/api/reports/search` | Hardware-accelerated SIMD semantic vector search |
| `GET` | `/api/prompts` | List all registered agent prompt templates |
| `POST` | `/api/prompts` | Register or update a versioned prompt template |
| `GET` | `/api/a2a/history` | Retrieve in-memory Agent-to-Agent message bus history |
| `GET` | `/metrics` | Prometheus metrics scraping endpoint |

---

## 🧪 Automated Testing

Run the automated test suite inside the official Microsoft .NET 9 SDK container:

```bash
docker run --rm -v $(pwd):/src -w /src mcr.microsoft.com/dotnet/sdk:9.0 dotnet test AgenticResearchHub.sln
```

### Test Coverage Highlights:
- ✅ **`ResearchOrchestrator_Should_Execute_Full_Workflow`**: Validates end-to-end multi-agent execution pipeline.
- ✅ **`GuardrailTests`**: Validates control character sanitization and prompt injection rejection.
- ✅ **`VectorSearchTests`**: Tests SIMD cosine similarity math, vector normalization, and semantic search retrieval.
- ✅ **`PromptAndCostTests`**: Tests prompt templating engine, agent prompt registration, and token cost calculation.

---

## ⚙️ Configuration

Copy `.env.example` to `.env` to configure external LLM providers and search engines:

```ini
# LLM Gateway Configuration
LLM_PROVIDER=OpenAI                     # OpenAI | AzureOpenAI | Ollama
LLM_ENDPOINT=https://api.openai.com/v1
LLM_API_KEY=your_api_key_here
LLM_MODEL_ID=gpt-4o
LLM_EMBEDDING_MODEL_ID=text-embedding-3-small

# Search Provider Configuration
WEB_SEARCH_PROVIDER=DuckDuckGo          # DuckDuckGo | Bing | Mcp
```

> **Zero-Config Resilient Mode**: If no external API keys are configured, the hub operates autonomously using built-in deterministic semantic reasoners and live DuckDuckGo web searches with zero cloud dependencies.

---

## 📄 License
This project is licensed under the MIT License.
