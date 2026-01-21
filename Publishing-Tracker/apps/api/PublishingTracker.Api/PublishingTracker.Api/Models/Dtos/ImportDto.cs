using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace PublishingTracker.Api.Models.Dtos
{
    public class ImportJobDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RecordsProcessed { get; set; }
        public int RecordsSuccessful { get; set; }
        public int RecordsFailed { get; set; }
        public string? ErrorLog { get; set; }
    }

    public class FileUploadDto
    {
        public IFormFile File { get; set; } = null!;
    }

    public class ColumnMappingDto
    {
        public string BookTitle { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string SaleDate { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public string UnitPrice { get; set; } = string.Empty;
        public string Royalty { get; set; } = string.Empty;
    }
}