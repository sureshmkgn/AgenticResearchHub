using System.Net.Http.Json;
using System.Text.Json;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Infrastructure.Mcp;

public class McpProtocolClient : IMcpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _mcpServerUrl;
    private readonly ILogger<McpProtocolClient> _logger;

    public McpProtocolClient(HttpClient httpClient, string mcpServerUrl, ILogger<McpProtocolClient> logger)
    {
        _httpClient = httpClient;
        _mcpServerUrl = mcpServerUrl;
        _logger = logger;
    }

    public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        using var activity = AgentDiagnostics.ActivitySource.StartActivity("McpClient.ListTools");

        if (string.IsNullOrWhiteSpace(_mcpServerUrl))
        {
            return
            [
                new McpToolDefinition("web_search", "Executes real-time web search and citation discovery.",
                    new Dictionary<string, object> { ["query"] = "string", ["limit"] = "number" }),
                new McpToolDefinition("document_fetch", "Fetches deep content from a target URL.",
                    new Dictionary<string, object> { ["url"] = "string" })
            ];
        }

        try
        {
            var rpcRequest = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/list",
                @params = new { }
            };

            var response = await _httpClient.PostAsJsonAsync(_mcpServerUrl, rpcRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var tools = json.GetProperty("result").GetProperty("tools");

            var list = new List<McpToolDefinition>();
            foreach (var t in tools.EnumerateArray())
            {
                var name = t.GetProperty("name").GetString() ?? "";
                var desc = t.GetProperty("description").GetString() ?? "";
                list.Add(new McpToolDefinition(name, desc));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch tools from external MCP server at {Url}. Using built-in MCP tool catalog.", _mcpServerUrl);
            return
            [
                new McpToolDefinition("web_search", "Executes real-time web search and citation discovery.",
                    new Dictionary<string, object> { ["query"] = "string", ["limit"] = "number" }),
                new McpToolDefinition("document_fetch", "Fetches deep content from a target URL.",
                    new Dictionary<string, object> { ["url"] = "string" })
            ];
        }
    }

    public async Task<McpToolResult> CallToolAsync(McpToolCall call, CancellationToken cancellationToken = default)
    {
        using var activity = AgentDiagnostics.ActivitySource.StartActivity("McpClient.CallTool");
        activity?.SetTag("mcp.tool_name", call.ToolName);

        if (string.IsNullOrWhiteSpace(_mcpServerUrl))
        {
            return new McpToolResult(true, $"Simulated MCP response for {call.ToolName}");
        }

        try
        {
            var rpcRequest = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/call",
                @params = new
                {
                    name = call.ToolName,
                    arguments = call.Arguments
                }
            };

            var response = await _httpClient.PostAsJsonAsync(_mcpServerUrl, rpcRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var result = json.GetProperty("result").ToString();

            return new McpToolResult(true, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool execution failed for {Tool}", call.ToolName);
            return new McpToolResult(false, string.Empty, ex.Message);
        }
    }
}
