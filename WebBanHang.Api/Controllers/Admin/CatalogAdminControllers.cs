using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;

namespace WebBanHang.Api.Controllers.Admin;

[Route("api/admin/products")]
[Tags("Admin · Sản phẩm")]
public class ProductsAdminController(AppDbContext db) : CmsCrudController<Product, ProductInput>(db)
{
    protected override void Apply(ProductInput i, Product e)
    {
        e.Title = i.Title.Trim();
        e.Code = i.Code?.Trim();
        e.CategoryId = i.CategoryId;
        e.ManufacturerId = i.ManufacturerId;
        e.ShortText = Html.Clean(i.ShortText);
        e.Description = Html.Clean(i.Description);
        e.Highlights = Html.Clean(i.Highlights);
        e.StandardIso = i.StandardIso?.Trim();
        e.BigImages = i.BigImages?.Trim();
        e.Images = i.Images?.Trim();
        e.FileCatalogue = i.FileCatalogue?.Trim();
        e.Price = i.Price ?? 0;
        e.PromotionalPrice = i.PromotionalPrice;
        e.PromotionalPercent = i.PromotionalPercent;
        e.InPromotion = Flag(i.InPromotion);
        e.IsNew = Flag(i.IsNew);
        e.IsSellers = Flag(i.IsSellers);
        e.IsOnHomepage = Flag(i.IsOnHomepage);
        e.TitleSeo = i.TitleSeo;
        e.SubProductId ??= 0;
        e.SubId ??= 0;
    }

    protected override IQueryable<Product> Search(IQueryable<Product> q, string pattern) =>
        q.Where(p => EF.Functions.Like(p.Title!, pattern, "\\") || EF.Functions.Like(p.Code!, pattern, "\\"));

    protected override IQueryable<Product> FilterByCategory(IQueryable<Product> q, int categoryId) =>
        q.Where(p => p.CategoryId == categoryId);

    protected override async Task ValidateAsync(ProductInput input, Product entity, CancellationToken ct)
    {
        if (!await Db.ProductCategories.Active().AnyAsync(c => (c.Id ?? c.Pk) == input.CategoryId, ct))
            throw ApiException.BadRequest("Danh mục không tồn tại");
        if (input.ManufacturerId is { } mid && !await Db.Manufacturers.Active().AnyAsync(m => (m.Id ?? m.Pk) == mid, ct))
            throw ApiException.BadRequest("Thương hiệu không tồn tại");
    }

    // ─── Gallery (dbo.ImagesProduct) ────────────────────────────────────────

    [HttpGet("{pk:int}/images")]
    public async Task<ApiResponse<List<ProductImage>>> Images(int pk, CancellationToken ct)
    {
        var id = await LogicalIdAsync(pk, ct);
        return ApiResult.Ok(await Db.ProductImages.AsNoTracking().Active().Where(i => i.ProductId == id)
            .OrderByDescending(i => i.Levels).ThenBy(i => i.Pk).ToListAsync(ct));
    }

    [HttpPost("{pk:int}/images")]
    public async Task<ApiResponse<ProductImage>> AddImage(int pk, ProductImageInput input, CancellationToken ct)
    {
        var id = await LogicalIdAsync(pk, ct);
        var image = new ProductImage { ProductId = id, Images = input.Images.Trim(), Title = input.Title, Levels = input.Levels, LanguageId = Common.Lang.Vi };
        await Db.InsertAsync(image, null, ct);
        return ApiResult.Ok(image, "Đã thêm ảnh");
    }

