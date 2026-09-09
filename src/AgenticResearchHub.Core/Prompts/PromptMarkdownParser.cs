using System.Text.RegularExpressions;

namespace AgenticResearchHub.Core.Prompts;

/// <summary>
/// Parser for .prompt.md files containing YAML frontmatter and Markdown prompt sections.
/// </summary>
public static class PromptMarkdownParser
{
    private static readonly Regex FrontmatterRegex = new(
        @"^---\r?\n(?<frontmatter>.*?)\r?\n---\r?\n(?<body>.*)$",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex KeyValueRegex = new(
        @"^(?<key>[a-zA-Z0-9_]+)\s*:\s*(?<value>.*)$",
        RegexOptions.Compiled);

    public static PromptTemplate Parse(string markdownContent, string fallbackName = "UnnamedPrompt")
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            throw new ArgumentException("Prompt markdown content cannot be empty.", nameof(markdownContent));
        }

        var match = FrontmatterRegex.Match(markdownContent.Trim());
        if (!match.Success)
        {
            // If no frontmatter, treat whole content as system prompt
            return new PromptTemplate(
                Name: fallbackName,
                Version: "1.0.0",
                SystemPromptTemplate: markdownContent.Trim(),
                UserPromptTemplate: string.Empty,
                RequiredVariables: Array.Empty<string>(),
                Description: string.Empty
            );
        }

        var frontmatterText = match.Groups["frontmatter"].Value;
        var bodyText = match.Groups["body"].Value;

        var name = fallbackName;
        var version = "1.0.0";
        var description = string.Empty;
        var requiredVariables = new List<string>();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var lines = frontmatterText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool parsingList = false;
        string currentListKey = string.Empty;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            if (line.StartsWith("- ") && parsingList)
            {
                var item = line[2..].Trim().Trim('"', '\'');
                if (currentListKey.Equals("requiredVariables", StringComparison.OrdinalIgnoreCase))
                {
                    requiredVariables.Add(item);
                }
                continue;
            }

            var kvMatch = KeyValueRegex.Match(line);
            if (kvMatch.Success)
            {
                var key = kvMatch.Groups["key"].Value.Trim();
                var val = kvMatch.Groups["value"].Value.Trim().Trim('"', '\'');

                if (string.IsNullOrEmpty(val))
                {
                    parsingList = true;
                    currentListKey = key;
                    continue;
                }

                parsingList = false;
                switch (key.ToLowerInvariant())
                {
                    case "name":
                        name = val;
                        break;
                    case "version":
                        version = val;
                        break;
                    case "description":
                        description = val;
                        break;
                    case "requiredvariables":
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            var parts = val.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            requiredVariables.AddRange(parts.Select(p => p.Trim('"', '\'')));
                        }
                        break;
                    default:
                        metadata[key] = val;
                        break;
                }
            }
        }

        // Parse Body: Split by Markdown Headers (# System Prompt, # User Prompt)
        var (systemPrompt, userPrompt) = ExtractPromptSections(bodyText);

        return new PromptTemplate(
            Name: name,
            Version: version,
            SystemPromptTemplate: systemPrompt,
            UserPromptTemplate: userPrompt,
            RequiredVariables: requiredVariables,
            Description: description,
            Metadata: metadata.Count > 0 ? metadata : null
        );
    }

    private static (string SystemPrompt, string UserPrompt) ExtractPromptSections(string body)
    {
        var systemPattern = @"#+\s*System\s*(?:Prompt)?\s*\r?\n(?<system>.*?)(?=#+\s*User\s*(?:Prompt)?|$)";
        var userPattern = @"#+\s*User\s*(?:Prompt)?\s*\r?\n(?<user>.*?)(?=#+\s*System\s*(?:Prompt)?|$)";

        var systemMatch = Regex.Match(body, systemPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var userMatch = Regex.Match(body, userPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var system = systemMatch.Success ? systemMatch.Groups["system"].Value.Trim() : body.Trim();
        var user = userMatch.Success ? userMatch.Groups["user"].Value.Trim() : string.Empty;

        return (system, user);
    }
}
