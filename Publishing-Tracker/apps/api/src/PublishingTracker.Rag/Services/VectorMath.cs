using System.Globalization;
using System.Text;

namespace PublishingTracker.Rag.Services;

public static class VectorMath
{
    public const int ExpectedDimensions = 1536;

    public static void ValidateEmbedding(ReadOnlySpan<float> vector, int expectedDimensions = ExpectedDimensions)
    {
        if (vector.Length != expectedDimensions)
        {
            throw new ArgumentException(
                $"Embedding vector must have exactly {expectedDimensions} dimensions; received {vector.Length}.");
        }

        double normSquared = 0;
        for (var i = 0; i < vector.Length; i++)
        {
            var value = vector[i];
            if (!float.IsFinite(value))
            {
                throw new ArgumentException($"Embedding vector contains non-finite value at index {i}.");
            }

            normSquared += (double)value * value;
        }

        if (normSquared <= 0d || !double.IsFinite(normSquared))
        {
            throw new ArgumentException("Embedding vector must have a strictly positive finite norm.");
        }
    }

    public static bool TryValidateEmbedding(ReadOnlySpan<float> vector, int expectedDimensions = ExpectedDimensions)
    {
        if (vector.Length != expectedDimensions)
            return false;

        double normSquared = 0;
        for (var i = 0; i < vector.Length; i++)
        {
            var value = vector[i];
            if (!float.IsFinite(value))
                return false;

            normSquared += (double)value * value;
        }

        return normSquared > 0d && double.IsFinite(normSquared);
    }

    public static double CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        ValidateEmbedding(left);
        ValidateEmbedding(right);

        double dot = 0;
        double leftNormSq = 0;
        double rightNormSq = 0;

        for (var i = 0; i < left.Length; i++)
        {
            var l = (double)left[i];
            var r = (double)right[i];
            dot += l * r;
            leftNormSq += l * l;
            rightNormSq += r * r;
        }

        var denominator = Math.Sqrt(leftNormSq) * Math.Sqrt(rightNormSq);
        return denominator <= 0d ? 0d : Math.Clamp(dot / denominator, -1d, 1d);
    }

    public static double CosineDistance(ReadOnlySpan<float> left, ReadOnlySpan<float> right) =>
        Math.Max(0d, 1d - CosineSimilarity(left, right));

    public static string FormatPgVectorLiteral(ReadOnlySpan<float> vector)
    {
        ValidateEmbedding(vector);
        var builder = new StringBuilder(vector.Length * 10);
        builder.Append('[');
        for (var i = 0; i < vector.Length; i++)
        {
            if (i > 0) builder.Append(',');
            builder.Append(vector[i].ToString("G9", CultureInfo.InvariantCulture));
        }
        builder.Append(']');
        return builder.ToString();
    }
}
