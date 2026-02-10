namespace JobTracker.Core.Models;

public class Email
{
    public string Id { get; set; } = string.Empty;
    public int? ApplicationId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public bool IsRead { get; set; }
    public string? ThreadId { get; set; }

    public Application? Application { get; set; }
}
