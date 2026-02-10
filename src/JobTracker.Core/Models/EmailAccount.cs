namespace JobTracker.Core.Models;

public class EmailAccount
{
    public int Id { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string ImapServer { get; set; } = string.Empty;
    public int ImapPort { get; set; }
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public DateTime? LastSyncDate { get; set; }
}
