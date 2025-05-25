using CodeAI.Api.Exceptions;
using CodeAI.Api.Models;
using CodeAI.Api.Prompts;
using System.Runtime.CompilerServices;
using System.Text.Json;

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
        List<ChatMessage> history,
        CancellationToken ct);

    IAsyncEnumerable<string> StreamChatResponseAsync(
        string prompt, 
        string? context,
        string language,
        List<ChatMessage> history, 
        CancellationToken ct);

    Task<string> GenerateXmlDocAsync(
        string prompt,
        string? context,
        string language,
        CancellationToken ct);
}

public sealed class OllamaService : IAIService
{
    private const string GenerateEndpoint = "/api/generate";
    private const string ChatEndpoint = "/api/chat";

    private readonly string _modelCode;  
    private readonly string _modelInstruct;

    private readonly HttpClient _http;
    private readonly ILogger<OllamaService> _log;

    public double Temperature { get; private set; }
    public int MaxTokens { get; private set; }

    public OllamaService(IHttpClientFactory clientFactory, IConfiguration cfg, ILogger<OllamaService> log)
    {
        _http = clientFactory.CreateClient("AIService");
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
        return CallGenerateAutoCompleteAsync(_modelCode, prompt, ct);
    }

    public Task<string> GenerateXmlDocAsync(
        string question,
        string? ctx,
        string lang,
        CancellationToken ct = default)
    {
        var prompt = PromptFactory.XmlDoc(question, ctx, lang);
        return CallGenerateXmlDocAsync(_modelInstruct, prompt, ct);
    }

    public Task<string> GenerateChatResponseAsync(
        string prompt,
        string? ctx,
        string lang, 
        List<ChatMessage> history,
        CancellationToken ct = default)
    {
        var messages = PromptFactory.Chat(prompt, ctx, history, lang);
        return CallChatAsync(_modelInstruct, messages, ct);
    }

    public async IAsyncEnumerable<string> StreamChatResponseAsync(
        string prompt, 
        string? ctx, 
        string lang, 
        List<ChatMessage> hist,
        [EnumeratorCancellation]
        CancellationToken ct = default)
    {
        var messages = PromptFactory.Chat(prompt, ctx, hist, lang);
        await foreach(var chunk in CallChatStreamAsync(_modelInstruct, messages, ct))
        {
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }
    }

    private async Task<string> CallGenerateAutoCompleteAsync(
        string model,
        string prompt,
        CancellationToken ct)
    {
        _log.LogDebug("OLLAMA /generate  model={Model}, len={Len}", model, prompt.Length);

        string? suffix = null;
        var body = new
        {
            model,
            prompt,
            suffix,       
            raw = true,
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

    private async Task<string> CallGenerateXmlDocAsync(
        string model,
        string prompt,
        CancellationToken ct)
    {
        _log.LogDebug("OLLAMA /generate  model={Model}, len={Len}", model, prompt.Length);

        var body = new
        {
            model,
            prompt,
            stream = false,
            options = new
            {
                temperature = Temperature,
                num_predict = MaxTokens
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
        List<ChatMessage> messages,
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

    private async IAsyncEnumerable<string> CallChatStreamAsync(
        string model, List<ChatMessage> msgs, [EnumeratorCancellation] CancellationToken ct)
    {
        var body = new
        {
            model,
            messages = msgs,
            stream = true,
            options = new { temperature = Temperature, num_predict = MaxTokens }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, ChatEndpoint)
        {
            Content = JsonContent.Create(body)
        };

        using var resp = await _http.SendAsync(
            req,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (!resp.IsSuccessStatusCode)
            throw new AiServiceException($"Ollama {(int)resp.StatusCode} {resp.ReasonPhrase}");

        await foreach (var chunk in ReadChunks<CChunk>(resp, ct))
        {
            if (!string.IsNullOrEmpty(chunk.Message?.Content))
            {
                yield return chunk.Message.Content;
            }
        }
    }

    private async IAsyncEnumerable<T> ReadChunks<T>(HttpResponseMessage resp,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var sr = new StreamReader(stream);
        string? line;
        while ((line = await sr.ReadLineAsync()) is not null && !ct.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var pkt = JsonSerializer.Deserialize<T>(line, _json);
            if (pkt is null) continue;

            if (pkt is IDone d && d.Done) yield break;
            yield return pkt;
        }
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

    private interface IDone { bool Done { get; } }

    private sealed class GChunk : IDone
    {
        public string? Response { get; init; }
        public bool Done { get; init; }
    }

    private sealed class CChunk : IDone
    {
        public Inner? Message { get; init; }
        public bool Done { get; init; }

        public sealed class Inner { public string? Content { get; init; } }
    }

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
}