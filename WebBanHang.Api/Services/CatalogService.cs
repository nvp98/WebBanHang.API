using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;

namespace WebBanHang.Api.Services;

/// <summary>Read side of the storefront: turns legacy Product/ProductCategory/ProductManufacturer rows into frontend DTOs.</summary>
public class CatalogService(AppDbContext db, MediaService media)
{
    /// <summary>Inventory is not tracked in the legacy schema; products are sold on quotation (B2B).</summary>
    private const int AssumedStock = 999;
    /// <summary>The storefront shows the "Bán chạy" badge from sold ≥ 100; IsSellers flags best sellers.</summary>
    private const int BestSellerSold = 100;

    // ─── Categories ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(int lang, CancellationToken ct)
    {
        var categories = await db.ProductCategories.AsNoTracking().Active().InLanguage(lang)
            .OrderByDescending(c => c.Levels).ThenBy(c => c.Pk)
            .ToListAsync(ct);

        var counts = await db.Products.Active().InLanguage(lang)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return categories.Select(c => ToDto(c, counts.GetValueOrDefault(c.Id ?? c.Pk))).ToList();
    }

    public async Task<CategoryDto> GetCategoryAsync(string slug, int lang, CancellationToken ct)
    {
        var id = await ResolveCategoryIdAsync(slug, ct) ?? throw ApiException.NotFound("Không tìm thấy danh mục");
        var category = await db.ProductCategories.AsNoTracking().Active()
            .Where(c => (c.Id ?? c.Pk) == id)
            .OrderBy(c => (c.LanguageId ?? Lang.Vi) == lang ? 0 : 1)
            .FirstAsync(ct);
        var count = await db.Products.Active().InLanguage(lang).CountAsync(p => p.CategoryId == id, ct);
        return ToDto(category, count);
    }

    private async Task<int?> ResolveCategoryIdAsync(string slug, CancellationToken ct)
    {
        var (vi, en) = Slug.Candidates(slug);
        var id = await db.ProductCategories.Active()
            .Where(c => c.TitleUrl == vi || c.TitleUrl == en)
            .Select(c => (int?)(c.Id ?? c.Pk))
            .FirstOrDefaultAsync(ct);
        if (id is not null) return id;

        var trailing = Slug.TrailingId(slug);
        if (trailing is null) return null;
        var exists = await db.ProductCategories.Active().AnyAsync(c => (c.Id ?? c.Pk) == trailing, ct);
        return exists ? trailing : null;
    }

    private CategoryDto ToDto(ProductCategory c, int? productCount) => new(
        (c.Id ?? c.Pk).ToString(),
        c.Title,
        Slug.For(c.TitleUrl, c.Title, c.Id ?? c.Pk),
        HtmlText.ToPlainText(c.ShortText) ?? HtmlText.ToPlainText(c.Description),
        media.Url(MediaKind.ProductCategory, c.Images),
        productCount);

    // ─── Brands (ProductManufacturer) ───────────────────────────────────────

    public async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(int lang, CancellationToken ct)
    {
        var brands = await LoadBrandsAsync(lang, ct);
        var counts = await db.Products.Active().InLanguage(lang)
            .Where(p => p.ManufacturerId != null)
            .GroupBy(p => p.ManufacturerId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return brands.Values
            .Select(b => b with { ProductCount = counts.GetValueOrDefault(int.Parse(b.Id)) })
            .OrderBy(b => b.Name)
            .ToList();
    }

    /// <summary>Logical manufacturer id → brand, one entry per id (falls back to the vi row).</summary>
    private async Task<Dictionary<int, BrandDto>> LoadBrandsAsync(int lang, CancellationToken ct)
    {
        var rows = await db.Manufacturers.AsNoTracking().Active()
            .OrderBy(m => (m.LanguageId ?? Lang.Vi) == lang ? 0 : 1)
            .ToListAsync(ct);

        var result = new Dictionary<int, BrandDto>();
        foreach (var m in rows)
        {
            var id = m.Id ?? m.Pk;
            if (result.ContainsKey(id) || string.IsNullOrWhiteSpace(m.Title)) continue;
            result[id] = new BrandDto(id.ToString(), m.Title.Trim(), Slug.Generate(m.Title), media.Url(MediaKind.Manufacturer, m.Images), null);
        }
        return result;
    }

    private async Task<Dictionary<int, CategoryDto>> LoadCategoryRefsAsync(int lang, CancellationToken ct)
    {
        var rows = await db.ProductCategories.AsNoTracking().Active()
            .OrderBy(c => (c.LanguageId ?? Lang.Vi) == lang ? 0 : 1)
            .ToListAsync(ct);

        var result = new Dictionary<int, CategoryDto>();
        foreach (var c in rows)
        {
            var id = c.Id ?? c.Pk;
            if (!result.ContainsKey(id)) result[id] = ToDto(c, null);
        }
        return result;
    }

    // ─── Products ───────────────────────────────────────────────────────────

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductQuery query, int lang, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking().Active().InLanguage(lang);
        var empty = new PagedResult<ProductDto>([], 0, Math.Max(1, query.Page), Math.Clamp(query.PageSize, 1, 100));

        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
        {
            var categoryId = await ResolveCategoryIdAsync(query.CategorySlug.Trim(), ct);
            if (categoryId is null) return empty;
            q = q.Where(p => p.CategoryId == categoryId);
        }

        var brands = await LoadBrandsAsync(lang, ct);
        if (!string.IsNullOrWhiteSpace(query.BrandSlug))
        {
            var wanted = query.BrandSlug.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ids = brands.Where(b => wanted.Contains(b.Value.Slug)).Select(b => b.Key).ToList();
            if (ids.Count == 0) return empty;
            q = q.Where(p => p.ManufacturerId != null && ids.Contains(p.ManufacturerId.Value));
        }

        if (query.MinPrice is { } min) q = q.Where(p => (p.Price ?? 0) >= min);
        if (query.MaxPrice is { } max) q = q.Where(p => (p.Price ?? 0) <= max);
        if (query.IsNew == true) q = q.Where(p => p.IsNew == 1);
        if (query.IsFeatured == true) q = q.Where(p => p.IsOnHomepage == 1);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(p => p.Title!.Contains(term) || p.Code!.Contains(term));
        }

