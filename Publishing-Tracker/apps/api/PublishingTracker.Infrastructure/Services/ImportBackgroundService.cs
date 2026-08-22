using System.Threading.Channels;
using MediatR;
using PublishingTracker.Application.Import.Commands;
using PublishingTracker.Application.Common.Interfaces;
using PublishingTracker.Domain.Entities;
using PublishingTracker.Application.Import.Models;

namespace PublishingTracker.Infrastructure.Services;

/// <summary>
/// A hosted BackgroundService that processes CSV imports off the HTTP request thread.
/// Uses <see cref="Channel{T}"/> (producer/consumer pattern) so the API endpoint can enqueue
/// work and immediately return 202 Accepted.
/// </summary>
public class ImportBackgroundService : BackgroundService, IImportQueue
{
    private readonly Channel<ImportRequest> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImportBackgroundService> _logger;

    // ── Custom Events ──────────────────────────────────────────────
    /// <summary>Raised after each batch of rows is processed.</summary>
    public event EventHandler<ImportProgressEventArgs>? ProgressChanged;

    /// <summary>Raised when an import job finishes (success or failure).</summary>
    public event EventHandler<ImportProgressEventArgs>? ImportCompleted;

    public ImportBackgroundService(
        Channel<ImportRequest> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ImportBackgroundService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Enqueues an import request. Called from the API endpoint.
    /// </summary>
    public async ValueTask EnqueueAsync(ImportRequest request, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(request, ct);
        _logger.LogInformation("Import job {JobId} enqueued for background processing.", request.JobId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImportBackgroundService started. Waiting for import requests...");

        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessImportAsync(request, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing import job {JobId}.", request.JobId);
            }
        }
    }

    private async Task ProcessImportAsync(ImportRequest request, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var command = new ImportCsvCommand(request.UserId, request.JobId, request.FilePath, request.Mapping);
        
        var result = await mediator.Send(command, ct);
        
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IQueryExecutor>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var user = await queries.FirstOrDefaultAsync(db.Users.Where(u => u.Id == request.UserId), ct);

        // Fire completion event
        if (result.IsSuccess && result.Value != null)
        {
            OnImportCompleted(new ImportProgressEventArgs
            {
                JobId = request.JobId,
                RowsProcessed = result.Value.RecordsProcessed,
                RowsSuccessful = result.Value.RecordsSuccessful,
                RowsFailed = result.Value.RecordsFailed,
                IsComplete = true
            });
            
            _logger.LogInformation(
                "Import job {JobId} finished. Status: {Status}, Processed: {Processed}, Success: {Success}, Failed: {Failed}",
                request.JobId, result.Value.Status, result.Value.RecordsProcessed, result.Value.RecordsSuccessful, result.Value.RecordsFailed);
                
            if (user != null)
            {
                await emailService.SendEmailAsync(
                    user.Email,
                    "Import Completed Successfully",
                    $"Your CSV import (Job ID: {request.JobId}) has completed. Processed: {result.Value.RecordsProcessed}, Success: {result.Value.RecordsSuccessful}, Failed: {result.Value.RecordsFailed}."
                );
            }
        }
        else
        {
            _logger.LogError("Import job {JobId} failed during CQRS execution: {Error}", request.JobId, result.ErrorMessage);
            
            if (user != null)
            {
                await emailService.SendEmailAsync(
                    user.Email,
                    "Import Failed",
                    $"Your CSV import (Job ID: {request.JobId}) failed to process. Error: {result.ErrorMessage}"
                );
            }
        }
    }

    protected virtual void OnProgressChanged(ImportProgressEventArgs e)
        => ProgressChanged?.Invoke(this, e);

    protected virtual void OnImportCompleted(ImportProgressEventArgs e)
        => ImportCompleted?.Invoke(this, e);
}
