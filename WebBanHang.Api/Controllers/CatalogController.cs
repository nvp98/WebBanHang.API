using Microsoft.AspNetCore.Mvc;
using WebBanHang.Api.Common;
using WebBanHang.Api.Models;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers;

/// <summary>Storefront catalog — matches frontend/lib/api/products.ts and categories.ts.</summary>
[ApiController]
[Route("api")]
[Tags("Storefront")]
public class CatalogController(CatalogService catalog) : ControllerBase
{
    private int LangId => Lang.FromRequest(Request);

    [HttpGet("products")]
    public async Task<ApiResponse<PagedResult<ProductDto>>> GetProducts([FromQuery] ProductQuery query, CancellationToken ct) =>
        ApiResult.Ok(await catalog.GetProductsAsync(query, LangId, ct));

    [HttpGet("products/search")]
    public async Task<ApiResponse<PagedResult<ProductDto>>> Search([FromQuery] string q, [FromQuery] int page = 1, CancellationToken ct = default) =>
        ApiResult.Ok(await catalog.GetProductsAsync(new ProductQuery { Search = q, Page = page }, LangId, ct));

    [HttpGet("products/featured")]
    public async Task<ApiResponse<IReadOnlyList<ProductDto>>> Featured(CancellationToken ct) =>
        ApiResult.Ok(await catalog.GetHighlightedAsync("featured", LangId, ct));

    [HttpGet("products/new")]
    public async Task<ApiResponse<IReadOnlyList<ProductDto>>> New(CancellationToken ct) =>
        ApiResult.Ok(await catalog.GetHighlightedAsync("new", LangId, ct));

    [HttpGet("products/best-sellers")]
    public async Task<ApiResponse<IReadOnlyList<ProductDto>>> BestSellers(CancellationToken ct) =>
        ApiResult.Ok(await catalog.GetHighlightedAsync("best-sellers", LangId, ct));

    [HttpGet("products/{id:int}/related")]
    public async Task<ApiResponse<IReadOnlyList<ProductDto>>> Related(int id, CancellationToken ct) =>
        ApiResult.Ok(await catalog.GetRelatedAsync(id, LangId, ct));

    [HttpGet("products/{slug}")]
    public async Task<ApiResponse<ProductDto>> GetProduct(string slug, CancellationToken ct) =>
        ApiResult.Ok(await catalog.GetProductAsync(slug, LangId, ct));

    /// <summary>Reviews are not part of the legacy schema; kept so the frontend call succeeds.</summary>
    [HttpGet("reviews/{productId}")]
    public ApiResponse<IReadOnlyList<object>> Reviews(string productId) => ApiResult.Ok<IReadOnlyList<object>>([]);

    [HttpGet("categories")]
    public async Task<ApiResponse<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken ct) =>
        ApiResult.Ok(await catalog.GetCategoriesAsync(LangId, ct));

    [HttpGet("categories/{slug}")]
    public async Task<ApiResponse<CategoryDto>> GetCategory(string slug, CancellationToken ct) =>
        ApiResult.Ok(await catalog.GetCategoryAsync(slug, LangId, ct));

    [HttpGet("brands")]
    public async Task<ApiResponse<IReadOnlyList<BrandDto>>> GetBrands(CancellationToken ct) =>
        ApiResult.Ok(await catalog.GetBrandsAsync(LangId, ct));
}
