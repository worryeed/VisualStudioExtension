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

    public AuthController(ITokenService tokens, AppDbContext db, IConfiguration cfg)
    {
        _tokens = tokens; _db = db; _cfg = cfg;
    }

    [HttpGet("signin/{provider}")]
    public IActionResult SignIn(string provider, string? redirectUri)
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback), new { provider, redirectUri })
        };
        return Challenge(props, provider);
    }

    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(string provider, string? redirectUri)
    {
        var ext = await HttpContext.AuthenticateAsync(provider);
        if (!ext.Succeeded || ext.Principal is null)
            return Unauthorized("External auth failed");

        var sub = ext.Principal.FindFirst("sub")?.Value
               ?? ext.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.ProviderId == sub)
                ?? new AppUser { Provider = provider, ProviderId = sub, DisplayName = ext.Principal.Identity!.Name! };
        if (user.Id == Guid.Empty) _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();

        var access = _tokens.CreateAccess(user);
        var refresh = _tokens.CreateRefresh(user);
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        var cName = _cfg["Refresh:CookieName"]!;
        Response.Cookies.Append(cName, refresh.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = refresh.Expires
        });

        if (string.IsNullOrWhiteSpace(redirectUri))
            return Ok(new { token = access });

        var url = QueryHelpers.AddQueryString(redirectUri, "token", access);
        return Redirect(url);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var cName = _cfg["Refresh:CookieName"]!;
        if (!Request.Cookies.TryGetValue(cName, out var rt))
            return Unauthorized("No refresh cookie");

        var newRt = await _tokens.RotateAsync(rt!, ct);
        if (newRt is null) return Unauthorized("Refresh invalid");

        var user = await _db.AppUsers.FindAsync(newRt.AppUserId);
        var access = _tokens.CreateAccess(user!);

        Response.Cookies.Append(cName, newRt.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = newRt.Expires
        });

        return Ok(new { token = access });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var cName = _cfg["Refresh:CookieName"]!;
        if (Request.Cookies.TryGetValue(cName, out var rt))
        {
            await _tokens.RotateAsync(rt!, ct);
            Response.Cookies.Delete(cName);
        }
        return NoContent();
    }
}