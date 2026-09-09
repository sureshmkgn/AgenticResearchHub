# C# 13 & .NET 9 Enterprise Engineering Standards

This rule governs all C# and .NET development in this repository. The Antigravity AI assistant must strictly adhere to these guidelines.

---

## 1. Language & Framework Standards
- **Target Framework**: .NET 9.0 (`net9.0`) with C# 13 language version (`<LangVersion>13</LangVersion>`).
- **Nullable Reference Types**: Strictly enabled (`<Nullable>enable</Nullable>`). Never suppress warnings with `#nullable disable` unless interfacing with unmanaged code.
- **Implicit Usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`).
- **Modern C# 13 Idioms**:
  - Use primary constructors for classes and dependency injection where appropriate.
  - Use collection expressions `[]` instead of `new List<T>()` or `new[] { ... }`.
  - Use file-scoped namespace declarations (`namespace MyNamespace;`).
  - Use pattern matching (`switch` expressions, relational patterns, `is not null`).
  - Use target-typed `new()` expressions when the type is obvious.

---

## 2. Asynchronous Programming & Cancellation
- **Async/Await**: All I/O-bound operations must be asynchronous with suffix `Async`.
- **CancellationToken**: Always accept and forward `CancellationToken cancellationToken = default` across every asynchronous layer.
- **Real-time Streaming**: Favor `IAsyncEnumerable<T>` with `[EnumeratorCancellation]` for streaming LLM events, SSE, or agent reasoning steps.
- **No Blocking Calls**: Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.

---

## 3. Microsoft Agentic Patterns & Semantic Kernel
- **Separation of Concerns**:
  - `Core`: Pure domain models, interfaces, guardrail rules, and telemetry contracts (zero heavy framework dependencies).
  - `Infrastructure`: LLM gateway clients, vector store persistence, search providers, and MCP clients.
  - `Agents`: Concrete agent personas, A2A message routing, blackboard memory, and workflow orchestrators.
  - `Web`: Minimal APIs, SSE streaming endpoints, static UI assets, and Prometheus scraping.
- **Resilience**: Wrap all outbound LLM and HTTP calls with Polly resilience pipelines (`Microsoft.Extensions.Http.Resilience` with retry, exponential backoff, jitter, timeout, and circuit breaker).
- **Prompt Isolation**: Never hardcode system prompts in agent business logic. Use `PromptTemplateEngine` and `PromptRegistry` with versioned templates.

---

## 4. Observability & Telemetry Standards
- **OpenTelemetry ActivitySource**: Every agent hop, search tool invocation, and LLM call must start a traced span (`AgentDiagnostics.ActivitySource.StartActivity`).
- **Meters & Counters**: Track requests, steps, token consumption, financial cost (USD), and guardrail violations via `AgentDiagnostics.Meter`.
- **Structured Logging**: Use `ILogger<T>` with structured placeholders (e.g. `_logger.LogInformation("Processing topic {Topic}", topic);`), never string interpolation.
