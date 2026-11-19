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
}
