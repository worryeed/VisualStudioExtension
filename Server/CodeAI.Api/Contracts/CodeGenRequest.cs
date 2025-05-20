namespace CodeAI.Api.Contracts;

public record CodeGenRequest(
Guid RequestId,
string Prompt,
string? Context,
string Language,
double Temperature,
int MaxTokens,
string? UserId);
