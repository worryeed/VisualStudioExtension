namespace CodeAI.Api.Models;

public class AppUser
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
