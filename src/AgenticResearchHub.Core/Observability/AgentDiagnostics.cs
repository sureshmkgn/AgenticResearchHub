using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AgenticResearchHub.Core.Observability;

public static class TelemetryConstants
{
    public const string ServiceName = "AgenticResearchHub";
    public const string ServiceVersion = "1.0.0";
    public const string ActivitySourceName = "AgenticResearchHub.Activities";
    public const string MeterName = "AgenticResearchHub.Metrics";
}

public static class AgentDiagnostics
{
    public static readonly ActivitySource ActivitySource = new(
        TelemetryConstants.ActivitySourceName,
        TelemetryConstants.ServiceVersion
    );

    public static readonly Meter Meter = new(
        TelemetryConstants.MeterName,
        TelemetryConstants.ServiceVersion
    );

    // Metrics counters & histograms
    public static readonly Counter<long> ResearchRequestsCounter = Meter.CreateCounter<long>(
        "agentic.research.requests.total",
        description: "Total number of research requests initiated"
    );

    public static readonly Counter<long> AgentStepCounter = Meter.CreateCounter<long>(
        "agentic.agent.steps.total",
        description: "Total count of individual agent processing steps"
    );

    public static readonly Counter<long> WebSearchQueryCounter = Meter.CreateCounter<long>(
        "agentic.search.queries.total",
        description: "Total web search tool queries executed"
    );

    public static readonly Counter<long> GuardrailViolationCounter = Meter.CreateCounter<long>(
        "agentic.guardrail.violations.total",
        description: "Count of safety/policy violations caught by guardrails"
    );

    public static readonly Histogram<double> ResearchDurationHistogram = Meter.CreateHistogram<double>(
        "agentic.research.duration.seconds",
        unit: "s",
        description: "End-to-end duration for completing multi-agent research workflow"
    );

    public static readonly Histogram<double> LlmLatencyHistogram = Meter.CreateHistogram<double>(
        "agentic.llm.call.latency.ms",
        unit: "ms",
        description: "Latency of LLM calls in milliseconds"
    );
}
