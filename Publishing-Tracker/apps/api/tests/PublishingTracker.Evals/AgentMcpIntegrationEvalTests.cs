using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PublishingTracker.Agent.Configuration;
using PublishingTracker.Agent.Contracts;
using PublishingTracker.Agent.Mcp;
using PublishingTracker.Agent.Orchestration;
using PublishingTracker.Agent.Tests.Doubles;
using PublishingTracker.Agent.Validation;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Infrastructure.Persistence;
using PublishingTracker.Rag.Entities;
using PublishingTracker.Rag.Persistence;
using PublishingTracker.Rag.Services;
using PublishingTracker.Rag.Tools;

namespace PublishingTracker.Evals;

[Collection("PostgresIntegrationTests")]
public sealed class AgentMcpIntegrationEvalTests : IClassFixture<PublishingTracker.Api.Tests.TestWebAppFactory>
{
    private readonly PublishingTracker.Api.Tests.TestWebAppFactory _apiFactory;

    public AgentMcpIntegrationEvalTests(PublishingTracker.Api.Tests.TestWebAppFactory apiFactory)
    {
        _apiFactory = apiFactory;
    }

    [Fact]
    public async Task RealPgvector_OperatorParity_MatchesCSharpCosineDistanceRanking()
    {
        var conn = PostgresTestDatabaseFixture.GetConnectionStringOrThrow();
        await using var db = await PostgresTestDatabaseFixture.CreateMigratedDbContextAsync(conn);

        // Clear existing test chunks for isolation
        var oldChunks = await db.RagDocumentChunks.Where(c => c.DocumentId.StartsWith("test-parity-")).ToListAsync();
        db.RagDocumentChunks.RemoveRange(oldChunks);
        await db.SaveChangesAsync();

        var queryVec = new float[VectorMath.ExpectedDimensions];
        queryVec[0] = 1.0f; // Unit vector on axis 0

        var closeVec = new float[VectorMath.ExpectedDimensions];
        closeVec[0] = 0.9f;
        closeVec[1] = 0.43588989f; // Unit vector close to axis 0

        var farVec = new float[VectorMath.ExpectedDimensions];
        farVec[10] = 1.0f; // Orthogonal unit vector on axis 10

        var chunkClose = new RagDocumentChunk
        {
            DocumentId = "test-parity-doc1",
            ChunkId = "test-parity-doc1#c1",
            ChunkIndex = 1,
            Title = "Close Chunk",
            Heading = "Close Chunk",
            Excerpt = "Text close to query",
            ContentHash = "h1",
            Embedding = closeVec,
            OwnerUserId = null
        };
        var chunkFar = new RagDocumentChunk
        {
            DocumentId = "test-parity-doc2",
            ChunkId = "test-parity-doc2#c1",
            ChunkIndex = 1,
            Title = "Far Chunk",
            Heading = "Far Chunk",
            Excerpt = "Text far from query",
            ContentHash = "h2",
            Embedding = farVec,
            OwnerUserId = null
        };

        db.RagDocumentChunks.AddRange(chunkClose, chunkFar);
        await db.SaveChangesAsync();

        // 1. Calculate C# ranking
        var distCloseCSharp = VectorMath.CosineDistance(queryVec, closeVec);
        var distFarCSharp = VectorMath.CosineDistance(queryVec, farVec);
        Assert.True(distCloseCSharp < distFarCSharp, "CSharp cosine distance should rank closeVec ahead of farVec");

        // 2. Query real pgvector SQL via IRagChunkStore.SearchByVectorAsync
        IRagChunkStore store = db;
        var allHits = await store.SearchByVectorAsync(
            authenticatedUserId: 1,
            queryVector: queryVec,
            topK: 10,
            cancellationToken: default);

        var hits = allHits.Where(h => h.DocumentId.StartsWith("test-parity-")).ToList();

        Assert.NotEmpty(hits);
        var hit1 = hits[0];
        var hit2 = hits[1];

        Assert.Equal("test-parity-doc1#c1", hit1.ChunkId);
        Assert.Equal("test-parity-doc2#c1", hit2.ChunkId);

        // Distance parity check
        Assert.True(hit1.CosineDistance < hit2.CosineDistance, "pgvector <=> ordering should match C# ranking");
        Assert.Equal(distCloseCSharp, hit1.CosineDistance, precision: 3);
        Assert.Equal(distFarCSharp, hit2.CosineDistance, precision: 3);
    }

