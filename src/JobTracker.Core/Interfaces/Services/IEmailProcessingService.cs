using JobTracker.Core.Models;

namespace JobTracker.Core.Interfaces.Services;

public interface IEmailProcessingService
{
    Task ProcessNewEmailAsync(Email email);
    Task ProcessEmailBatchAsync(List<Email> emails);
    Task ClassifyAndLinkEmailAsync(Email email);
    Task<Application?> DetectApplicationFromEmailAsync(Email email);
    Task<Application?> FindMatchingApplicationAsync(string companyName, string jobTitle);
    Task<Application> CreateApplicationFromEmailAsync(Email email);
}
