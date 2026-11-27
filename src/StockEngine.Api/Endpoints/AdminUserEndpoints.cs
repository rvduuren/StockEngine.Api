using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockEngine.Api.Auth;
using StockEngine.Api.Contracts;
using StockEngine.Api.Data;

namespace StockEngine.Api.Endpoints;

public static class AdminUserEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/admin/users")
            .WithTags("Admin")
            .RequireAuthorization("AdminOnly");

        group.MapPost("/", CreateUserAsync)
            .WithName("CreateUser")
            .WithSummary("Provision a user with email, initial password, and roles (Admin only)")
            .Produces<ProvisionedUserResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/roles", ReplaceUserRolesAsync)
            .WithName("ReplaceUserRoles")
            .WithSummary("Replace a user's entire role set (Admin only)")
            .Produces<ProvisionedUserResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Email)] = ["Email is required."],
            });
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Password)] = ["Password is required."],
            });
        }

        if (!WarehouseRoleRules.TryValidateRoleSet(request.Roles, out var roleErrors))
        {
            return Results.ValidationProblem(roleErrors);
        }

        foreach (var role in request.Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Roles)] = [$"Role '{role}' is not registered."],
                });
            }
        }

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return Results.Problem(
                title: "Conflict",
                detail: "A user with this email already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            UserName = request.Email,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
            {
                return Results.Problem(
                    title: "Conflict",
                    detail: "A user with this email already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return IdentityErrorResult(createResult);
        }

        var addRolesResult = await userManager.AddToRolesAsync(user, request.Roles);
        if (!addRolesResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return IdentityErrorResult(addRolesResult);
        }

        var response = new ProvisionedUserResponse(user.Id, user.Email!, request.Roles.ToList());
        return Results.Created($"/api/admin/users/{user.Id}", response);
    }

    private static async Task<IResult> ReplaceUserRolesAsync(
        Guid id,
        UpdateUserRolesRequest request,
        ClaimsPrincipal caller,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext db,
        AuthTokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (!WarehouseRoleRules.TryValidateRoleSet(request.Roles, out var roleErrors))
        {
            return Results.ValidationProblem(roleErrors);
        }

        foreach (var role in request.Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Roles)] = [$"Role '{role}' is not registered."],
                });
            }
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Results.Problem(
                title: "Not Found",
                detail: "User was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var currentRoles = (await userManager.GetRolesAsync(user)).ToList();
        if (WarehouseRoleRules.RoleSetsEqual(currentRoles, request.Roles))
        {
            return Results.Ok(new ProvisionedUserResponse(user.Id, user.Email!, currentRoles));
        }

        var callerId = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        if (callerId is not null
            && Guid.TryParse(callerId, out var callerGuid)
            && callerGuid == user.Id
            && currentRoles.Contains(WarehouseRoleRules.Admin, StringComparer.Ordinal)
            && !request.Roles.Contains(WarehouseRoleRules.Admin, StringComparer.Ordinal))
        {
            return Results.Problem(
                title: "Bad Request",
                detail: "You cannot remove your own Admin role.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (currentRoles.Contains(WarehouseRoleRules.Admin, StringComparer.Ordinal)
            && !request.Roles.Contains(WarehouseRoleRules.Admin, StringComparer.Ordinal))
        {
            var adminUserIds = await userManager.GetUsersInRoleAsync(WarehouseRoleRules.Admin);
            if (adminUserIds.Count <= 1)
            {
                return Results.Problem(
                    title: "Conflict",
                    detail: "At least one user must retain the Admin role.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var toRemove = currentRoles.Except(request.Roles, StringComparer.Ordinal).ToList();
        var toAdd = request.Roles.Except(currentRoles, StringComparer.Ordinal).ToList();

        if (toRemove.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return IdentityErrorResult(removeResult);
            }
        }

        if (toAdd.Count > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return IdentityErrorResult(addResult);
            }
        }

        await tokenService.RevokeAllActiveRefreshTokensForUserAsync(user.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var updatedRoles = (await userManager.GetRolesAsync(user)).OrderBy(r => r, StringComparer.Ordinal).ToList();
        return Results.Ok(new ProvisionedUserResponse(user.Id, user.Email!, updatedRoles));
    }

    private static IResult IdentityErrorResult(IdentityResult result)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["identityErrors"] = result.Errors.Select(e => new { e.Code, e.Description }).ToList(),
        };

        return Results.Problem(
            title: "Bad Request",
            detail: "One or more identity validation errors occurred.",
            statusCode: StatusCodes.Status400BadRequest,
            extensions: extensions);
    }
}