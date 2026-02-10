using JobTracker.Core.Enums;

namespace JobTracker.Core.DTOs;

public class ApplicationDto
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedDate { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? Notes { get; set; }
    public int EmailCount { get; set; }
}
