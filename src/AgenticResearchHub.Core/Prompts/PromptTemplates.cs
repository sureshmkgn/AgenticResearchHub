using System.Text.RegularExpressions;

namespace AgenticResearchHub.Core.Prompts;

public record PromptTemplate(
    string Name,
    string Version,
    string SystemPromptTemplate,
    string UserPromptTemplate,
    IReadOnlyList<string> RequiredVariables,
    string Description = "",
    Dictionary<string, string>? Metadata = null
);

public interface IPromptRegistry
{
    void Register(PromptTemplate template);
    PromptTemplate Get(string name, string? version = null);
    IReadOnlyList<PromptTemplate> ListAll();
}

public class PromptTemplateEngine
{
    private static readonly Regex VariableRegex = new(@"\{\{([a-zA-Z0-9_\.]+)\}\}", RegexOptions.Compiled);

    public string Render(string templateText, IDictionary<string, object?> variables)
    {
        if (string.IsNullOrEmpty(templateText)) return string.Empty;

        return VariableRegex.Replace(templateText, match =>
        {
            var key = match.Groups[1].Value.Trim();
            if (variables.TryGetValue(key, out var val) && val is not null)
            {
                return val.ToString() ?? string.Empty;
            }
            return match.Value; // Keep unmatched template tag intact
        });
    }

    public (string SystemPrompt, string UserPrompt) RenderPrompts(
        PromptTemplate template,
        IDictionary<string, object?> variables)
    {
        // Validate required variables
        foreach (var req in template.RequiredVariables)
        {
            if (!variables.ContainsKey(req) || variables[req] == null)
            {
                throw new ArgumentException($"Missing required prompt variable '{req}' for template '{template.Name}' v{template.Version}");
            }
        }

        var system = Render(template.SystemPromptTemplate, variables);
        var user = Render(template.UserPromptTemplate, variables);
        return (system, user);
    }
}

public class PromptRegistry : IPromptRegistry
{
    private readonly Dictionary<string, Dictionary<string, PromptTemplate>> _registry = new(StringComparer.OrdinalIgnoreCase);

    public void Register(PromptTemplate template)
    {
        if (!_registry.TryGetValue(template.Name, out var versions))
        {
            versions = new Dictionary<string, PromptTemplate>(StringComparer.OrdinalIgnoreCase);
            _registry[template.Name] = versions;
        }
        versions[template.Version] = template;
    }

    public PromptTemplate Get(string name, string? version = null)
    {
        if (!_registry.TryGetValue(name, out var versions))
        {
            throw new KeyNotFoundException($"Prompt template '{name}' is not registered in Prompt Library.");
        }

        if (string.IsNullOrEmpty(version) || version.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            return versions.Values.OrderByDescending(v => v.Version).First();
        }

        if (versions.TryGetValue(version, out var template))
        {
            return template;
        }

        throw new KeyNotFoundException($"Version '{version}' for prompt template '{name}' was not found.");
    }

    public IReadOnlyList<PromptTemplate> ListAll()
    {
        return _registry.Values.SelectMany(v => v.Values).ToList();
    }
}
