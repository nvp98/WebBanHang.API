using Ganss.Xss;

namespace WebBanHang.Api.Services;

/// <summary>
/// Rich-text fields (Description, ShortText…) are rendered with dangerouslySetInnerHTML on the storefront,
/// so HTML coming from the admin is sanitized before it is stored.
/// </summary>
public class ContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer = new();

    public ContentSanitizer()
    {
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedSchemes.Add("tel");
    }

    public string? Clean(string? html) => string.IsNullOrWhiteSpace(html) ? html : _sanitizer.Sanitize(html);
}
