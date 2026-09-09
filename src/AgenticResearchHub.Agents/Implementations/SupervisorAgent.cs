using System.Text.Json;
using AgenticResearchHub.Agents.A2A;
using AgenticResearchHub.Core.Cost;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using AgenticResearchHub.Core.Prompts;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Agents.Implementations;

public class SupervisorAgent : IAgent
{
    // Co-located Prompt Definition owned exclusively by SupervisorAgent
    public static readonly PromptTemplate DefaultPrompt = new(
        Name: "SupervisorPlanner",
        Version: "1.0.0",
        Description: "Deconstructs research topic into structured investigative sub-queries and execution phases.",
        RequiredVariables: ["Topic"],
        SystemPromptTemplate: @"You are the Chief Research Supervisor in an enterprise AI system.
Your mission:
1. Analyze the research topic: {{Topic}}.
2. Deconstruct it into 3-4 targeted, high-impact sub-queries for web intelligence.
3. Formulate a 4-phase research execution plan.

Format your response:
Sub-Queries:
- Query 1: ...
- Query 2: ...
- Query 3: ...
Plan:
1. Plan step...
2. Plan step...",
        UserPromptTemplate: "Research Topic: \"{{Topic}}\"\nContext: {{UserContext}}"
    );

    private readonly ILlmGateway _llmGateway;
    private readonly PromptTemplateEngine _templateEngine;
    private readonly IPromptRegistry? _promptRegistry;
    private readonly ILogger<SupervisorAgent> _logger;

    public AgentRole Role => AgentRole.Supervisor;
    public string Name => "Research Supervisor & Planner";
    public string Description => "Deconstructs complex research inquiries into structured sub-hypotheses and strategic execution plans.";

    public SupervisorAgent(
        ILlmGateway llmGateway,
        PromptTemplateEngine templateEngine,
        ILogger<SupervisorAgent> logger,
        IPromptRegistry? promptRegistry = null)
    {
        _llmGateway = llmGateway;
        _templateEngine = templateEngine;
        _logger = logger;
        _promptRegistry = promptRegistry;

        // Auto-register owned prompt if registry is available
        _promptRegistry?.Register(DefaultPrompt);
    }

    public async Task<AgentMessage> ProcessMessageAsync(AgentMessage incomingMessage, CancellationToken cancellationToken = default)
    {
        using var activity = AgentDiagnostics.ActivitySource.StartActivity("SupervisorAgent.ProcessMessage");
        AgentDiagnostics.AgentStepCounter.Add(1);

        // Retrieve prompt from registry or fallback to agent-owned default
        var template = _promptRegistry != null ? _promptRegistry.Get(DefaultPrompt.Name, "latest") : DefaultPrompt;

        var variables = new Dictionary<string, object?>
        {
            ["Topic"] = incomingMessage.Content,
            ["UserContext"] = incomingMessage.Metadata?.GetValueOrDefault("Context") ?? "None"
        };

        var (systemPrompt, userPrompt) = _templateEngine.RenderPrompts(template, variables);

        var planOutput = await _llmGateway.GenerateCompletionAsync(systemPrompt, userPrompt, temperature: 0.3, cancellationToken: cancellationToken);

        var (inTok, outTok) = TokenCostCalculator.EstimateTokenCount(systemPrompt + userPrompt, planOutput);
        var costRecord = TokenCostCalculator.Calculate("gpt-4o", inTok, outTok);

        return new AgentMessage(
            MessageId: Guid.NewGuid().ToString(),
            Sender: Role,
            Recipient: AgentRole.Researcher,
            Content: planOutput,
            TimestampUtc: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>
            {
                ["Cost"] = costRecord,
                ["PromptTemplate"] = $"{template.Name} v{template.Version}"
            }
        );
    }
}
