using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AgenticResearchHub.Agents.A2A;
using AgenticResearchHub.Agents.Implementations;
using AgenticResearchHub.Core.Guardrails;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Agents.Orchestration;

public class ResearchOrchestrator : IAgentOrchestrator
{
    private readonly SupervisorAgent _supervisorAgent;
    private readonly ResearcherAgent _researcherAgent;
    private readonly FactCheckerAgent _factCheckerAgent;
    private readonly SynthesizerAgent _synthesizerAgent;
    private readonly IA2AMessageBus _messageBus;
    private readonly GuardrailPipeline _guardrailPipeline;
    private readonly ISemanticReportStore _reportStore;
    private readonly ILogger<ResearchOrchestrator> _logger;

    public ResearchOrchestrator(
        SupervisorAgent supervisorAgent,
        ResearcherAgent researcherAgent,
        FactCheckerAgent factCheckerAgent,
        SynthesizerAgent synthesizerAgent,
        IA2AMessageBus messageBus,
        GuardrailPipeline guardrailPipeline,
        ISemanticReportStore reportStore,
        ILogger<ResearchOrchestrator> logger)
    {
        _supervisorAgent = supervisorAgent;
        _researcherAgent = researcherAgent;
        _factCheckerAgent = factCheckerAgent;
        _synthesizerAgent = synthesizerAgent;
        _messageBus = messageBus;
        _guardrailPipeline = guardrailPipeline;
        _reportStore = reportStore;
        _logger = logger;
    }

    public async IAsyncEnumerable<AgentStep> ExecuteResearchWorkflowAsync(
        ResearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var workflowActivity = AgentDiagnostics.ActivitySource.StartActivity("ResearchWorkflow.Execute");
        workflowActivity?.SetTag("research.topic", request.Topic);
        AgentDiagnostics.ResearchRequestsCounter.Add(1);

        var stopwatch = Stopwatch.StartNew();
        var blackboard = new AgentBlackboard { Topic = request.Topic };

        // 1. Guardrail Validation Phase
        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.Guardrail,
            Title: "Security & Policy Guardrail Evaluation",
            Details: "Evaluating input topic against injection shields, safety policies, and input sanitation rules...",
            Status: StepStatus.InProgress,
            TimestampUtc: DateTime.UtcNow
        );

