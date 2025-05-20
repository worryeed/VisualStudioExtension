namespace CodeAI.Api.Models;

public class QueryHistory
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? AppUserId { get; set; }
    public string Prompt { get; set; } = "";
    public string Response { get; set; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public AppUser? AppUser { get; set; }
}
