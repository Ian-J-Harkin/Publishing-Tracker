namespace PublishingTracker.Api.Services;

/// <summary>
/// Event args raised when import processing makes progress or completes.
/// Demonstrates custom C# events — used by <see cref="ImportBackgroundService"/> to
/// report row-level progress that consumers (logging, SignalR, etc.) can subscribe to.
/// </summary>
public class ImportProgressEventArgs : EventArgs
{
    public int JobId { get; init; }
    public int RowsProcessed { get; init; }
    public int RowsSuccessful { get; init; }
    public int RowsFailed { get; init; }
    public string? LastError { get; init; }
    public bool IsComplete { get; init; }
}

/// <summary>
/// Represents a queued import request for the background processing channel.
/// </summary>
public class ImportRequest
{
    public int UserId { get; init; }
    public int JobId { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public Models.Dtos.ColumnMappingDto Mapping { get; init; } = null!;
}
