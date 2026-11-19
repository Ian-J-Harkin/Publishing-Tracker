using Microsoft.EntityFrameworkCore;
using PublishingTracker.Agent.Tests.Doubles;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Rag.Services;

namespace PublishingTracker.Agent.Tests;

public sealed class Phase4RagRetrievalAndIsolationTests
{
    [Fact]
    public void RagIngestion_RejectsInvalidEmbeddingDimensions_OrNaNInfinityVectors()
    {
        Assert.False(VectorMath.TryValidateEmbedding(new float[1535]));
        Assert.Throws<ArgumentException>(() => VectorMath.ValidateEmbedding(new float[1535]));

        var zeroVector = new float[VectorMath.ExpectedDimensions];
        Assert.False(VectorMath.TryValidateEmbedding(zeroVector));
        Assert.Throws<ArgumentException>(() => VectorMath.ValidateEmbedding(zeroVector));

        var nanVector = new float[VectorMath.ExpectedDimensions];
        nanVector[0] = float.NaN;
        Assert.False(VectorMath.TryValidateEmbedding(nanVector));
        Assert.Throws<ArgumentException>(() => VectorMath.ValidateEmbedding(nanVector));

        var infVector = new float[VectorMath.ExpectedDimensions];
        infVector[10] = float.PositiveInfinity;
        Assert.False(VectorMath.TryValidateEmbedding(infVector));
        Assert.Throws<ArgumentException>(() => VectorMath.ValidateEmbedding(infVector));

        var valid = new float[VectorMath.ExpectedDimensions];
        valid[0] = 1.0f;
        Assert.True(VectorMath.TryValidateEmbedding(valid));
        Assert.Equal(0d, VectorMath.CosineDistance(valid, valid), precision: 6);
    }

