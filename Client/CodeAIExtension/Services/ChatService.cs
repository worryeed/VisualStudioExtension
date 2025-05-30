using CodeAIExtension.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeAIExtension.Services;

public interface IChatService
{
    Task StreamChatAsync(
        ChatRequest request,
        IProgress<string> onDelta,
        CancellationToken ct);
}

public class ChatService : IChatService
{
    private readonly HttpClient _http;
    private readonly IAuthService _authService;
    private readonly string _baseUrl;

    public ChatService(IAuthService authService, IConfiguration cfg)
    {
        _authService = authService;
        _baseUrl = cfg.GetSection("Auth:BaseUrl").Value;
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
    }

    public async Task StreamChatAsync(
        ChatRequest request,
        IProgress<string> onDelta,
        CancellationToken ct)
    {
        if (!_authService.IsAuthenticated)
        {
            throw new InvalidOperationException("User is not authenticated.");
        }

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authService.Jwt);

        var httpReq = new HttpRequestMessage(HttpMethod.Post, "/api/code/chat/stream")
        {
            Content = JsonContent.Create(request)
        };
        httpReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync();
        var buffer = new byte[1024];
        while (!ct.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            if (read == 0) break;
            
            var chunk = Encoding.UTF8.GetString(buffer, 0, read);
            chunk = chunk.Replace("data: ", string.Empty);
            chunk = chunk.Replace("data:[DONE]", string.Empty);
            chunk = chunk.Replace("event:done", string.Empty);
            chunk = chunk.Replace("\r\n\r\n", string.Empty);
            onDelta.Report(chunk);
        }
    }
}