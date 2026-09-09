using System.Text.Json;
using AgenticResearchHub.Agents.A2A;
using AgenticResearchHub.Core.Cost;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using AgenticResearchHub.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Agents.Implementations;

public class FactCheckerAgent : IAgent
{
    // Co-located Prompt Definition owned exclusively by FactCheckerAgent
    public static readonly PromptTemplate DefaultPrompt = new(
        Name: "FactCheckerReview",
        Version: "1.0.0",
        Description: "Rigorous peer-review, hallucination mitigation, claim validation, and confidence scoring.",
        RequiredVariables: ["RawFindings"],
        SystemPromptTemplate: @"You are a strict, objective AI Fact-Checker and Peer Review Critic.
Verify that the research claims are credible, logically coherent, well-attributed, and free from speculation or hallucinated facts.
Provide a concise verification report including:
- Verified Core Claims
- Identified Gaps or Nuances
- Confidence Score (0-100%)",
        UserPromptTemplate: "Raw Findings to Verify:\n{{RawFindings}}"
    );

    private readonly ILlmGateway _llmGateway;
    private readonly PromptTemplateEngine _templateEngine;
    private readonly IPromptRegistry? _promptRegistry;
    private readonly ILogger<FactCheckerAgent> _logger;

    public AgentRole Role => AgentRole.FactChecker;
    public string Name => "Fact-Checker & Critic";
    public string Description => "Validates raw evidence against source citations, mitigates hallucinations, and scores confidence.";

    public FactCheckerAgent(
        ILlmGateway llmGateway,
        PromptTemplateEngine templateEngine,
        ILogger<FactCheckerAgent> logger,
        IPromptRegistry? promptRegistry = null)
    {
        _llmGateway = llmGateway;
        _templateEngine = templateEngine;
        _logger = logger;
        _promptRegistry = promptRegistry;

        _promptRegistry?.Register(DefaultPrompt);
    }

    public async Task<AgentMessage> ProcessMessageAsync(AgentMessage incomingMessage, CancellationToken cancellationToken = default)
    {
        using var activity = AgentDiagnostics.ActivitySource.StartActivity("FactCheckerAgent.ProcessMessage");
        AgentDiagnostics.AgentStepCounter.Add(1);

        var rawFindings = incomingMessage.Content;

        var template = _promptRegistry != null ? _promptRegistry.Get(DefaultPrompt.Name, "latest") : DefaultPrompt;

        var variables = new Dictionary<string, object?>
        {
            ["RawFindings"] = rawFindings
        };

        var (systemPrompt, userPrompt) = _templateEngine.RenderPrompts(template, variables);

        var verifiedReport = await _llmGateway.GenerateCompletionAsync(systemPrompt, userPrompt, temperature: 0.2, cancellationToken: cancellationToken);

        var (inTok, outTok) = TokenCostCalculator.EstimateTokenCount(systemPrompt + userPrompt, verifiedReport);
        var costRecord = TokenCostCalculator.Calculate("gpt-4o", inTok, outTok);

        var metadata = new Dictionary<string, object>(incomingMessage.Metadata ?? [])
        {
            ["Cost"] = costRecord,
            ["PromptTemplate"] = $"{template.Name} v{template.Version}"
        };

        return new AgentMessage(
            MessageId: Guid.NewGuid().ToString(),
            Sender: Role,
            Recipient: AgentRole.Synthesizer,
            Content: verifiedReport,
            TimestampUtc: DateTime.UtcNow,
            Metadata: metadata
        );
    }
}
