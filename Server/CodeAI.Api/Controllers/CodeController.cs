using CodeAI.Api.Attribute;
using CodeAI.Api.Contracts;
using CodeAI.Api.Data;
using CodeAI.Api.Models;
using CodeAI.Api.Services;
using MassTransit;
using MassTransit.Internals.GraphValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class CodeController : ControllerBase
{
    private readonly IRedisCacheService _cache;
    private readonly AppDbContext _db;
    private readonly ILogger<CodeController> _logger;
    private readonly IRequestClient<CodeGenRequest> _client;
    private readonly IChatHistoryStore _chatHistoryStore;
    private readonly IAIService _ai;
    private static readonly TimeSpan _ttl = TimeSpan.FromHours(6);

    public CodeController(
        IRedisCacheService cache,
        AppDbContext db,
        ILogger<CodeController> logger,
        IRequestClient<CodeGenRequest> client,
        IChatHistoryStore chatHistoryStore,
        IAIService ai)
    {
        _cache = cache;
        _db = db;
        _logger = logger;
        _client = client;
        _chatHistoryStore = chatHistoryStore; 
        _ai = ai;
    }

    [HttpPost("autoComplete")]
    [ValidateRequest]
    [EnableRateLimiting("ai-requests")]
    [ProducesResponseType<CodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CodeResponse>> GenerateCode(
        [FromBody] CodeRequest request, CancellationToken ct)
    {
        var cacheKey = BuildCacheKey("autoComplete", request);
        if (await _cache.GetAsync<CodeResponse>(cacheKey, ct) is { } cached)
            return cached;

        var resp = await SendViaQueueAsync(CodeGenKind.Code, request, ct);
        await _cache.SetAsync(cacheKey, resp, _ttl, ct);
        await SaveHistoryAsync(request.Prompt, resp.Code, ct);
        return resp;
    }

    [HttpPost("chat")]
    [ValidateRequest]
    [EnableRateLimiting("ai-requests")]
    [ProducesResponseType<CodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CodeResponse>> GenerateChat(
        [FromBody] CodeRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var userKey = GetHistoryKey(userId);
        var history = await _cache.GetAsync<List<ChatMessage>>(userKey, ct)
                     ?? new List<ChatMessage>();

        var resp = await SendViaQueueAsync(CodeGenKind.Chat, request, ct, history);

        history = _chatHistoryStore.Get(userId) ?? new List<ChatMessage>();
        Trim(history);

        await _cache.SetAsync(userKey, history, _ttl, ct);
        await SaveHistoryAsync(request.Prompt, resp.Code, ct);
        _chatHistoryStore.Clear(userId);

        return resp;
    }

    [HttpPost("chat/stream")]
    [ValidateRequest]
    [Produces("text/event-stream")]
    [EnableRateLimiting("ai-requests")]
    [ProducesResponseType<CodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task ChatStream(
        [FromBody] CodeRequest r, CancellationToken ct)
    {
        var userId = GetUserId();
        var userKey = GetHistoryKey(userId);
        var history = await _cache.GetAsync<List<ChatMessage>>(userKey, ct)
            ?? new List<ChatMessage>();

        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.ContentType = "text/event-stream";

        HttpContext.Features
            .Get<IHttpResponseBodyFeature>()?
            .DisableBuffering();

        var sb = new StringBuilder();
        await foreach (var chunk in _ai.StreamChatResponseAsync(r.Prompt, r.Context, r.Language, history, ct))
        {
             await Response.WriteAsync($"data: {chunk}\r\n\r\n", ct);
            await Response.Body.FlushAsync(ct);
            sb.Append(chunk);
        }
        await Response.WriteAsync("event:done\r\ndata:[DONE]\r\n\r\n", ct);
        await Response.Body.FlushAsync(ct);

        var answer = sb.ToString();

        history!.Add(new ChatMessage("AI bot", answer));
       _chatHistoryStore.Set(userId, history);
        Trim(history);
        await _cache.SetAsync(userKey, history, _ttl, ct);
        _chatHistoryStore.Clear(userId);
        await SaveHistoryAsync(r.Prompt, answer, ct);
    }

    [HttpPost("docs")]
    [ValidateRequest]
    [EnableRateLimiting("ai-requests")]
    [ProducesResponseType<CodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CodeResponse>> GenerateXmlDoc(
        [FromBody] CodeRequest request, CancellationToken ct)
    {
        var cacheKey = BuildCacheKey("docs", request);
        if (await _cache.GetAsync<CodeResponse>(cacheKey, ct) is { } cached)
            return cached;

        var resp = await SendViaQueueAsync(CodeGenKind.Docs, request, ct);
        await _cache.SetAsync(cacheKey, resp, _ttl, ct);
        await SaveHistoryAsync(request.Prompt, resp.Code, ct);
        return resp;
    }

    private async Task<CodeResponse> SendViaQueueAsync(
        CodeGenKind kind, CodeRequest req, CancellationToken ct, List<ChatMessage>? history = null)
    {
        var requestId = Guid.NewGuid();
        _logger.LogInformation("{Kind} queued. ReqId={Id}", kind, requestId);

        var reply = await _client.GetResponse<CodeGenResult>(
            new CodeGenRequest(
                requestId, kind,
                req.Prompt, req.Context ?? string.Empty,
                req.Language, req.Temperature, req.MaxTokens,
                GetUserId(), history),
            ct);

        _logger.LogInformation("{Kind} done. ReqId={Id}", kind, requestId);
        return new CodeResponse(reply.Message.Completion);
    }

    private static string BuildCacheKey(string prefix, CodeRequest req) =>
        $"{prefix}:{req.Language}:{HashCode.Combine(req.Prompt, req.Context, req.Language, req.MaxTokens, req.Temperature)}";

    private static string GetHistoryKey(string userId) => $"chatHistory:{userId}";

    private string GetUserId() => HttpContext.User.Identity?.Name ?? HttpContext.Connection.RemoteIpAddress!.ToString();

    private static void Trim(List<ChatMessage> history)
    {
        const int max = 64;
        if (history.Count > max * 2)
            history.RemoveRange(1, history.Count - max * 2);
    }
    private async Task SaveHistoryAsync(string prompt, string response, CancellationToken ct)
    {
        Guid? userId = null;
        var claim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(claim, out var guid) &&
            await _db.AppUsers.AnyAsync(u => u.Id == guid, ct))
            userId = guid;

        _db.QueryHistories.Add(new QueryHistory
        {
            Prompt = prompt,
            Response = response,
            AppUserId = userId
        });
        await _db.SaveChangesAsync(ct);
    }
}