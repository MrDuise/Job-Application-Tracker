using JobTracker.Core.DTOs;

namespace JobTracker.Core.Interfaces.Services;

public interface ILLMService
{
    Task<EmailClassificationResult> ClassifyEmailAsync(string emailContent);
    Task<bool> IsJobRelatedAsync(string emailContent);
    Task<EmailClassificationResult> ExtractApplicationDataAsync(string emailContent);
}
