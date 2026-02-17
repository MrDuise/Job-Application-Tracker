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
            You are classifying emails to determine if they are related to a job application that the recipient has ALREADY submitted.

            CRITICAL DISTINCTION: Only classify an email as job-related if the recipient has ALREADY applied or is ALREADY in an interview process. Emails inviting someone to apply or suggesting they apply are NOT job-related.

            An email IS job-related if it:
            - Confirms that a job application was received ("thank you for applying", "your application has been submitted", "we received your application")
            - Is a rejection notice ("we decided to move forward with other candidates", "unfortunately we will not be moving forward")
            - Is an interview invitation for a role the recipient already applied to ("we'd like to schedule an interview", "we'd like to invite you to interview")
            - Is a job offer or offer letter
            - Is a status update on an existing application ("your application is under review", "the hiring manager has reviewed your application")
            - Is from an ATS system (Greenhouse, Lever, Workday, iCIMS, SmartRecruiters) confirming an application

            An email is NOT job-related if it:
            - Is a recruiter reaching out to ask the recipient to apply ("I came across your profile", "I think you'd be a great fit", "are you open to new opportunities", "would you be interested in applying")
            - Is a staffing agency or recruiting firm's mass outreach
            - Is a LinkedIn message from a recruiter trying to get the recipient to apply to a new role
            - Is a job board digest or "jobs you might like" email (these are ads, not applications)
            - Is a newsletter, marketing email, or promotional email
            - Is a social media notification
            - Is a receipt, shipping notification, or purchase confirmation
            - Is spam or phishing
            - Suggests "apply now" or "check out this role" — the recipient has NOT applied yet

            Email Content:
            {{emailContent}}

            Respond ONLY with valid JSON (no extra text) in this exact format:
            {"isJobRelated": false, "companyName": null, "jobTitle": null, "status": null, "actionItems": []}

            If the email IS job-related, set isJobRelated to true and fill in the fields:
            - companyName: the hiring company name (must be a real company name, not a placeholder)
            - jobTitle: the job title if mentioned, otherwise null (must be a real job title, not a placeholder)
            - status: one of "applied", "rejected", "interview_request", "offer", "under_review", or "other"
            - actionItems: list of action items or deadlines if any

            If unsure whether the recipient applied or is being asked to apply, set isJobRelated to false.
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
                CompanyName = SanitizeLLMString(parsed.TryGetProperty("companyName", out var cn) ? cn.GetString() : null),
                JobTitle = SanitizeLLMString(parsed.TryGetProperty("jobTitle", out var jt) ? jt.GetString() : null),
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

    /// <summary>
    /// Returns null if the LLM returned a placeholder/type-name instead of a real value.
    /// </summary>
    private static string? SanitizeLLMString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Reject common type-name placeholders that LLMs sometimes emit
        string[] invalidValues =
        [
            "string", "null", "undefined", "n/a", "none", "unknown",
            "not specified", "not mentioned", "not available", "not provided"
        ];

        if (invalidValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
            return null;

        return value.Trim();
    }

    private class OllamaResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}
