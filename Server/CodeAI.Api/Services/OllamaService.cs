namespace CodeAI.Api.Services;

public class OllamaService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public OllamaService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;

        _httpClient.BaseAddress = new Uri(_config["Ollama:BaseUrl"]!);
    }

    public async Task<string> GenerateCodeAsync(string prompt, string context, CancellationToken ct)
    {
        try
        {
            var request = new
            {
                model = _config["Ollama:Model"],
                prompt = prompt,

                stream = false,
                options = new
                {
                    temperature = _config.GetValue<double>("Ollama:Temperature"),
                    max_tokens = _config.GetValue<int>("Ollama:MaxTokens")
                }
            };

            var result = await SendRequestAsync("api/generate", request, ct);

            return result;
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    public async Task<string> GenerateCodeForChatAsync(string prompt, string context, CancellationToken ct)
    {
        try
        {
            var request = new
            {
                model = _config["Ollama:ModelInstruct"],
                prompt = $"[INST] Напиши код на C# для: {prompt}. Контекст, который был передан в запросе: {context}.  [/INST]",
                stream = false,
                options = new
                {
                    temperature = _config.GetValue<double>("Ollama:Temperature"),
                    max_tokens = _config.GetValue<int>("Ollama:MaxTokens")
                }
            };

            var result = await SendRequestAsync("api/generate", request, ct);

            return result;
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    public async Task<string> GenerateXmlDocAsync(string prompt, string context, CancellationToken ct)
    {
        try
        {
            var request = new
            {
                model = _config["Ollama:ModelInstruct"],
                prompt = $"[INST] Напиши XML комментарии (документацию) к этому коду: {prompt}. Контекст, который был передан в запросе: {context}. [/INST]",
                stream = false,
                options = new
                {
                    temperature = _config.GetValue<double>("Ollama:Temperature"),
                    max_tokens = _config.GetValue<int>("Ollama:MaxTokens")
                }
            };

            var result = await SendRequestAsync("api/generate", request, ct);

            return result;
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }

    private async Task<string> SendRequestAsync(string url, object request, CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync(url, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return $"Ошибка: {response.StatusCode}";
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        return result?.Response.Trim() ?? "Пустой ответ";
    }

    private class OllamaResponse
    {
        public string Response { get; set; } = "";
    }
}