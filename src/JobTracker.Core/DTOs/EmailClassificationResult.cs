namespace JobTracker.Core.DTOs;

public class EmailClassificationResult
{
    public bool IsJobRelated { get; set; }
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Status { get; set; }
    public List<string> ActionItems { get; set; } = new();
}
