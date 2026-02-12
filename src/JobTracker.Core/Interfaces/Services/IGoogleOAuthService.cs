using JobTracker.Core.Models;

namespace JobTracker.Core.Interfaces.Services;

public interface IGoogleOAuthService
{
    Task<EmailAccount> ExchangeCodeAsync(string authorizationCode);
    Task<string> RefreshAccessTokenAsync(string encryptedRefreshToken);
}
