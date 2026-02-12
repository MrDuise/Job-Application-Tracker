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

    private string BuildClassificationPrompt(string emailContent)
    {
        return $$"""
            Analyze this email and determine:
            1. Is this job application related? (true/false)
            2. Company name (extract from sender or body)
            3. Job title (if mentioned)
            4. Status: applied/rejected/interview_request/offer/other
            5. Any action items or deadlines

            Email Content:
            {{emailContent}}

            Respond ONLY with JSON in this format:
            {"isJobRelated": true, "companyName": "Company Name", "jobTitle": "Software Engineer", "status": "applied", "actionItems": []}
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
