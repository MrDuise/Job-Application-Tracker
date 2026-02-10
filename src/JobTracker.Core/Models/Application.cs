using JobTracker.Core.Enums;

namespace JobTracker.Core.Models;

public class Application
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedDate { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? Notes { get; set; }

    public List<Email> RelatedEmails { get; set; } = new();
}
