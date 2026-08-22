namespace PublishingTracker.Infrastructure.Services;

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
