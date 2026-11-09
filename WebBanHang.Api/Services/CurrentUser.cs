using System.Security.Claims;

namespace WebBanHang.Api.Services;

public static class Roles
{
    public const string Admin = "admin";
    public const string Staff = "staff";
    public const string Customer = "customer";
}

public interface ICurrentUser
{
    int? Id { get; }
    string? Role { get; }
    string? Email { get; }
    /// <summary>Logons.Pk when the caller is a back-office user (used for CreateBy/ModifyBy).</summary>
    int? AdminId { get; }
    /// <summary>Account.Pk when the caller is a storefront customer.</summary>
    int? CustomerId { get; }
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int? Id => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);
    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public int? AdminId => Role is Roles.Admin or Roles.Staff ? Id : null;
    public int? CustomerId => Role == Roles.Customer ? Id : null;
}
