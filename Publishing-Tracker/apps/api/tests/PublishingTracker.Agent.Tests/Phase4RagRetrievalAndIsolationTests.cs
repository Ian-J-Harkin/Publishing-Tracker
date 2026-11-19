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
}
