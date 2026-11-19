using System.Security.Cryptography;
using System.Text;
using PublishingTracker.Rag.Entities;

namespace PublishingTracker.Rag.Services;

public static class MarkdownChunker
{
    public const int DefaultMaxChunkChars = 1_000;
    public const int DefaultOverlapChars = 150;
    public const int MaxExcerptBytes = 1_500;

    public static IReadOnlyList<RagDocumentChunk> Chunk(
        string documentId,
        string version,
        string markdown,
        int? ownerUserId = null,
        int maxChunkChars = DefaultMaxChunkChars,
        int overlapChars = DefaultOverlapChars)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("DocumentId is required.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version is required.", nameof(version));
        if (string.IsNullOrWhiteSpace(markdown))
            throw new ArgumentException("Markdown content must not be empty.", nameof(markdown));
        if (maxChunkChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChunkChars), "maxChunkChars must be positive.");
        if (overlapChars < 0 || overlapChars >= maxChunkChars)
            throw new ArgumentOutOfRangeException(nameof(overlapChars), "overlapChars must be >= 0 and < maxChunkChars.");

        var normalizedId = documentId.Trim();
        var normalizedVersion = version.Trim();
        var normalizedText = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

        var lines = normalizedText.Split('\n');
        var documentTitle = normalizedId;
        var currentHeading = string.Empty;
        var bodyLines = new List<string>();
        var sections = new List<(string Heading, string Body)>();

        var inFencedCodeBlock = false;
        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.TrimEnd();
            var lineTrimmedStart = trimmed.TrimStart();
            if (lineTrimmedStart.StartsWith("```", StringComparison.Ordinal) || lineTrimmedStart.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFencedCodeBlock = !inFencedCodeBlock;
                bodyLines.Add(trimmed);
                continue;
            }

            if (!inFencedCodeBlock)
            {
                if (trimmed.StartsWith("# ", StringComparison.Ordinal) && documentTitle == normalizedId && sections.Count == 0 && bodyLines.Count == 0)
                {
                    documentTitle = trimmed[2..].Trim();
                    currentHeading = documentTitle;
                    continue;
                }

                if (trimmed.StartsWith("## ", StringComparison.Ordinal) || trimmed.StartsWith("### ", StringComparison.Ordinal))
                {
                    FlushSection(sections, currentHeading, bodyLines, documentTitle);
                    var markerLength = trimmed.StartsWith("### ", StringComparison.Ordinal) ? 4 : 3;
                    currentHeading = trimmed[markerLength..].Trim();
                    bodyLines.Clear();
                    continue;
                }
            }

            bodyLines.Add(trimmed);
        }

        FlushSection(sections, currentHeading, bodyLines, documentTitle);

        var chunks = new List<RagDocumentChunk>();
        var chunkIndex = 0;

        foreach (var (heading, body) in sections)
        {
            foreach (var window in SplitIntoBoundedWindows(body, maxChunkChars, overlapChars))
            {
                chunkIndex++;
                var trimmedWindow = window.Trim();
                if (trimmedWindow.Length == 0)
                    continue;

                var chunkId = $"{normalizedId}#c{chunkIndex}";
                var contentHash = ComputeHash(normalizedVersion, documentTitle, heading, trimmedWindow);

                chunks.Add(new RagDocumentChunk
                {
                    DocumentId = normalizedId,
                    Version = normalizedVersion,
                    ChunkId = chunkId,
                    Title = documentTitle,
                    Heading = heading,
                    ChunkIndex = chunkIndex,
                    Excerpt = trimmedWindow,
                    ContentHash = contentHash,
                    OwnerUserId = ownerUserId
                });
            }
        }

        return chunks;
    }

    private static void FlushSection(
        List<(string Heading, string Body)> sections,
        string heading,
        List<string> lines,
        string fallbackHeading)
    {
        var body = string.Join('\n', lines).Trim();
        if (string.IsNullOrWhiteSpace(body))
            return;

        var resolvedHeading = string.IsNullOrWhiteSpace(heading) ? fallbackHeading : heading;
        sections.Add((resolvedHeading, body));
    }

    private static IEnumerable<string> SplitIntoBoundedWindows(string text, int maxChunkChars, int overlapChars)
    {
        if (text.Length <= maxChunkChars && Encoding.UTF8.GetByteCount(text) <= MaxExcerptBytes)
        {
            yield return text;
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var maxEnd = Math.Min(start + maxChunkChars, text.Length);
            var end = FindSplitBoundary(text, start, maxEnd);
            var candidate = TrimToMaxUtf8Bytes(text, start, end, MaxExcerptBytes, out var rawConsumedLength);
            if (candidate.Length == 0)
            {
                start = Math.Max(start + 1, start + rawConsumedLength);
                continue;
            }

            yield return candidate;

            var consumedEnd = start + rawConsumedLength;
            if (consumedEnd >= text.Length)
                yield break;

            var nextStart = Math.Max(start + 1, consumedEnd - overlapChars);
            while (nextStart < text.Length && char.IsLowSurrogate(text[nextStart]))
                nextStart++;
            start = nextStart;
        }
    }

    private static int FindSplitBoundary(string text, int start, int maxEnd)
    {
        if (maxEnd >= text.Length)
            return text.Length;

        var paragraphBreak = text.LastIndexOf("\n\n", maxEnd - 1, maxEnd - start, StringComparison.Ordinal);
        if (paragraphBreak > start + (maxEnd - start) / 3)
            return paragraphBreak;

        var whitespaceBreak = maxEnd;
        while (whitespaceBreak > start + (maxEnd - start) / 2 && !char.IsWhiteSpace(text[whitespaceBreak - 1]))
            whitespaceBreak--;

        var chosen = whitespaceBreak > start + (maxEnd - start) / 2 ? whitespaceBreak : maxEnd;
        while (chosen > start + 1 && char.IsSurrogatePair(text, chosen - 1))
            chosen--;
        return chosen;
    }

    private static string TrimToMaxUtf8Bytes(string fullText, int start, int end, int maxBytes, out int rawConsumedLength)
    {
        var slice = fullText[start..end];
        if (Encoding.UTF8.GetByteCount(slice) <= maxBytes)
        {
            rawConsumedLength = slice.Length;
            return slice.Trim();
        }

        var len = slice.Length;
        while (len > 0 && Encoding.UTF8.GetByteCount(slice.AsSpan(0, len)) > maxBytes)
        {
            len--;
            if (len > 0 && char.IsLowSurrogate(slice[len]))
                len--;
        }

        rawConsumedLength = len;
        return slice[..len].Trim();
    }

    private static string ComputeHash(string version, string title, string heading, string excerpt)
    {
        var payload = $"{version}\n{title}\n{heading}\n{excerpt}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
