using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using PublishingTracker.Agent.Configuration;
using PublishingTracker.Agent.Mcp;
using PublishingTracker.Agent.Tests.Doubles;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Rag.Services;
using PublishingTracker.Rag.Tools;

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

    [Fact]
    public async Task AIProviderSetup_CreateEmbeddingGenerator_ConfiguresEndpointAndOpenRouterPrivacyHeaders()
    {
        const string openAiEmbeddingJson = """
        {"object":"list","data":[{"object":"embedding","index":0,"embedding":[0.1,0.2]}],"model":"text-embedding-3-small","usage":{"prompt_tokens":1,"total_tokens":1}}
        """;

        foreach (var provider in new[] { "openrouter", "openai", "ollama" })
        {
            var resolved = AIProviderSetup.Resolve(
                new AIProviderOptions { Type = provider, ApiKey = "test-key", OpenRouterApiKey = "test-key" },
                _ => null);

            var expectedEmbeddingModel = provider switch
            {
                "openrouter" => AIProviderSetup.OpenRouterDefaultEmbeddingModelId,
                "openai" => AIProviderSetup.OpenAiDefaultEmbeddingModelId,
                _ => AIProviderSetup.OllamaDefaultEmbeddingModelId
            };
            Assert.Equal(expectedEmbeddingModel, resolved.EmbeddingModelId);
            Assert.Equal(1536, resolved.EmbeddingDimensions);

            var recorder = new RecordingHttpMessageHandler(openAiEmbeddingJson);
            using var http = new HttpClient(recorder);
            using var generator = AIProviderSetup.CreateEmbeddingGenerator(resolved, http);
            _ = await generator.GenerateAsync(["audiobook narrator"]);

            Assert.Single(recorder.Requests);
            Assert.Contains("embeddings", recorder.Requests[0].RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
            if (provider == "openrouter")
            {
                Assert.Contains("PublishingTracker", recorder.Requests[0].Headers.GetValues("X-Title"));
                Assert.Contains("https://publishingtracker.local", recorder.Requests[0].Headers.GetValues("HTTP-Referer"));
                Assert.Contains("deny", recorder.Requests[0].Headers.GetValues("data_collection"));
            }
        }
    }

    [Fact]
    public void RagSearchTool_SchemaInspection_ExposesOnlyQueryAndTopK_NeverUserIdOrTenantParameters()
    {
        var accessor = new HttpContextAccessor();
        var factory = new RagSearchToolFactory(null!, accessor);
        var tools = factory.CreateToolsForCaller(1);
        var tool = Assert.Single(tools);

        Assert.Equal(RagSearchToolFactory.ToolName, tool.Name);
        var schemaJson = tool.JsonSchema.ToString();

        Assert.Contains("query", schemaJson);
        Assert.Contains("topK", schemaJson);
        Assert.DoesNotContain("userId", schemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ownerId", schemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("callerUserId", schemaJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenantId", schemaJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RagSearchTool_TwoUserIsolation_ReturnsSharedGuidelinesAndCallerNotes_NeverOtherUsersNotes()
    {
        await using var db = CreateInMemoryDbContext();
        var embeddings = new DeterministicTestEmbeddingGenerator();
        var ingestion = new RagIngestionService(db, embeddings);

        // Seed shared guide, User 1 note, and User 2 note
        await ingestion.UpsertDocumentAsync("doc-shared", "v1", "# Shared\n## Rules\nStandard platform rules.", ownerUserId: null);
        await ingestion.UpsertDocumentAsync("doc-user1", "v1", "# Note U1\n## Secret 1\nUser 1 personal pricing note.", ownerUserId: 1);
        await ingestion.UpsertDocumentAsync("doc-user2", "v1", "# Note U2\n## Secret 2\nUser 2 personal strategy note.", ownerUserId: 2);

        var retrieval = new RagRetrievalService(db, embeddings);

        var user1Hits = await retrieval.SearchAsync(authenticatedUserId: 1, query: "note rules", topK: 5);
        var user2Hits = await retrieval.SearchAsync(authenticatedUserId: 2, query: "note rules", topK: 5);

        Assert.Contains(user1Hits.Hits, h => h.DocumentId == "doc-shared");
        Assert.Contains(user1Hits.Hits, h => h.DocumentId == "doc-user1");
        Assert.DoesNotContain(user1Hits.Hits, h => h.DocumentId == "doc-user2");

        Assert.Contains(user2Hits.Hits, h => h.DocumentId == "doc-shared");
        Assert.Contains(user2Hits.Hits, h => h.DocumentId == "doc-user2");
        Assert.DoesNotContain(user2Hits.Hits, h => h.DocumentId == "doc-user1");
    }

    [Fact]
    public async Task CompositeToolProvider_UnauthenticatedCaller_AndDownstreamFailure_DisposesMcpSessionCleanly()
    {
        // Case A: unauthenticated / non-positive / non-integer caller -> rejected BEFORE /mcp is opened.
        foreach (var principal in new[]
                 {
                     new ClaimsPrincipal(new ClaimsIdentity()), // unauthenticated
                     AuthenticatedAs("abc"),
                     AuthenticatedAs("0"),
                     AuthenticatedAs("-5")
                 })
        {
            var stubMcp = new StubToolProvider();
            var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
            var composite = new CompositeToolProvider(stubMcp, new RagSearchToolFactory(null!, accessor));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => composite.OpenSessionAsync());
            Assert.Equal(0, stubMcp.OpenCount);
        }

        // Case B: /mcp opens, then RAG tool construction throws -> the remote session is disposed (no leak).
        var leakProbe = new StubToolProvider();
        var throwing = new CompositeToolProvider(leakProbe, new ThrowingRagToolProvider(userId: 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => throwing.OpenSessionAsync());
        Assert.Equal(1, leakProbe.OpenCount);
        Assert.Equal(1, leakProbe.DisposeCount);

        // Case C: happy path composes both tool sets and disposes the inner session exactly once.
        var okMcp = new StubToolProvider();
        var okAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = AuthenticatedAs("1") } };
        var ok = new CompositeToolProvider(okMcp, new RagSearchToolFactory(null!, okAccessor));
        await using (var session = await ok.OpenSessionAsync())
        {
            Assert.Contains(session.Tools, t => t.Name == RagSearchToolFactory.ToolName);
        }
        Assert.Equal(1, okMcp.DisposeCount);
    }

    private static ClaimsPrincipal AuthenticatedAs(string nameIdentifier) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, nameIdentifier)], "TestAuth"));

    private sealed class ThrowingRagToolProvider(int userId) : IRagSearchToolProvider
    {
        public int ResolveAuthenticatedUserId() => userId;
        public IReadOnlyList<AIFunction> CreateToolsForCaller(int authenticatedUserId) =>
            throw new InvalidOperationException("Simulated RAG tool construction failure");
    }
}
