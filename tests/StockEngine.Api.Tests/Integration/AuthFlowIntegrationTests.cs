using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StockEngine.Api.Contracts;

namespace StockEngine.Api.Tests.Integration;

public sealed class AuthFlowIntegrationTests(StockEngineApiFactory factory) : IClassFixture<StockEngineApiFactory>, IAsyncLifetime
{
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Sign_in_me_refresh_rotates_and_sign_out()
    {
        var signIn = await _client.PostAsJsonAsync(
            "/auth/sign-in",
            new SignInRequest("user@localhost", "User123!"));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var me = await _client.GetAsync("/api/me");
        me.EnsureSuccessStatusCode();
        var profile = await me.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(profile);
        Assert.NotEqual(Guid.Empty, profile.UserId);
        Assert.Equal("user@localhost", profile.Email);
        Assert.Equal(["User"], profile.Roles);

        var meAgain = await _client.GetAsync("/api/me");
        meAgain.EnsureSuccessStatusCode();
        var profileAgain = await meAgain.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(profileAgain);
        Assert.Equal(profile.UserId, profileAgain.UserId);

        var firstRefreshToken = tokens.RefreshToken;

        var refresh = await _client.PostAsJsonAsync(
            "/auth/refresh",
            new RefreshRequest(firstRefreshToken));
        refresh.EnsureSuccessStatusCode();
        var rotated = await refresh.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(firstRefreshToken, rotated.RefreshToken);

        var signOut = await _client.PostAsJsonAsync(
            "/auth/sign-out",
            new SignOutRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        var reuseOldRefresh = await _client.PostAsJsonAsync(
            "/auth/refresh",
            new RefreshRequest(firstRefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseOldRefresh.StatusCode);

        var refreshAfterSignOut = await _client.PostAsJsonAsync(
            "/auth/refresh",
            new RefreshRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterSignOut.StatusCode);
    }
}