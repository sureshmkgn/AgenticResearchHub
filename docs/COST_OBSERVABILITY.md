# Real-Time Cost Observability & Token Accounting

## 1. Overview

**AgenticResearchHub** implements fine-grained financial and token observability. Every multi-agent step tracks prompt (input) and completion (output) tokens, estimates pricing based on the active LLM tier, and publishes metrics to OpenTelemetry and Prometheus.

---

## 2. Pricing Lookup Matrix

| Model Tier | Input Price (per 1M tokens) | Output Price (per 1M tokens) |
|---|---|---|
| `gpt-4o` | **$2.50** | **$10.00** |
| `gpt-4o-mini` | **$0.15** | **$0.60** |
| `gpt-4-turbo` | **$10.00** | **$30.00** |
| `claude-3-5-sonnet` | **$3.00** | **$15.00** |
| `text-embedding-3-small`| **$0.02** | **$0.00** |
| `ollama / local` | **$0.00 (Free)** | **$0.00 (Free)** |

---

## 3. Real-Time Telemetry & Prometheus Metric Series

1. **`agentic_llm_cost_usd_total`**: Total cumulative USD cost of all agent operations.
2. **`agentic_llm_tokens_input_total`**: Total prompt tokens passed across agents.
3. **`agentic_llm_tokens_output_total`**: Total completion tokens generated.

---

## 4. Viewing Cost in Grafana and UI

- **Grafana Panel**: Displays real-time dollar burn rate, token velocity per second, and cost per research topic.
- **Web UI & SSE Stream**: Each research report includes `totalEstimatedCostUsd`, `totalInputTokens`, and `totalOutputTokens` in its metadata card.
