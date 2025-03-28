using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace CodeAI.Api.Services;

public class HuggingFaceService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    private string _modelUrl = string.Empty;
    private string _modelInstructUrl = string.Empty;


    public HuggingFaceService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.BaseAddress = new Uri(_config["HuggingFace:ApiUrl"]!);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config["HuggingFace:ApiToken"]);
        _modelUrl = _config["HuggingFace:Model"]!;
        _modelInstructUrl = _config["HuggingFace:ModelInstruct"]!;
    }

    private class HuggingFaceResponse
    {
        [JsonPropertyName("generated_text")]
        public string GeneratedText { get; set; } = "";
    }

    public async Task<string> GenerateCodeAsync(string prompt, string context, CancellationToken ct)
    {
        try
        {
            var request = new
            {
                inputs = prompt
            };

            var result = await SendRequestAsync(_modelUrl, request, ct);

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
            var cleanPrompt = $"[INST] Напиши код на C# для: {prompt}. Контекст, который был передан в запросе: {context}. [/INST]";

            var request = new
            {
                inputs = cleanPrompt
            };

            var result = await SendRequestAsync(_modelInstructUrl, request, ct);

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
            var cleanPrompt = $"[INST] Напиши XML комментарии (документацию) к этому коду: {prompt}. Контекст, который был передан в запросе: {context}. [/INST]";

            var request = new
            {
                inputs = cleanPrompt
            };

            var result = await SendRequestAsync(_modelInstructUrl, request, ct);

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
            var error = await response.Content.ReadAsStringAsync();
            return $"Ошибка: {response.StatusCode} - {error}";
        }

        var resultArray = await response.Content.ReadFromJsonAsync<List<HuggingFaceResponse>>();
        var result = resultArray?[0].GeneratedText ?? "Пустой ответ";

        return result.Trim();
    }
}
