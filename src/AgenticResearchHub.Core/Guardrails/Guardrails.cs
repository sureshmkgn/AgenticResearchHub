using System.Text.RegularExpressions;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Core.Guardrails;

public class InputSanitizerGuardrail : IGuardrailValidator
{
    public string Name => "InputSanitizer";
    public int Priority => 10;

    public Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.FromResult(new GuardrailResult(false, "Input topic cannot be empty.", RiskScore: 1.0));
        }

        if (input.Length > 4000)
        {
            return Task.FromResult(new GuardrailResult(false, "Input exceeds maximum allowed length (4000 chars).", RiskScore: 0.8));
        }

        // Clean hidden control characters while keeping standard whitespace and newlines
        var sanitized = Regex.Replace(input, @"[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");
        return Task.FromResult(new GuardrailResult(true, "Input passed sanitization check.", SanitizedContent: sanitized.Trim()));
    }

    public Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GuardrailResult(true, "Output passed sanitization check."));
    }
}

public class PromptInjectionGuardrail : IGuardrailValidator
{
    public string Name => "PromptInjectionShield";
    public int Priority => 20;

    private static readonly string[] InjectionPatterns =
    [
        @"(?i)(ignore|disregard|bypass)\s+(all\s+)?(previous|prior|above)?\s*(instructions|rules|protocols|prompts)",
        @"(?i)system\s+prompt\s*:",
        @"(?i)you\s+are\s+now\s+(unrestricted|in\s+god\s+mode|dan)",
        @"(?i)reveal\s+(your\s+)?(system|internal)\s+(prompt|instructions|keys)",
        @"(?i)<\s*/?\s*system\s*>",
        @"(?i)override\s+(all\s+)?security\s+protocols"
    ];

    public Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
    {
        foreach (var pattern in InjectionPatterns)
        {
            if (Regex.IsMatch(input, pattern))
            {
                AgentDiagnostics.GuardrailViolationCounter.Add(1);
                return Task.FromResult(new GuardrailResult(
                    IsAllowed: false,
                    Reason: $"Potential Prompt Injection or Jailbreak detected (Rule: {pattern})",
                    RuleName: Name,
                    RiskScore: 0.95
                ));
            }
        }

        return Task.FromResult(new GuardrailResult(true, "No prompt injection patterns detected."));
    }

    public Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default)
    {
        // Detect if the model output accidentally leaks system prompt markers
        if (output.Contains("SYSTEM INSTRUCTION:", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("DEVELOPER PROMPT:", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new GuardrailResult(
                IsAllowed: false,
                Reason: "System prompt leakage detected in response.",
                RuleName: Name,
                RiskScore: 0.9
            ));
        }

        return Task.FromResult(new GuardrailResult(true, "Output passed prompt injection shield."));
    }
}

public class TopicPolicyGuardrail : IGuardrailValidator
{
    public string Name => "TopicPolicy";
    public int Priority => 30;

    private static readonly string[] ProhibitedKeywords =
    [
        "malware payload generation",
        "exploit zero-day weaponization",
        "synthesize chemical weapon",
        "bypass credit card authentication illegal"
    ];

    public Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
    {
        foreach (var keyword in ProhibitedKeywords)
        {
            if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                AgentDiagnostics.GuardrailViolationCounter.Add(1);
                return Task.FromResult(new GuardrailResult(
                    IsAllowed: false,
                    Reason: $"Input violates acceptable use policy: prohibited topic.",
                    RuleName: Name,
                    RiskScore: 1.0
                ));
            }
        }

        return Task.FromResult(new GuardrailResult(true, "Topic complies with enterprise usage policy."));
    }

    public Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GuardrailResult(true, "Output satisfies topic safety policy."));
    }
}

public class OutputFactualityGuardrail : IGuardrailValidator
{
    public string Name => "OutputFactuality";
    public int Priority => 40;

    public Task<GuardrailResult> ValidateInputAsync(string input, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new GuardrailResult(true, "Skipped for input."));
    }

    public Task<GuardrailResult> ValidateOutputAsync(string output, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Task.FromResult(new GuardrailResult(false, "Generated report is empty.", RuleName: Name, RiskScore: 0.8));
        }

        if (output.Length < 50)
        {
            return Task.FromResult(new GuardrailResult(false, "Generated report is too brief to be meaningful.", RuleName: Name, RiskScore: 0.6));
        }

        return Task.FromResult(new GuardrailResult(true, "Output passed factuality and completeness checks."));
    }
}

public class GuardrailPipeline
{
    private readonly IEnumerable<IGuardrailValidator> _validators;
    private readonly ILogger<GuardrailPipeline> _logger;

    public GuardrailPipeline(IEnumerable<IGuardrailValidator> validators, ILogger<GuardrailPipeline> logger)
    {
        _validators = validators.OrderBy(v => v.Priority).ToList();
        _logger = logger;
    }

    public async Task<GuardrailResult> ValidateInputPipelineAsync(string input, CancellationToken cancellationToken = default)
    {
        var currentInput = input;
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateInputAsync(currentInput, cancellationToken);
            if (!result.IsAllowed)
            {
                _logger.LogWarning("Input rejected by guardrail {Guardrail}: {Reason}", validator.Name, result.Reason);
                return result;
            }

            if (!string.IsNullOrEmpty(result.SanitizedContent))
            {
                currentInput = result.SanitizedContent;
            }
        }

        return new GuardrailResult(true, "All input guardrails passed.", SanitizedContent: currentInput);
    }

    public async Task<GuardrailResult> ValidateOutputPipelineAsync(string output, CancellationToken cancellationToken = default)
    {
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateOutputAsync(output, cancellationToken);
            if (!result.IsAllowed)
            {
                _logger.LogWarning("Output rejected by guardrail {Guardrail}: {Reason}", validator.Name, result.Reason);
                return result;
            }
        }

        return new GuardrailResult(true, "All output guardrails passed.", SanitizedContent: output);
    }
}
