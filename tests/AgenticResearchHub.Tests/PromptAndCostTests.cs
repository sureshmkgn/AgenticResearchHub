using AgenticResearchHub.Core.Cost;
using AgenticResearchHub.Core.Prompts;
using FluentAssertions;
using Xunit;

namespace AgenticResearchHub.Tests;

public class PromptAndCostTests
{
    [Fact]
    public void PromptTemplateEngine_Should_Render_Variables_Correctly()
    {
        var engine = new PromptTemplateEngine();
        var template = new PromptTemplate(
            Name: "TestPrompt",
            Version: "1.0.0",
            Description: "Test template",
            RequiredVariables: ["Topic", "Context"],
            SystemPromptTemplate: "You are researching {{Topic}}.",
            UserPromptTemplate: "Context: {{Context}}"
        );

        var vars = new Dictionary<string, object?>
        {
            ["Topic"] = "Semantic Kernel",
            ["Context"] = "High Performance"
        };

        var (sys, usr) = engine.RenderPrompts(template, vars);

        sys.Should().Be("You are researching Semantic Kernel.");
        usr.Should().Be("Context: High Performance");
    }

    [Fact]
    public void PromptRegistry_Should_Retrieve_Registered_Prompts()
    {
        var registry = new PromptRegistry();
        AgenticResearchHub.Agents.Prompts.AgentPromptRegistrar.RegisterAllAgentPrompts(registry);

        var supervisorTemplate = registry.Get("SupervisorPlanner");

        supervisorTemplate.Should().NotBeNull();
        supervisorTemplate.Name.Should().Be("SupervisorPlanner");
        supervisorTemplate.RequiredVariables.Should().Contain("Topic");
    }

    [Fact]
    public void TokenCostCalculator_Should_Accurately_Calculate_USD()
    {
        int inputTokens = 1000;
        int outputTokens = 500;

        var usage = TokenCostCalculator.Calculate("gpt-4o", inputTokens, outputTokens);

        usage.TotalTokens.Should().Be(1500);
        usage.EstimatedCostUsd.Should().BeGreaterThan(0m);
        // gpt-4o: $2.50 / 1M input ($0.0025) + $10.00 / 1M output ($0.005) = $0.0075
        usage.EstimatedCostUsd.Should().Be(0.0075m);
    }
}
