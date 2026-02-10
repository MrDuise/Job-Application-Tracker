using JobTracker.Core.Enums;

namespace JobTracker.Core.DTOs;

public class UpdateApplicationDto
{
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public ApplicationStatus? Status { get; set; }
    public string? Notes { get; set; }
}