    [Fact]
    public void MarkdownChunker_SplitsHeadingsDeterministically_AssignsStableChunkIds_AndComputesContentHash()
    {
        const string markdown = """
        # Guide Title

        Introductory context before any level-2 heading.

        ## Empty Heading To Skip

        ## Section One
        First rule details.

        ### Subsection A
        Subsection details.
        """;

        var first = MarkdownChunker.Chunk("doc-sample-v1", "v1", markdown);
        var second = MarkdownChunker.Chunk("doc-sample-v1", "v1", markdown.Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.Equal(3, first.Count);
        Assert.Equal("doc-sample-v1#c1", first[0].ChunkId);
        Assert.Equal("Guide Title", first[0].Heading); // Preserves intro text
        Assert.Contains("Introductory context", first[0].Excerpt);
        Assert.Equal("Section One", first[1].Heading);
        Assert.Equal("Subsection A", first[2].Heading);
        Assert.All(first, c => Assert.False(string.IsNullOrWhiteSpace(c.Excerpt)));

        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].ChunkId, second[i].ChunkId);
            Assert.Equal(first[i].ContentHash, second[i].ContentHash);
        }
    }

    [Fact]
    public void MarkdownChunker_OversizedSection_SplitsWithBoundedWindowAndOverlap()
    {
        Assert.Throws<ArgumentException>(() => MarkdownChunker.Chunk("", "v1", "# Title\nBody"));
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkdownChunker.Chunk("doc", "v1", "# Title\nBody", maxChunkChars: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkdownChunker.Chunk("doc", "v1", "# Title\nBody", maxChunkChars: 100, overlapChars: 100));

        var repeatedClause = string.Join(' ', Enumerable.Range(1, 140).Select(i => $"word{i:D3}"));
        var markdown = $"# Large Guide\n\n## Long Section\n{repeatedClause}";

        var chunks = MarkdownChunker.Chunk("doc-long-v1", "v1", markdown, maxChunkChars: 300, overlapChars: 60);

        Assert.True(chunks.Count >= 3);
        Assert.All(chunks, c =>
        {
            Assert.True(c.Excerpt.Length <= 300);
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(c.Excerpt) <= MarkdownChunker.MaxExcerptBytes);
        });
        Assert.Contains("word001", chunks[0].Excerpt);
        Assert.Contains("word140", chunks[^1].Excerpt);

        // Multi-byte UTF-8 test enforcing <= MaxExcerptBytes even when chars < MaxChunkChars
        var multiByte = "# UTF8\n\n## Section\n" + new string('\u00e9', 900); // 1,800 UTF-8 bytes
        var utf8Chunks = MarkdownChunker.Chunk("doc-utf8-v1", "v1", multiByte, maxChunkChars: 1_000, overlapChars: 150);
        Assert.True(utf8Chunks.Count >= 2);
        Assert.All(utf8Chunks, c => Assert.True(System.Text.Encoding.UTF8.GetByteCount(c.Excerpt) <= MarkdownChunker.MaxExcerptBytes));
    }

    [Fact]
    public void MarkdownChunker_FencedCodeBlocksWithHashComments_AndEmptySections_HandledSafely()
    {
        const string markdownWithCode = """
        # Code Guide

        Introductory text before fenced code.

        ## Script Section
        Here is a bash script:
        ```bash
        # This is a comment, not a title
        echo "hello"
        ## This is another comment, not a section split
        echo "world"
        ```
        Post-code explanation.

        ## Empty Section
        ## Section Followed By Empty Lines


        ## Final Active Section
        Final active section body.
        """;

        var chunks = MarkdownChunker.Chunk("doc-code-v1", "v1", markdownWithCode);
        Assert.Equal(3, chunks.Count);
        Assert.Equal("Code Guide", chunks[0].Title);
        Assert.Equal("Code Guide", chunks[0].Heading);
        Assert.Equal("Script Section", chunks[1].Heading);
        Assert.Contains("# This is a comment", chunks[1].Excerpt);
        Assert.Contains("## This is another comment", chunks[1].Excerpt);
        Assert.Equal("Final Active Section", chunks[2].Heading);
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Excerpt)));
    }

    private static PublishingTrackerDbContext CreateInMemoryDbContext([System.Runtime.CompilerServices.CallerMemberName] string testName = "test")
    {
        var options = new DbContextOptionsBuilder<PublishingTrackerDbContext>()
            .UseInMemoryDatabase($"TestRagDb_{testName}_{Guid.NewGuid()}")
            .Options;
        return new PublishingTrackerDbContext(options);
    }

    [Fact]
    public async Task RagIngestion_IdempotentUpsert_UpdatesChangedChunks_AndRemovesStaleChunksOnVersionUpdate()
    {
        await using var db = CreateInMemoryDbContext();
        var embeddings = new DeterministicTestEmbeddingGenerator();
        var ingestion = new RagIngestionService(db, embeddings);

        const string v1 = """
        # Guideline
        ## First Section
        First section body.
        ## Second Section
        Second section body that will be removed in v2.
        """;

        await ingestion.UpsertDocumentAsync("doc-test", "v1", v1);
        Assert.Equal(2, embeddings.CallCount);
        Assert.Equal(2, db.RagDocumentChunks.Count());

        // Re-running same v1 content skips all embedding calls
        await ingestion.UpsertDocumentAsync("doc-test", "v1", v1);
        Assert.Equal(2, embeddings.CallCount);

        // Upgrading to v2 removes stale second chunk and embeds updated content
        const string v2 = """
        # Guideline
        ## Updated First Section
        Updated rule body.
        """;
        await ingestion.UpsertDocumentAsync("doc-test", "v2", v2);
        Assert.Equal(3, embeddings.CallCount);
        var stored = Assert.Single(db.RagDocumentChunks.ToList());
        Assert.Equal("v2", stored.Version);
        Assert.Equal("doc-test#c1", stored.ChunkId);
    }

    [Fact]
    public async Task RagIngestion_EmbeddingFailureMidDocument_LeavesExistingChunksUntouched()
    {
        await using var db = CreateInMemoryDbContext();
        var ingestion = new RagIngestionService(db, new DeterministicTestEmbeddingGenerator());

        const string v1 = """
        # Guideline
        ## First Section
        First section body.
        ## Second Section
        Second section body.
        """;
        await ingestion.UpsertDocumentAsync("doc-resilient", "v1", v1);
        var before = db.RagDocumentChunks.AsNoTracking()
            .Where(c => c.DocumentId == "doc-resilient")
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new { c.ChunkId, c.Version, c.ContentHash })
            .ToList();
        Assert.Equal(2, before.Count);

        // v2 has TWO changed chunks; the generator succeeds on the first and throws on the second,
        // proving no partial removal/mutation is flushed when embedding fails mid-document.
        var calls = 0;
        var failingGenerator = new DeterministicTestEmbeddingGenerator(text =>
            ++calls == 2
                ? throw new InvalidOperationException("Simulated embedding provider failure")
                : DeterministicTestEmbeddingGenerator.EncodeText(text));
        var failingIngestion = new RagIngestionService(db, failingGenerator);

        const string v2 = """
        # Guideline
        ## Rewritten First Section
        Rewritten first body.
        ## Rewritten Second Section
        Rewritten second body.
        """;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failingIngestion.UpsertDocumentAsync("doc-resilient", "v2", v2));
        Assert.Equal(2, calls);

        // Same scoped DbContext: a later SaveChanges must not flush anything partial.
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var after = db.RagDocumentChunks.AsNoTracking()
            .Where(c => c.DocumentId == "doc-resilient")
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new { c.ChunkId, c.Version, c.ContentHash })
            .ToList();
        Assert.Equal(before, after);
    }
}
