using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;

namespace WebBanHang.Api.Controllers.Admin;

/// <summary>Storefront customer accounts (dbo.Account). Passwords are never returned.</summary>
[ApiController]
[Route("api/admin/customers")]
[Tags("Admin · Hệ thống")]
[Authorize(Policy = Policies.Staff)]
public class CustomersAdminController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ApiResponse<PagedResult<Account>>> List([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = db.Accounts.AsNoTracking().Active();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a => a.Email.Contains(term) || a.Username.Contains(term) || a.Name!.Contains(term) || a.Phone!.Contains(term));
        }
        return ApiResult.Ok(await query.OrderByDescending(a => a.Pk).ToPagedAsync(page, pageSize, ct));
    }

    [HttpGet("{pk:int}")]
    public async Task<ApiResponse<Account>> Get(int pk, CancellationToken ct) =>
        ApiResult.Ok(await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Pk == pk, ct) ?? throw ApiException.NotFound());

    [HttpPut("{pk:int}/active")]
    public async Task<ApiResponse<Account>> SetActive(int pk, ActiveInput input, CancellationToken ct)
    {
        var account = await db.Accounts.Active().FirstOrDefaultAsync(a => a.Pk == pk, ct) ?? throw ApiException.NotFound();
        account.IsActived = input.IsActive ? 1 : 0;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok(account, input.IsActive ? "Đã kích hoạt" : "Đã khoá tài khoản");
    }

    [HttpDelete("{pk:int}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ApiResponse<object?>> Delete(int pk, CancellationToken ct)
    {
        var account = await db.Accounts.Active().FirstOrDefaultAsync(a => a.Pk == pk, ct) ?? throw ApiException.NotFound();
        account.IsDeleted = 1;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, "Đã xoá tài khoản");
    }
}
