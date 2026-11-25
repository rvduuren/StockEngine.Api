using StockEngine.Api.Auth;

namespace StockEngine.Api.Tests;

public sealed class TokenHasherTests
{
    [Fact]
    public void Hash_is_deterministic()
    {
        const string token = "sample-refresh-token";

        var first = TokenHasher.Hash(token);
        var second = TokenHasher.Hash(token);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Hash_differs_for_different_tokens()
    {
        Assert.NotEqual(TokenHasher.Hash("a"), TokenHasher.Hash("b"));
    }
}