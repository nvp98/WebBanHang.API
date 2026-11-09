using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Data.Entities;

namespace WebBanHang.Api.Common;

public static class QueryExtensions
{
    /// <summary>Rows that are not soft-deleted (legacy data stores NULL or 0 for active rows).</summary>
    public static IQueryable<T> Active<T>(this IQueryable<T> q) where T : AuditEntity =>
        q.Where(x => x.IsDeleted == null || x.IsDeleted == 0);

    public static IQueryable<T> InLanguage<T>(this IQueryable<T> q, int languageId) where T : CmsEntity =>
        q.Where(x => (x.LanguageId ?? Lang.Vi) == languageId);

    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> q, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, total, page, pageSize);
    }
}
