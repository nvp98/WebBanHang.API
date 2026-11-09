using System.Text;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;

namespace WebBanHang.Api.Services;

/// <summary>Quote requests (Orders + ordersdetailed) and contact messages (Contacts).</summary>
public class OrderService(AppDbContext db, MediaService media)
{
    public async Task<QuoteCreatedDto> CreateQuoteAsync(QuoteRequest request, CancellationToken ct)
    {
        var quantities = new Dictionary<int, int>();
        foreach (var item in request.Items)
        {
            if (!int.TryParse(item.ProductId, out var pid))
                throw ApiException.BadRequest($"Mã sản phẩm không hợp lệ: {item.ProductId}");
            quantities[pid] = quantities.GetValueOrDefault(pid) + item.Quantity;
        }

        var ids = quantities.Keys.ToList();
        var products = (await db.Products.AsNoTracking().Active()
                .Where(p => ids.Contains(p.Id ?? p.Pk))
                .OrderBy(p => p.LanguageId ?? Lang.Vi)
                .ToListAsync(ct))
            .GroupBy(p => p.Id ?? p.Pk)
            .ToDictionary(g => g.Key, g => g.First());

        var missing = ids.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw ApiException.BadRequest($"Sản phẩm không tồn tại hoặc đã ngừng kinh doanh: {string.Join(", ", missing)}");

        var note = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.Company)) note.AppendLine($"Công ty: {request.Company.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.Note)) note.Append(request.Note.Trim());

        var order = new Order
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            Address = request.Address?.Trim(),
            Note = note.ToString().Trim(),
            Status = OrderStatuses.Pending,
            LanguageId = Lang.Vi,
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.InsertAsync(order, null, ct);

        foreach (var (productId, quantity) in quantities)
        {
            var p = products[productId];
            db.OrderDetails.Add(new OrderDetail
            {
                Id = 0,
                OrderId = order.Id,
                ProductId = productId,
                Quantity = quantity,
                Amount = p.PromotionalPrice is > 0 && p.InPromotion == 1 ? p.PromotionalPrice : p.Price ?? 0,
                Images = p.BigImages ?? p.Images,
                LanguageId = Lang.Vi,
            });
        }
        await db.SaveChangesAsync(ct);

        foreach (var d in db.ChangeTracker.Entries<OrderDetail>().Select(e => e.Entity).Where(d => d.Id == 0))
            d.Id = d.Pk;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new QuoteCreatedDto(OrderStatuses.Code(order.Id!.Value));
    }

    public async Task CreateContactAsync(ContactRequest request, CancellationToken ct)
    {
        var contents = string.IsNullOrWhiteSpace(request.Company)
            ? request.Message.Trim()
            : $"Công ty: {request.Company.Trim()}\n{request.Message.Trim()}";

        await db.InsertAsync(new Contact
        {
            Fullname = request.FullName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            Contents = contents,
            IsProcess = 0,
            LanguageId = Lang.Vi,
        }, null, ct);
    }

    /// <summary>Maps orders (with their lines) to the frontend Order shape.</summary>
    public async Task<IReadOnlyList<OrderDto>> MapAsync(IReadOnlyList<Order> orders, int lang, CancellationToken ct)
    {
        if (orders.Count == 0) return [];

        var orderIds = orders.Select(o => o.Id ?? o.Pk).ToList();
        var details = await db.OrderDetails.AsNoTracking().Active()
            .Where(d => d.OrderId != null && orderIds.Contains(d.OrderId.Value))
            .OrderBy(d => d.Pk)
            .ToListAsync(ct);

        var productIds = details.Where(d => d.ProductId != null).Select(d => d.ProductId!.Value).Distinct().ToList();
        var products = (await db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id ?? p.Pk))
                .Select(p => new { Id = p.Id ?? p.Pk, p.Title, p.TitleUrl, p.BigImages, p.Images, p.LanguageId })
                .ToListAsync(ct))
            .OrderBy(p => (p.LanguageId ?? Lang.Vi) == lang ? 0 : 1)
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var linesByOrder = details.ToLookup(d => d.OrderId!.Value);

        return orders.Select(o =>
        {
            var id = o.Id ?? o.Pk;
            var items = linesByOrder[id].Select(d =>
            {
                var pid = d.ProductId ?? 0;
                var p = products.GetValueOrDefault(pid);
                var imageUrl = media.Url(MediaKind.Product, d.Images ?? p?.BigImages ?? p?.Images);
                IReadOnlyList<ProductImageDto> images = imageUrl is null ? [] : [new ProductImageDto($"od{d.Pk}", imageUrl, p?.Title, true)];
                var qty = d.Quantity ?? 1;
                var unit = d.Amount ?? 0;
                return new OrderItemDto(
                    (d.Id ?? d.Pk).ToString(),
                    new OrderItemProductDto(pid.ToString(), p?.Title ?? $"#{pid}", Slug.For(p?.TitleUrl, p?.Title, pid), images),
                    qty, unit, qty * unit);
            }).ToList();

            var subtotal = items.Sum(i => i.Subtotal);
            return new OrderDto
            {
                Id = id.ToString(),
                OrderCode = OrderStatuses.Code(id),
                Status = OrderStatuses.ToName(o.Status),
                ShippingAddress = new AddressDto("", o.FullName ?? "", o.Phone ?? "", o.Address ?? "", "", "", "", false),
                Items = items,
                Subtotal = subtotal,
                Total = subtotal,
                Note = o.Note,
                CreatedAt = o.CreateDate ?? DateTime.MinValue,
                UpdatedAt = o.ModifyDate ?? o.CreateDate ?? DateTime.MinValue,
                Email = o.Email,
                DeliveryDate = o.DateOfDelivery,
            };
        }).ToList();
    }
}
