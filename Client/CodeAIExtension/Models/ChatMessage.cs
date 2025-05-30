namespace CodeAIExtension.Models;

public enum Role { User, Assistant, System }

public class ChatMessage
{
    public Role Role { get; }

    public string Content { get; set; }

    public ChatMessage(Role role, string content)
    {
        Role = role;
        Content = content;
    }
}
