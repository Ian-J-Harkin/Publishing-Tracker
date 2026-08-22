using MediatR;
using PublishingTracker.Application.Common.Dtos;
using PublishingTracker.Domain;
using System.Text;

namespace PublishingTracker.Application.Import.Commands;

public record UploadImportFileCommand(int UserId, string FileName, Stream FileStream) : IRequest<Result<PreviewDataDto>>;

public class UploadImportFileCommandHandler : IRequestHandler<UploadImportFileCommand, Result<PreviewDataDto>>
{
    public async Task<Result<PreviewDataDto>> Handle(UploadImportFileCommand request, CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads");
        Directory.CreateDirectory(tempDir);

        var filePath = Path.Combine(tempDir, $"{request.UserId}_{request.FileName}");
        await using (var target = File.Create(filePath))
        {
            await request.FileStream.CopyToAsync(target, cancellationToken);
        }

        var preview = new PreviewDataDto { FileName = request.FileName };
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(headerLine))
        {
            preview.Headers = SplitCsvLine(headerLine);
        }

        var previewCount = 0;
        while (previewCount < 5)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var values = SplitCsvLine(line);
            var row = new Dictionary<string, string>();
            for (var i = 0; i < preview.Headers.Count; i++)
            {
                row[preview.Headers[i]] = i < values.Count ? values[i] : string.Empty;
            }

            preview.PreviewRows.Add(row);
            previewCount++;
        }

        return Result<PreviewDataDto>.Success(preview);
    }

    private static List<string> SplitCsvLine(string line)
        => line.Split(',', StringSplitOptions.TrimEntries).ToList();
}
