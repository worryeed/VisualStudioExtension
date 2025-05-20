namespace CodeAI.Api.Contracts;

public record CodeGenResult(
    Guid RequestId,
    string Completion,
    DateTime GeneratedAt);
