using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;

namespace WebBanHang.Api.Services;

public class AuthOptions
{
    /// <summary>
    /// Re-hash legacy MD5 passwords with PBKDF2 after a successful login. Requires Database/upgrade.sql.
    /// Turn off while the old ASP.NET site still authenticates against the same database.
    /// </summary>
    public bool UpgradeLegacyPasswords { get; set; } = true;
}

public class AuthService(AppDbContext db, TokenService tokens, IOptions<AuthOptions> options, ILogger<AuthService> logger)
{
    /// <summary>Groups.IDGroups of the "Admin" group; everyone else in Logons is staff.</summary>
    private const int AdminGroupId = 1;

    private static readonly ApiException InvalidCredentials = new(StatusCodes.Status401Unauthorized, "Sai thông tin đăng nhập");

    // ─── Customers (dbo.Account) ────────────────────────────────────────────

    public async Task<LoginResult> CustomerLoginAsync(LoginRequest request, CancellationToken ct)
    {
        var login = request.Email.Trim();
        var account = await db.Accounts.Active()
            .Where(a => a.Email == login || a.Username == login)
            .FirstOrDefaultAsync(ct);

        if (account is null || !Passwords.Verify(request.Password, account.Password)) throw InvalidCredentials;
        if ((account.IsActived ?? 1) != 1) throw new ApiException(StatusCodes.Status403Forbidden, "Tài khoản chưa được kích hoạt");

        await UpgradeHashAsync(account, request.Password, ct);
        var user = ToUser(account);
        var t = tokens.Issue(account.Pk, Roles.Customer, user.FullName, user.Email);
        return new LoginResult(t.AccessToken, t.RefreshToken, t.ExpiresIn, user);
    }

    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (await db.Accounts.Active().AnyAsync(a => a.Email == email || a.Username == email, ct))
            throw ApiException.Conflict("Email đã được sử dụng");

