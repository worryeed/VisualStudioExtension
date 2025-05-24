namespace CodeAI.Api.Services;

public interface IAIService
{
    void SetSettings(double temperature, int maxTokens);
    Task<string> GenerateAutoCompleteAsync(
        string prompt,
        string? context,
        string language,
        CancellationToken ct);

    Task<string> GenerateChatResponseAsync(
        string prompt,
        string? context,
        string language,
        CancellationToken ct);

    Task<string> GenerateXmlDocAsync(
        string prompt,
        string? context,
        string language,
        CancellationToken ct);
}