    [Fact]
    public async Task RealPgvector_OwnerFilterEnforcedInSql_NotOnlyByQueryFilter()
    {
        var conn = PostgresTestDatabaseFixture.GetConnectionStringOrThrow();
        await using var db = await PostgresTestDatabaseFixture.CreateMigratedDbContextAsync(conn);

        var oldChunks = await db.RagDocumentChunks.Where(c => c.DocumentId.StartsWith("test-owner-")).ToListAsync();
        db.RagDocumentChunks.RemoveRange(oldChunks);
        await db.SaveChangesAsync();

        var vec = new float[VectorMath.ExpectedDimensions];
        vec[0] = 1.0f;

        // Document shared (Owner = null)
        var sharedChunk = new RagDocumentChunk
        {
            DocumentId = "test-owner-shared",
            ChunkId = "test-owner-shared#c1",
            ChunkIndex = 1,
            Title = "Shared Guide",
            Heading = "Shared",
            Excerpt = "Shared text",
            ContentHash = "h",
            Embedding = vec,
            OwnerUserId = null
        };
        // Document owned by User 1
        var user1Chunk = new RagDocumentChunk
        {
            DocumentId = "test-owner-u1",
            ChunkId = "test-owner-u1#c1",
            ChunkIndex = 1,
            Title = "U1 Note",
            Heading = "U1",
            Excerpt = "User 1 note",
            ContentHash = "h",
            Embedding = vec,
            OwnerUserId = 1
        };
        // Document owned by User 2
        var user2Chunk = new RagDocumentChunk
        {
            DocumentId = "test-owner-u2",
            ChunkId = "test-owner-u2#c1",
            ChunkIndex = 1,
            Title = "U2 Note",
            Heading = "U2",
            Excerpt = "User 2 note",
            ContentHash = "h",
            Embedding = vec,
            OwnerUserId = 2
        };

        db.RagDocumentChunks.AddRange(sharedChunk, user1Chunk, user2Chunk);
        await db.SaveChangesAsync();

        IRagChunkStore store = db;

        // User 1 search should return shared + user1, but never user2
        var u1Hits = await store.SearchByVectorAsync(1, vec, 5, default);
        Assert.Contains(u1Hits, h => h.DocumentId == "test-owner-shared");
        Assert.Contains(u1Hits, h => h.DocumentId == "test-owner-u1");
        Assert.DoesNotContain(u1Hits, h => h.DocumentId == "test-owner-u2");

        // User 2 search should return shared + user2, but never user1
        var u2Hits = await store.SearchByVectorAsync(2, vec, 5, default);
        Assert.Contains(u2Hits, h => h.DocumentId == "test-owner-shared");
        Assert.Contains(u2Hits, h => h.DocumentId == "test-owner-u2");
        Assert.DoesNotContain(u2Hits, h => h.DocumentId == "test-owner-u1");
    }

    [Fact]
    public async Task ConcurrentUsers_NoCrossTenantContamination_UnderInterleavedRequests()
    {
        var conn = PostgresTestDatabaseFixture.GetConnectionStringOrThrow();
        await using var db = await PostgresTestDatabaseFixture.CreateMigratedDbContextAsync(conn);

        // Seed distinct notes for User 10 and User 20
        var old = await db.RagDocumentChunks.Where(c => c.DocumentId.StartsWith("test-concurrent-")).ToListAsync();
        db.RagDocumentChunks.RemoveRange(old);
        await db.SaveChangesAsync();

        var vec = new float[VectorMath.ExpectedDimensions];
        vec[0] = 1.0f;

        db.RagDocumentChunks.Add(new RagDocumentChunk
        {
            DocumentId = "test-concurrent-u10",
            ChunkId = "test-concurrent-u10#c1",
            ChunkIndex = 1,
            Title = "U10 Title",
            Heading = "U10",
            Excerpt = "Secret for 10",
            ContentHash = "h",
            Embedding = vec,
            OwnerUserId = 10
        });
        db.RagDocumentChunks.Add(new RagDocumentChunk
        {
            DocumentId = "test-concurrent-u20",
            ChunkId = "test-concurrent-u20#c1",
            ChunkIndex = 1,
            Title = "U20 Title",
            Heading = "U20",
            Excerpt = "Secret for 20",
            ContentHash = "h",
            Embedding = vec,
            OwnerUserId = 20
        });
        await db.SaveChangesAsync();

        // Run 20 concurrent queries alternating user 10 and 20, each in its own DbContext scope
        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            var userId = (i % 2 == 0) ? 10 : 20;
            var expectedDoc = (userId == 10) ? "test-concurrent-u10" : "test-concurrent-u20";
            var forbiddenDoc = (userId == 10) ? "test-concurrent-u20" : "test-concurrent-u10";

            await using var reqDb = PostgresTestDatabaseFixture.CreateDbContext(conn);
            IRagChunkStore store = reqDb;
            var hits = await store.SearchByVectorAsync(userId, vec, topK: 5, default);

            Assert.Contains(hits, h => h.DocumentId == expectedDoc);
            Assert.DoesNotContain(hits, h => h.DocumentId == forbiddenDoc);
        });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task AgentRunner_MidRunTokenExpiry_FailsWithToolError_AndNoSecretLeak()
    {
        // When token expires mid-run and MCP tool provider encounters unauthorized/forbidden,
        // it fails closed with TOOL_ERROR and redacts any exception details.
        var chat = new ScriptedChatClient();
        chat.EnqueueFunctionCalls(("find_titles_missing_metadata", new Dictionary<string, object?> { ["page"] = 1 }));

        var throwingTool = StubToolProvider.CreateTool("find_titles_missing_metadata", (_, _, _, _) =>
            throw new HttpRequestException("401 Unauthorized: Bearer token expired at 2026-11-20T12:00:00Z"));

        var provider = new StubToolProvider(throwingTool);

        var runner = new AgentLoopRunner(
            chat,
            provider,
            Options.Create(new AgentLoopOptions { PerCallTimeout = TimeSpan.FromSeconds(5), OverallTimeout = TimeSpan.FromSeconds(15) }),
            NullLogger<AgentLoopRunner>.Instance);

        var result = await runner.RunVerifiedAsync("Find missing metadata");

        Assert.Equal("could_not_answer", result.Status);
        Assert.NotNull(result.Error);
        Assert.StartsWith("TOOL_ERROR:", result.Error);
        Assert.DoesNotContain("Bearer token expired", result.Error);
    }
}
