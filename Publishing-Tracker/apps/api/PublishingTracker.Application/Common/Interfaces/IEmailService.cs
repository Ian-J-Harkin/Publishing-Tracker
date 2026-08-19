using System.Threading.Tasks;

namespace PublishingTracker.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}
