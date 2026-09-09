using System.Numerics;
using System.Text.Json;
using AgenticResearchHub.Core.Interfaces;
using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgenticResearchHub.Infrastructure.VectorStore;

public static class CosineSimilarity
{
    public static double Calculate(ReadOnlySpan<float> vectorA, ReadOnlySpan<float> vectorB)
    {
        if (vectorA.Length != vectorB.Length || vectorA.Length == 0)
        {
            return 0.0;
        }

        float dot = 0f;
        float magA = 0f;
        float magB = 0f;

        // Hardware-accelerated SIMD computation where available
        int i = 0;
        int vectorSize = Vector<float>.Count;

        while (i <= vectorA.Length - vectorSize)
        {
            var va = new Vector<float>(vectorA.Slice(i, vectorSize));
            var vb = new Vector<float>(vectorB.Slice(i, vectorSize));

            dot += Vector.Dot(va, vb);
            magA += Vector.Dot(va, va);
            magB += Vector.Dot(vb, vb);

            i += vectorSize;
        }

        // Remaining elements
        for (; i < vectorA.Length; i++)
        {
            dot += vectorA[i] * vectorB[i];
            magA += vectorA[i] * vectorA[i];
            magB += vectorB[i] * vectorB[i];
        }

        if (magA <= 0f || magB <= 0f)
        {
            return 0.0;
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}

public class SemanticReportStore : ISemanticReportStore
{
    private readonly ILlmGateway _llmGateway;
    private readonly ILogger<SemanticReportStore> _logger;
    private readonly Dictionary<string, ResearchReport> _reports = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public SemanticReportStore(ILlmGateway llmGateway, ILogger<SemanticReportStore> logger)
    {
        _llmGateway = llmGateway;
        _logger = logger;
    }

    public async Task SaveReportAsync(ResearchReport report, CancellationToken cancellationToken = default)
    {
        using var activity = AgentDiagnostics.ActivitySource.StartActivity("SemanticReportStore.SaveReport");
        activity?.SetTag("report.id", report.Id);
        activity?.SetTag("report.topic", report.Topic);

        // Generate embedding vector if not present
        if (report.EmbeddingVector == null || report.EmbeddingVector.Length == 0)
        {
            var textToEmbed = $"{report.Title} {report.Topic} {report.ExecutiveSummary} {string.Join(" ", report.KeyFindings)}";
            report.EmbeddingVector = await _llmGateway.GenerateEmbeddingAsync(textToEmbed, cancellationToken);
        }

        _lock.EnterWriteLock();
        try
        {
            _reports[report.Id] = report;
            _logger.LogInformation("Saved report {ReportId} for topic '{Topic}' to Semantic Vector Store. Total reports: {Count}",
                report.Id, report.Topic, _reports.Count);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Task<ResearchReport?> GetReportByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            _reports.TryGetValue(id, out var report);
            return Task.FromResult(report);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<IReadOnlyList<ResearchReport>> GetAllReportsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            var list = _reports.Values
                .OrderByDescending(r => r.CreatedAtUtc)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<ResearchReport>>(list);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> SemanticSearchAsync(
        string query,
        int limit = 5,
        double minSimilarity = 0.2,
        CancellationToken cancellationToken = default)
    {
        using var activity = AgentDiagnostics.ActivitySource.StartActivity("SemanticReportStore.SemanticSearch");
        activity?.SetTag("search.query", query);

        var queryVector = await _llmGateway.GenerateEmbeddingAsync(query, cancellationToken);

        List<ResearchReport> snapshot;
        _lock.EnterReadLock();
        try
        {
            snapshot = _reports.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        var results = new List<SemanticSearchResult>();

        foreach (var report in snapshot)
        {
            if (report.EmbeddingVector == null || report.EmbeddingVector.Length == 0)
            {
                continue;
            }

            var similarity = CosineSimilarity.Calculate(queryVector, report.EmbeddingVector);

            if (similarity >= minSimilarity)
            {
                var snippet = string.IsNullOrWhiteSpace(report.ExecutiveSummary)
                    ? report.Topic
                    : (report.ExecutiveSummary.Length > 250 ? report.ExecutiveSummary[..250] + "..." : report.ExecutiveSummary);

                results.Add(new SemanticSearchResult(report, similarity, snippet));
            }
        }

        var ranked = results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(limit)
            .ToList();

        _logger.LogInformation("Semantic search for '{Query}' found {Count} matching reports (top score: {TopScore:F4})",
            query, ranked.Count, ranked.FirstOrDefault()?.SimilarityScore ?? 0.0);

        return ranked;
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            return Task.FromResult(_reports.Count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
