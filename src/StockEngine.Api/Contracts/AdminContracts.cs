namespace StockEngine.Api.Contracts;

public sealed record CreateUserRequest(string Email, string Password, IReadOnlyList<string> Roles);

public sealed record UpdateUserRolesRequest(IReadOnlyList<string> Roles);

public sealed record ProvisionedUserResponse(Guid Id, string Email, IReadOnlyList<string> Roles);