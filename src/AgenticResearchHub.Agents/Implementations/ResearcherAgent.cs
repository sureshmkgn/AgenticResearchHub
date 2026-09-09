using System.Text.Json;
using AgenticResearchHub.Agents.A2A;
using AgenticResearchHub.Core.Cost;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using AgenticResearchHub.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Agents.Implementations;

public class ResearcherAgent : IAgent
{
    // Co-located Prompt Definition owned exclusively by ResearcherAgent
    public static readonly PromptTemplate DefaultPrompt = new(
        Name: "ResearcherIntelligence",
        Version: "1.0.0",
        Description: "Extracts technical insights, empirical findings, and citations from retrieved web data.",
        RequiredVariables: ["Topic", "CitationsContext"],
        SystemPromptTemplate: @"You are an autonomous Senior Web Intelligence Researcher.
Your job is to digest the retrieved search citations and extract key technical insights, empirical findings, and architectural facts.
Be precise and cite the source index [1], [2], etc.",
        UserPromptTemplate: "Topic: {{Topic}}\nRetrieved Web Evidence:\n{{CitationsContext}}"
    );

    private readonly ILlmGateway _llmGateway;
    private readonly IWebSearchService _searchService;
    private readonly PromptTemplateEngine _templateEngine;
    private readonly IPromptRegistry? _promptRegistry;
    private readonly ILogger<ResearcherAgent> _logger;

    public AgentRole Role => AgentRole.Researcher;
    public string Name => "Autonomous Web Researcher";
    public string Description => "Executes web searches, discovers authoritative citations, and extracts pertinent technical findings.";

    public ResearcherAgent(
        ILlmGateway llmGateway,
        IWebSearchService searchService,
        PromptTemplateEngine templateEngine,
        ILogger<ResearcherAgent> logger,
        IPromptRegistry? promptRegistry = null)
    {
        _llmGateway = llmGateway;
        _searchService = searchService;
        _templateEngine = templateEngine;
        _logger = logger;
        _promptRegistry = promptRegistry;

        _promptRegistry?.Register(DefaultPrompt);
    }

    public async Task<AgentMessage> ProcessMessageAsync(AgentMessage incomingMessage, CancellationToken cancellationToken = default)
    {
        using var activity = AgentDiagnostics.ActivitySource.StartActivity("ResearcherAgent.ProcessMessage");
        AgentDiagnostics.AgentStepCounter.Add(1);

        var topic = incomingMessage.Content;
        var citations = await _searchService.SearchAsync(topic, maxResults: 5, cancellationToken);
        var citationsContext = string.Join("\n\n", citations.Select(c => $"[{c.Index}] {c.Title} ({c.Url})\n{c.Snippet}"));

        var template = _promptRegistry != null ? _promptRegistry.Get(DefaultPrompt.Name, "latest") : DefaultPrompt;

        var variables = new Dictionary<string, object?>
        {
            ["Topic"] = topic,
            ["CitationsContext"] = citationsContext
        };

        var (systemPrompt, userPrompt) = _templateEngine.RenderPrompts(template, variables);

        var findings = await _llmGateway.GenerateCompletionAsync(systemPrompt, userPrompt, temperature: 0.5, cancellationToken: cancellationToken);

        var (inTok, outTok) = TokenCostCalculator.EstimateTokenCount(systemPrompt + userPrompt, findings);
        var costRecord = TokenCostCalculator.Calculate("gpt-4o", inTok, outTok);

        var metadata = new Dictionary<string, object>
        {
            ["Citations"] = citations,
            ["Cost"] = costRecord,
            ["PromptTemplate"] = $"{template.Name} v{template.Version}"
        };

        return new AgentMessage(
            MessageId: Guid.NewGuid().ToString(),
            Sender: Role,
            Recipient: AgentRole.FactChecker,
            Content: findings,
            TimestampUtc: DateTime.UtcNow,
            Metadata: metadata
        );
    }
}
