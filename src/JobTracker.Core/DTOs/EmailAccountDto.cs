using JobTracker.Core.Enums;

namespace JobTracker.Core.DTOs;

public class EmailAccountDto
{
    public string EmailAddress { get; set; } = string.Empty;
    public string ImapServer { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public EmailAuthType AuthType { get; set; } = EmailAuthType.Password;
}

public class GoogleOAuthCallbackDto
{
    public string Code { get; set; } = string.Empty;
}
