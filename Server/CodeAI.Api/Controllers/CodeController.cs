using CodeAI.Api.Attribute;
using CodeAI.Api.Contracts;
using CodeAI.Api.Data;
using CodeAI.Api.Models;
using CodeAI.Api.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
    private static readonly TimeSpan _ttl = TimeSpan.FromHours(6);

    public CodeController(
        IRedisCacheService cache,
        AppDbContext db,
        ILogger<CodeController> logger,
        IRequestClient<CodeGenRequest> client)
    {
        _cache = cache;
        _db = db;
        _logger = logger;
        _client = client;
    }

    [HttpPost("autoComplete")]
    [ValidateRequest]
    [ResponseCache(Duration = 60)]
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
    [ResponseCache(Duration = 60)]
    [EnableRateLimiting("ai-requests")]
    [ProducesResponseType<CodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CodeResponse>> GenerateChat(
        [FromBody] CodeRequest request, CancellationToken ct)
    {
        var cacheKey = BuildCacheKey("chat", request);
        if (await _cache.GetAsync<CodeResponse>(cacheKey, ct) is { } cached)
            return cached;

        var resp = await SendViaQueueAsync(CodeGenKind.Chat, request, ct);
        await _cache.SetAsync(cacheKey, resp, _ttl, ct);
        await SaveHistoryAsync(request.Prompt, resp.Code, ct);
        return resp;
    }

    [HttpPost("docs")]
    [ValidateRequest]
    [ResponseCache(Duration = 60)]
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
        CodeGenKind kind, CodeRequest req, CancellationToken ct)
    {
        var requestId = Guid.NewGuid();
        _logger.LogInformation("{Kind} queued. ReqId={Id}", kind, requestId);

        var reply = await _client.GetResponse<CodeGenResult>(
            new CodeGenRequest(
                requestId, kind,
                req.Prompt, req.Context ?? string.Empty,
                req.Language, req.Temperature, req.MaxTokens,
                HttpContext.User.Identity?.Name),
            ct);

        _logger.LogInformation("{Kind} done. ReqId={Id}", kind, requestId);
        return new CodeResponse(reply.Message.Completion);
    }

    private static string BuildCacheKey(string prefix, CodeRequest req) =>
        $"{prefix}:{req.Language}:{HashCode.Combine(req.Prompt, req.Context, req.Language, req.MaxTokens, req.Temperature)}";

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