namespace StockEngine.Api.Contracts;

public sealed record SignInRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record SignOutRequest(string RefreshToken);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt);

public sealed record MeResponse(Guid UserId, string? Email, IReadOnlyList<string> Roles);