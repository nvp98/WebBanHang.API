using System.Text.Json.Serialization;

namespace WebBanHang.Api.Data.Entities;

/// <summary>
/// Common columns shared by every table in the legacy CMS schema.
/// Pk  = PKxxx  (IDENTITY, one row per language)
/// Id  = IDxxx  (logical id, shared by the vi/en rows of the same item; equals Pk of the first row)
/// </summary>
public abstract class AuditEntity
{
    public int Pk { get; set; }
    public int? Id { get; set; }
    public int? CreateBy { get; set; }
    public DateTime? CreateDate { get; set; }
    public int? ModifyBy { get; set; }
    public DateTime? ModifyDate { get; set; }
    /// <summary>Sort order (higher = shown first) in the legacy site.</summary>
    public int? Levels { get; set; }
    /// <summary>0 = active, 1 = soft-deleted.</summary>
    public int? IsDeleted { get; set; }
}

/// <summary>Content tables that carry a LanguageID column (0 = vi, 1 = en).</summary>
public abstract class CmsEntity : AuditEntity
{
    public int? LanguageId { get; set; }
}

/// <summary>Tables that own a public URL stored in the legacy "TitleURL" format: "/slug.html" or "/en/slug.html".</summary>
public interface ISeoEntity
{
    string? TitleUrl { get; set; }
    string? MetaTitle { get; set; }
    string? MetaDescription { get; set; }
    string? MetaKeywords { get; set; }
    [JsonIgnore] string SlugSource { get; }
}