    [HttpDelete("{pk:int}/images/{imagePk:int}")]
    public async Task<ApiResponse<object?>> DeleteImage(int pk, int imagePk, CancellationToken ct)
    {
        var id = await LogicalIdAsync(pk, ct);
        var image = await Db.ProductImages.FirstOrDefaultAsync(i => i.Pk == imagePk && i.ProductId == id, ct) ?? throw ApiException.NotFound();
        image.IsDeleted = 1;
        await Db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, "Đã xoá ảnh");
    }

    // ─── Test standards (dbo.ProductStandardISO) ────────────────────────────

    [HttpGet("{pk:int}/standards")]
    public async Task<ApiResponse<List<int>>> Standards(int pk, CancellationToken ct)
    {
        var id = await LogicalIdAsync(pk, ct);
        return ApiResult.Ok(await Db.ProductStandardIsos.AsNoTracking()
            .Where(l => l.ProductId == id && (l.IsDeleted ?? 0) == 0 && (l.LanguageId ?? Common.Lang.Vi) == Common.Lang.Vi && l.StandardIsoId != null)
            .Select(l => l.StandardIsoId!.Value).Distinct().ToListAsync(ct));
    }

    /// <summary>Replaces the product's standards (stored for both languages, as the legacy admin did).</summary>
    [HttpPut("{pk:int}/standards")]
    public async Task<ApiResponse<List<int>>> SetStandards(int pk, ProductStandardsInput input, CancellationToken ct)
    {
        var id = await LogicalIdAsync(pk, ct);
        var wanted = input.StandardIds.Distinct().ToList();
        var valid = await Db.StandardIsos.Active().Where(s => wanted.Contains(s.Id ?? s.Pk)).Select(s => s.Id ?? s.Pk).Distinct().ToListAsync(ct);
        if (valid.Count != wanted.Count) throw ApiException.BadRequest("Có tiêu chuẩn không tồn tại");

        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        await Db.ProductStandardIsos.Where(l => l.ProductId == id).ExecuteDeleteAsync(ct);
        foreach (var standardId in wanted)
        foreach (var lang in new[] { Common.Lang.Vi, Common.Lang.En })
            Db.ProductStandardIsos.Add(new ProductStandardIso { ProductId = id, StandardIsoId = standardId, LanguageId = lang });
        await Db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ApiResult.Ok(wanted, "Đã cập nhật tiêu chuẩn");
    }
}

[Route("api/admin/product-categories")]
[Tags("Admin · Sản phẩm")]
public class ProductCategoriesAdminController(AppDbContext db) : CmsCrudController<ProductCategory, ProductCategoryInput>(db)
{
    protected override void Apply(ProductCategoryInput i, ProductCategory e)
    {
        e.Title = i.Title.Trim();
        e.ShortText = Html.Clean(i.ShortText);
        e.Description = Html.Clean(i.Description);
        e.Images = i.Images?.Trim();
        e.FileCatalogue = i.FileCatalogue?.Trim();
        e.IsHome = Flag(i.IsHome);
        e.IsMenu = Flag(i.IsMenu);
        e.TitleSeo = i.TitleSeo;
    }

    protected override IQueryable<ProductCategory> Search(IQueryable<ProductCategory> q, string pattern) =>
        q.Where(c => EF.Functions.Like(c.Title, pattern, "\\"));

    protected override async Task EnsureCanDeleteAsync(int logicalId, CancellationToken ct)
    {
        var count = await Db.Products.Active().CountAsync(p => p.CategoryId == logicalId, ct);
        if (count > 0) throw ApiException.Conflict($"Danh mục còn {count} sản phẩm, hãy chuyển hoặc xoá sản phẩm trước");
    }
}

[Route("api/admin/manufacturers")]
[Tags("Admin · Sản phẩm")]
public class ManufacturersAdminController(AppDbContext db) : CmsCrudController<ProductManufacturer, ManufacturerInput>(db)
{
    protected override void Apply(ManufacturerInput i, ProductManufacturer e)
    {
        e.Title = i.Title.Trim();
        e.Description = Html.Clean(i.Description);
        e.Images = i.Images?.Trim();
    }

    protected override IQueryable<ProductManufacturer> Search(IQueryable<ProductManufacturer> q, string pattern) =>
        q.Where(m => EF.Functions.Like(m.Title!, pattern, "\\"));

    protected override IOrderedQueryable<ProductManufacturer> Sort(IQueryable<ProductManufacturer> q) =>
        q.OrderBy(m => m.Title);

    protected override async Task EnsureCanDeleteAsync(int logicalId, CancellationToken ct)
    {
        var count = await Db.Products.Active().CountAsync(p => p.ManufacturerId == logicalId, ct);
        if (count > 0) throw ApiException.Conflict($"Thương hiệu đang gắn với {count} sản phẩm");
    }
}

[Route("api/admin/standards")]
[Tags("Admin · Sản phẩm")]
public class StandardsAdminController(AppDbContext db) : CmsCrudController<StandardIso, StandardIsoInput>(db)
{
    protected override void Apply(StandardIsoInput i, StandardIso e)
    {
        e.Title = i.Title.Trim();
        e.Description = Html.Clean(i.Description);
        e.Images = i.Images?.Trim();
    }

    protected override IQueryable<StandardIso> Search(IQueryable<StandardIso> q, string pattern) =>
        q.Where(s => EF.Functions.Like(s.Title!, pattern, "\\"));

    protected override IOrderedQueryable<StandardIso> Sort(IQueryable<StandardIso> q) => q.OrderBy(s => s.Title);
}
