using Microsoft.AspNetCore.Identity;
using StockEngine.Api.Auth;
using StockEngine.Api.Contracts;
using StockEngine.Api.Data;

namespace StockEngine.Api.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/sign-in", SignInAsync)
            .WithName("SignIn")
            .WithSummary("Sign-in with email and password; returns an access token and refresh token")
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .WithSummary("Exchange a refresh token for a new access token and refresh token (rotation)")
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/sign-out", SignOutAsync)
            .WithName("SignOut")
            .WithSummary("Sign-out by revoking the presented refresh token")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task<IResult> SignInAsync(
        SignInRequest request,
        UserManager<ApplicationUser> userManager,
        AuthTokenService tokenService,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Problem(
                title: "Unauthorized",
                detail: "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var tokens = await tokenService.IssueTokensAsync(user, cancellationToken);
        if (tokens is null)
        {
            return Results.Problem(
                title: "Unauthorized",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(new TokenResponse(tokens.Value.AccessToken, tokens.Value.RefreshToken, tokens.Value.ExpiresAt));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        AuthTokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.RefreshToken)] = ["Refresh token is required."],
            });
        }

        var tokens = await tokenService.RotateRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (tokens is null)
        {
            return Results.Problem(
                title: "Unauthorized",
                detail: "Refresh token is invalid or expired.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(new TokenResponse(tokens.Value.AccessToken, tokens.Value.RefreshToken, tokens.Value.ExpiresAt));
    }

    private static async Task<IResult> SignOutAsync(
        SignOutRequest request,
        AuthTokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.RefreshToken)] = ["Refresh token is required."],
            });
        }

        var revoked = await tokenService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (!revoked)
        {
            return Results.Problem(
                title: "Bad Request",
                detail: "Refresh token was not found or already revoked.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.NoContent();
    }
}