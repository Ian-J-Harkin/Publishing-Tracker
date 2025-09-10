using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PublishingTracker.Api.Models
{
    public class PlatformRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string? BaseUrl { get; set; }

        [Column(TypeName = "decimal(5, 2)")]
        public decimal CommissionRate { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }
    }
}