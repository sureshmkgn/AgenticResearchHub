using AgenticResearchHub.Core.Models;
using AgenticResearchHub.Infrastructure.LlmGateway;
using AgenticResearchHub.Infrastructure.VectorStore;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AgenticResearchHub.Tests;

public class VectorSearchTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_Should_Equal_One()
    {
        float[] vectorA = [1.0f, 0.5f, 0.2f, -0.1f];
        float[] vectorB = [1.0f, 0.5f, 0.2f, -0.1f];

        var sim = CosineSimilarity.Calculate(vectorA, vectorB);
        sim.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_Should_Equal_Zero()
    {
        float[] vectorA = [1.0f, 0.0f, 0.0f];
        float[] vectorB = [0.0f, 1.0f, 0.0f];

        var sim = CosineSimilarity.Calculate(vectorA, vectorB);
        sim.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void DeterministicEmbeddingGenerator_Should_Produce_Normalized_Vectors()
    {
        var text = "Microsoft Semantic Kernel Multi-Agent Research System";
        var embedding = LlmGatewayClient.GenerateDeterministicEmbedding(text, 384);

        embedding.Length.Should().Be(384);

        float sumSquares = 0f;
        foreach (var val in embedding)
        {
            sumSquares += val * val;
        }

        MathF.Sqrt(sumSquares).Should().BeApproximately(1.0f, 0.01f);
    }

    [Fact]
    public async Task SemanticReportStore_Should_Index_And_Retrieve_Relevant_Reports()
    {
        var mockLlm = new Mock<Core.Interfaces.ILlmGateway>();
        mockLlm.Setup(l => l.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync((string text, CancellationToken _) => LlmGatewayClient.GenerateDeterministicEmbedding(text, 384));

        var store = new SemanticReportStore(mockLlm.Object, NullLogger<SemanticReportStore>.Instance);

        var report1 = new ResearchReport
        {
            Id = "rep-1",
            Topic = "Quantum Computing Algorithms",
            Title = "Quantum Algorithms & Qubits",
            ExecutiveSummary = "Research on superconducting qubits, quantum annealing, and fault-tolerant computing."
        };

        var report2 = new ResearchReport
        {
            Id = "rep-2",
            Topic = "Kubernetes Microservices Architecture",
            Title = "Container Orchestration with K8s",
            ExecutiveSummary = "Best practices for deploying Docker containers, ingress controllers, and service meshes."
        };

        await store.SaveReportAsync(report1);
        await store.SaveReportAsync(report2);

        var count = await store.GetCountAsync();
        count.Should().Be(2);

        // Search for quantum related topic
        var results = await store.SemanticSearchAsync("quantum superposition computing qubits", limit: 5, minSimilarity: 0.1);

        results.Should().NotBeEmpty();
        results.First().Report.Id.Should().Be("rep-1");
        results.First().SimilarityScore.Should().BeGreaterThan(0.2);
    }
}
