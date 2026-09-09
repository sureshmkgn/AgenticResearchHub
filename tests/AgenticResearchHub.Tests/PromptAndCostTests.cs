using AgenticResearchHub.Agents.Prompts;
using AgenticResearchHub.Core.Cost;
using AgenticResearchHub.Core.Prompts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
        registry.RegisterAllAgentPrompts();

        var supervisorTemplate = registry.Get("SupervisorPlanner");

        supervisorTemplate.Should().NotBeNull();
        supervisorTemplate.Name.Should().Be("SupervisorPlanner");
        supervisorTemplate.RequiredVariables.Should().Contain("Topic");
    }

    [Fact]
    public void PromptMarkdownParser_Should_Parse_Yaml_Frontmatter_And_Sections()
    {
        var markdown = @"---
name: CustomAnalyst
version: 2.1.0
description: Custom analysis prompt template
requiredVariables:
  - Subject
  - Scope
---

# System Prompt
You are a senior analyst analyzing {{Subject}}.

# User Prompt
Scope: {{Scope}}
";

        var template = PromptMarkdownParser.Parse(markdown);

        template.Name.Should().Be("CustomAnalyst");
        template.Version.Should().Be("2.1.0");
        template.Description.Should().Be("Custom analysis prompt template");
        template.RequiredVariables.Should().ContainInOrder("Subject", "Scope");
        template.SystemPromptTemplate.Should().Be("You are a senior analyst analyzing {{Subject}}.");
        template.UserPromptTemplate.Should().Be("Scope: {{Scope}}");
    }

    [Fact]
    public void PromptFileWatcher_Should_Load_Embedded_Templates()
    {
        var registry = new PromptRegistry();
        var watcher = new PromptFileWatcher(registry, NullLogger<PromptFileWatcher>.Instance);

        int count = watcher.LoadEmbeddedTemplates();

        count.Should().BeGreaterThanOrEqualTo(4);
        var supervisor = registry.Get("SupervisorPlanner");
        supervisor.Should().NotBeNull();
        supervisor.SystemPromptTemplate.Should().Contain("Chief Research Supervisor");
    }

    [Fact]
    public void PromptFileWatcher_Should_HotReload_On_File_Write()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "agent_prompts_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var registry = new PromptRegistry();
            using var watcher = new PromptFileWatcher(registry, NullLogger<PromptFileWatcher>.Instance);
            watcher.WatchDirectory(tempDir);

            // Write a new prompt file to the watched directory
            var filePath = Path.Combine(tempDir, "DynamicTester.prompt.md");
            var content = @"---
name: DynamicTester
version: 1.0.0
description: Hot-reload test
requiredVariables:
  - InputData
---

# System Prompt
Test system prompt for {{InputData}}.

# User Prompt
Data: {{InputData}}
";
            File.WriteAllText(filePath, content);

            // Give file watcher a brief moment to process event
            Thread.Sleep(200);

            var template = registry.Get("DynamicTester");
            template.Should().NotBeNull();
            template.Name.Should().Be("DynamicTester");
            template.SystemPromptTemplate.Should().Contain("Test system prompt for {{InputData}}.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
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
