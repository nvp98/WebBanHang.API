using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Models;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers;

/// <summary>
/// "Đơn hàng của tôi" — quote requests of the signed-in customer. The legacy Orders table has no user FK,
/// so ownership is matched on the account e-mail.
/// </summary>
[ApiController]
[Route("api/orders")]
[Tags("Storefront")]
[Authorize(Roles = Roles.Customer)]
public class OrdersController(AppDbContext db, OrderService orders, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ApiResponse<IReadOnlyList<OrderDto>>> List(CancellationToken ct)
    {
        var email = await CustomerEmailAsync(ct);
        var rows = await db.Orders.AsNoTracking().Active()
            .Where(o => o.Email == email)
            .OrderByDescending(o => o.Pk)
            .Take(100)
            .ToListAsync(ct);
        return ApiResult.Ok(await orders.MapAsync(rows, Lang.FromRequest(Request), ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ApiResponse<OrderDto>> Get(int id, CancellationToken ct)
    {
        var order = await FindOwnAsync(id, ct);
        return ApiResult.Ok((await orders.MapAsync([order], Lang.FromRequest(Request), ct))[0]);
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<ApiResponse<OrderDto>> Cancel(int id, CancellationToken ct)
    {
        var order = await FindOwnAsync(id, ct, tracking: true);
        if ((order.Status ?? OrderStatuses.Pending) != OrderStatuses.Pending)
            throw ApiException.BadRequest("Chỉ huỷ được yêu cầu đang chờ xử lý");

        order.Status = OrderStatuses.Cancelled;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok((await orders.MapAsync([order], Lang.FromRequest(Request), ct))[0], "Đã huỷ yêu cầu");
    }

    private async Task<string> CustomerEmailAsync(CancellationToken ct)
    {
        var id = currentUser.CustomerId!.Value;
        return await db.Accounts.Where(a => a.Pk == id).Select(a => a.Email).FirstOrDefaultAsync(ct)
               ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Tài khoản không tồn tại");
    }

    private async Task<Data.Entities.Order> FindOwnAsync(int id, CancellationToken ct, bool tracking = false)
    {
        var email = await CustomerEmailAsync(ct);
        var q = db.Orders.Active().Where(o => (o.Id ?? o.Pk) == id && o.Email == email);
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(ct) ?? throw ApiException.NotFound("Không tìm thấy đơn hàng");
    }
}
