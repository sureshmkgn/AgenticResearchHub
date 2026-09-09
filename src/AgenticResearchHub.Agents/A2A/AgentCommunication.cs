using System.Collections.Concurrent;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Agents.A2A;

public class InMemoryA2AMessageBus : IA2AMessageBus
{
    private readonly ConcurrentBag<AgentMessage> _history = new();
    private readonly ILogger<InMemoryA2AMessageBus> _logger;

    public event Func<AgentMessage, Task>? OnMessagePublished;

    public InMemoryA2AMessageBus(ILogger<InMemoryA2AMessageBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        using var activity = AgentDiagnostics.ActivitySource.StartActivity("A2A.PublishMessage");
        activity?.SetTag("a2a.sender", message.Sender.ToString());
        activity?.SetTag("a2a.recipient", message.Recipient?.ToString() ?? "Broadcast");

        _history.Add(message);
        _logger.LogInformation("[A2A Protocol] {Sender} -> {Recipient}: {ContentPreview}",
            message.Sender,
            message.Recipient?.ToString() ?? "All",
            message.Content.Length > 80 ? message.Content[..80] + "..." : message.Content);

        if (OnMessagePublished != null)
        {
            await OnMessagePublished.Invoke(message);
        }
    }

    public async Task<AgentMessage> SendDirectAsync(AgentRole targetRole, AgentMessage message, CancellationToken cancellationToken = default)
    {
        var routedMessage = message with { Recipient = targetRole };
        await PublishAsync(routedMessage, cancellationToken);
        return routedMessage;
    }

    public IReadOnlyList<AgentMessage> GetConversationHistory()
    {
        return _history.OrderBy(m => m.TimestampUtc).ToList();
    }

    public void ClearHistory()
    {
        _history.Clear();
    }
}

public class AgentBlackboard
{
    public string Topic { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public List<string> SubQueries { get; set; } = [];
    public List<Citation> DiscoveredCitations { get; set; } = [];
    public List<string> VerifiedFindings { get; set; } = [];
    public List<string> CriticNotes { get; set; } = [];
    public string DraftReportMarkdown { get; set; } = string.Empty;
    public int IterationCount { get; set; } = 1;
    public Dictionary<string, object> State { get; } = new();

    public void AddCitations(IEnumerable<Citation> citations)
    {
        foreach (var c in citations)
        {
            if (!DiscoveredCitations.Any(x => x.Url.Equals(c.Url, StringComparison.OrdinalIgnoreCase)))
            {
                var indexed = c with { Index = DiscoveredCitations.Count + 1 };
                DiscoveredCitations.Add(indexed);
            }
        }
    }
}
