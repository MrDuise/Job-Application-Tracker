namespace JobTracker.Core.DTOs;

public class EmailAccountDto
{
    public string EmailAddress { get; set; } = string.Empty;
}

public class GoogleOAuthCallbackDto
{
    public string Code { get; set; } = string.Empty;
}
