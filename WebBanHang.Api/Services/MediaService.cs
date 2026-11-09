using Microsoft.Extensions.Options;
using WebBanHang.Api.Common;

namespace WebBanHang.Api.Services;

public class MediaOptions
{
    /// <summary>Where the legacy site serves its photos, e.g. https://vienvienco.com/Photos</summary>
    public string LegacyBaseUrl { get; set; } = "";
    /// <summary>Public base URL of this API, used for files uploaded through /api/admin/uploads.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5000";
    /// <summary>Media kind → legacy sub-folder (e.g. "product" → "Products").</summary>
    public Dictionary<string, string> Folders { get; set; } = new();
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>Kinds of media, used both for legacy folder lookup and the upload sub-folder.</summary>
public static class MediaKind
{
    public const string Product = "product";
    public const string ProductCategory = "product-category";
    public const string Manufacturer = "manufacturer";
    public const string News = "news";
    public const string Banner = "banner";
    public const string Partner = "partner";
    public const string Service = "service";
    public const string Project = "project";
    public const string Document = "document";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Product, ProductCategory, Manufacturer, News, Banner, Partner, Service, Project, Document,
    };
}

/// <summary>
/// The legacy DB stores bare file names ("may-giat_e7b.jpg"). This turns them into absolute URLs and handles
/// files uploaded through the new admin ("/uploads/product/x.jpg") and full URLs as-is.
/// </summary>
public partial class MediaService(IOptions<MediaOptions> options, IWebHostEnvironment env)
{
    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private static readonly HashSet<string> FileExtensions = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".zip"];

    private readonly MediaOptions _opt = options.Value;

    public string? Url(string kind, string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        var value = stored.Trim();

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return value;

        if (value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return _opt.PublicBaseUrl.TrimEnd('/') + value;

        if (value.StartsWith('/'))
            return BaseOfLegacySite() + value;

        var folder = _opt.Folders.GetValueOrDefault(kind, kind);

        // Bản sao tải về bằng `sync-images --download` được ưu tiên, để không phụ thuộc site cũ
        if (File.Exists(Path.Combine(WebRoot, "Photos", folder, value)))
            return $"{_opt.PublicBaseUrl.TrimEnd('/')}/Photos/{folder}/{Uri.EscapeDataString(value)}";

        return $"{_opt.LegacyBaseUrl.TrimEnd('/')}/{folder}/{Uri.EscapeDataString(value)}";
    }

    /// <summary>Saves an uploaded file under wwwroot/uploads/{kind}/ and returns the value to store in the DB.</summary>
    public async Task<string> SaveAsync(string kind, IFormFile file, CancellationToken ct)
    {
        if (!MediaKind.All.Contains(kind)) throw ApiException.BadRequest($"Loại tệp không hợp lệ: {kind}");
        if (file.Length == 0) throw ApiException.BadRequest("Tệp rỗng");
        if (file.Length > _opt.MaxUploadBytes) throw ApiException.BadRequest($"Tệp vượt quá {_opt.MaxUploadBytes / 1024 / 1024} MB");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ImageExtensions.Contains(ext) && !FileExtensions.Contains(ext))
            throw ApiException.BadRequest($"Định dạng {ext} không được hỗ trợ");

        var baseName = Slug.Generate(Path.GetFileNameWithoutExtension(file.FileName));
        if (baseName.Length == 0) baseName = "file";
        if (baseName.Length > 80) baseName = baseName[..80];
        var fileName = $"{baseName}_{Guid.NewGuid().ToString("N")[..6]}{ext}";

        var dir = Path.Combine(WebRoot, "uploads", kind);
        Directory.CreateDirectory(dir);

        await using (var stream = File.Create(Path.Combine(dir, fileName)))
        {
            await file.CopyToAsync(stream, ct);
        }
        return $"/uploads/{kind}/{fileName}";
    }

    /// <summary>
    /// Legacy rich text links its images as src="/Uploads/…", relative to the old site. Rewrites root-relative
    /// src/href attributes to absolute URLs so they still load when rendered by the new storefront.
    /// </summary>
    public string? AbsolutizeHtml(string? html)
    {
        if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(_opt.LegacyBaseUrl)) return html;
        var origin = BaseOfLegacySite();
        var self = _opt.PublicBaseUrl.TrimEnd('/');
        return RootRelativeAttribute().Replace(html, m =>
        {
            // "/uploads/…" (lower-case) = files uploaded through this API; anything else belongs to the old site.
            var path = m.Groups["path"].Value;
            var host = path.StartsWith("uploads/", StringComparison.Ordinal) ? self : origin;
            return $"{m.Groups["attr"].Value}{host}/{path}";
        });
    }

    [System.Text.RegularExpressions.GeneratedRegex("""(?<attr>\s(?:src|href)\s*=\s*["'])/(?!/)(?<path>[^"']*)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex RootRelativeAttribute();

    private string WebRoot => env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    private string BaseOfLegacySite()
    {
        var uri = new Uri(_opt.LegacyBaseUrl);
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
