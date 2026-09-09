using System.Reflection;
using AgenticResearchHub.Agents.Implementations;
using AgenticResearchHub.Core.Prompts;

namespace AgenticResearchHub.Agents.Prompts;

public static class AgentPromptRegistrar
{
    /// <summary>
    /// Registers all agent prompts by first loading embedded .prompt.md markdown templates,
    /// then ensuring fallback defaults for all specialist agents.
    /// </summary>
    public static void RegisterAllAgentPrompts(this IPromptRegistry registry)
    {
        // 1. Load embedded .prompt.md templates from assembly
        LoadEmbeddedTemplates(registry);

        // 2. Ensure fallback registrations if embedded templates are absent
        EnsureFallback(registry, SupervisorAgent.DefaultPrompt);
        EnsureFallback(registry, ResearcherAgent.DefaultPrompt);
        EnsureFallback(registry, FactCheckerAgent.DefaultPrompt);
        EnsureFallback(registry, SynthesizerAgent.DefaultPrompt);
    }

    private static void LoadEmbeddedTemplates(IPromptRegistry registry)
    {
        var assembly = typeof(AgentPromptRegistrar).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".prompt.md", StringComparison.OrdinalIgnoreCase));

        foreach (var resName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resName);
                if (stream == null) continue;

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                var fallbackName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(resName));

                var template = PromptMarkdownParser.Parse(content, fallbackName);
                registry.Register(template);
            }
            catch
            {
                // Continue to ensure fallback
            }
        }
    }

    private static void EnsureFallback(IPromptRegistry registry, PromptTemplate fallback)
    {
        try
        {
            _ = registry.Get(fallback.Name, fallback.Version);
        }
        catch (KeyNotFoundException)
        {
            registry.Register(fallback);
        }
    }
}
