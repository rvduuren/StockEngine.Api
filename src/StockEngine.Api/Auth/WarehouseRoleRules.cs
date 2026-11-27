namespace StockEngine.Api.Auth;

public static class WarehouseRoleRules
{
    public const string Admin = "Admin";
    public const string WarehouseManager = "WarehouseManager";
    public const string Picker = "Picker";
    public const string Packer = "Packer";

    public static readonly IReadOnlyList<string> AllRoles =
        [Admin, WarehouseManager, Picker, Packer];

    private static readonly HashSet<string> WarehouseJobRoles =
        new([WarehouseManager, Picker, Packer], StringComparer.Ordinal);

    /// <summary>
    /// Validates a requested role set for provisioning or replacement (catalog names only; existence in Identity is checked separately).
    /// </summary>
    public static bool TryValidateRoleSet(
        IReadOnlyList<string>? roles,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();

        if (roles is null || roles.Count == 0)
        {
            errors[nameof(roles)] = ["At least one role is required."];
            return false;
        }

        var distinct = roles.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count != roles.Count)
        {
            errors[nameof(roles)] = ["Duplicate role names are not allowed."];
            return false;
        }

        var unknown = roles.Where(r => !AllRoles.Contains(r, StringComparer.Ordinal)).ToList();
        if (unknown.Count > 0)
        {
            errors[nameof(roles)] = [$"Unknown role(s): {string.Join(", ", unknown)}."];
            return false;
        }

        var hasAdmin = roles.Contains(Admin, StringComparer.Ordinal);
        if (hasAdmin && roles.Count > 1)
        {
            errors[nameof(roles)] = ["When Admin is assigned, it must be the only role."];
            return false;
        }

        if (!hasAdmin && !roles.Any(r => WarehouseJobRoles.Contains(r)))
        {
            errors[nameof(roles)] =
            [
                "Non-admin users must have at least one warehouse job role (WarehouseManager, Picker, or Packer).",
            ];
            return false;
        }

        return true;
    }

    public static bool RoleSetsEqual(IReadOnlyList<string> current, IReadOnlyList<string> requested) =>
        new HashSet<string>(current, StringComparer.Ordinal)
            .SetEquals(requested);
}