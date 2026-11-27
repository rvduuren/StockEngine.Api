using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StockEngine.Api.Data;

namespace StockEngine.Api.Auth;

public sealed class AuthTokenService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken)?> IssueTokensAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var access = await CreateAccessTokenAsync(user, cancellationToken);
        var refreshPlain = GenerateRefreshToken();
        var refreshEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(refreshPlain),
            CreatedAt = timeProvider.GetUtcNow(),
            ExpiresAt = timeProvider.GetUtcNow().AddDays(_jwt.RefreshTokenDays),
        };

        db.RefreshTokens.Add(refreshEntity);
        await db.SaveChangesAsync(cancellationToken);

        return (access.Token, access.ExpiresAt, refreshPlain);
    }

    public async Task<(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken)?> RotateRefreshTokenAsync(
        string refreshTokenPlain,
        CancellationToken cancellationToken = default)
    {
        var hash = TokenHasher.Hash(refreshTokenPlain);
        var stored = await db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (stored is null)
        {
            return null;
        }

        if (stored.RevokedAt is not null)
        {
            await RevokeDescendantTokensAsync(stored, cancellationToken);
            return null;
        }

        if (stored.ExpiresAt <= timeProvider.GetUtcNow())
        {
            return null;
        }

        var newPlain = GenerateRefreshToken();
        var newHash = TokenHasher.Hash(newPlain);

        stored.RevokedAt = timeProvider.GetUtcNow();
        stored.ReplacedByTokenHash = newHash;

        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = stored.UserId,
            TokenHash = newHash,
            CreatedAt = timeProvider.GetUtcNow(),
            ExpiresAt = timeProvider.GetUtcNow().AddDays(_jwt.RefreshTokenDays),
        };

        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);

        var access = await CreateAccessTokenAsync(stored.User, cancellationToken);
        return (access.Token, access.ExpiresAt, newPlain);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshTokenPlain, CancellationToken cancellationToken = default)
    {
        var hash = TokenHasher.Hash(refreshTokenPlain);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return false;
        }

        stored.RevokedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAllActiveRefreshTokensForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var active = await db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var item in active)
        {
            item.RevokedAt = now;
        }

        if (active.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RevokeDescendantTokensAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens
            .Where(x => x.UserId == token.UserId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        foreach (var item in active)
        {
            item.RevokedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(string Token, DateTimeOffset ExpiresAt)> CreateAccessTokenAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var roles = await userManager.GetRolesAsync(user);
        var expires = timeProvider.GetUtcNow().AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: timeProvider.GetUtcNow().UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(jwt), expires);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}