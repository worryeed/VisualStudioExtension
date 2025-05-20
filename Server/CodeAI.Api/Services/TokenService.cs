using CodeAI.Api.Data;
using CodeAI.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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
    private readonly AppDbContext _db;
    private readonly JwtSecurityTokenHandler _h = new();

    public TokenService(IConfiguration cfg, AppDbContext db)
    {
        _cfg = cfg; _db = db;
    }

    public string CreateAccess(AppUser u)
    {
        var s = _cfg.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(s["Key"]!));

        var jwt = new JwtSecurityToken(
            issuer: s["Issuer"],
            audience: s["Audience"],
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
                new Claim(ClaimTypes.Name, u.DisplayName)
            },
            expires: DateTime.UtcNow.AddMinutes(s.GetValue<int>("AccessMinutes")),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return _h.WriteToken(jwt);
    }

    public RefreshToken CreateRefresh(AppUser u)
    {
        var days = _cfg.GetValue<int>("Refresh:Days");
        return new RefreshToken
        {
            AppUserId = u.Id,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            Expires = DateTime.UtcNow.AddDays(days)
        };
    }

    public async Task<RefreshToken?> RotateAsync(string oldToken, CancellationToken ct)
    {
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == oldToken && !r.Revoked, ct);
        if (rt is null || rt.Expires <= DateTime.UtcNow) return null;

        rt.Revoked = true;
        var user = await _db.AppUsers.FindAsync(rt.AppUserId);
        var newRt = CreateRefresh(user!);
        _db.RefreshTokens.Add(newRt);
        await _db.SaveChangesAsync(ct);
        return newRt;
    }
}