using System.ComponentModel.DataAnnotations;

namespace PublishingTracker.Api.Models.Dtos
{ 

    // What you send TO the API to create/update
    public class PlatformRequestDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        [Range(0, 1)]
        public decimal CommissionRate { get; set; }
    }

    // What the API sends BACK to the frontend
    public class PlatformResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BaseUrl { get; set; }
        public decimal CommissionRate { get; set; }
        public bool IsActive { get; set; }
    }
}