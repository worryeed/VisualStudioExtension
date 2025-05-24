using CodeAI.Api.Exceptions;
using CodeAI.Api.Prompts;

namespace CodeAI.Api.Services;

public sealed class OllamaService : IAIService
{
    private const string GenerateEndpoint = "/api/generate";
    private const string ChatEndpoint = "/api/chat";

    private readonly HttpClient _http;
    private readonly ILogger<OllamaService> _log;
    private readonly string _modelCode;  
    private readonly string _modelInstruct;

    public double Temperature { get; private set; }
    public int MaxTokens { get; private set; }

    public OllamaService(HttpClient http, IConfiguration cfg, ILogger<OllamaService> log)
    {
        _http = http;
        _log = log;

        var s = cfg.GetSection("Ollama");

        _http.BaseAddress = new Uri(s["BaseUrl"] ?? "http://localhost:11434/");
        _modelCode = s["Model"] ?? "qwen2.5-coder:7b";
        _modelInstruct = s["ModelInstruct"] ?? $"{_modelCode}:instruct";

        Temperature = s.GetValue("Temperature", 0.7);
        MaxTokens = s.GetValue("MaxTokens", 2048);
    }

    public void SetSettings(double temperature, int maxTokens)
    {
        if (temperature is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0 и 1.");
        if (maxTokens is < 16 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "MaxTokens must be 16…4096.");

        Temperature = temperature;
        MaxTokens = maxTokens;
    }

    public Task<string> GenerateAutoCompleteAsync(
        string prefix,
        string? ctx,
        string lang,
        CancellationToken ct = default)
    {
        (string prompt, bool raw) = PromptFactory.AutoComplete(prefix, ctx, lang);
        return CallGenerateAsync(_modelCode, prompt, suffix: null, raw: true, ct);
    }

    public Task<string> GenerateXmlDocAsync(
        string question,
        string? ctx,
        string lang,
        CancellationToken ct = default)
    {
        var prompt = PromptFactory.XmlDoc(question, ctx, lang);
        return CallGenerateAsync(_modelInstruct, prompt, suffix: null, raw: false, ct);
    }

    public Task<string> GenerateChatResponseAsync(
        string question,
        string? ctx,
        string lang,
        CancellationToken ct = default)
    {
        var messages = PromptFactory.Chat(question, ctx, lang);
        return CallChatAsync(_modelInstruct, messages, ct);
    }

    private async Task<string> CallGenerateAsync(
        string model,
        string prompt,
        string? suffix,
        bool raw,
        CancellationToken ct)
    {
        _log.LogDebug("OLLAMA /generate  model={Model}, len={Len}, raw={Raw}", model, prompt.Length, raw);

        var body = new
        {
            model,
            prompt,
            suffix,       
            raw,  
            stream = false,
            options = new 
            { 
                temperature = Temperature, 
                num_predict = MaxTokens,
                stop = new[] { "```" }
            }
        };

        using var resp = await _http.PostAsJsonAsync(GenerateEndpoint, body, ct);

        if (!resp.IsSuccessStatusCode)
            throw new AiServiceException($"Ollama {(int)resp.StatusCode} {resp.ReasonPhrase}");

        var data = await resp.Content.ReadFromJsonAsync<OGenerateResponse>(cancellationToken: ct);
        return Extract(data?.Response);
    }

    private async Task<string> CallChatAsync(
        string model,
        object messages,
        CancellationToken ct)
    {
        _log.LogDebug("OLLAMA /chat      model={Model}", model);

        var body = new
        {
            model,
            messages,
            stream = false,
            options = new { temperature = Temperature, num_predict = MaxTokens }
        };

        using var resp = await _http.PostAsJsonAsync(ChatEndpoint, body, ct);

        if (!resp.IsSuccessStatusCode)
            throw new AiServiceException($"Ollama {(int)resp.StatusCode} {resp.ReasonPhrase}");

        var data = await resp.Content.ReadFromJsonAsync<OChatResponse>(cancellationToken: ct);
        return Extract(data?.Message.Content);
    }

    private static string Extract(string? txt) =>
        string.IsNullOrWhiteSpace(txt)
            ? throw new AiServiceException("Empty response from Ollama.")
            : txt.Trim();

    private class OGenerateResponse
    {
        public string Response { get; init; } = string.Empty;
    }

    private class OChatResponse
    {
        public ChatMessage Message { get; set; } = null!;
    }
}