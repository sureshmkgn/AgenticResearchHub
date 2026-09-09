# Prompt Templating & Versioned Prompt Library

## 1. Overview

The platform uses a dedicated **`PromptTemplateEngine`** and **`PromptRegistry`** to decouple LLM prompt engineering from agent business logic.

Prompts support:
- **Variable Injection**: Double-brace syntax `{{VariableName}}`.
- **Validation**: Required variables check before execution.
- **Versioning**: Semantic versioning (e.g. `v1.0.0`, `v1.1.0`, or `latest`).
- **Dynamic Hot-Reloading**: Prompts can be inspected or registered at runtime via REST APIs without restarting containers.

---

## 2. Default Prompt Catalog

| Template Name | Version | Role | Key Required Variables |
|---|---|---|---|
| `SupervisorPlanner` | `1.0.0` | Supervisor | `Topic`, `UserContext` |
| `ResearcherIntelligence` | `1.0.0` | Researcher | `Topic`, `CitationsContext` |
| `FactCheckerReview` | `1.0.0` | Fact-Checker | `RawFindings` |
| `SynthesizerReportWriter` | `1.0.0` | Synthesizer | `VerifiedEvidence` |

---

## 3. Managing Prompts via REST API

### List all registered prompt templates:
```http
GET /api/prompts
```

### Fetch a specific prompt template version:
```http
GET /api/prompts/SupervisorPlanner?version=1.0.0
```

### Register or override a prompt template at runtime:
```http
POST /api/prompts
Content-Type: application/json

{
  "name": "SupervisorPlanner",
  "version": "2.0.0",
  "description": "Enhanced multi-domain supervisor with deep financial sector reasoning",
  "requiredVariables": ["Topic", "UserContext"],
  "systemPromptTemplate": "You are an executive research planner...",
  "userPromptTemplate": "Topic: {{Topic}}"
}
```
