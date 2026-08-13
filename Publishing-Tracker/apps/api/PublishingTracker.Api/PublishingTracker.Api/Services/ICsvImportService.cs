using PublishingTracker.Domain.Dtos;

namespace PublishingTracker.Api.Services;

public interface ICsvImportService
{
    Task<ImportJobDto> ProcessImportAsync(int userId, IFormFile file, ColumnMappingDto mapping);
}
