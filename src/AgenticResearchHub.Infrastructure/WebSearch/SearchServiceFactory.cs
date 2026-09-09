using AgenticResearchHub.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticResearchHub.Infrastructure.WebSearch;

public static class SearchServiceFactory
{
    public static IWebSearchService GetService(IServiceProvider serviceProvider, string? preferredEngine = null)
    {
        var normalized = preferredEngine?.ToLowerInvariant() ?? "duckduckgo";

        return normalized switch
        {
            "bing" => serviceProvider.GetRequiredService<BingSearchService>(),
            "duckduckgo" or _ => serviceProvider.GetRequiredService<DuckDuckGoSearchService>()
        };
    }
}
