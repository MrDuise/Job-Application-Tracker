using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Channels;
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
    private HashSet<string> _knownSenderDomains = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] AtsDomains =
    [
        "greenhouse.io", "lever.co", "myworkday.com",
        "icims.com", "smartrecruiters.com", "jobvite.com",
        "ashbyhq.com", "breezy.hr",
        "indeed.com", "glassdoor.com"
    ];

    private static readonly string[] SubjectSignals =
    [
        "application", "interview", "applied", "offer",
        "candidate", "rejected", "phone screen", "assessment",
        "your candidacy", "offer letter", "onsite", "on-site"
    ];

    public MailKitEmailService(ILogger<MailKitEmailService> logger)
    {
        _logger = logger;
    }

    public void Configure(EmailAccount account)
    {
        _account = account;
    }

    public void SetKnownSenderDomains(HashSet<string> domains)
    {
        _knownSenderDomains = domains;
    }

    public async Task ConnectAsync()
    {
        if (_account is null)
            throw new InvalidOperationException("Email account not configured. Call Configure() first.");

        _client = new ImapClient();
        await _client.ConnectAsync("imap.gmail.com", 993, SecureSocketOptions.SslOnConnect);

        var oauth2 = new SaslMechanismOAuth2(_account.EmailAddress, _account.AccessToken);
        await _client.AuthenticateAsync(oauth2);
        _logger.LogInformation("Connected to Gmail via OAuth2 for {Email}", _account.EmailAddress);
    }

    public async Task DisconnectAsync()
    {
        if (_client is { IsConnected: true })
        {
            await _client.DisconnectAsync(true);
            _logger.LogInformation("Disconnected from Gmail");
        }
    }

    public async Task FetchAndStreamEmailsAsync(
        DateTime since,
        ChannelWriter<Email> channel,
        Action<int> onTotalKnown,
        CancellationToken ct)
    {
        if (_client is null || !_client.IsConnected)
            await ConnectAsync();

        var inbox = _client!.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        // Phase 1: Gmail search → UIDs
        var rawQuery = BuildGmailRawQuery(since);
        var query = SearchQuery.GMailRawSearch(rawQuery);
        _logger.LogInformation("Gmail search: {Query}", rawQuery);

        var uids = await inbox.SearchAsync(query, ct);
        _logger.LogInformation("Gmail search matched {Count} UIDs", uids.Count);

        if (uids.Count == 0)
        {
            onTotalKnown(0);
            return;
        }

        // Phase 2: Bulk header fetch (single IMAP round-trip)
        var summaries = await inbox.FetchAsync(
            uids,
            MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId,
            ct);

        // Phase 3: Header pre-filter
        var passed = summaries.Where(PassesHeaderFilter).ToList();
        _logger.LogInformation("Pre-filter passed {Passed}/{Total} emails", passed.Count, summaries.Count);
        onTotalKnown(passed.Count);

        // Phase 4: Download bodies for filtered set, stream to channel
        foreach (var summary in passed)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var message = await inbox.GetMessageAsync(summary.UniqueId, ct);
                var email = ConvertToEmail(message, summary.UniqueId.ToString());
                await channel.WriteAsync(email, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download email UID {Uid}, skipping", summary.UniqueId);
            }
        }
    }

    private bool PassesHeaderFilter(IMessageSummary summary)
    {
        var from = summary.Envelope.From?.ToString() ?? string.Empty;
        var subject = summary.Envelope.Subject ?? string.Empty;

        var domain = ExtractDomain(from);

        // Known sender passthrough — company already in DB
        if (domain is not null && _knownSenderDomains.Contains(domain))
            return true;

        // ATS domain match
        if (domain is not null && AtsDomains.Any(ats => domain.EndsWith(ats, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Subject signal match
        var subjectLower = subject.ToLowerInvariant();
        if (SubjectSignals.Any(signal => subjectLower.Contains(signal)))
            return true;

        return false;
    }

    private static string? ExtractDomain(string from)
    {
        var atIndex = from.LastIndexOf('@');
        if (atIndex < 0) return null;

        var rest = from[(atIndex + 1)..];
        // Strip trailing '>' if present (e.g. "name <user@domain.com>")
        var endIndex = rest.IndexOf('>');
        if (endIndex >= 0)
            rest = rest[..endIndex];

        return rest.Trim().ToLowerInvariant();
    }

    private static string BuildGmailRawQuery(DateTime since)
    {
        var dateFilter = $"after:{since:yyyy/MM/dd}";

        var clauses = new[]
        {
            "\"your application\"",
            "\"thank you for applying\"",
            "\"application received\"",
            "\"application submitted\"",
            "\"we received your application\"",
            "\"interview scheduled\"",
            "\"interview invitation\"",
            "\"phone screen\"",
            "\"moved forward with other\"",
            "\"offer letter\"",
            "\"pleased to offer\"",
            "\"you applied for\"",
            "\"technical assessment\"",
            "applied",
            "rejected",
            "from:@greenhouse.io",
            "from:@lever.co",
            "from:@myworkday.com",
            "from:@icims.com",
            "from:@smartrecruiters.com",
            "from:@jobvite.com",
            "from:@ashbyhq.com",
            "from:@breezy.hr",
            "from:@indeed.com",
            "from:@glassdoor.com"
        };

        return $"{dateFilter} ({string.Join(" OR ", clauses)})";
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
        var cleaned = Regex.Replace(html, @"<(style|script)[^>]*>[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<(br|p|div|tr|li|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", " ");
        cleaned = WebUtility.HtmlDecode(cleaned);
        cleaned = Regex.Replace(cleaned, @"[ \t]+", " ");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        return cleaned.Trim();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
