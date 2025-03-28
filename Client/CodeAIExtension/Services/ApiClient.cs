using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace CodeAIExtension.Services;

public static class ApiClient
{
    private static readonly HttpClient _client = new HttpClient();
    private const string ApiUrl = "https://localhost:7001/api/code/docs";

    public static async Task<string> GetXmlDocsAsync(string code)
    {
        try
        {
            var prompt = $"Рефакторинг этого кода на C#:\n{code}";

            var request = new
            {
                prompt = prompt,
                context = "",
            };

            var response = await _client.PostAsync(ApiUrl, new StringContent(JsonConvert.SerializeObject(request), System.Text.Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(jsonResponse);

            return json?.code ?? "Ошибка: пустой ответ";
        }
        catch (Exception ex)
        {
            return $"Ошибка: {ex.Message}";
        }
    }
}
