using CodeAI.Api.Attribute;
using CodeAI.Api.Exceptions;
using CodeAI.Api.Models;
using CodeAI.Api.Services;
using CodeAI.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using CodeAI.Api.Contracts;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CodeController : ControllerBase
{
    private readonly IAIService _aiService;
    private readonly IRedisCacheService _cache;
    private readonly AppDbContext _db;
    private static readonly TimeSpan _ttl = TimeSpan.FromHours(6);

    public CodeController(
        IAIService aiService,
        IRedisCacheService cache,
        AppDbContext db)
    {
        _aiService = aiService;
        _cache = cache;
        _db = db;
    }

    private static string BuildCacheKey(string prefix, CodeRequest req) =>
        $"{prefix}:{req.Language}:{HashCode.Combine(req.Prompt, req.Context)}";

    private async Task SaveHistoryAsync(string prompt, string response, CancellationToken ct)
    {
        Guid? userId = null;

        var idClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(idClaim, out var guid))
            userId = guid;

        _db.QueryHistories.Add(new QueryHistory
        {
            Prompt = prompt,
            Response = response,
            AppUserId = userId 
        });

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Generates code based on prompt
    /// </summary>
    /// <param name="request">Prompt and context for code generation</param>
    [HttpPost("code")]
    [ValidateRequest]
    [ResponseCache(Duration = 60)]
    [EnableRateLimiting("ai-requests")]
    [ProducesResponseType<CodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CodeResponse>> GenerateCode(
        [FromBody] CodeRequest request,
        CancellationToken ct)
    {
        const string prefix = "code";
        var cacheKey = BuildCacheKey(prefix, request);

        if (await _cache.GetAsync<CodeResponse>(cacheKey, ct) is { } cached)
            return cached;

        try
        {
            var code = await _aiService.GenerateCodeAsync(request.Prompt, request.Context, request.Language, ct);
            var resp = new CodeResponse(code);

            await _cache.SetAsync(cacheKey, resp, _ttl, ct);
            await SaveHistoryAsync(request.Prompt, code, ct);

            return resp;
        }
        catch (AiServiceException ex)
        {
            return StatusCode(502, new ProblemDetails
            {
                Title = "AI Service Error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Generates code in chat-like format
    /// </summary>
    [HttpPost("chat")]
    [ValidateRequest]
    [ResponseCache(Duration = 60)]
    [EnableRateLimiting("ai-requests")]
    [ProducesResponseType<CodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CodeResponse>> GenerateCodeForChat(
        [FromBody] CodeRequest request,
        CancellationToken ct)
    {
        const string prefix = "chat";
        var cacheKey = BuildCacheKey(prefix, request);

        if (await _cache.GetAsync<CodeResponse>(cacheKey, ct) is { } cached)
            return cached;

        try
        {
            var code = await _aiService.GenerateCodeForChatAsync(request.Prompt, request.Context, request.Language, ct);
            var resp = new CodeResponse(code);

            await _cache.SetAsync(cacheKey, resp, _ttl, ct);
            await SaveHistoryAsync(request.Prompt, code, ct);

            return resp;
        }
        catch (AiServiceException ex)
        {
            return StatusCode(502, new ProblemDetails
            {
                Title = "AI Service Error",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Generates XML documentation for code
    /// </summary>
    [HttpPost("docs")]
    [ValidateRequest]
    [ResponseCache(Duration = 60)]
    [EnableRateLimiting("ai-requests")]
    [ProducesResponseType<CodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<CodeResponse>> GenerateXmlDoc(
        [FromBody] CodeRequest request,
        CancellationToken ct)
    {
        const string prefix = "docs";
        var cacheKey = BuildCacheKey(prefix, request);

        if (await _cache.GetAsync<CodeResponse>(cacheKey, ct) is { } cached)
            return cached;

        try
        {
            var code = await _aiService.GenerateXmlDocAsync(request.Prompt, request.Context, request.Language, ct);
            var resp = new CodeResponse(code);

            await _cache.SetAsync(cacheKey, resp, _ttl, ct);
            await SaveHistoryAsync(request.Prompt, code, ct);

            return resp;
        }
        catch (AiServiceException ex)
        {
            return StatusCode(502, new ProblemDetails
            {
                Title = "AI Service Error",
                Detail = ex.Message
            });
        }
    }

    [HttpPost("code-async")]
    [Authorize]
    public async Task<ActionResult<CodeResponse>> GenerateAsync(
        [FromBody] CodeRequest req,
        [FromServices] IRequestClient<CodeGenRequest> client,
        CancellationToken ct)
    {
        var id = Guid.NewGuid();

        var resp = await client.GetResponse<CodeGenResult>(
            new CodeGenRequest(id, req.Prompt, req.Context,
                               req.Language, req.Temperature,
                               req.MaxTokens, User.Identity?.Name), ct);

        return Ok(new CodeResponse(resp.Message.Completion));
    }
}
