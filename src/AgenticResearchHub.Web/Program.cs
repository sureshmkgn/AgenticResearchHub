using System.Text.Json;
using AgenticResearchHub.Agents.A2A;
using AgenticResearchHub.Agents.Implementations;
using AgenticResearchHub.Agents.Orchestration;
using AgenticResearchHub.Agents.Prompts;
using AgenticResearchHub.Core.Cost;
using AgenticResearchHub.Core.Guardrails;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using AgenticResearchHub.Core.Prompts;
using AgenticResearchHub.Infrastructure.LlmGateway;
using AgenticResearchHub.Infrastructure.Mcp;
using AgenticResearchHub.Infrastructure.VectorStore;
using AgenticResearchHub.Infrastructure.WebSearch;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuration Setup
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));

// 2. OpenTelemetry Observability (Prometheus, OTLP Collector, Jaeger Tracing)
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: TelemetryConstants.ServiceName,
        serviceVersion: TelemetryConstants.ServiceVersion
    ))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(TelemetryConstants.ActivitySourceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter(opt =>
            {
                if (Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var uri))
                {
                    opt.Endpoint = uri;
                }
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(TelemetryConstants.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter()
            .AddConsoleExporter();
    });

// 3. HTTP Clients with Resilience & Microsoft Standards
builder.Services.AddHttpClient<ILlmGateway, LlmGatewayClient>();
builder.Services.AddHttpClient<DuckDuckGoSearchService>();
builder.Services.AddHttpClient<BingSearchService>();
builder.Services.AddHttpClient<IMcpClient, McpProtocolClient>((httpClient, sp) =>
{
    var mcpUrl = builder.Configuration["WebSearch:McpServerUrl"] ?? "";
    return new McpProtocolClient(httpClient, mcpUrl, sp.GetRequiredService<ILogger<McpProtocolClient>>());
});

// Register Web Search Services
builder.Services.AddScoped<IWebSearchService>(sp =>
{
    var configProvider = builder.Configuration["WebSearch:Provider"] ?? "DuckDuckGo";
    return configProvider.ToLowerInvariant() switch
    {
        "bing" => sp.GetRequiredService<BingSearchService>(),
        "duckduckgo" or _ => sp.GetRequiredService<DuckDuckGoSearchService>()
    };
});

// 4. Semantic Report Vector Store
builder.Services.AddSingleton<ISemanticReportStore, SemanticReportStore>();

// 5. Guardrails Pipeline
builder.Services.AddSingleton<IGuardrailValidator, InputSanitizerGuardrail>();
builder.Services.AddSingleton<IGuardrailValidator, PromptInjectionGuardrail>();
builder.Services.AddSingleton<IGuardrailValidator, TopicPolicyGuardrail>();
builder.Services.AddSingleton<IGuardrailValidator, OutputFactualityGuardrail>();
builder.Services.AddSingleton<GuardrailPipeline>();

// 6. Prompt Templating Engine, Prompt Library & Hot-Reload Watcher
builder.Services.AddSingleton<IPromptRegistry, PromptRegistry>(sp =>
{
    var registry = new PromptRegistry();
    registry.RegisterAllAgentPrompts();
    return registry;
});
builder.Services.AddSingleton<PromptFileWatcher>(sp =>
{
    var registry = sp.GetRequiredService<IPromptRegistry>();
    var logger = sp.GetRequiredService<ILogger<PromptFileWatcher>>();
    var watcher = new PromptFileWatcher(registry, logger);

    // Baseline embedded templates
    watcher.LoadEmbeddedTemplates();

    // Check application base directory templates
    var baseDir = Path.Combine(AppContext.BaseDirectory, "Prompts", "Templates");
    if (Directory.Exists(baseDir))
    {
        watcher.WatchDirectory(baseDir);
    }

    // Check optional custom mounted directory
    var customDir = builder.Configuration["PromptDirectory"] ?? Environment.GetEnvironmentVariable("PROMPT_DIRECTORY");
    if (!string.IsNullOrEmpty(customDir) && Directory.Exists(customDir))
    {
        watcher.WatchDirectory(customDir);
    }

    return watcher;
});
builder.Services.AddSingleton<PromptTemplateEngine>();

// 7. Agents & A2A Collaboration Infrastructure
builder.Services.AddSingleton<IA2AMessageBus, InMemoryA2AMessageBus>();
builder.Services.AddTransient<SupervisorAgent>();
builder.Services.AddTransient<ResearcherAgent>();
builder.Services.AddTransient<FactCheckerAgent>();
builder.Services.AddTransient<SynthesizerAgent>();
builder.Services.AddTransient<IAgentOrchestrator, ResearchOrchestrator>();

// 8. Web API Setup
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 9. Initialize Hot-Reload Prompt Watcher & Embedded Baseline
_ = app.Services.GetRequiredService<PromptFileWatcher>();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// Prometheus Scraping Endpoint for Grafana / Prometheus
app.UseOpenTelemetryPrometheusScrapingEndpoint();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// -------------------------------------------------------------
// REST & SSE Stream Endpoints
// -------------------------------------------------------------

// Real-time SSE Stream for Multi-Agent Workflow
app.MapPost("/api/research/stream", async (
    [FromBody] ResearchRequest request,
    [FromServices] IAgentOrchestrator orchestrator,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    httpContext.Response.Headers.Append("Content-Type", "text/event-stream");
    httpContext.Response.Headers.Append("Cache-Control", "no-cache");
    httpContext.Response.Headers.Append("Connection", "keep-alive");

    await using var writer = new StreamWriter(httpContext.Response.Body);

    try
    {
        await foreach (var step in orchestrator.ExecuteResearchWorkflowAsync(request, cancellationToken))
        {
            var json = JsonSerializer.Serialize(step, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await writer.WriteAsync($"data: {json}\n\n");
            await writer.FlushAsync();
        }

        await writer.WriteAsync("data: [DONE]\n\n");
        await writer.FlushAsync();
    }
    catch (Exception ex)
    {
        var errorStep = new AgentStep(
            StepId: Guid.NewGuid().ToString(),
            Agent: AgentRole.System,
            Title: "Workflow Execution Error",
            Details: ex.Message,
            Status: StepStatus.Failed,
            TimestampUtc: DateTime.UtcNow
        );
        var errJson = JsonSerializer.Serialize(errorStep, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await writer.WriteAsync($"data: {errJson}\n\n");
        await writer.FlushAsync();
    }
});

// Synchronous Execution Endpoint
app.MapPost("/api/research/execute", async (
    [FromBody] ResearchRequest request,
    [FromServices] IAgentOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    ResearchReport? finalReport = null;
    await foreach (var step in orchestrator.ExecuteResearchWorkflowAsync(request, cancellationToken))
    {
        if (step.Payload is ResearchReport report)
        {
            finalReport = report;
        }
    }

    if (finalReport == null)
    {
        return Results.BadRequest(new { message = "Workflow completed without generating a report. Please verify guardrails." });
    }

    return Results.Ok(finalReport);
});

// Semantic Vector Search on Stored Reports
app.MapPost("/api/reports/search", async (
    [FromBody] SemanticSearchQuery query,
    [FromServices] ISemanticReportStore store,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(query.Query))
    {
        return Results.BadRequest(new { message = "Search query is required." });
    }

    var results = await store.SemanticSearchAsync(query.Query, query.Limit ?? 5, query.MinSimilarity ?? 0.2, cancellationToken);
    return Results.Ok(results);
});

// List All Reports
app.MapGet("/api/reports", async (
    [FromServices] ISemanticReportStore store,
    [FromQuery] int limit = 20,
    CancellationToken cancellationToken = default) =>
{
    var reports = await store.GetAllReportsAsync(limit, cancellationToken);
    return Results.Ok(reports);
});

// Get Report by ID
app.MapGet("/api/reports/{id}", async (
    string id,
    [FromServices] ISemanticReportStore store,
    CancellationToken cancellationToken) =>
{
    var report = await store.GetReportByIdAsync(id, cancellationToken);
    return report is not null ? Results.Ok(report) : Results.NotFound(new { message = $"Report '{id}' not found." });
});

// -------------------------------------------------------------
// INDEPENDENT AGENT MICROSERVICE ENDPOINTS
// -------------------------------------------------------------
app.MapPost("/api/agents/supervisor/process", async (
    [FromBody] AgentMessage message,
    [FromServices] SupervisorAgent agent,
    CancellationToken ct) =>
{
    var result = await agent.ProcessMessageAsync(message, ct);
    return Results.Ok(result);
});

app.MapPost("/api/agents/researcher/process", async (
    [FromBody] AgentMessage message,
    [FromServices] ResearcherAgent agent,
    CancellationToken ct) =>
{
    var result = await agent.ProcessMessageAsync(message, ct);
    return Results.Ok(result);
});

app.MapPost("/api/agents/factchecker/process", async (
    [FromBody] AgentMessage message,
    [FromServices] FactCheckerAgent agent,
    CancellationToken ct) =>
{
    var result = await agent.ProcessMessageAsync(message, ct);
    return Results.Ok(result);
});

app.MapPost("/api/agents/synthesizer/process", async (
    [FromBody] AgentMessage message,
    [FromServices] SynthesizerAgent agent,
    CancellationToken ct) =>
{
    var result = await agent.ProcessMessageAsync(message, ct);
    return Results.Ok(result);
});

// -------------------------------------------------------------
// PROMPT TEMPLATES & LIBRARY ENDPOINTS
// -------------------------------------------------------------
app.MapGet("/api/prompts", ([FromServices] IPromptRegistry registry) =>
{
    return Results.Ok(registry.ListAll());
});

app.MapGet("/api/prompts/{name}", (string name, [FromQuery] string? version, [FromServices] IPromptRegistry registry) =>
{
    try
    {
        return Results.Ok(registry.Get(name, version));
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { message = ex.Message });
    }
});

app.MapPost("/api/prompts", ([FromBody] PromptTemplate template, [FromServices] IPromptRegistry registry) =>
{
    registry.Register(template);
    return Results.Created($"/api/prompts/{template.Name}", template);
});

// A2A Conversation Message Bus History
app.MapGet("/api/a2a/history", ([FromServices] IA2AMessageBus messageBus) =>
{
    return Results.Ok(messageBus.GetConversationHistory());
});

// System Info & Telemetry Diagnostics
app.MapGet("/api/system/info", async ([FromServices] ISemanticReportStore store, IConfiguration config) =>
{
    var reportCount = await store.GetCountAsync();
    return Results.Ok(new
    {
        service = TelemetryConstants.ServiceName,
        version = TelemetryConstants.ServiceVersion,
        llmProvider = config["LlmGateway:Provider"] ?? "OpenAI",
        llmModel = config["LlmGateway:ModelId"] ?? "gpt-4o",
        searchEngine = config["WebSearch:Provider"] ?? "DuckDuckGo",
        storedReportsCount = reportCount,
        observability = new
        {
            prometheus = "/metrics",
            otlpEndpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317",
            tracing = "Jaeger / OpenTelemetry ActivitySource"
        },
        guardrails = new[]
        {
            "InputSanitizerGuardrail (Control Char Shield)",
            "PromptInjectionGuardrail (Jailbreak Detection)",
            "TopicPolicyGuardrail (Enterprise Scope)",
            "OutputFactualityGuardrail (Completeness & Safety)"
        },
        agents = new[]
        {
            new { role = "Supervisor", name = "Research Supervisor & Planner", endpoint = "/api/agents/supervisor/process" },
            new { role = "Researcher", name = "Autonomous Web Researcher", endpoint = "/api/agents/researcher/process" },
            new { role = "FactChecker", name = "Fact-Checker & Critic", endpoint = "/api/agents/factchecker/process" },
            new { role = "Synthesizer", name = "Executive Report Synthesizer", endpoint = "/api/agents/synthesizer/process" }
        }
    });
});

app.Run();

public record SemanticSearchQuery(string Query, int? Limit = 5, double? MinSimilarity = 0.2);
