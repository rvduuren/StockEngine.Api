using Microsoft.AspNetCore.Identity;
using StockEngine.Api.Auth;
using StockEngine.Api.Data;

namespace StockEngine.Api.Seed;

public static class DevDataSeeder
{
    public static readonly string[] Roles = WarehouseRoleRules.AllRoles.ToArray();

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        await EnsureUserAsync(userManager, "admin@localhost", "Admin123!", [WarehouseRoleRules.Admin]);
        await EnsureUserAsync(userManager, "picker@localhost", "Picker123!", [WarehouseRoleRules.Picker]);
        await EnsureUserAsync(userManager, "packer@localhost", "Packer123!", [WarehouseRoleRules.Packer]);
        await EnsureUserAsync(userManager, "manager@localhost", "Manager123!", [WarehouseRoleRules.WarehouseManager]);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        IReadOnlyList<string> roles)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed user {email}: {errors}");
        }

        await userManager.AddToRolesAsync(user, roles);
    }
}