        q = query.SortBy switch
        {
            "price_asc" => q.OrderBy(p => p.Price).ThenByDescending(p => p.Pk),
            "price_desc" => q.OrderByDescending(p => p.Price).ThenByDescending(p => p.Pk),
            "newest" => q.OrderByDescending(p => p.CreateDate).ThenByDescending(p => p.Pk),
            "best_seller" => q.OrderByDescending(p => p.IsSellers).ThenByDescending(p => p.PCount).ThenByDescending(p => p.Pk),
            "rating" => q.OrderByDescending(p => p.SEvaluation).ThenByDescending(p => p.Pk),
            _ => q.OrderByDescending(p => p.Levels).ThenByDescending(p => p.Pk),
        };

        var page = await q.ToPagedAsync(query.Page, query.PageSize, ct);
        var items = await MapAsync(page.Items, lang, brands, withDetails: false, ct);
        return new PagedResult<ProductDto>(items, page.Total, page.Page, page.PageSize);
    }

    public async Task<ProductDto> GetProductAsync(string slug, int lang, CancellationToken ct)
    {
        var (vi, en) = Slug.Candidates(slug);
        var id = await db.Products.Active()
            .Where(p => p.TitleUrl == vi || p.TitleUrl == en)
            .Select(p => (int?)(p.Id ?? p.Pk))
            .FirstOrDefaultAsync(ct);

        if (id is null && Slug.TrailingId(slug) is { } trailing &&
            await db.Products.Active().AnyAsync(p => (p.Id ?? p.Pk) == trailing, ct))
            id = trailing;

        if (id is null) throw ApiException.NotFound("Không tìm thấy sản phẩm");

        var product = await db.Products.AsNoTracking().Active()
            .Where(p => (p.Id ?? p.Pk) == id)
            .OrderBy(p => (p.LanguageId ?? Lang.Vi) == lang ? 0 : 1)
            .FirstAsync(ct);

        var brands = await LoadBrandsAsync(lang, ct);
        return (await MapAsync([product], lang, brands, withDetails: true, ct))[0];
    }

    public async Task<IReadOnlyList<ProductDto>> GetRelatedAsync(int productId, int lang, CancellationToken ct)
    {
        var categoryId = await db.Products.Active()
            .Where(p => (p.Id ?? p.Pk) == productId)
            .Select(p => (int?)p.CategoryId)
            .FirstOrDefaultAsync(ct);
        if (categoryId is null) return [];

        var rows = await db.Products.AsNoTracking().Active().InLanguage(lang)
            .Where(p => p.CategoryId == categoryId && (p.Id ?? p.Pk) != productId)
            .OrderByDescending(p => p.Levels).ThenByDescending(p => p.Pk)
            .Take(8)
            .ToListAsync(ct);
        return await MapAsync(rows, lang, await LoadBrandsAsync(lang, ct), withDetails: false, ct);
    }

    /// <summary>Flagged products first; falls back to the newest ones so homepage sections are never empty.</summary>
    public async Task<IReadOnlyList<ProductDto>> GetHighlightedAsync(string kind, int lang, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking().Active().InLanguage(lang);
        var flagged = kind switch
        {
            "featured" => q.Where(p => p.IsOnHomepage == 1),
            "new" => q.Where(p => p.IsNew == 1),
            "best-sellers" => q.Where(p => p.IsSellers == 1),
            _ => throw ApiException.NotFound(),
        };

        var rows = await flagged.OrderByDescending(p => p.Levels).ThenByDescending(p => p.Pk).Take(12).ToListAsync(ct);
        if (rows.Count == 0)
            rows = await q.OrderByDescending(p => p.CreateDate).ThenByDescending(p => p.Pk).Take(12).ToListAsync(ct);

        return await MapAsync(rows, lang, await LoadBrandsAsync(lang, ct), withDetails: false, ct);
    }

    private async Task<IReadOnlyList<ProductDto>> MapAsync(
        IReadOnlyList<Product> products, int lang, Dictionary<int, BrandDto> brands, bool withDetails, CancellationToken ct)
    {
        if (products.Count == 0) return [];

        var categories = await LoadCategoryRefsAsync(lang, ct);
        var ids = products.Select(p => p.Id ?? p.Pk).ToList();

        var gallery = await db.ProductImages.AsNoTracking().Active()
            .Where(i => i.ProductId != null && ids.Contains(i.ProductId.Value))
            .OrderByDescending(i => i.Levels).ThenBy(i => i.Pk)
            .ToListAsync(ct);
        var galleryByProduct = gallery.ToLookup(i => i.ProductId!.Value);

        Dictionary<int, List<string>> standards = [];
        if (withDetails)
        {
            standards = (await (
                    from link in db.ProductStandardIsos
                    join iso in db.StandardIsos on link.StandardIsoId equals iso.Id
                    where ids.Contains(link.ProductId ?? 0)
                          && (link.IsDeleted ?? 0) == 0 && (iso.IsDeleted ?? 0) == 0
                          && (iso.LanguageId ?? Lang.Vi) == Lang.Vi
                    select new { ProductId = link.ProductId!.Value, iso.Title })
                .Distinct()
                .ToListAsync(ct))
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Title ?? "").Where(t => t != "").ToList());
        }

        return products.Select(p =>
        {
            var id = p.Id ?? p.Pk;
            var category = categories.GetValueOrDefault(p.CategoryId)
                ?? new CategoryDto(p.CategoryId.ToString(), "", p.CategoryId.ToString(), null, null, null);
            var brand = p.ManufacturerId is { } mid ? brands.GetValueOrDefault(mid) : null;
            var onSale = p.InPromotion == 1 && p.PromotionalPrice is > 0;

            return new ProductDto
            {
                Id = id.ToString(),
                Name = p.Title ?? "",
                Slug = Slug.For(p.TitleUrl, p.Title, id),
                Sku = p.Code ?? "",
                Description = withDetails ? media.AbsolutizeHtml(p.Description) ?? "" : "",
                ShortDescription = HtmlText.ToPlainText(p.ShortText) ?? HtmlText.ToPlainText(p.Description),
                Price = p.Price ?? 0,
                SalePrice = onSale ? p.PromotionalPrice : null,
                DiscountPercent = onSale ? p.PromotionalPercent : null,
                Stock = AssumedStock,
                Sold = Math.Max(p.PCount ?? 0, p.IsSellers == 1 ? BestSellerSold : 0),
                Category = category,
                Brand = brand,
                Images = BuildImages(p, galleryByProduct[id]),
                Specs = withDetails ? BuildSpecs(p, brand, standards.GetValueOrDefault(id), lang) : [],
                IsFeatured = p.IsOnHomepage == 1,
                IsNew = p.IsNew == 1,
                CreatedAt = p.CreateDate ?? p.ModifyDate ?? DateTime.MinValue,
                CatalogueUrl = withDetails ? media.Url(MediaKind.Document, p.FileCatalogue) : null,
                HighlightsHtml = withDetails && !string.IsNullOrWhiteSpace(p.Highlights) ? media.AbsolutizeHtml(p.Highlights) : null,
                MetaTitle = withDetails ? p.MetaTitle : null,
                MetaDescription = withDetails ? p.MetaDescription : null,
            };
        }).ToList();
    }

    private List<ProductImageDto> BuildImages(Product p, IEnumerable<ProductImage> gallery)
    {
        var images = new List<ProductImageDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string id, string? file, string? alt)
        {
            var url = media.Url(MediaKind.Product, file);
            if (url is null || !seen.Add(url)) return;
            images.Add(new ProductImageDto(id, url, alt ?? p.Title, images.Count == 0));
        }

        Add($"p{p.Pk}", p.BigImages, p.Title);
        Add($"p{p.Pk}-thumb", p.Images, p.Title);
        foreach (var g in gallery) Add($"g{g.Pk}", g.Images, g.Title);
        return images;
    }

    private static List<ProductSpecDto> BuildSpecs(Product p, BrandDto? brand, List<string>? linkedStandards, int lang)
    {
        var en = lang == Lang.En;
        var specs = new List<ProductSpecDto>();
        if (!string.IsNullOrWhiteSpace(p.Code)) specs.Add(new(en ? "Model" : "Mã sản phẩm", p.Code.Trim()));
        if (brand is not null) specs.Add(new(en ? "Brand" : "Thương hiệu", brand.Name));

        var standards = !string.IsNullOrWhiteSpace(p.StandardIso)
            ? HtmlText.ToPlainText(p.StandardIso, 1000)
            : linkedStandards is { Count: > 0 } ? string.Join(", ", linkedStandards) : null;
        if (!string.IsNullOrWhiteSpace(standards)) specs.Add(new(en ? "Standards" : "Tiêu chuẩn", standards));
        return specs;
    }
}
