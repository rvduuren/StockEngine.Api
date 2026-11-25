using Microsoft.AspNetCore.Identity;

namespace StockEngine.Api.Data;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}