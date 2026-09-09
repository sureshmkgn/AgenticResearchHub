namespace AgenticResearchHub.Core.Models;

public record ResearchRequest(
    string Topic,
    string? UserContext = null,
    int MaxIterations = 3,
    string? SearchEngine = "DuckDuckGo",
    bool EnableGuardrails = true,
    bool ForceDeepResearch = false
);

public record Citation(
    int Index,
    string Title,
    string Url,
    string Snippet,
    string? SourceDomain = null,
    double RelevanceScore = 1.0
);

public class ResearchReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Topic { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ExecutiveSummary { get; set; } = string.Empty;
    public string FullContentMarkdown { get; set; } = string.Empty;
    public List<string> KeyFindings { get; set; } = [];
    public List<Citation> Citations { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public TimeSpan TotalExecutionDuration { get; set; }
    public int IterationsCount { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public decimal TotalEstimatedCostUsd { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
    public float[]? EmbeddingVector { get; set; }
}

public record AgentMessage(
    string MessageId,
    AgentRole Sender,
    AgentRole? Recipient,
    string Content,
    DateTime TimestampUtc,
    Dictionary<string, object>? Metadata = null
);

public record AgentStep(
    string StepId,
    AgentRole Agent,
    string Title,
    string Details,
    StepStatus Status,
    DateTime TimestampUtc,
    TimeSpan? Duration = null,
    decimal? StepCostUsd = null,
    object? Payload = null
);

public record GuardrailResult(
    bool IsAllowed,
    string Reason,
    string? SanitizedContent = null,
    string? RuleName = null,
    double RiskScore = 0.0
);

public record SemanticSearchResult(
    ResearchReport Report,
    double SimilarityScore,
    string MatchedSnippet
);

public record McpToolDefinition(
    string Name,
    string Description,
    Dictionary<string, object>? InputSchema = null
);

public record McpToolCall(
    string ToolName,
    Dictionary<string, object> Arguments
);

public record McpToolResult(
    bool IsSuccess,
    string Output,
    string? ErrorMessage = null
);
