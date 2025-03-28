using CodeAI.Api.Attribute;
using CodeAI.Api.Exceptions;
using CodeAI.Api.Models;
using CodeAI.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CodeController : ControllerBase
{
    private readonly IAIService _aiService;

    public CodeController(IAIService aiService)
    {
        _aiService = aiService;
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
        try
        {
            var code = await _aiService.GenerateCodeAsync(request.Prompt, request.Context, ct);
            return new CodeResponse(code);
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
    [ProducesResponseType<CodeResponse>(200)]
    public async Task<ActionResult<CodeResponse>> GenerateCodeForChat(
        [FromBody] CodeRequest request,
        CancellationToken ct)
    {
        try
        {
            var code = await _aiService.GenerateCodeForChatAsync(request.Prompt, request.Context, ct);
            return new CodeResponse(code);
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
    [ProducesResponseType<CodeResponse>(200)]
    public async Task<ActionResult<CodeResponse>> GenerateXmlDoc(
        [FromBody] CodeRequest request,
        CancellationToken ct)
    {
        try
        {
            var code = await _aiService.GenerateXmlDocAsync(request.Prompt, request.Context, ct);
            return new CodeResponse(code);
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
}