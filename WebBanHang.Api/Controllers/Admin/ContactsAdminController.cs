using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;

namespace WebBanHang.Api.Controllers.Admin;

/// <summary>Messages from the contact form (dbo.Contacts).</summary>
[ApiController]
[Route("api/admin/contacts")]
[Tags("Admin · Báo giá & Liên hệ")]
[Authorize(Policy = Policies.Staff)]
public class ContactsAdminController(AppDbContext db) : ControllerBase
{
    /// <param name="processed">true = processed only, false = new only, omitted = all.</param>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<Contact>>> List(
        [FromQuery] string? q, [FromQuery] bool? processed, [FromQuery] bool deleted = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = deleted ? db.Contacts.AsNoTracking().Where(c => c.IsDeleted == 1) : db.Contacts.AsNoTracking().Active();
        if (processed is not null) query = query.Where(c => (c.IsProcess ?? 0) == (processed.Value ? 1 : 0));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Fullname!.Contains(term) || c.Email!.Contains(term) || c.Phone!.Contains(term));
        }
        return ApiResult.Ok(await query.OrderByDescending(c => c.Pk).ToPagedAsync(page, pageSize, ct));
    }

    [HttpGet("{pk:int}")]
    public async Task<ApiResponse<Contact>> Get(int pk, CancellationToken ct) =>
        ApiResult.Ok(await db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Pk == pk, ct) ?? throw ApiException.NotFound());

    [HttpPut("{pk:int}/process")]
    public async Task<ApiResponse<Contact>> Process(int pk, ContactProcessInput input, CancellationToken ct)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Pk == pk, ct) ?? throw ApiException.NotFound();
        contact.IsProcess = input.IsProcessed ? 1 : 0;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok(contact, "Đã cập nhật");
    }

    [HttpDelete("{pk:int}")]
    public async Task<ApiResponse<object?>> Delete(int pk, CancellationToken ct)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Pk == pk, ct) ?? throw ApiException.NotFound();
        contact.IsDeleted = 1;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, "Đã chuyển vào thùng rác");
    }

    [HttpPost("{pk:int}/restore")]
    public async Task<ApiResponse<object?>> Restore(int pk, CancellationToken ct)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Pk == pk, ct) ?? throw ApiException.NotFound();
        contact.IsDeleted = 0;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, "Đã khôi phục");
    }

    /// <summary>Moves many messages to the trash at once (spam clean-up).</summary>
    [HttpPost("bulk-delete")]
    public async Task<ApiResponse<object?>> BulkDelete(BulkIdsInput input, CancellationToken ct)
    {
        var count = await db.Contacts.Where(c => input.Ids.Contains(c.Pk))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, 1).SetProperty(c => c.ModifyDate, DateTime.Now), ct);
        return ApiResult.Ok<object?>(null, $"Đã xoá {count} liên hệ");
    }
}
