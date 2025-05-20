using CodeAI.Api.Exceptions;

namespace CodeAI.Api.Services;

public sealed class OllamaService : IAIService
{
    private const string Endpoint = "api/generate";

    private readonly HttpClient _http;
    private readonly string _modelCode;
    private readonly string _modelInstruct;
    private readonly double _temperature;
    private readonly int _maxTokens;

    public OllamaService(HttpClient httpClient, IConfiguration cfg)
    {
        _http = httpClient;

        var root = cfg.GetSection("Ollama");
        _http.BaseAddress = new Uri(root["BaseUrl"]!);

        _modelCode = root["Model"] ?? "codellama";
        _modelInstruct = root["ModelInstruct"] ?? _modelCode;
        _temperature = root.GetValue("Temperature", 0.2);
        _maxTokens = root.GetValue("MaxTokens", 512);
    }

    public Task<string> GenerateCodeAsync(
        string prompt, string? context, string language, CancellationToken ct) =>
        GenerateAsync(_modelCode, BuildPlainPrompt(prompt, context, language), ct);

    public Task<string> GenerateCodeForChatAsync(
        string prompt, string? context, string language, CancellationToken ct) =>
        GenerateAsync(_modelInstruct, BuildChatPrompt(prompt, context, language), ct);

    public Task<string> GenerateXmlDocAsync(
        string prompt, string? context, string language, CancellationToken ct) =>
        GenerateAsync(_modelInstruct, BuildXmlDocPrompt(prompt, context, language), ct);

    private async Task<string> GenerateAsync(string model, string prompt, CancellationToken ct)
    {
        var payload = new
        {
            model,
            prompt,
            stream = false,
            options = new { temperature = _temperature, max_tokens = _maxTokens }
        };

        var resp = await _http.PostAsJsonAsync(Endpoint, payload, ct);
        if (!resp.IsSuccessStatusCode)
            throw new AiServiceException($"Ollama returned {(int)resp.StatusCode} {resp.StatusCode}");

        var data = await resp.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
        if (data is null || string.IsNullOrWhiteSpace(data.Response))
            throw new AiServiceException("Empty response from Ollama");

        return data.Response.Trim();
    }

    private static string BuildPlainPrompt(string prompt, string? context, string lang) =>
        string.IsNullOrWhiteSpace(context)
            ? $"// language: {lang}\n{prompt}"
            : $"{context}\n\n// ↓ Based on the code above ({lang}), fulfil the requirement:\n{prompt}";

    private static string BuildChatPrompt(string prompt, string? context, string lang) =>
        $"""
        [INST]
        You are a senior {lang} developer. Generate idiomatic, compilable {lang} code that fulfils the requirement below.
        Requirement: {prompt}
        {(string.IsNullOrWhiteSpace(context) ? "" : $"\nContext:\n{context}\n")}
        [/INST]
        """;

    private static string BuildXmlDocPrompt(string prompt, string? context, string lang) =>
        $"""
        [INST]
        Add full documentation comments to the following {lang} code. 
        Use the standard comment style of {lang} and write docs in Russian.
        Code:
        {prompt}
        {(string.IsNullOrWhiteSpace(context) ? "" : $"\nContext:\n{context}")}
        [/INST]
        """;

    private sealed class OllamaResponse { public string Response { get; set; } = ""; }
}