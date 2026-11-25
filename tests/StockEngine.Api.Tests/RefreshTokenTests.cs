using StockEngine.Api.Data;

namespace StockEngine.Api.Tests;

public sealed class RefreshTokenTests
{
    [Fact]
    public void IsActiveAt_respects_revocation_and_expiry()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var token = new RefreshToken
        {
            ExpiresAt = now.AddHours(1),
        };

        Assert.True(token.IsActiveAt(now));

        token.RevokedAt = now;
        Assert.False(token.IsActiveAt(now));

        token.RevokedAt = null;
        token.ExpiresAt = now;
        Assert.False(token.IsActiveAt(now));
    }
}