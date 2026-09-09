using AgenticResearchHub.Core.Models;

namespace AgenticResearchHub.Core.Interfaces;

public interface IAgent
{
    AgentRole Role { get; }
    string Name { get; }
    string Description { get; }
    Task<AgentMessage> ProcessMessageAsync(AgentMessage incomingMessage, CancellationToken cancellationToken = default);
}

public interface IAgentOrchestrator
{
    IAsyncEnumerable<AgentStep> ExecuteResearchWorkflowAsync(
        ResearchRequest request,
        CancellationToken cancellationToken = default
    );

    Task<ResearchReport> GetFinalReportAsync(
        string reportId,
        CancellationToken cancellationToken = default
    );
}

public interface IA2AMessageBus
{
    Task PublishAsync(AgentMessage message, CancellationToken cancellationToken = default);
    Task<AgentMessage> SendDirectAsync(AgentRole targetRole, AgentMessage message, CancellationToken cancellationToken = default);
    IReadOnlyList<AgentMessage> GetConversationHistory();
    void ClearHistory();
    event Func<AgentMessage, Task>? OnMessagePublished;
}

public interface IGuardrailValidator
{
    string Name { get; }
    int Priority { get; }
    Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default);
    Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default);
}

public interface IWebSearchService
{
    string ProviderName { get; }
    Task<IReadOnlyList<Citation>> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default);
}

public interface ISemanticReportStore
{
    Task SaveReportAsync(ResearchReport report, CancellationToken cancellationToken = default);
    Task<ResearchReport?> GetReportByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResearchReport>> GetAllReportsAsync(int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SemanticSearchResult>> SemanticSearchAsync(string query, int limit = 5, double minSimilarity = 0.3, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}

public interface ILlmGateway
{
    Task<string> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.7,
        int maxTokens = 2048,
        CancellationToken cancellationToken = default
    );

    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default
    );
}

public interface IMcpClient
{
    Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default);
    Task<McpToolResult> CallToolAsync(McpToolCall call, CancellationToken cancellationToken = default);
}
