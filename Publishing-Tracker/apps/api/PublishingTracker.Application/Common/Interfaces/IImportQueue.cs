using PublishingTracker.Application.Import.Models;

namespace PublishingTracker.Application.Common.Interfaces;

public interface IImportQueue
{
    ValueTask EnqueueAsync(ImportRequest request, CancellationToken cancellationToken = default);
}
