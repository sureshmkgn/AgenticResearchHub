---
name: docker-microservices-deploy
description: Multi-stage Docker builds, Docker Compose orchestration, and independently deployable micro-agent service clusters in .NET 9.
---

# Docker & Microservices Deployment Skill

Use this skill when managing Dockerfiles, building container images, or scaling independent micro-agent services.

## Operational Topologies

### 1. Consolidated High-Density Mode (`docker-compose.yml`)
- Runs Orchestrator, All Agents, Vector Store, Prometheus, Grafana, Jaeger, and OTel Collector in one compose stack.
```bash
docker compose up -d
```
- **Web UI & API**: `http://localhost:5050`
- **Grafana**: `http://localhost:3000`
- **Jaeger**: `http://localhost:16686`
- **Prometheus**: `http://localhost:9090`

### 2. Distributed Micro-Agent Mesh Mode (`docker-compose.microservices.yml`)
- Runs each agent as an isolated, independently scalable container:
  - `orchestrator-gateway` (`:5060`)
  - `supervisor-agent-service` (`:5061`)
  - `researcher-agent-service` (`:5062`)
  - `factchecker-agent-service` (`:5063`)
  - `synthesizer-agent-service` (`:5064`)
```bash
docker compose -f docker-compose.microservices.yml up -d
```

## Security Best Practices
- Multi-stage build (`mcr.microsoft.com/dotnet/sdk:9.0` for build/test & `mcr.microsoft.com/dotnet/aspnet:9.0` for runtime).
- Always run under non-root user `USER app`.
