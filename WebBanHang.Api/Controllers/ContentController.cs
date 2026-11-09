using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Models;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers;

/// <summary>Read-only CMS content for the storefront (news, banners, partners, services, site settings).</summary>
[ApiController]
[Route("api")]
[Tags("Storefront")]
public class ContentController(AppDbContext db, MediaService media) : ControllerBase
{
    /// <summary>Config keys that are safe to expose publicly (the table also holds SMTP credentials).</summary>
    private static readonly string[] PublicConfigKeys =
        ["CompanyName", "Address", "Phone", "Fax", "Email", "Website", "HotLine", "FaceBookLink", "Mapxy", "footer", "WebKeyword"];

    private int LangId => Lang.FromRequest(Request);

    [HttpGet("news")]
    public async Task<ApiResponse<PagedResult<NewsDto>>> News([FromQuery] int page = 1, [FromQuery] int pageSize = 12, [FromQuery] int? categoryId = null, CancellationToken ct = default)
    {
        var q = db.News.AsNoTracking().Active().InLanguage(LangId);
        if (categoryId is not null) q = q.Where(n => n.CategoryId == categoryId);
        var paged = await q.OrderByDescending(n => n.CreateDate).ThenByDescending(n => n.Pk).ToPagedAsync(page, pageSize, ct);
        return ApiResult.Ok(new PagedResult<NewsDto>(paged.Items.Select(n => ToDto(n, withContent: false)).ToList(), paged.Total, paged.Page, paged.PageSize));
    }

    [HttpGet("news/{slug}")]
    public async Task<ApiResponse<NewsDto>> NewsDetail(string slug, CancellationToken ct)
    {
        var (vi, en) = Slug.Candidates(slug);
        var trailing = Slug.TrailingId(slug);
        var news = await db.News.AsNoTracking().Active()
                       .Where(n => n.TitleUrl == vi || n.TitleUrl == en || ((n.TitleUrl == null || n.TitleUrl == "") && (n.Id ?? n.Pk) == trailing))
                       .FirstOrDefaultAsync(ct)
                   ?? throw ApiException.NotFound("Không tìm thấy bài viết");
        return ApiResult.Ok(ToDto(news, withContent: true));
    }

    [HttpGet("banners")]
    public async Task<ApiResponse<IReadOnlyList<BannerDto>>> Banners(CancellationToken ct)
    {
        var rows = await db.Banners.AsNoTracking().Active().InLanguage(LangId)
            .OrderByDescending(b => b.Levels).ThenByDescending(b => b.Pk).ToListAsync(ct);
        return ApiResult.Ok<IReadOnlyList<BannerDto>>(rows
            .Select(b => new BannerDto((b.Id ?? b.Pk).ToString(), b.Title, b.Subtitle,
                media.Url(MediaKind.Banner, string.IsNullOrWhiteSpace(b.BigImages) ? b.Images : b.BigImages), b.Url, "hero"))
            .ToList());
    }

    [HttpGet("partners")]
    public async Task<ApiResponse<IReadOnlyList<PartnerDto>>> Partners(CancellationToken ct)
    {
        var rows = await db.Partners.AsNoTracking().Active()
            .OrderByDescending(p => p.Levels).ThenBy(p => p.Pk).ToListAsync(ct);
        return ApiResult.Ok<IReadOnlyList<PartnerDto>>(rows
            .GroupBy(p => p.Id ?? p.Pk).Select(g => g.First())
            .Select(p => new PartnerDto((p.Id ?? p.Pk).ToString(), p.Title, media.Url(MediaKind.Partner, p.Images), p.Url))
            .ToList());
    }

    [HttpGet("services")]
    public async Task<ApiResponse<IReadOnlyList<ServiceDto>>> Services(CancellationToken ct)
    {
        var rows = await db.Services.AsNoTracking().Active().InLanguage(LangId)
            .OrderByDescending(s => s.Levels).ThenByDescending(s => s.Pk).ToListAsync(ct);
        return ApiResult.Ok<IReadOnlyList<ServiceDto>>(rows
            .Select(s => new ServiceDto((s.Id ?? s.Pk).ToString(), s.Title, HtmlText.ToPlainText(s.ShortText), media.AbsolutizeHtml(s.Description),
                media.Url(MediaKind.Service, s.Images), s.Video))
            .ToList());
    }

    [HttpGet("site-config")]
    public async Task<ApiResponse<Dictionary<string, string>>> SiteConfig(CancellationToken ct)
    {
        var lang = LangId;
        var rows = await db.Configs.AsNoTracking().Active()
            .Where(c => c.ConfigName != null && PublicConfigKeys.Contains(c.ConfigName))
            .OrderBy(c => (c.LanguageId ?? Lang.Vi) == lang ? 0 : 1).ThenBy(c => c.Pk)
            .Select(c => new { c.ConfigName, c.ConfigValue })
            .ToListAsync(ct);

        var result = new Dictionary<string, string>();
        foreach (var r in rows) result.TryAdd(r.ConfigName!, r.ConfigValue ?? "");
        return ApiResult.Ok(result);
    }

    private NewsDto ToDto(Data.Entities.News n, bool withContent)
    {
        var id = n.Id ?? n.Pk;
        return new NewsDto(id.ToString(), n.Title ?? "", Slug.For(n.TitleUrl, n.Title, id),
            HtmlText.ToPlainText(n.ShortText), withContent ? media.AbsolutizeHtml(n.Description) : null,
            media.Url(MediaKind.News, n.Images), n.CategoryId?.ToString(), n.CreateDate);
    }
}
