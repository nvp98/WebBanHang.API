using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers.Admin;

/// <summary>Quote requests (dbo.Orders + dbo.ordersdetailed). Addressed by the logical id shown as BG000123.</summary>
[ApiController]
[Route("api/admin/quotes")]
[Tags("Admin · Báo giá & Liên hệ")]
[Authorize(Policy = Policies.Staff)]
public class QuotesAdminController(AppDbContext db, OrderService orders) : ControllerBase
{
    /// <param name="q">Name, e-mail, phone or code (BG000123).</param>
    /// <param name="status">pending | confirmed | processing | shipping | delivered | cancelled</param>
    /// <param name="deleted">Show the trash instead of active rows.</param>
    [HttpGet]
    public async Task<ApiResponse<PagedResult<OrderDto>>> List(
        [FromQuery] string? q, [FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] bool deleted = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = deleted ? db.Orders.AsNoTracking().Where(o => o.IsDeleted == 1) : db.Orders.AsNoTracking().Active();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = OrderStatuses.FromName(status) ?? throw ApiException.BadRequest("Trạng thái không hợp lệ");
            query = s == OrderStatuses.Pending ? query.Where(o => o.Status == null || o.Status == s) : query.Where(o => o.Status == s);
        }
        if (from is not null) query = query.Where(o => o.CreateDate >= from);
        if (to is not null) query = query.Where(o => o.CreateDate < to.Value.Date.AddDays(1));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var codeId = term.StartsWith("BG", StringComparison.OrdinalIgnoreCase) && int.TryParse(term[2..], out var n) ? n : (int?)null;
            query = codeId is not null
                ? query.Where(o => (o.Id ?? o.Pk) == codeId)
                : query.Where(o => o.FullName!.Contains(term) || o.Email!.Contains(term) || o.Phone!.Contains(term));
        }

        var paged = await query.OrderByDescending(o => o.Pk).ToPagedAsync(page, pageSize, ct);
        var items = await orders.MapAsync(paged.Items, Lang.Vi, ct);
        return ApiResult.Ok(new PagedResult<OrderDto>(items, paged.Total, paged.Page, paged.PageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ApiResponse<OrderDto>> Get(int id, CancellationToken ct)
    {
        var order = await FindAsync(id, ct, tracking: false);
        return ApiResult.Ok((await orders.MapAsync([order], Lang.Vi, ct))[0]);
    }

    [HttpPut("{id:int}")]
    public async Task<ApiResponse<OrderDto>> Update(int id, OrderUpdate input, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        if (input.FullName is not null) order.FullName = input.FullName.Trim();
        if (input.Email is not null) order.Email = input.Email.Trim();
        if (input.Phone is not null) order.Phone = input.Phone.Trim();
        if (input.Address is not null) order.Address = input.Address.Trim();
        if (input.Note is not null) order.Note = input.Note;
        if (input.DeliveryDate is not null) order.DateOfDelivery = input.DeliveryDate;
        if (input.Status is not null) order.Status = OrderStatuses.FromName(input.Status) ?? throw ApiException.BadRequest("Trạng thái không hợp lệ");
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok((await orders.MapAsync([order], Lang.Vi, ct))[0], "Đã lưu");
    }

    [HttpPut("{id:int}/status")]
    public async Task<ApiResponse<OrderDto>> UpdateStatus(int id, OrderStatusUpdate input, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        order.Status = OrderStatuses.FromName(input.Status) ?? throw ApiException.BadRequest("Trạng thái không hợp lệ");
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok((await orders.MapAsync([order], Lang.Vi, ct))[0], "Đã cập nhật trạng thái");
    }

    /// <summary>Replaces the quoted lines — used to enter quantities and unit prices for the customer.</summary>
    [HttpPut("{id:int}/items")]
    public async Task<ApiResponse<OrderDto>> ReplaceItems(int id, QuoteItemsInput input, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        var orderId = order.Id ?? order.Pk;

        var productIds = input.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = (await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id ?? p.Pk))
                .Select(p => new { Id = p.Id ?? p.Pk, p.BigImages, p.Images, p.LanguageId }).ToListAsync(ct))
            .OrderBy(p => p.LanguageId ?? Lang.Vi)
            .GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
        var missing = productIds.Where(pid => !products.ContainsKey(pid)).ToList();
        if (missing.Count > 0) throw ApiException.BadRequest($"Sản phẩm không tồn tại: {string.Join(", ", missing)}");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var old = await db.OrderDetails.Active().Where(d => d.OrderId == orderId).ToListAsync(ct);
        foreach (var d in old) d.IsDeleted = 1;

        var added = input.Items.Select(i => new OrderDetail
        {
            Id = 0,
            OrderId = orderId,
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            Amount = i.UnitPrice,
            Images = products[i.ProductId].BigImages ?? products[i.ProductId].Images,
            LanguageId = Lang.Vi,
        }).ToList();
        db.OrderDetails.AddRange(added);
        await db.SaveChangesAsync(ct);
        foreach (var d in added) d.Id = d.Pk;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ApiResult.Ok((await orders.MapAsync([order], Lang.Vi, ct))[0], "Đã cập nhật báo giá");
    }

    [HttpDelete("{id:int}")]
    public async Task<ApiResponse<object?>> Delete(int id, CancellationToken ct)
    {
        var order = await FindAsync(id, ct);
        order.IsDeleted = 1;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, "Đã chuyển vào thùng rác");
    }

    [HttpPost("{id:int}/restore")]
    public async Task<ApiResponse<object?>> Restore(int id, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => (o.Id ?? o.Pk) == id, ct) ?? throw ApiException.NotFound("Không tìm thấy yêu cầu báo giá");
        order.IsDeleted = 0;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, "Đã khôi phục");
    }

    /// <summary>Moves many quote requests to the trash at once (spam clean-up).</summary>
    [HttpPost("bulk-delete")]
    public async Task<ApiResponse<object?>> BulkDelete(BulkIdsInput input, CancellationToken ct)
    {
        var count = await db.Orders.Where(o => input.Ids.Contains(o.Id ?? o.Pk))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsDeleted, 1).SetProperty(o => o.ModifyDate, DateTime.Now), ct);
        return ApiResult.Ok<object?>(null, $"Đã xoá {count} yêu cầu");
    }

    private async Task<Order> FindAsync(int id, CancellationToken ct, bool tracking = true)
    {
        var q = db.Orders.Active().Where(o => (o.Id ?? o.Pk) == id);
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct) ?? throw ApiException.NotFound("Không tìm thấy yêu cầu báo giá");
    }
}
