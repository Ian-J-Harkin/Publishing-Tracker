using System;

namespace PublishingTracker.Api.Models
{
    public class ImportJob
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Processing, Completed, Failed
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RecordsProcessed { get; set; }
        public int RecordsSuccessful { get; set; }
        public int RecordsFailed { get; set; }
        public string? ErrorLog { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
    }
}