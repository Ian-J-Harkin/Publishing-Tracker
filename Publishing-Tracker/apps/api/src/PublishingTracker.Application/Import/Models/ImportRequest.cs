using PublishingTracker.Application.Common.Dtos;

namespace PublishingTracker.Application.Import.Models;

public record ImportRequest(
    int UserId,
    int JobId,
    string FilePath,
    string FileName,
    ColumnMappingDto Mapping);
