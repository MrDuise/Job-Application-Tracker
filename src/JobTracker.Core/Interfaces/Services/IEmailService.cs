using System.Threading.Channels;
using JobTracker.Core.Models;

namespace JobTracker.Core.Interfaces.Services;

public interface IEmailService
{
    Task ConnectAsync();
    Task DisconnectAsync();
    Task FetchAndStreamEmailsAsync(DateTime since, ChannelWriter<Email> channel, Action<int> onTotalKnown, CancellationToken ct);
    Task<Email?> GetEmailByIdAsync(string emailId);
    Task MarkAsReadAsync(string emailId);
    void Configure(EmailAccount account);
    void SetKnownSenderDomains(HashSet<string> domains);
}
