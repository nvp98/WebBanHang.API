using WebBanHang.Api.Data.Entities;

namespace WebBanHang.Api.Data;

public static class DbExtensions
{
    /// <summary>
    /// Inserts a row following the legacy convention: a new item gets IDxxx = PKxxx, while a translation
    /// reuses the logical id of the item it translates. The id column is NOT NULL in most tables, so the row is
    /// inserted with 0 and patched inside the same transaction.
    /// </summary>
    public static async Task InsertAsync<T>(this AppDbContext db, T entity, int? logicalId, CancellationToken ct)
        where T : AuditEntity
    {
        var ownTransaction = db.Database.CurrentTransaction is null;
        await using var tx = ownTransaction ? await db.Database.BeginTransactionAsync(ct) : null;

        entity.Id = logicalId ?? 0;
        db.Add(entity);
        await db.SaveChangesAsync(ct);

        if (logicalId is null)
        {
            entity.Id = entity.Pk;
            await db.SaveChangesAsync(ct);
        }

        if (tx is not null) await tx.CommitAsync(ct);
    }
}
