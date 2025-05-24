using CodeAI.Api.Models;

namespace CodeAI.Api.Contracts;

public enum CodeGenKind { Code, Chat, Docs }

public record CodeGenRequest(
    Guid RequestId,
    CodeGenKind Kind,
    string Prompt,
    string? Context,
    string Language,
    double Temperature,
    int MaxTokens,
    string UserId,
    List<ChatMessage>? History = null);