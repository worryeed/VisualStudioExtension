namespace CodeAI.Api.Models;

public class RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AppUserId { get; init; }  
    public string Token { get; init; } = "";
    public DateTime Expires { get; init; }
    public bool Revoked { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
