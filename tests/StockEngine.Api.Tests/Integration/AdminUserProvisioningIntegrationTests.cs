using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StockEngine.Api.Auth;
using StockEngine.Api.Contracts;

namespace StockEngine.Api.Tests.Integration;

public sealed class AdminUserProvisioningIntegrationTests(StockEngineApiFactory factory)
    : IClassFixture<StockEngineApiFactory>
{
    [Fact]
    public async Task Admin_provisions_Picker_sign_in_succeeds_forbidden_on_admin_dashboard()
    {
        var admin = await SignInAsync("admin@localhost", "Admin123!");
        var email = $"new-picker-{Guid.NewGuid():N}@localhost";

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var create = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest(email, "NewPicker1!", [WarehouseRoleRules.Picker]));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var body = await create.Content.ReadFromJsonAsync<ProvisionedUserResponse>();
        Assert.NotNull(body);
        Assert.Equal($"/api/admin/users/{body.Id}", create.Headers.Location?.ToString());
        Assert.Equal(email, body.Email);
        Assert.Equal([WarehouseRoleRules.Picker], body.Roles);

        var pickerTokens = await SignInAsync(email, "NewPicker1!");
        using var pickerClient = factory.CreateClient();
        pickerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pickerTokens.AccessToken);

        var me = await pickerClient.GetAsync("/api/me");
        me.EnsureSuccessStatusCode();
        var profile = await me.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(profile);
        Assert.Equal([WarehouseRoleRules.Picker], profile.Roles);

        var dashboard = await pickerClient.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, dashboard.StatusCode);
    }

    [Fact]
    public async Task Role_change_revokes_prior_refresh_token_updated_roles_after_refresh()
    {
        var admin = await SignInAsync("admin@localhost", "Admin123!");
        var email = $"role-change-{Guid.NewGuid():N}@localhost";

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var create = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest(email, "Worker123!", [WarehouseRoleRules.Picker]));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<ProvisionedUserResponse>();
        Assert.NotNull(created);

        var workerTokens = await SignInAsync(email, "Worker123!");
        var priorRefresh = workerTokens.RefreshToken;

        var updateRoles = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{created.Id}/roles",
            new UpdateUserRolesRequest([WarehouseRoleRules.Packer]));
        updateRoles.EnsureSuccessStatusCode();

        using var workerClient = factory.CreateClient();
        var staleRefresh = await workerClient.PostAsJsonAsync(
            "/auth/refresh",
            new RefreshRequest(priorRefresh));
        Assert.Equal(HttpStatusCode.Unauthorized, staleRefresh.StatusCode);

        var newTokens = await SignInAsync(email, "Worker123!");
        workerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newTokens.AccessToken);

        var me = await workerClient.GetAsync("/api/me");
        me.EnsureSuccessStatusCode();
        var profile = await me.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(profile);
        Assert.Equal([WarehouseRoleRules.Packer], profile.Roles);
    }

    [Fact]
    public async Task Identical_role_replace_leaves_refresh_token_valid()
    {
        var admin = await SignInAsync("admin@localhost", "Admin123!");
        var email = $"noop-roles-{Guid.NewGuid():N}@localhost";

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var create = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest(email, "Worker123!", [WarehouseRoleRules.Picker]));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<ProvisionedUserResponse>();
        Assert.NotNull(created);

        var tokens = await SignInAsync(email, "Worker123!");

        var replace = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{created.Id}/roles",
            new UpdateUserRolesRequest([WarehouseRoleRules.Picker]));
        replace.EnsureSuccessStatusCode();

        using var client = factory.CreateClient();
        var refresh = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        refresh.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Admin_cannot_remove_own_admin_role()
    {
        var admin = await SignInAsync("admin@localhost", "Admin123!");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var me = await client.GetAsync("/api/me");
        me.EnsureSuccessStatusCode();
        var profile = await me.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(profile);

        var update = await client.PutAsJsonAsync(
            $"/api/admin/users/{profile.UserId}/roles",
            new UpdateUserRolesRequest([WarehouseRoleRules.Picker]));
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Peer_admin_can_demote_another_admin_when_two_exist()
    {
        var admin = await SignInAsync("admin@localhost", "Admin123!");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var secondAdminEmail = $"second-admin-{Guid.NewGuid():N}@localhost";
        var createSecond = await client.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest(secondAdminEmail, "Admin456!", [WarehouseRoleRules.Admin]));
        createSecond.EnsureSuccessStatusCode();
        var secondAdmin = await createSecond.Content.ReadFromJsonAsync<ProvisionedUserResponse>();
        Assert.NotNull(secondAdmin);

        var demotePeer = await client.PutAsJsonAsync(
            $"/api/admin/users/{secondAdmin.Id}/roles",
            new UpdateUserRolesRequest([WarehouseRoleRules.Picker]));
        demotePeer.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Duplicate_email_on_create_returns_conflict()
    {
        var admin = await SignInAsync("admin@localhost", "Admin123!");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var duplicate = await client.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("picker@localhost", "Another123!", [WarehouseRoleRules.Picker]));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_empty_roles_admin_with_job_role_and_unknown_role()
    {
        var admin = await SignInAsync("admin@localhost", "Admin123!");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var emptyRoles = await client.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("x@localhost", "Valid123!", []));
        Assert.Equal(HttpStatusCode.BadRequest, emptyRoles.StatusCode);

        var adminPlusJob = await client.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("bad-admin@localhost", "Valid123!",
                [WarehouseRoleRules.Admin, WarehouseRoleRules.Picker]));
        Assert.Equal(HttpStatusCode.BadRequest, adminPlusJob.StatusCode);

        var unknownRole = await client.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("bad-role@localhost", "Valid123!", ["NotARealRole"]));
        Assert.Equal(HttpStatusCode.BadRequest, unknownRole.StatusCode);
    }

    private async Task<TokenResponse> SignInAsync(string email, string password)
    {
    using var client = factory.CreateClient();
    var signIn = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, password));
    signIn.EnsureSuccessStatusCode();
    var tokens = await signIn.Content.ReadFromJsonAsync<TokenResponse>();
    Assert.NotNull(tokens);
        return tokens;
    }
}