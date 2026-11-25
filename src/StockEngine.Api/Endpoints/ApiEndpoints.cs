using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using StockEngine.Api.Contracts;

namespace StockEngine.Api.Endpoints;

public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" }))
            .WithTags("Health")
            .WithName("Health")
            .AllowAnonymous();

        routes.MapGet("/api/me", (ClaimsPrincipal user) =>
            {
                var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

                if (userId is null || !Guid.TryParse(userId, out var id))
                {
                    return Results.Unauthorized();
                }

                var email = user.FindFirstValue(ClaimTypes.Email);
                var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

                return Results.Ok(new MeResponse(id, email, roles));
            })
            .WithTags("Account")
            .WithName("GetMe")
            .RequireAuthorization();

        routes.MapGet("/api/admin/dashboard", (TimeProvider time) => Results.Ok(new
            {
                message = "Admin-only sample endpoint.",
                generatedAt = time.GetUtcNow(),
            }))
            .WithTags("Admin")
            .WithName("AdminDashboard")
            .RequireAuthorization("AdminOnly");

        return routes;
    }
}