using PublishingTracker.Domain;
using System;

namespace PublishingTracker.Domain.Entities
{
    public class ImportJob : IEntity
    {
        public int Id { get; private set; }
        public int UserId { get; private set; }
        public string FileName { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;
        public DateTime StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public int RecordsProcessed { get; private set; }
        public int RecordsSuccessful { get; private set; }
        public int RecordsFailed { get; private set; }
        public string? ErrorLog { get; private set; }

        public User User { get; private set; } = null!;

        public static ImportJob Create(int userId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty.", nameof(fileName));

            return new ImportJob
            {
                UserId = userId,
                FileName = fileName,
                Status = "Queued",
                StartedAt = DateTime.UtcNow,
                RecordsProcessed = 0,
                RecordsSuccessful = 0,
                RecordsFailed = 0,
                ErrorLog = string.Empty
            };
        }

        public void MarkProcessing()
        {
            Status = "Processing";
        }

        public void MarkCompleted()
        {
            Status = "Completed";
            CompletedAt = DateTime.UtcNow;
        }

        public void MarkCancelled()
        {
            Status = "Cancelled";
        }

        public void MarkFailed(string errorMessage)
        {
            Status = "Failed";
            AppendError(errorMessage);
            CompletedAt = DateTime.UtcNow;
        }

        public void IncrementProcessed() => RecordsProcessed++;
        public void IncrementSuccessful() => RecordsSuccessful++;
        public void IncrementFailed(string message)
        {
            RecordsFailed++;
            AppendError(message);
        }

        private void AppendError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            ErrorLog = string.IsNullOrEmpty(ErrorLog) ? message : $"{ErrorLog}\n{message}";
        }

        private ImportJob() { }
    }
}
