namespace AgenticResearchHub.Infrastructure.LlmGateway;

public class LlmOptions
{
    public const string SectionName = "LlmGateway";

    public string Provider { get; set; } = "OpenAI"; // OpenAI, AzureOpenAI, LiteLLM, Ollama, LocalFallback
    public string Endpoint { get; set; } = "https://api.openai.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "gpt-4o";
    public string EmbeddingModelId { get; set; } = "text-embedding-3-small";
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 60;
    public double Temperature { get; set; } = 0.7;
    public bool UseOfflineMockFallbackIfNoApiKey { get; set; } = true;
}
