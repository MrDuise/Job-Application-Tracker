using System.Net.Http.Json;
using System.Text.Json;
using JobTracker.Core.DTOs;
using JobTracker.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobTracker.Infrastructure.Services.LLM;

public class OllamaLLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLLMService> _logger;
    private readonly string _model;
    private readonly float _temperature;

    public OllamaLLMService(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaLLMService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _model = configuration["Ollama:Model"] ?? "llama3.2";
        _temperature = float.Parse(configuration["Ollama:Temperature"] ?? "0.3");
    }

    public async Task<EmailClassificationResult> ClassifyEmailAsync(string emailContent)
    {
        var prompt = BuildClassificationPrompt(emailContent);
        var response = await SendPromptAsync(prompt);
        return ParseClassificationResponse(response);
    }

    public async Task<bool> IsJobRelatedAsync(string emailContent)
    {
        var result = await ClassifyEmailAsync(emailContent);
        return result.IsJobRelated;
    }

    public async Task<EmailClassificationResult> ExtractApplicationDataAsync(string emailContent)
    {
        return await ClassifyEmailAsync(emailContent);
    }

    private const int MaxEmailContentLength = 3000;

    private string BuildClassificationPrompt(string emailContent)
    {
        // Truncate to avoid overwhelming the local LLM context window
        if (emailContent.Length > MaxEmailContentLength)
            emailContent = emailContent[..MaxEmailContentLength] + "\n[TRUNCATED]";

        return $$"""
            You are classifying emails to determine if they are related to a job application process.

            An email IS job-related if it is:
            - A confirmation that a job application was received
            - A rejection or "we decided to move forward with other candidates" notice
            - An interview invitation or scheduling email
            - A job offer or offer letter
            - A follow-up from a recruiter about a specific role
            - A status update on a job application

            An email is NOT job-related if it is:
            - An advertisement, promotion, or marketing email
            - A newsletter or mailing list email
            - A social media notification
            - A receipt, shipping notification, or purchase confirmation
            - Spam or phishing
            - A job board digest or "jobs you might like" blast (these are ads, not applications)
            - Any email with "unsubscribe" language that is clearly bulk/marketing

            Email Content:
            {{emailContent}}

            Respond ONLY with valid JSON (no extra text) in this exact format:
            {"isJobRelated": false, "companyName": null, "jobTitle": null, "status": null, "actionItems": []}

            If the email IS job-related, set isJobRelated to true and fill in the fields:
            - companyName: the hiring company name
            - jobTitle: the job title if mentioned, otherwise null
            - status: one of "applied", "rejected", "interview_request", "offer", "under_review", or "other"
            - actionItems: list of action items or deadlines if any
            """;
    }

    private async Task<string> SendPromptAsync(string prompt)
    {
        var request = new
        {
            model = _model,
            prompt,
            stream = false,
            options = new { temperature = _temperature }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
            return result?.Response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get response from Ollama");
            return string.Empty;
        }
    }

    private EmailClassificationResult ParseClassificationResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd < 0)
                return new EmailClassificationResult { IsJobRelated = false };

            var json = response[jsonStart..(jsonEnd + 1)];
            var parsed = JsonSerializer.Deserialize<JsonElement>(json);

            return new EmailClassificationResult
            {
                IsJobRelated = parsed.GetProperty("isJobRelated").GetBoolean(),
                CompanyName = parsed.TryGetProperty("companyName", out var cn) ? cn.GetString() : null,
                JobTitle = parsed.TryGetProperty("jobTitle", out var jt) ? jt.GetString() : null,
                Status = parsed.TryGetProperty("status", out var s) ? s.GetString() : null,
                ActionItems = parsed.TryGetProperty("actionItems", out var ai)
                    ? ai.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList()
                    : new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM classification response");
            return new EmailClassificationResult { IsJobRelated = false };
        }
    }

    private class OllamaResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}
