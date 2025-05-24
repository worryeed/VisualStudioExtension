using CodeAI.Api.Data;
using CodeAI.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace CodeAI.Api.Services;

public interface ITokenService
{
    string CreateAccess(AppUser user);
    RefreshToken CreateRefresh(AppUser user);
    Task<RefreshToken?> RotateAsync(string token, CancellationToken ct);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _cfg;
    private readonly IJwtTokenService _jwtToken;
    private readonly AppDbContext _db;
    private readonly ILogger<TokenService> _logger;
    private readonly JwtSecurityTokenHandler _h = new();

    public TokenService(
        IConfiguration cfg,
        IJwtTokenService jwtToken,
        AppDbContext db,
        ILogger<TokenService> logger)
    {
        _cfg = cfg;
        _jwtToken = jwtToken;
        _db = db;
        _logger = logger;
    }

    public string CreateAccess(AppUser u)
    {
        _logger.LogInformation("Creating access token for UserId={UserId}, DisplayName={Name}", u.Id, u.DisplayName);

        var claims = new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
                new Claim(ClaimTypes.Name, u.DisplayName)
            })
        });

        var token = _jwtToken.Create(claims);
        _logger.LogDebug("Access token created. TokenLength={Length}", token.Length);
        return token;
    }

    public RefreshToken CreateRefresh(AppUser u)
    {
        _logger.LogInformation("Generating refresh token for UserId={UserId}", u.Id);

        var days = _cfg.GetValue<int>("Refresh:Days");
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenString = Convert.ToBase64String(tokenBytes);
        var expires = DateTime.UtcNow.AddDays(days);

        var rt = new RefreshToken
        {
            AppUserId = u.Id,
            Token = tokenString,
            Expires = expires
        };

        _logger.LogDebug(
            "Refresh token generated. Expires={ExpiresUtc}",
            rt.Expires.ToString("o"));

        return rt;
    }

    public async Task<RefreshToken?> RotateAsync(string oldToken, CancellationToken ct)
    {
        _logger.LogInformation("Rotating refresh token. OldTokenHash={Hash}", oldToken.GetHashCode());

        var rt = await _db.RefreshTokens
                          .FirstOrDefaultAsync(r => r.Token == oldToken && !r.Revoked, ct);

        if (rt == null)
        {
            _logger.LogWarning("Refresh token not found or already revoked. TokenHash={Hash}", oldToken.GetHashCode());
            return null;
        }

        if (rt.Expires <= DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token expired. Expires={ExpiresUtc}", rt.Expires.ToString("o"));
            return null;
        }

        rt.Revoked = true;
        _logger.LogDebug("Old refresh token revoked. TokenId={TokenId}", rt.Id);

        var user = await _db.AppUsers.FindAsync(new object?[] { rt.AppUserId }, ct);
        if (user == null)
        {
            _logger.LogError("User not found for AppUserId={UserId}", rt.AppUserId);
            return null;
        }

        var newRt = CreateRefresh(user);
        _db.RefreshTokens.Add(newRt);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Refresh token rotated successfully. NewTokenId={NewTokenId}", newRt.Id);

        return newRt;
    }
}
