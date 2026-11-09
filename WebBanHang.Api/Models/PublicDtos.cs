using System.ComponentModel.DataAnnotations;

namespace WebBanHang.Api.Models;

// Shapes mirror frontend/types/index.ts — keep them in sync.

public record CategoryDto(string Id, string Name, string Slug, string? Description, string? ImageUrl, int? ProductCount);

public record BrandDto(string Id, string Name, string Slug, string? LogoUrl, int? ProductCount);

public record ProductImageDto(string Id, string Url, string? AltText, bool IsPrimary);

public record ProductSpecDto(string Key, string Value, string? Unit = null);

public class ProductDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Sku { get; init; }
    /// <summary>HTML. Empty in list responses to keep payloads small.</summary>
    public required string Description { get; init; }
    public string? ShortDescription { get; init; }
    public double Price { get; init; }
    public double? SalePrice { get; init; }
    public double? DiscountPercent { get; init; }
    public int Stock { get; init; }
    public int Sold { get; init; }
    public required CategoryDto Category { get; init; }
    public BrandDto? Brand { get; init; }
    public required IReadOnlyList<ProductImageDto> Images { get; init; }
    public required IReadOnlyList<ProductSpecDto> Specs { get; init; }
    public double AverageRating { get; init; }
    public int ReviewCount { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsNew { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public DateTime CreatedAt { get; init; }

    // Extras from the legacy schema (not in the frontend type yet, safe to ignore).
    public string? CatalogueUrl { get; init; }
    public string? HighlightsHtml { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
}

public class ProductQuery
{
    public string? CategorySlug { get; set; }
    /// <summary>Comma-separated brand slugs.</summary>
    public string? BrandSlug { get; set; }
    public double? MinPrice { get; set; }
    public double? MaxPrice { get; set; }
    public bool? IsNew { get; set; }
    public bool? IsFeatured { get; set; }
    public string? Search { get; set; }
    /// <summary>price_asc | price_desc | newest | best_seller | rating</summary>
    public string? SortBy { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public record NewsDto(
    string Id, string Title, string Slug, string? Excerpt, string? Content, string? ImageUrl,
    string? CategoryId, DateTime? PublishedAt);

public record BannerDto(string Id, string? Title, string? Subtitle, string? ImageUrl, string? LinkUrl, string Position);

public record PartnerDto(string Id, string? Name, string? LogoUrl, string? Url);

public record ServiceDto(string Id, string? Title, string? Excerpt, string? Content, string? ImageUrl, string? VideoUrl);

// ─── Forms ──────────────────────────────────────────────────────────────────

public class ContactRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(50)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(50)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"), StringLength(50)]
    public string Phone { get; set; } = "";

    [StringLength(100)]
    public string? Company { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung"), StringLength(4000)]
    public string Message { get; set; } = "";
}

public class QuoteItemRequest
{
    [Required]
    public string ProductId { get; set; } = "";

    [Range(1, 100_000)]
    public int Quantity { get; set; } = 1;
}

public class QuoteRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(250)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(250)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"), StringLength(50)]
    public string Phone { get; set; } = "";

    [StringLength(250)]
    public string? Company { get; set; }

    [StringLength(550)]
    public string? Address { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }

    [Required, MinLength(1, ErrorMessage = "Danh sách sản phẩm trống"), MaxLength(100)]
    public List<QuoteItemRequest> Items { get; set; } = [];
}

public record QuoteCreatedDto(string Code);
