---
name: guardrails-and-security
description: Enterprise security guardrailing, prompt injection detection, input sanitization, and output moderation in C# .NET 9.
---

# Enterprise Guardrails & Security Skill

Use this skill when designing safety filters, jailbreak shields, and input/output validation pipelines.

## Guardrail Pipeline Architecture

```
User Input ──► [InputSanitizer] ──► [PromptInjectionShield] ──► [TopicPolicy] ──► LLM / Agents
                                                                                       │
Output Display ◄── [OutputFactualityGuardrail] ◄── [SystemPromptLeakShield] ◄──────────┘
```

## Validation Checklist

### 1. Input Sanitization (`InputSanitizerGuardrail`)
- Clean non-printable ASCII/Unicode control characters (`\u0000` to `\u001F`).
- Enforce length limits (e.g. 4,000 characters).
- Reject empty or whitespace-only inputs.

### 2. Prompt Injection & Jailbreak Defense (`PromptInjectionGuardrail`)
- Match against systemic attack vectors:
  - System instruction bypass: `ignore all previous instructions`, `disregard prior rules`.
  - Role hijacking: `you are now in god mode`, `DAN persona`.
  - System prompt extraction: `reveal your system prompt/keys`.
- Record security violations to OpenTelemetry metric `agentic.guardrail.violations.total`.

### 3. Output Moderation (`OutputFactualityGuardrail`)
- Validate non-empty and minimum content length.
- Ensure no accidental exposure of internal system prompts or API keys in model output.
