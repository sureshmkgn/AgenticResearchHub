using System.ComponentModel;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using Microsoft.SemanticKernel;

namespace AgenticResearchHub.Agents.Skills;

public class WebSearchSkill
{
    private readonly IWebSearchService _searchService;

    public WebSearchSkill(IWebSearchService searchService)
    {
        _searchService = searchService;
    }

    [KernelFunction, Description("Performs a live web search for relevant articles, technical papers, and documentation.")]
    public async Task<string> SearchWebAsync(
        [Description("The query to search the web for")] string query,
        [Description("Maximum number of results to retrieve")] int maxResults = 5)
    {
        var results = await _searchService.SearchAsync(query, maxResults);
        if (results.Count == 0)
        {
            return "No web results found for query: " + query;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var item in results)
        {
            sb.AppendLine($"[{item.Index}] {item.Title}");
            sb.AppendLine($"URL: {item.Url}");
            sb.AppendLine($"Snippet: {item.Snippet}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public class CitationSkill
{
    [KernelFunction, Description("Formats citations and extracts domain authority metadata.")]
    public string FormatCitations(List<Citation> citations)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## References & Web Intelligence");
        foreach (var c in citations)
        {
            sb.AppendLine($"- **[{c.Index}]** [{c.Title}]({c.Url}) - *{c.SourceDomain}* (Relevance: {c.RelevanceScore:P0})");
        }
        return sb.ToString();
    }
}
