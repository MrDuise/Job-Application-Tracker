using System.Net;
using System.Text.RegularExpressions;
using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace JobTracker.Infrastructure.Services.EmailClient;

public class MailKitEmailService : IEmailService, IDisposable
{
    private readonly ILogger<MailKitEmailService> _logger;
    private ImapClient? _client;
    private EmailAccount? _account;

    public MailKitEmailService(ILogger<MailKitEmailService> logger)
    {
        _logger = logger;
    }

    public void Configure(EmailAccount account)
    {
        _account = account;
    }

    public async Task ConnectAsync()
    {
        if (_account is null)
            throw new InvalidOperationException("Email account not configured. Call Configure() first.");

        _client = new ImapClient();
        await _client.ConnectAsync(_account.ImapServer, _account.ImapPort, SecureSocketOptions.SslOnConnect);

        if (_account.AuthType == EmailAuthType.GoogleOAuth && !string.IsNullOrEmpty(_account.AccessToken))
        {
            var oauth2 = new SaslMechanismOAuth2(_account.EmailAddress, _account.AccessToken);
            await _client.AuthenticateAsync(oauth2);
            _logger.LogInformation("Connected to IMAP server {Server} using OAuth2", _account.ImapServer);
        }
        else
        {
            await _client.AuthenticateAsync(_account.Username, _account.EncryptedPassword);
            _logger.LogInformation("Connected to IMAP server {Server}", _account.ImapServer);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_client is { IsConnected: true })
        {
            await _client.DisconnectAsync(true);
            _logger.LogInformation("Disconnected from IMAP server");
        }
    }

    public async Task<bool> TestConnectionAsync(EmailAccount account)
    {
        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(account.ImapServer, account.ImapPort, SecureSocketOptions.SslOnConnect);

            if (account.AuthType == EmailAuthType.GoogleOAuth && !string.IsNullOrEmpty(account.AccessToken))
            {
                var oauth2 = new SaslMechanismOAuth2(account.EmailAddress, account.AccessToken);
                await client.AuthenticateAsync(oauth2);
            }
            else
            {
                await client.AuthenticateAsync(account.Username, account.EncryptedPassword);
            }

            await client.DisconnectAsync(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email connection test failed for {Email}", account.EmailAddress);
            return false;
        }
    }

    public async Task<List<Email>> FetchNewEmailsAsync()
    {
        return await FetchEmailsSinceAsync(DateTime.UtcNow.AddDays(-1));
    }

    public async Task<List<Email>> FetchEmailsSinceAsync(DateTime since)
    {
        if (_client is null || !_client.IsConnected)
            await ConnectAsync();

        var inbox = _client!.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly);

        // Build keyword filter to only fetch likely job-related emails
        var dateQuery = SearchQuery.DeliveredAfter(since);
        var keywordQuery = BuildJobKeywordQuery();
        var query = dateQuery.And(keywordQuery);

        var uids = await inbox.SearchAsync(query);
        _logger.LogInformation("IMAP search matched {Count} emails since {Since}", uids.Count, since);

        var emails = new List<Email>();
        foreach (var uid in uids)
        {
            var message = await inbox.GetMessageAsync(uid);
            emails.Add(ConvertToEmail(message, uid.ToString()));
        }

        _logger.LogInformation("Fetched {Count} emails since {Since}", emails.Count, since);
        return emails;
    }

    private static SearchQuery BuildJobKeywordQuery()
    {
        // Common terms found in job application emails (subject OR body)
        string[] keywords =
        [
            "application", "interview", "position", "offer",
            "candidate", "resume", "hiring", "recruiter",
            "applied", "rejected", "opportunity", "job"
        ];

        SearchQuery? combined = null;
        foreach (var keyword in keywords)
        {
            var subjectMatch = SearchQuery.SubjectContains(keyword);
            var match = combined is null ? subjectMatch : combined.Or(subjectMatch);
            combined = match;
        }

        return combined!;
    }

    public async Task<Email?> GetEmailByIdAsync(string emailId)
    {
        if (_client is null || !_client.IsConnected)
            await ConnectAsync();

        var inbox = _client!.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly);

        if (!uint.TryParse(emailId, out var uidValue))
            return null;

        var uid = new UniqueId(uidValue);
        try
        {
            var message = await inbox.GetMessageAsync(uid);
            return ConvertToEmail(message, emailId);
        }
        catch
        {
            return null;
        }
    }

    public async Task MarkAsReadAsync(string emailId)
    {
        if (_client is null || !_client.IsConnected)
            await ConnectAsync();

        var inbox = _client!.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite);

        if (uint.TryParse(emailId, out var uidValue))
        {
            var uid = new UniqueId(uidValue);
            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true);
        }
    }

    private static Email ConvertToEmail(MimeMessage message, string uid)
    {
        var body = message.TextBody;
        if (string.IsNullOrEmpty(body) && !string.IsNullOrEmpty(message.HtmlBody))
        {
            body = StripHtml(message.HtmlBody);
        }

        return new Email
        {
            Id = uid,
            Subject = message.Subject ?? string.Empty,
            From = message.From.ToString(),
            To = message.To.ToString(),
            Body = body ?? string.Empty,
            ReceivedDate = message.Date.UtcDateTime,
            IsRead = false,
            ThreadId = message.Headers["In-Reply-To"] ?? message.Headers["References"]
        };
    }

    private static string StripHtml(string html)
    {
        // Remove style and script blocks entirely
        var cleaned = Regex.Replace(html, @"<(style|script)[^>]*>[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        // Replace block-level tags with newlines for readability
        cleaned = Regex.Replace(cleaned, @"<(br|p|div|tr|li|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        // Remove all remaining HTML tags
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", " ");
        // Decode HTML entities
        cleaned = WebUtility.HtmlDecode(cleaned);
        // Collapse whitespace
        cleaned = Regex.Replace(cleaned, @"[ \t]+", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        return cleaned.Trim();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
