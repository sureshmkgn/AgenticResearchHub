using System.Diagnostics.Metrics;
using AgenticResearchHub.Core.Observability;

namespace AgenticResearchHub.Core.Cost;

public record ModelPricing(
    string ModelId,
    decimal InputPricePerMillionTokensUsd,
    decimal OutputPricePerMillionTokensUsd
);

public record CostUsageRecord(
    string ModelId,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd,
    DateTime TimestampUtc
)
{
    public int TotalTokens => InputTokens + OutputTokens;
}

public static class CostMetrics
{
    public static readonly Counter<decimal> LlmCostUsdCounter = AgentDiagnostics.Meter.CreateCounter<decimal>(
        "agentic.llm.cost.usd",
        unit: "USD",
        description: "Estimated financial cost in USD of LLM completions"
    );

    public static readonly Counter<long> LlmInputTokensCounter = AgentDiagnostics.Meter.CreateCounter<long>(
        "agentic.llm.tokens.input",
        unit: "tokens",
        description: "Total input (prompt) tokens consumed across agents"
    );

    public static readonly Counter<long> LlmOutputTokensCounter = AgentDiagnostics.Meter.CreateCounter<long>(
        "agentic.llm.tokens.output",
        unit: "tokens",
        description: "Total output (completion) tokens generated across agents"
    );
}

public class TokenCostCalculator
{
    private static readonly Dictionary<string, ModelPricing> PricingTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4o"] = new("gpt-4o", 2.50m, 10.00m),
        ["gpt-4o-mini"] = new("gpt-4o-mini", 0.15m, 0.60m),
        ["gpt-4-turbo"] = new("gpt-4-turbo", 10.00m, 30.00m),
        ["gpt-3.5-turbo"] = new("gpt-3.5-turbo", 0.50m, 1.50m),
        ["claude-3-5-sonnet"] = new("claude-3-5-sonnet", 3.00m, 15.00m),
        ["text-embedding-3-small"] = new("text-embedding-3-small", 0.02m, 0.00m),
        ["text-embedding-3-large"] = new("text-embedding-3-large", 0.13m, 0.00m),
        ["ollama"] = new("ollama", 0.00m, 0.00m),
        ["local"] = new("local", 0.00m, 0.00m)
    };

    public static CostUsageRecord Calculate(string modelId, int inputTokens, int outputTokens)
    {
        if (!PricingTable.TryGetValue(modelId, out var pricing))
        {
            pricing = PricingTable["gpt-4o"]; // Default standard tier
        }

        var inputCost = (inputTokens / 1_000_000m) * pricing.InputPricePerMillionTokensUsd;
        var outputCost = (outputTokens / 1_000_000m) * pricing.OutputPricePerMillionTokensUsd;
        var totalCost = Math.Round(inputCost + outputCost, 6);

        // Record OpenTelemetry metrics
        CostMetrics.LlmCostUsdCounter.Add(totalCost);
        CostMetrics.LlmInputTokensCounter.Add(inputTokens);
        CostMetrics.LlmOutputTokensCounter.Add(outputTokens);

        return new CostUsageRecord(modelId, inputTokens, outputTokens, totalCost, DateTime.UtcNow);
    }

    public static (int InputTokens, int OutputTokens) EstimateTokenCount(string prompt, string response)
    {
        // Approximately 4 characters per token for English text
        int inputTokens = Math.Max(1, (int)Math.Ceiling((prompt?.Length ?? 0) / 4.0));
        int outputTokens = Math.Max(1, (int)Math.Ceiling((response?.Length ?? 0) / 4.0));
        return (inputTokens, outputTokens);
    }
}
