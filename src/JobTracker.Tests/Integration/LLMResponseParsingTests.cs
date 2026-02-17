using System.Net;
using System.Text.Json;
using JobTracker.Infrastructure.Services.LLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobTracker.Tests.Integration;

/// <summary>
/// Tests for OllamaLLMService's JSON parsing and sanitization logic.
/// Uses a mock HttpMessageHandler to inject canned Ollama API responses,
/// so we can test parsing without a running Ollama instance.
/// </summary>
public class LLMResponseParsingTests
{
    private OllamaLLMService CreateService(string ollamaResponseJson)
    {
        // Wrap the raw JSON in an Ollama API response envelope
        var ollamaEnvelope = JsonSerializer.Serialize(new { response = ollamaResponseJson });

        var handler = new FakeHttpMessageHandler(ollamaEnvelope);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:Model"] = "llama3.2",
                ["Ollama:Temperature"] = "0.3"
            })
            .Build();

        var logger = new Mock<ILogger<OllamaLLMService>>();
        return new OllamaLLMService(httpClient, config, logger.Object);
    }

    // ---------------------------------------------------------------
    // Basic JSON parsing
    // ---------------------------------------------------------------

    [Fact]
    public async Task ParsesValidJobRelatedResponse()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "Google", "jobTitle": "Software Engineer", "status": "applied", "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test email content");

        Assert.True(result.IsJobRelated);
        Assert.Equal("Google", result.CompanyName);
        Assert.Equal("Software Engineer", result.JobTitle);
        Assert.Equal("applied", result.Status);
    }

    [Fact]
    public async Task ParsesValidNonJobRelatedResponse()
    {
        var service = CreateService(
            """{"isJobRelated": false, "companyName": null, "jobTitle": null, "status": null, "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test email content");

        Assert.False(result.IsJobRelated);
        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task ParsesResponseWithExtraTextAroundJson()
    {
        var service = CreateService(
            """Here is the classification:\n{"isJobRelated": true, "companyName": "Meta", "jobTitle": "Product Manager", "status": "interview_request", "actionItems": ["Schedule interview by Friday"]}\nI hope this helps!""");

        var result = await service.ClassifyEmailAsync("test email");

        Assert.True(result.IsJobRelated);
        Assert.Equal("Meta", result.CompanyName);
        Assert.Equal("Product Manager", result.JobTitle);
        Assert.Equal("interview_request", result.Status);
        Assert.Contains("Schedule interview by Friday", result.ActionItems);
    }

    [Fact]
    public async Task ParsesResponseWithActionItems()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "Amazon", "jobTitle": "SDE II", "status": "interview_request", "actionItems": ["Complete OA by March 15", "Upload resume to portal"]}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Equal(2, result.ActionItems.Count);
        Assert.Contains("Complete OA by March 15", result.ActionItems);
        Assert.Contains("Upload resume to portal", result.ActionItems);
    }

    // ---------------------------------------------------------------
    // Placeholder / type-name sanitization — the "String" bug
    // ---------------------------------------------------------------

    [Fact]
    public async Task SanitizesLiteralStringAsCompanyName()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "String", "jobTitle": "String", "status": "applied", "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task SanitizesLowercaseStringPlaceholder()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "string", "jobTitle": "string", "status": "applied", "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task SanitizesNullStringLiteral()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "null", "jobTitle": "null", "status": "applied", "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task SanitizesUndefinedPlaceholder()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "undefined", "jobTitle": "undefined", "status": null, "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task SanitizesNAPlaceholder()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "N/A", "jobTitle": "N/A", "status": null, "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task SanitizesNonePlaceholder()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "none", "jobTitle": "None", "status": null, "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task SanitizesUnknownPlaceholder()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "unknown", "jobTitle": "Unknown", "status": null, "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task SanitizesNotSpecifiedPlaceholder()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "not specified", "jobTitle": "Not Mentioned", "status": null, "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task SanitizesWhitespaceOnlyValues()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "   ", "jobTitle": "\t", "status": null, "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
    }

    [Fact]
    public async Task DoesNotSanitizeRealCompanyNames()
    {
        // "None" is a placeholder, but real companies with "None" in the name?
        // More importantly, real company names should pass through fine.
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "Netflix", "jobTitle": "Senior Engineer", "status": "applied", "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Equal("Netflix", result.CompanyName);
        Assert.Equal("Senior Engineer", result.JobTitle);
    }

    [Fact]
    public async Task TrimsWhitespaceFromValidValues()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "  Google  ", "jobTitle": "  SWE  ", "status": "applied", "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Equal("Google", result.CompanyName);
        Assert.Equal("SWE", result.JobTitle);
    }

    // ---------------------------------------------------------------
    // Malformed / edge-case responses
    // ---------------------------------------------------------------

    [Fact]
    public async Task HandlesEmptyResponse()
    {
        var service = CreateService("");

        var result = await service.ClassifyEmailAsync("test");

        Assert.False(result.IsJobRelated);
    }

    [Fact]
    public async Task HandlesResponseWithNoJson()
    {
        var service = CreateService("I cannot classify this email because the content is unclear.");

        var result = await service.ClassifyEmailAsync("test");

        Assert.False(result.IsJobRelated);
    }

    [Fact]
    public async Task HandlesBrokenJson()
    {
        var service = CreateService("""{"isJobRelated": true, "companyName": "Google", "jobTitle":""");

        var result = await service.ClassifyEmailAsync("test");

        // Should fall back to non-job-related rather than crash
        Assert.False(result.IsJobRelated);
    }

    [Fact]
    public async Task HandlesMissingFieldsGracefully()
    {
        var service = CreateService("""{"isJobRelated": true}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.True(result.IsJobRelated);
        Assert.Null(result.CompanyName);
        Assert.Null(result.JobTitle);
        Assert.Null(result.Status);
    }

    [Fact]
    public async Task HandlesEmptyActionItemsArray()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "Stripe", "jobTitle": "Backend Engineer", "status": "applied", "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Empty(result.ActionItems);
    }

    [Fact]
    public async Task HandlesMissingActionItemsField()
    {
        var service = CreateService(
            """{"isJobRelated": true, "companyName": "Stripe", "jobTitle": "Backend Engineer", "status": "applied"}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Empty(result.ActionItems);
    }

    // ---------------------------------------------------------------
    // Status mapping variations that the LLM might produce
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("applied")]
    [InlineData("Applied")]
    [InlineData("APPLIED")]
    public async Task ParsesAppliedStatusVariations(string status)
    {
        var service = CreateService(
            $$$"""{"isJobRelated": true, "companyName": "Test Co", "jobTitle": "Dev", "status": "{{{status}}}", "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Equal(status, result.Status);
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("interview_request")]
    [InlineData("offer")]
    [InlineData("under_review")]
    [InlineData("other")]
    public async Task ParsesAllValidStatuses(string status)
    {
        var service = CreateService(
            $$$"""{"isJobRelated": true, "companyName": "Test Co", "jobTitle": "Dev", "status": "{{{status}}}", "actionItems": []}""");

        var result = await service.ClassifyEmailAsync("test");

        Assert.Equal(status, result.Status);
    }

    // ---------------------------------------------------------------
    // Helper: fake HTTP handler to mock Ollama API
    // ---------------------------------------------------------------

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public FakeHttpMessageHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
