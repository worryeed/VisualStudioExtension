using CodeAI.Api.Data;
using CodeAI.Api.Models;
using CodeAI.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace CodeAI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokens;
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ITokenService tokens,
        AppDbContext db,
        IConfiguration cfg,
        ILogger<AuthController> logger)
    {
        _tokens = tokens;
        _db = db;
        _cfg = cfg;
        _logger = logger;
    }

    [HttpGet("signin/{provider}")]
    public IActionResult SignIn(string provider, string? redirectUri)
    {
        _logger.LogInformation("SignIn requested. Provider={Provider}, RedirectUri={RedirectUri}", provider, redirectUri);

        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback), new { provider, redirectUri })
        };

        _logger.LogDebug("Challenge properties prepared. RedirectUri={RedirectUri}", props.RedirectUri);
        return Challenge(props, provider);
    }

    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(string provider, string? redirectUri)
    {
        _logger.LogInformation("Callback start. Provider={Provider}, RedirectUri={RedirectUri}", provider, redirectUri);

        var ext = await HttpContext.AuthenticateAsync(provider);
        if (!ext.Succeeded || ext.Principal is null)
        {
            _logger.LogWarning("External authentication failed for provider {Provider}", provider);
            return Unauthorized("External auth failed");
        }

        var sub = ext.Principal.FindFirst("sub")?.Value
               ?? ext.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        _logger.LogDebug("External auth principal found. Subject={Subject}", sub);

        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.ProviderId == sub)
                   ?? new AppUser
                   {
                       Provider = provider,
                       ProviderId = sub,
                       DisplayName = ext.Principal.Identity!.Name!
                   };

        if (user.Id == Guid.Empty)
        {
            _logger.LogInformation("New user detected. Creating user with ProviderId={ProviderId}", sub);
            _db.AppUsers.Add(user);
        }
        else
        {
            _logger.LogInformation("Existing user found. UserId={UserId}", user.Id);
        }

        await _db.SaveChangesAsync();
        _logger.LogDebug("Database changes saved.");

        var access = _tokens.CreateAccess(user);
        var refresh = _tokens.CreateRefresh(user);
        _logger.LogInformation("Tokens generated for UserId={UserId}", user.Id);

        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();
        _logger.LogDebug("Refresh token persisted.");

        var cName = _cfg["Refresh:CookieName"]!;
        Response.Cookies.Append(cName, refresh.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = refresh.Expires
        });
        _logger.LogDebug("Refresh cookie '{CookieName}' set with expiration {Expires}", cName, refresh.Expires);

        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            _logger.LogInformation("No redirectUri provided. Returning access token in body.");
            return Ok(new { token = access });
        }

        var url = QueryHelpers.AddQueryString(redirectUri, "token", access);
        _logger.LogInformation("Redirecting to URL with token: {RedirectUrl}", url);
        return Redirect(url);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        _logger.LogInformation("Refresh token request started.");

        var cName = _cfg["Refresh:CookieName"]!;
        if (!Request.Cookies.TryGetValue(cName, out var rt))
        {
            _logger.LogWarning("No refresh cookie '{CookieName}' found in request.", cName);
            return Unauthorized("No refresh cookie");
        }

        _logger.LogDebug("Refresh cookie '{CookieName}' retrieved.", cName);
        var newRt = await _tokens.RotateAsync(rt!, ct);
        if (newRt is null)
        {
            _logger.LogWarning("Refresh token rotation failed or invalid token provided.");
            return Unauthorized("Refresh invalid");
        }

        _logger.LogInformation("Refresh token rotated successfully for TokenId={TokenId}", newRt.Id);
        var user = await _db.AppUsers.FindAsync(newRt.AppUserId);
        var access = _tokens.CreateAccess(user!);
        _logger.LogDebug("New access token created for UserId={UserId}", newRt.AppUserId);

        Response.Cookies.Append(cName, newRt.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = newRt.Expires
        });
        _logger.LogDebug("Refresh cookie '{CookieName}' updated with new token.", cName);

        return Ok(new { token = access });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        _logger.LogInformation("Logout requested by User={User}", User.Identity?.Name);

        var cName = _cfg["Refresh:CookieName"]!;
        if (Request.Cookies.TryGetValue(cName, out var rt))
        {
            _logger.LogDebug("Found refresh cookie '{CookieName}' for logout.", cName);
            await _tokens.RotateAsync(rt!, ct);
            Response.Cookies.Delete(cName);
            _logger.LogInformation("Refresh token rotated and cookie deleted.");
        }
        else
        {
            _logger.LogWarning("No refresh cookie '{CookieName}' found during logout.", cName);
        }

        return NoContent();
    }
}