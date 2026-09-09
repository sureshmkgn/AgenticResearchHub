---
name: mcp-tool-integrations
description: Model Context Protocol (MCP) tool discovery, JSON-RPC 2.0 communication, and external web search integration in C# .NET 9.
---

# Model Context Protocol (MCP) & Tools Skill

Use this skill when integrating MCP tool servers or adding custom search plugins.

## Protocol Implementation
- **Tool Listing**: `tools/list` returns available functions and parameter schemas.
- **Tool Calling**: `tools/call` executes the target tool with JSON argument payloads.

## Code Blueprint
```csharp
var rpcRequest = new
{
    jsonrpc = "2.0",
    id = Guid.NewGuid().ToString(),
    method = "tools/call",
    @params = new
    {
        name = "web_search",
        arguments = new { query = "Microsoft Semantic Kernel 1.38", limit = 5 }
    }
};
var response = await _httpClient.PostAsJsonAsync(mcpServerUrl, rpcRequest, cancellationToken);
```