        var guardrailResult = await _guardrailPipeline.ValidateInputPipelineAsync(request.Topic, cancellationToken);
        if (!guardrailResult.IsAllowed)
        {
            yield return new AgentStep(
                StepId: Guid.NewGuid().ToString(),
                Agent: AgentRole.Guardrail,
                Title: "Guardrail Policy Violation",
                Details: $"Request rejected: {guardrailResult.Reason}",
                Status: StepStatus.Failed,
                TimestampUtc: DateTime.UtcNow,
                Payload: guardrailResult
            );
            yield break;
        }

        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.Guardrail,
            Title: "Guardrails Verified",
            Details: "Topic is cleared. Proceeding to Agent Orchestration Pipeline.",
            Status: StepStatus.Completed,
            TimestampUtc: DateTime.UtcNow
        );

        var effectiveTopic = guardrailResult.SanitizedContent ?? request.Topic;

        // 2. Supervisor Planning Phase
        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.Supervisor,
            Title: "Supervisor: Planning & Decomposition",
            Details: "Analyzing topic semantics, decomposing into targeted investigative sub-queries...",
            Status: StepStatus.InProgress,
            TimestampUtc: DateTime.UtcNow
        );

        var supervisorInput = new AgentMessage(
            MessageId: Guid.NewGuid().ToString(),
            Sender: AgentRole.System,
            Recipient: AgentRole.Supervisor,
            Content: effectiveTopic,
            TimestampUtc: DateTime.UtcNow,
            Metadata: request.UserContext != null ? new() { ["Context"] = request.UserContext } : null
        );

        var supervisorMsg = await _supervisorAgent.ProcessMessageAsync(supervisorInput, cancellationToken);
        await _messageBus.PublishAsync(supervisorMsg, cancellationToken);
        blackboard.Plan = supervisorMsg.Content;

        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.Supervisor,
            Title: "Supervisor Plan Finalized",
            Details: supervisorMsg.Content,
            Status: StepStatus.Completed,
            TimestampUtc: DateTime.UtcNow,
            Payload: supervisorMsg.Content
        );

        // 3. Autonomous Web Researcher Phase
        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.Researcher,
            Title: "Researcher: Web Search & Citation Discovery",
            Details: $"Executing live web intelligence retrieval for topic: '{effectiveTopic}'...",
            Status: StepStatus.InProgress,
            TimestampUtc: DateTime.UtcNow
        );

        var researcherMsg = await _researcherAgent.ProcessMessageAsync(supervisorMsg, cancellationToken);
        await _messageBus.PublishAsync(researcherMsg, cancellationToken);

        if (researcherMsg.Metadata?.TryGetValue("Citations", out var rawCitations) == true && rawCitations is IEnumerable<Citation> citationsList)
        {
            blackboard.AddCitations(citationsList);
        }

        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.Researcher,
            Title: $"Retrieved {blackboard.DiscoveredCitations.Count} Authoritative Citations & Evidence",
            Details: researcherMsg.Content,
            Status: StepStatus.Completed,
            TimestampUtc: DateTime.UtcNow,
            Payload: blackboard.DiscoveredCitations
        );

        // 4. Fact-Checker & Critic Peer-Review Phase
        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.FactChecker,
            Title: "Fact-Checker: Verification & Hallucination Mitigation",
            Details: "Cross-referencing technical claims against discovered web citations...",
            Status: StepStatus.InProgress,
            TimestampUtc: DateTime.UtcNow
        );

        var factCheckerMsg = await _factCheckerAgent.ProcessMessageAsync(researcherMsg, cancellationToken);
        await _messageBus.PublishAsync(factCheckerMsg, cancellationToken);
        blackboard.VerifiedFindings.Add(factCheckerMsg.Content);

        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.FactChecker,
            Title: "Fact-Checking & Confidence Scoring Complete",
            Details: factCheckerMsg.Content,
            Status: StepStatus.Completed,
            TimestampUtc: DateTime.UtcNow,
            Payload: factCheckerMsg.Content
        );

        // 5. Synthesizer & Report Writer Phase
        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.Synthesizer,
            Title: "Synthesizer: Executive Markdown Generation",
            Details: "Formulating cohesive technical report with executive summary and structured findings...",
            Status: StepStatus.InProgress,
            TimestampUtc: DateTime.UtcNow
        );

        var synthesizerMsg = await _synthesizerAgent.ProcessMessageAsync(factCheckerMsg, cancellationToken);
        await _messageBus.PublishAsync(synthesizerMsg, cancellationToken);
        blackboard.DraftReportMarkdown = synthesizerMsg.Content;

        // 6. Output Guardrail Validation
        var outputGuardResult = await _guardrailPipeline.ValidateOutputPipelineAsync(blackboard.DraftReportMarkdown, cancellationToken);
        if (!outputGuardResult.IsAllowed)
        {
            yield return new AgentStep(
                StepId: Guid.NewGuid().ToString(),
                Agent: AgentRole.Guardrail,
                Title: "Output Safety Alert",
                Details: outputGuardResult.Reason,
                Status: StepStatus.Warning,
                TimestampUtc: DateTime.UtcNow
            );
        }

        stopwatch.Stop();
        AgentDiagnostics.ResearchDurationHistogram.Record(stopwatch.Elapsed.TotalSeconds);

        // Aggregate token & cost telemetry
        decimal totalCost = 0m;
        int totalInTokens = 0;
        int totalOutTokens = 0;

        foreach (var msg in _messageBus.GetConversationHistory())
        {
            if (msg.Metadata?.TryGetValue("Cost", out var costObj) == true && costObj is AgenticResearchHub.Core.Cost.CostUsageRecord cost)
            {
                totalCost += cost.EstimatedCostUsd;
                totalInTokens += cost.InputTokens;
                totalOutTokens += cost.OutputTokens;
            }
        }

        // Extract key findings and executive summary
        var (title, execSummary, findings) = ParseReportSections(blackboard.DraftReportMarkdown, effectiveTopic);

        var finalReport = new ResearchReport
        {
            Id = Guid.NewGuid().ToString(),
            Topic = effectiveTopic,
            Title = title,
            ExecutiveSummary = execSummary,
            FullContentMarkdown = blackboard.DraftReportMarkdown,
            KeyFindings = findings,
            Citations = blackboard.DiscoveredCitations,
            CreatedAtUtc = DateTime.UtcNow,
            TotalExecutionDuration = stopwatch.Elapsed,
            IterationsCount = blackboard.IterationCount,
            TotalInputTokens = totalInTokens,
            TotalOutputTokens = totalOutTokens,
            TotalEstimatedCostUsd = totalCost,
            Metadata = new Dictionary<string, string>
            {
                ["SearchEngine"] = request.SearchEngine ?? "DuckDuckGo",
                ["GuardrailsActive"] = request.EnableGuardrails.ToString(),
                ["A2AMessagesCount"] = _messageBus.GetConversationHistory().Count.ToString(),
                ["EstimatedCostUsd"] = $"${totalCost:F5}"
            }
        };

        // 7. Store Report in Semantic Vector Store
        await _reportStore.SaveReportAsync(finalReport, cancellationToken);

        yield return new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.Synthesizer,
            Title: "Research Report Finalized & Indexed in Semantic Store",
            Details: $"Generated comprehensive report with {finalReport.Citations.Count} citations. Cost: ${totalCost:F5} ({totalInTokens + totalOutTokens} tokens). Indexed into Vector Store for semantic search.",
            Status: StepStatus.Completed,
            TimestampUtc: DateTime.UtcNow,
            Duration: stopwatch.Elapsed,
            StepCostUsd: totalCost,
            Payload: finalReport
        );
    }

    public async Task<ResearchReport> GetFinalReportAsync(string reportId, CancellationToken cancellationToken = default)
    {
        var report = await _reportStore.GetReportByIdAsync(reportId, cancellationToken);
        if (report == null)
        {
            throw new KeyNotFoundException($"Research report with ID {reportId} was not found.");
        }
        return report;
    }

    private static (string Title, string Summary, List<string> KeyFindings) ParseReportSections(string markdown, string topic)
    {
        var titleMatch = Regex.Match(markdown, @"^#\s+(.+)$", RegexOptions.Multiline);
        var title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : $"Intelligence Report: {topic}";

        var summaryMatch = Regex.Match(markdown, @"##\s+Executive Summary\s*\n+([^#]+)", RegexOptions.Multiline);
        var summary = summaryMatch.Success ? summaryMatch.Groups[1].Value.Trim() : $"Comprehensive multi-agent investigation into {topic}.";

        var findings = new List<string>();
        var findingsMatch = Regex.Match(markdown, @"##\s+Key Findings\s*\n+([^#]+)", RegexOptions.Multiline);
        if (findingsMatch.Success)
        {
            var lines = findingsMatch.Groups[1].Value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('-') || trimmed.StartsWith('*'))
                {
                    findings.Add(trimmed.TrimStart('-', '*', ' '));
                }
            }
        }

        if (findings.Count == 0)
        {
            findings.Add($"Detailed findings on {topic} derived from multi-agent peer review.");
        }

        return (title, summary, findings);
    }
}
