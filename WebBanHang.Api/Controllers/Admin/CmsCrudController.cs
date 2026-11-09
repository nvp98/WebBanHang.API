using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers.Admin;

public static class Policies
{
    /// <summary>Any back-office user (Admin or Nhân viên group).</summary>
    public const string Staff = "staff";
    /// <summary>Members of the Admin group only.</summary>
    public const string Admin = "admin";
}

/// <summary>
/// CRUD for a multilingual CMS table. Rows are addressed by <c>pk</c> (one row per language);
/// <c>id</c> groups the translations of the same item.
/// <list type="bullet">
/// <item>POST <c>?lang=en&amp;translationOf={id}</c> adds the English version of an existing item.</item>
/// <item>DELETE is a soft delete (IsDeleted = 1) of every language version; <c>/restore</c> undoes it.</item>
/// </list>
/// </summary>
[ApiController]
[Authorize(Policy = Policies.Staff)]
public abstract class CmsCrudController<TEntity, TInput>(AppDbContext db) : ControllerBase
    where TEntity : CmsEntity, new()
    where TInput : CmsInput
{
    protected AppDbContext Db => db;
    protected DbSet<TEntity> Set => db.Set<TEntity>();
    protected ContentSanitizer Html => HttpContext.RequestServices.GetRequiredService<ContentSanitizer>();

    /// <summary>Copies the request body onto the entity.</summary>
    protected abstract void Apply(TInput input, TEntity entity);

    /// <summary>Free-text search; use <see cref="Like"/> so ntext columns work.</summary>
    protected abstract IQueryable<TEntity> Search(IQueryable<TEntity> q, string pattern);

    protected virtual IQueryable<TEntity> FilterByCategory(IQueryable<TEntity> q, int categoryId) => q;

    protected virtual IOrderedQueryable<TEntity> Sort(IQueryable<TEntity> q) =>
        q.OrderByDescending(x => x.Levels).ThenByDescending(x => x.Pk);

    /// <summary>Business validation before saving (foreign keys, uniqueness…). Throw <see cref="ApiException"/>.</summary>
    protected virtual Task ValidateAsync(TInput input, TEntity entity, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Blocks deleting items that are still referenced. Throw <see cref="ApiException"/>.</summary>
    protected virtual Task EnsureCanDeleteAsync(int logicalId, CancellationToken ct) => Task.CompletedTask;

    /// <summary>"%term%" with LIKE wildcards escaped (escape char '\').</summary>
    protected static string Like(string term) =>
        "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";

    protected static int Flag(bool value) => value ? 1 : 0;

    // ─── Endpoints ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ApiResponse<PagedResult<TEntity>>> List([FromQuery] AdminListQuery query, CancellationToken ct)
    {
        var q = Set.AsNoTracking();

        q = (query.Status ?? "active").ToLowerInvariant() switch
        {
            "deleted" => q.Where(x => x.IsDeleted == 1),
            "all" => q,
            _ => q.Active(),
        };

        if (!string.Equals(query.Lang, "all", StringComparison.OrdinalIgnoreCase))
            q = q.InLanguage(Common.Lang.Parse(query.Lang) ?? Common.Lang.Vi);

        if (query.CategoryId is { } categoryId) q = FilterByCategory(q, categoryId);
        if (!string.IsNullOrWhiteSpace(query.Q)) q = Search(q, Like(query.Q.Trim()));

        return ApiResult.Ok(await Sort(q).ToPagedAsync(query.Page, query.PageSize, ct));
    }

    [HttpGet("{pk:int}")]
    public async Task<ApiResponse<TEntity>> Get(int pk, CancellationToken ct) =>
        ApiResult.Ok(await Set.AsNoTracking().FirstOrDefaultAsync(x => x.Pk == pk, ct) ?? throw ApiException.NotFound());

    /// <summary>All language versions of the item that row <paramref name="pk"/> belongs to.</summary>
    [HttpGet("{pk:int}/translations")]
    public async Task<ApiResponse<List<TEntity>>> Translations(int pk, CancellationToken ct)
    {
        var id = await LogicalIdAsync(pk, ct);
        return ApiResult.Ok(await Set.AsNoTracking().Where(x => (x.Id ?? x.Pk) == id).OrderBy(x => x.LanguageId).ToListAsync(ct));
    }

    /// <param name="lang">vi (default) | en</param>
    /// <param name="translationOf">Logical id of the item this row translates.</param>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<TEntity>>> Create(TInput input, [FromQuery] string? lang, [FromQuery] int? translationOf, CancellationToken ct)
    {
        var languageId = Common.Lang.Parse(lang) ?? Common.Lang.Vi;

        if (translationOf is { } sourceId)
        {
            var languages = await Set.Active().Where(x => (x.Id ?? x.Pk) == sourceId).Select(x => x.LanguageId ?? Common.Lang.Vi).ToListAsync(ct);
            if (languages.Count == 0) throw ApiException.NotFound("Không tìm thấy bản gốc để dịch");
            if (languages.Contains(languageId)) throw ApiException.Conflict("Ngôn ngữ này đã có bản dịch");
        }

        var entity = new TEntity { LanguageId = languageId, Levels = input.Levels, IsDeleted = 0 };
        Apply(input, entity);
        await ValidateAsync(input, entity, ct);
        await ApplySeoAsync(input, entity, ct);

        await db.InsertAsync(entity, translationOf, ct);
        return CreatedAtAction(nameof(Get), new { pk = entity.Pk }, ApiResult.Ok(entity, "Đã tạo"));
    }

    [HttpPut("{pk:int}")]
    public async Task<ApiResponse<TEntity>> Update(int pk, TInput input, CancellationToken ct)
    {
        var entity = await Set.FirstOrDefaultAsync(x => x.Pk == pk, ct) ?? throw ApiException.NotFound();
        entity.Levels = input.Levels;
        Apply(input, entity);
        await ValidateAsync(input, entity, ct);
        await ApplySeoAsync(input, entity, ct);
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok(entity, "Đã lưu");
    }

    /// <param name="allLanguages">Also delete the other language versions (default true).</param>
    [HttpDelete("{pk:int}")]
    public async Task<ApiResponse<object?>> Delete(int pk, [FromQuery] bool allLanguages = true, CancellationToken ct = default)
    {
        var id = await LogicalIdAsync(pk, ct);
        await EnsureCanDeleteAsync(id, ct);

        var rows = await Set.Where(x => allLanguages ? (x.Id ?? x.Pk) == id : x.Pk == pk).ToListAsync(ct);
        foreach (var row in rows) row.IsDeleted = 1;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, $"Đã xoá {rows.Count} bản ghi");
    }

    [HttpPost("{pk:int}/restore")]
    public async Task<ApiResponse<object?>> Restore(int pk, [FromQuery] bool allLanguages = true, CancellationToken ct = default)
    {
        var id = await LogicalIdAsync(pk, ct);
        var rows = await Set.Where(x => allLanguages ? (x.Id ?? x.Pk) == id : x.Pk == pk).ToListAsync(ct);
        foreach (var row in rows) row.IsDeleted = 0;
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, $"Đã khôi phục {rows.Count} bản ghi");
    }

    /// <summary>Permanently removes a row that is already soft-deleted. Admin only.</summary>
    [HttpDelete("{pk:int}/permanent")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ApiResponse<object?>> DeletePermanently(int pk, CancellationToken ct)
    {
        var entity = await Set.FirstOrDefaultAsync(x => x.Pk == pk, ct) ?? throw ApiException.NotFound();
        if (entity.IsDeleted != 1) throw ApiException.BadRequest("Chỉ xoá vĩnh viễn bản ghi đã nằm trong thùng rác");
        await EnsureCanDeleteAsync(entity.Id ?? entity.Pk, ct);
        Set.Remove(entity);
        await db.SaveChangesAsync(ct);
        return ApiResult.Ok<object?>(null, "Đã xoá vĩnh viễn");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    protected async Task<int> LogicalIdAsync(int pk, CancellationToken ct) =>
        await Set.Where(x => x.Pk == pk).Select(x => (int?)(x.Id ?? x.Pk)).FirstOrDefaultAsync(ct)
        ?? throw ApiException.NotFound();

    /// <summary>Keeps TitleURL in the legacy "/slug.html" | "/en/slug.html" format and unique within the table.</summary>
    private async Task ApplySeoAsync(TInput input, TEntity entity, CancellationToken ct)
    {
        if (entity is not ISeoEntity seo || input is not ISeoInput seoInput) return;

        seo.MetaTitle = seoInput.MetaTitle;
        seo.MetaDescription = seoInput.MetaDescription;
        seo.MetaKeywords = seoInput.MetaKeywords;

        var languageId = entity.LanguageId ?? Common.Lang.Vi;
        var requested = string.IsNullOrWhiteSpace(seoInput.Slug) ? null : seoInput.Slug.Trim();
        var current = Common.Slug.FromTitleUrl(seo.TitleUrl);
        if (requested is null && current is not null) return;

        var baseSlug = requested ?? Common.Slug.Generate(seo.SlugSource);
        if (baseSlug.Length == 0) baseSlug = typeof(TEntity).Name.ToLowerInvariant();

        var slug = baseSlug;
        for (var n = 2; await Set.AnyAsync(x => x.Pk != entity.Pk && EF.Property<string>(x, nameof(ISeoEntity.TitleUrl)) == Common.Slug.ToTitleUrl(slug, languageId), ct); n++)
        {
            if (requested is not null) throw ApiException.Conflict($"Slug \"{requested}\" đã được sử dụng");
            slug = $"{baseSlug}-{n}";
        }
        seo.TitleUrl = Common.Slug.ToTitleUrl(slug, languageId);
    }
}
