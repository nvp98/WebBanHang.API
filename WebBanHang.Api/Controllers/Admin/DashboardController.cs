using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;

namespace WebBanHang.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/dashboard")]
[Tags("Admin · Hệ thống")]
[Authorize(Policy = Policies.Staff)]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ApiResponse<object>> Get(CancellationToken ct)
    {
        var since = DateTime.Today.AddDays(-29);

        var products = await db.Products.Active().InLanguage(Lang.Vi).CountAsync(ct);
        var categories = await db.ProductCategories.Active().InLanguage(Lang.Vi).CountAsync(ct);
        var brands = await db.Manufacturers.Active().InLanguage(Lang.Vi).CountAsync(ct);
        var quotesPending = await db.Orders.Active().CountAsync(o => (o.Status ?? 0) == OrderStatuses.Pending, ct);
        var quotesLast30Days = await db.Orders.Active().CountAsync(o => o.CreateDate >= since, ct);
        var contactsNew = await db.Contacts.Active().CountAsync(c => (c.IsProcess ?? 0) == 0, ct);

        var quotesByDay = (await db.Orders.Active()
                .Where(o => o.CreateDate >= since)
                .GroupBy(o => o.CreateDate!.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .OrderBy(x => x.Date)
            .ToList();

        var recentQuotes = await db.Orders.AsNoTracking().Active()
            .OrderByDescending(o => o.Pk).Take(5)
            .Select(o => new { Id = o.Id ?? o.Pk, o.FullName, o.Email, o.Phone, Status = o.Status ?? 0, o.CreateDate })
            .ToListAsync(ct);

        var recentContacts = await db.Contacts.AsNoTracking().Active()
            .OrderByDescending(c => c.Pk).Take(5)
            .Select(c => new { c.Pk, c.Fullname, c.Email, c.Phone, IsProcess = c.IsProcess ?? 0, c.CreateDate })
            .ToListAsync(ct);

        return ApiResult.Ok<object>(new
        {
            counts = new { products, categories, brands, quotesPending, quotesLast30Days, contactsNew },
            quotesByDay,
            recentQuotes = recentQuotes.Select(o => new
            {
                id = o.Id.ToString(), code = OrderStatuses.Code(o.Id), o.FullName, o.Email, o.Phone,
                status = OrderStatuses.ToName(o.Status), o.CreateDate,
            }),
            recentContacts,
        });
    }
}
