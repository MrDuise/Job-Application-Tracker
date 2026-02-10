namespace JobTracker.Core.DTOs;

public class DashboardStatsDto
{
    public int TotalApplications { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public List<RecentActivityDto> RecentActivity { get; set; } = new();
}

public class RecentActivityDto
{
    public int ApplicationId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}
