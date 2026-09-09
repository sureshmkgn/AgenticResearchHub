namespace AgenticResearchHub.Core.Models;

public enum AgentRole
{
    Supervisor,
    Researcher,
    FactChecker,
    Synthesizer,
    Guardrail,
    System
}

public enum StepStatus
{
    Started,
    InProgress,
    Completed,
    Failed,
    Warning
}