        var account = new Account
        {
            Username = email,
            Email = email,
            Password = Passwords.Hash(request.Password),
            Name = request.FullName.Trim(),
            Phone = request.Phone?.Trim(),
            IsActived = 1,
        };
        await db.InsertAsync(account, null, ct);
        return ToUser(account);
    }

    public async Task<UserDto> GetCustomerAsync(int id, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().Active().FirstOrDefaultAsync(a => a.Pk == id, ct)
                      ?? throw ApiException.NotFound("Tài khoản không tồn tại");
        return ToUser(account);
    }

    public async Task ChangeCustomerPasswordAsync(int id, ChangePasswordRequest request, CancellationToken ct)
    {
        var account = await db.Accounts.Active().FirstOrDefaultAsync(a => a.Pk == id, ct)
                      ?? throw ApiException.NotFound("Tài khoản không tồn tại");
        if (!Passwords.Verify(request.CurrentPassword, account.Password))
            throw ApiException.BadRequest("Mật khẩu hiện tại không đúng");
        account.Password = Passwords.Hash(request.NewPassword);
        await db.SaveChangesAsync(ct);
    }

    private static UserDto ToUser(Account a) =>
        new(a.Pk.ToString(), a.Email, string.IsNullOrWhiteSpace(a.Name) ? a.Username : a.Name, a.Phone, Roles.Customer, a.CreateDate);

    // ─── Back office (dbo.Logons + Assigns + Members) ───────────────────────

    public async Task<LoginResult> AdminLoginAsync(AdminLoginRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var logon = await db.Logons.Active().FirstOrDefaultAsync(l => l.Username == username, ct);

        if (logon is null || !Passwords.Verify(request.Password, logon.Password)) throw InvalidCredentials;
        if ((logon.IsActived ?? 1) != 1) throw new ApiException(StatusCodes.Status403Forbidden, "Tài khoản đã bị khoá");

        await UpgradeHashAsync(logon, request.Password, ct);
        var user = await ToAdminUserAsync(logon, ct);
        var t = tokens.Issue(logon.Pk, user.Role, user.FullName, user.Email);
        return new LoginResult(t.AccessToken, t.RefreshToken, t.ExpiresIn, user);
    }

    public async Task<UserDto> GetAdminAsync(int id, CancellationToken ct)
    {
        var logon = await db.Logons.AsNoTracking().Active().FirstOrDefaultAsync(l => l.Pk == id, ct)
                    ?? throw ApiException.NotFound("Tài khoản không tồn tại");
        return await ToAdminUserAsync(logon, ct);
    }

    public async Task ChangeAdminPasswordAsync(int id, ChangePasswordRequest request, CancellationToken ct)
    {
        var logon = await db.Logons.Active().FirstOrDefaultAsync(l => l.Pk == id, ct)
                    ?? throw ApiException.NotFound("Tài khoản không tồn tại");
        if (!Passwords.Verify(request.CurrentPassword, logon.Password))
            throw ApiException.BadRequest("Mật khẩu hiện tại không đúng");
        logon.Password = Passwords.Hash(request.NewPassword);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> AdminRoleAsync(Logon logon, CancellationToken ct)
    {
        var logicalId = logon.Id ?? logon.Pk;
        var isAdmin = await db.Assigns.Active().AnyAsync(a => a.LogonsId == logicalId && a.GroupsId == AdminGroupId, ct);
        return isAdmin ? Roles.Admin : Roles.Staff;
    }

    private async Task<UserDto> ToAdminUserAsync(Logon logon, CancellationToken ct)
    {
        var logicalId = logon.Id ?? logon.Pk;
        var member = await db.Members.AsNoTracking().Active()
            .Where(m => m.LogonsId == logicalId)
            .OrderByDescending(m => m.Pk)
            .FirstOrDefaultAsync(ct);
        var role = await AdminRoleAsync(logon, ct);
        var name = string.IsNullOrWhiteSpace(member?.Fullname) ? logon.Username : member.Fullname;
        return new UserDto(logon.Pk.ToString(), member?.Email ?? "", name, member?.Phone, role, logon.CreateDate, logon.Username);
    }

    // ─── Tokens ─────────────────────────────────────────────────────────────

    /// <summary>Validates the refresh token and re-checks that the account still exists and is active.</summary>
    public async Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var principal = tokens.ReadRefreshToken(refreshToken)
                        ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Phiên đăng nhập đã hết hạn");
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            throw new ApiException(StatusCodes.Status401Unauthorized, "Token không hợp lệ");

        if (principal.IsInRole(Roles.Customer))
        {
            var account = await db.Accounts.AsNoTracking().Active().FirstOrDefaultAsync(a => a.Pk == id && (a.IsActived ?? 1) == 1, ct)
                          ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Tài khoản không còn hiệu lực");
            var user = ToUser(account);
            var t = tokens.Issue(account.Pk, Roles.Customer, user.FullName, user.Email);
            return new LoginResult(t.AccessToken, t.RefreshToken, t.ExpiresIn, user);
        }

        var logon = await db.Logons.AsNoTracking().Active().FirstOrDefaultAsync(l => l.Pk == id && (l.IsActived ?? 1) == 1, ct)
                    ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Tài khoản không còn hiệu lực");
        var admin = await ToAdminUserAsync(logon, ct);
        var at = tokens.Issue(logon.Pk, admin.Role, admin.FullName, admin.Email);
        return new LoginResult(at.AccessToken, at.RefreshToken, at.ExpiresIn, admin);
    }

    private async Task UpgradeHashAsync(AuditEntity entity, string password, CancellationToken ct)
    {
        if (!options.Value.UpgradeLegacyPasswords) return;

        var entry = db.Entry(entity);
        var stored = entry.Property<string>("Password").CurrentValue;
        if (!Passwords.IsLegacy(stored)) return;

        entry.Property<string>("Password").CurrentValue = Passwords.Hash(password);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Most likely the Password column is still nvarchar(50): run Database/upgrade.sql.
            logger.LogWarning(ex, "Could not upgrade legacy password hash for {Entity} {Pk}", entity.GetType().Name, entity.Pk);
            entry.Property<string>("Password").CurrentValue = stored;
            entry.State = EntityState.Unchanged;
        }
    }
}
