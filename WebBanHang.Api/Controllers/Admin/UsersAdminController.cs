using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers.Admin;

/// <summary>Back-office accounts: dbo.Logons + Members (profile) + Assigns (group). Admin only.</summary>
[ApiController]
[Route("api/admin/users")]
[Tags("Admin · Hệ thống")]
[Authorize(Policy = Policies.Admin)]
public class UsersAdminController(AppDbContext db, AuthService auth, ICurrentUser currentUser) : ControllerBase
{
    private const int AdminGroupId = 1;
    private const int StaffGroupId = 2;

    [HttpGet]
    public async Task<ApiResponse<List<UserAdminDto>>> List([FromQuery] bool deleted = false, CancellationToken ct = default)
    {
        var logons = await db.Logons.AsNoTracking()
            .Where(l => deleted ? l.IsDeleted == 1 : l.IsDeleted == null || l.IsDeleted == 0)
            .OrderBy(l => l.Username).ToListAsync(ct);
        var result = new List<UserAdminDto>();
        foreach (var l in logons) result.Add(await ToDtoAsync(l, ct));
        return ApiResult.Ok(result);
    }

    [HttpGet("{pk:int}")]
    public async Task<ApiResponse<UserAdminDto>> Get(int pk, CancellationToken ct) =>
        ApiResult.Ok(await ToDtoAsync(await FindAsync(pk, ct), ct));

    [HttpPost]
    public async Task<ApiResponse<UserAdminDto>> Create(AdminUserCreate input, CancellationToken ct)
    {
        var username = input.Username.Trim();
        if (await db.Logons.Active().AnyAsync(l => l.Username == username, ct))
            throw ApiException.Conflict("Tên đăng nhập đã tồn tại");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var logon = new Logon { Username = username, Password = Passwords.Hash(input.Password), IsActived = 1, IsSeo = 0 };
        await db.InsertAsync(logon, null, ct);
        await SaveProfileAsync(logon, input.FullName, input.Email, input.Phone, input.Role, ct);
        await tx.CommitAsync(ct);

        return ApiResult.Ok(await ToDtoAsync(logon, ct), "Đã tạo tài khoản");
    }

    [HttpPut("{pk:int}")]
    public async Task<ApiResponse<UserAdminDto>> Update(int pk, AdminUserUpdate input, CancellationToken ct)
    {
        var logon = await FindAsync(pk, ct, tracking: true);
        if (pk == currentUser.AdminId && (!input.IsActive || input.Role != Roles.Admin))
            throw ApiException.BadRequest("Không thể tự khoá hoặc tự hạ quyền tài khoản đang đăng nhập");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        logon.IsActived = input.IsActive ? 1 : 0;
        await db.SaveChangesAsync(ct);
        await SaveProfileAsync(logon, input.FullName, input.Email, input.Phone, input.Role, ct);
        await tx.CommitAsync(ct);

        return ApiResult.Ok(await ToDtoAsync(logon, ct), "Đã lưu");
    }

    [HttpPut("{pk:int}/password")]
    public async Task<ApiResponse<object?>> ResetPassword(int pk, ResetPasswordInput input, CancellationToken ct)
    {
        var logon = await FindAsync(pk, ct, tracking: true);
        logon.Password = Passwords.Hash(input.NewPassword);
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, "Đã đặt lại mật khẩu");
    }

    [HttpDelete("{pk:int}")]
    public async Task<ApiResponse<object?>> Delete(int pk, CancellationToken ct)
    {
        if (pk == currentUser.AdminId) throw ApiException.BadRequest("Không thể xoá tài khoản đang đăng nhập");
        var logon = await FindAsync(pk, ct, tracking: true);
        logon.IsDeleted = 1;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, "Đã xoá tài khoản");
    }

    private async Task<Logon> FindAsync(int pk, CancellationToken ct, bool tracking = false)
    {
        var q = db.Logons.Active().Where(l => l.Pk == pk);
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct) ?? throw ApiException.NotFound("Tài khoản không tồn tại");
    }

    private async Task SaveProfileAsync(Logon logon, string? fullName, string? email, string? phone, string role, CancellationToken ct)
    {
        var logicalId = logon.Id ?? logon.Pk;

        var member = await db.Members.Active().Where(m => m.LogonsId == logicalId).OrderByDescending(m => m.Pk).FirstOrDefaultAsync(ct);
        if (member is null)
        {
            member = new Member { LogonsId = logicalId, IsStatus = 1, IsStaff = 1, LanguageId = Lang.Vi };
            ApplyProfile(member, fullName, email, phone);
            await db.InsertAsync(member, null, ct);
        }
        else
        {
            ApplyProfile(member, fullName, email, phone);
        }

        var groupId = role == Roles.Admin ? AdminGroupId : StaffGroupId;
        var assigns = await db.Assigns.Active().Where(a => a.LogonsId == logicalId).ToListAsync(ct);
        foreach (var a in assigns.Where(a => a.GroupsId != groupId)) a.IsDeleted = 1;
        await db.SaveChangesAsync(ct);

        if (assigns.All(a => a.GroupsId != groupId))
            await db.InsertAsync(new Assign { LogonsId = logicalId, GroupsId = groupId, LanguageId = Lang.Vi }, null, ct);
    }

    private static void ApplyProfile(Member m, string? fullName, string? email, string? phone)
    {
        m.Fullname = fullName?.Trim();
        m.Email = email?.Trim();
        m.Phone = phone?.Trim();
    }

    private async Task<UserAdminDto> ToDtoAsync(Logon l, CancellationToken ct)
    {
        var logicalId = l.Id ?? l.Pk;
        var member = await db.Members.AsNoTracking().Active().Where(m => m.LogonsId == logicalId).OrderByDescending(m => m.Pk).FirstOrDefaultAsync(ct);
        return new UserAdminDto(l.Pk, l.Username, member?.Fullname, member?.Email, member?.Phone,
            await auth.AdminRoleAsync(l, ct), (l.IsActived ?? 1) == 1, l.CreateDate, l.ModifyDate);
    }
}

public record UserAdminDto(int Pk, string Username, string? FullName, string? Email, string? Phone, string Role, bool IsActive, DateTime? CreateDate, DateTime? ModifyDate);
