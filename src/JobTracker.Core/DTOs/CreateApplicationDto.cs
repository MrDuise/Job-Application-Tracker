using JobTracker.Core.Enums;

namespace JobTracker.Core.DTOs;

public class CreateApplicationDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Applied;
    public DateTime? AppliedDate { get; set; }
    public string? Notes { get; set; }
}
