---
name: semantic-vector-search
description: High-performance SIMD cosine vector search, embedding generation, and report knowledge base indexing in C# .NET 9.
---

# Semantic Vector Search Skill

Use this skill when implementing vector similarity ranking, indexing generated intelligence reports, or tuning embedding models.

## Vector Store Mechanics

### 1. Hardware SIMD Cosine Similarity
```csharp
public static double Calculate(ReadOnlySpan<float> vectorA, ReadOnlySpan<float> vectorB)
{
    float dot = 0f, magA = 0f, magB = 0f;
    int vectorSize = Vector<float>.Count;
    int i = 0;
    while (i <= vectorA.Length - vectorSize)
    {
        var va = new Vector<float>(vectorA.Slice(i, vectorSize));
        var vb = new Vector<float>(vectorB.Slice(i, vectorSize));
        dot += Vector.Dot(va, vb);
        magA += Vector.Dot(va, va);
        magB += Vector.Dot(vb, vb);
        i += vectorSize;
    }
    // Remaining elements calculation & L2 normalization
    return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
}
```

### 2. Semantic Search Ranking
- Calculate cosine similarity score against all stored report vectors.
- Rank by descending score and filter by `minSimilarity` threshold (e.g. 0.20 to 0.75).
- Return matched report and contextual snippet for instant UI preview.
