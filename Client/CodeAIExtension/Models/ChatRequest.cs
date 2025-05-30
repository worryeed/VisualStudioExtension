namespace CodeAIExtension.Models;

public class ChatRequest
{
	public string Prompt { get; set; }

	public string Context { get; set; }

	public string Language { get; set; }

	public double Temperature { get; set; }

	public int MaxTokens { get; set; }
}