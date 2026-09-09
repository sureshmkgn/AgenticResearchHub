using AgenticResearchHub.Agents.Implementations;
using AgenticResearchHub.Core.Prompts;

namespace AgenticResearchHub.Agents.Prompts;

public static class AgentPromptRegistrar
{
    public static void RegisterAllAgentPrompts(this IPromptRegistry registry)
    {
        registry.Register(SupervisorAgent.DefaultPrompt);
        registry.Register(ResearcherAgent.DefaultPrompt);
        registry.Register(FactCheckerAgent.DefaultPrompt);
        registry.Register(SynthesizerAgent.DefaultPrompt);
    }
}
