# Security, Guardrails & Observability Standards

This rule governs security guardrails, cost controls, and telemetry across all agentic services.

---

## 1. Security & Guardrails
- **Input Sanitization**: All user-supplied inputs must pass through `InputSanitizerGuardrail` to strip non-printable control characters before processing.
- **Prompt Injection Defense**: Scan queries against prompt injection and jailbreak patterns before invoking LLMs.
- **Output Moderation**: Validate generated agent outputs for completeness and ensure no system prompt/internal key leakage occurs.
- **Non-Root Containers**: Ensure all Dockerfiles run under non-root users (`USER app`).

---

## 2. Cost & Token Accounting
- Calculate input/output token counts for every LLM invocation.
- Convert token usage into estimated USD cost using `TokenCostCalculator`.
- Record cost metrics via `CostMetrics.LlmCostUsdCounter`.

---

## 3. Distributed Tracing & Metrics
- Ensure `OTEL_EXPORTER_OTLP_ENDPOINT` is respected in containerized environments.
- Expose the Prometheus scraping endpoint at `/metrics` on port `8080` (`5050` / `5060` on host).
