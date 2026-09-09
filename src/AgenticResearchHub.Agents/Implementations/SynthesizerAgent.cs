using System.Text.Json;
using AgenticResearchHub.Agents.A2A;
using AgenticResearchHub.Core.Cost;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using AgenticResearchHub.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Agents.Implementations;

public class SynthesizerAgent : IAgent
{
    // Co-located Prompt Definition owned exclusively by SynthesizerAgent
    public static readonly PromptTemplate DefaultPrompt = new(
        Name: "SynthesizerReportWriter",
        Version: "1.0.0",
        Description: "Compiles verified claims, strategic recommendations, and citations into executive markdown.",
        RequiredVariables: ["VerifiedEvidence"],
        SystemPromptTemplate: @"You are a Principal Technical Writer and Executive Intelligence Synthesizer.
Produce a comprehensive, beautifully structured research report in Markdown.
Structure your report with:
# [Clear, Professional Title]
## Executive Summary
## Key Findings (bulleted with bold highlights)
## Deep Dive Technical Analysis
## Practical Architecture & Recommendations
## Conclusion

Write with high clarity and depth. Avoid conversational filler.",
        UserPromptTemplate: "Verified Evidence Context:\n{{VerifiedEvidence}}"
    );

    private readonly ILlmGateway _llmGateway;
    private readonly PromptTemplateEngine _templateEngine;
    private readonly IPromptRegistry? _promptRegistry;
    private readonly ILogger<SynthesizerAgent> _logger;

    public AgentRole Role => AgentRole.Synthesizer;
    public string Name => "Executive Report Synthesizer";
    public string Description => "Compiles peer-reviewed findings, citations, and strategic takeaways into a polished markdown report.";

    public SynthesizerAgent(
        ILlmGateway llmGateway,
        PromptTemplateEngine templateEngine,
        ILogger<SynthesizerAgent> logger,
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
        using var activity = AgentDiagnostics.ActivitySource.StartActivity("SynthesizerAgent.ProcessMessage");
        AgentDiagnostics.AgentStepCounter.Add(1);

        var verifiedContent = incomingMessage.Content;

        var template = _promptRegistry != null ? _promptRegistry.Get(DefaultPrompt.Name, "latest") : DefaultPrompt;

        var variables = new Dictionary<string, object?>
        {
            ["VerifiedEvidence"] = verifiedContent
        };

        var (systemPrompt, userPrompt) = _templateEngine.RenderPrompts(template, variables);

        var finalMarkdown = await _llmGateway.GenerateCompletionAsync(systemPrompt, userPrompt, temperature: 0.6, maxTokens: 3000, cancellationToken: cancellationToken);

        var (inTok, outTok) = TokenCostCalculator.EstimateTokenCount(systemPrompt + userPrompt, finalMarkdown);
        var costRecord = TokenCostCalculator.Calculate("gpt-4o", inTok, outTok);

        var metadata = new Dictionary<string, object>(incomingMessage.Metadata ?? [])
        {
            ["Cost"] = costRecord,
            ["PromptTemplate"] = $"{template.Name} v{template.Version}"
        };

        return new AgentMessage(
            MessageId: Guid.NewGuid().ToString(),
            Sender: Role,
            Recipient: AgentRole.System,
            Content: finalMarkdown,
            TimestampUtc: DateTime.UtcNow,
            Metadata: metadata
        );
    }
}
