using System.Text.Json;
using System.Text.Json.Serialization;
using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobTracker.Infrastructure.Services;

public class GoogleOAuthService : IGoogleOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IDataProtector _protector;
    private readonly ILogger<GoogleOAuthService> _logger;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    public GoogleOAuthService(
        HttpClient httpClient,
        IDataProtectionProvider dataProtectionProvider,
        IConfiguration configuration,
        ILogger<GoogleOAuthService> logger)
    {
        _httpClient = httpClient;
        _protector = dataProtectionProvider.CreateProtector("EmailAccount.Password");
        _logger = logger;
        _clientId = configuration["Google:ClientId"] ?? "";
        _clientSecret = configuration["Google:ClientSecret"] ?? "";
    }

    public async Task<EmailAccount> ExchangeCodeAsync(string authorizationCode)
    {
        var tokenRequest = new Dictionary<string, string>
        {
            ["code"] = authorizationCode,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = "postmessage",
            ["grant_type"] = "authorization_code",
        };

        var response = await _httpClient.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(tokenRequest));

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google token exchange failed: {Response}", json);
            throw new InvalidOperationException($"Google OAuth token exchange failed: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(json)
            ?? throw new InvalidOperationException("Failed to parse Google token response");

        // Get the user's email address from the ID token or userinfo
        var email = await GetUserEmailAsync(tokenResponse.AccessToken);

        var account = new EmailAccount
        {
            EmailAddress = email,
            ImapServer = "imap.gmail.com",
            ImapPort = 993,
            Username = email,
            EncryptedPassword = string.Empty,
            AuthType = EmailAuthType.GoogleOAuth,
            AccessToken = tokenResponse.AccessToken,
            TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
        };

        if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            account.EncryptedRefreshToken = _protector.Protect(tokenResponse.RefreshToken);
        }

        return account;
    }

    public async Task<string> RefreshAccessTokenAsync(string encryptedRefreshToken)
    {
        var refreshToken = _protector.Unprotect(encryptedRefreshToken);

        var refreshRequest = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        };

        var response = await _httpClient.PostAsync(
            TokenEndpoint,
            new FormUrlEncodedContent(refreshRequest));

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google token refresh failed: {Response}", json);
            throw new InvalidOperationException($"Google OAuth token refresh failed: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(json)
            ?? throw new InvalidOperationException("Failed to parse Google token refresh response");

        return tokenResponse.AccessToken;
    }

    private async Task<string> GetUserEmailAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get user info from Google: {Response}", json);
            throw new InvalidOperationException("Failed to get user email from Google");
        }

        var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(json);
        return userInfo?.Email ?? throw new InvalidOperationException("No email in Google user info");
    }

    private class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    private class GoogleUserInfo
    {
        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}
