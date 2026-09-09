using AgenticResearchHub.Agents.A2A;
using AgenticResearchHub.Agents.Implementations;
using AgenticResearchHub.Agents.Orchestration;
using AgenticResearchHub.Core.Guardrails;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Prompts;
using AgenticResearchHub.Infrastructure.LlmGateway;
using AgenticResearchHub.Infrastructure.VectorStore;
using AgenticResearchHub.Infrastructure.WebSearch;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AgenticResearchHub.Tests;

public class A2AMessageBusTests
{
    [Fact]
    public async Task MessageBus_Publish_Should_Broadcast_And_Record_History()
    {
        var bus = new InMemoryA2AMessageBus(NullLogger<InMemoryA2AMessageBus>.Instance);
        AgentMessage? receivedMessage = null;

        bus.OnMessagePublished += msg =>
        {
            receivedMessage = msg;
            return Task.CompletedTask;
        };

        var message = new AgentMessage(
            MessageId: Guid.NewGuid().ToString(),
            Sender: AgentRole.Supervisor,
            Recipient: AgentRole.Researcher,
            Content: "Investigate Semantic Kernel plugins",
            TimestampUtc: DateTime.UtcNow
        );

        await bus.PublishAsync(message);

        receivedMessage.Should().NotBeNull();
        receivedMessage!.Content.Should().Be("Investigate Semantic Kernel plugins");

        var history = bus.GetConversationHistory();
        history.Should().HaveCount(1);
    }
}

public class OrchestrationTests
{
    [Fact]
    public async Task ResearchOrchestrator_Should_Execute_Full_Workflow()
    {
        var mockLlm = new Mock<ILlmGateway>();
        mockLlm.Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), default))
            .ReturnsAsync("# Title: Enterprise AI\n\n## Executive Summary\nExecutive summary details.\n\n## Key Findings\n- Finding 1\n- Finding 2\n\n## Analysis\nDeep content.");

        mockLlm.Setup(l => l.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync((string text, CancellationToken _) => LlmGatewayClient.GenerateDeterministicEmbedding(text, 384));

        var mockSearch = new Mock<IWebSearchService>();
        mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), default))
            .ReturnsAsync([
                new Citation(1, "Semantic Kernel Docs", "https://learn.microsoft.com/sk", "Official SK documentation", "learn.microsoft.com", 0.95)
            ]);

        var bus = new InMemoryA2AMessageBus(NullLogger<InMemoryA2AMessageBus>.Instance);
        var store = new SemanticReportStore(mockLlm.Object, NullLogger<SemanticReportStore>.Instance);
        var guardrailPipeline = new GuardrailPipeline([new InputSanitizerGuardrail(), new OutputFactualityGuardrail()], NullLogger<GuardrailPipeline>.Instance);

        var promptRegistry = new PromptRegistry();
        var templateEngine = new PromptTemplateEngine();

        var supervisor = new SupervisorAgent(mockLlm.Object, templateEngine, NullLogger<SupervisorAgent>.Instance, promptRegistry);
        var researcher = new ResearcherAgent(mockLlm.Object, mockSearch.Object, templateEngine, NullLogger<ResearcherAgent>.Instance, promptRegistry);
        var critic = new FactCheckerAgent(mockLlm.Object, templateEngine, NullLogger<FactCheckerAgent>.Instance, promptRegistry);
        var synthesizer = new SynthesizerAgent(mockLlm.Object, templateEngine, NullLogger<SynthesizerAgent>.Instance, promptRegistry);

        var orchestrator = new ResearchOrchestrator(
            supervisor,
            researcher,
            critic,
            synthesizer,
            bus,
            guardrailPipeline,
            store,
            NullLogger<ResearchOrchestrator>.Instance
        );

        var request = new ResearchRequest(
            Topic: "Enterprise Agentic Workflows in C#",
            SearchEngine: "DuckDuckGo",
            EnableGuardrails: true
        );

        var steps = new List<AgentStep>();
        await foreach (var step in orchestrator.ExecuteResearchWorkflowAsync(request))
        {
            steps.Add(step);
        }

        steps.Should().NotBeEmpty();
        steps.Should().Contain(s => s.Agent == AgentRole.Guardrail);
        steps.Should().Contain(s => s.Agent == AgentRole.Supervisor);
        steps.Should().Contain(s => s.Agent == AgentRole.Researcher);
        steps.Should().Contain(s => s.Agent == AgentRole.FactChecker);
        steps.Should().Contain(s => s.Agent == AgentRole.Synthesizer && s.Status == StepStatus.Completed);

        // Verify report was indexed in store
        var storedCount = await store.GetCountAsync();
        storedCount.Should().Be(1);
    }
}
