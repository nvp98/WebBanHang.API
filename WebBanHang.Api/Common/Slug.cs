using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WebBanHang.Api.Common;

/// <summary>
/// Converts between frontend slugs ("may-do-do-cung-vai") and the legacy TitleURL column
/// ("/may-do-do-cung-vai.html" for vi, "/en/fabric-stiffness-tester.html" for en).
/// </summary>
public static partial class Slug
{
    public static string Generate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var normalized = text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }

        var ascii = NonAlphanumeric().Replace(sb.ToString(), "-").Trim('-');
        return ascii.Length > 200 ? ascii[..200].TrimEnd('-') : ascii;
    }

    /// <summary>"/en/abc-def.html" → "abc-def". Returns null when the column is empty.</summary>
    public static string? FromTitleUrl(string? titleUrl)
    {
        if (string.IsNullOrWhiteSpace(titleUrl)) return null;
        var s = titleUrl.Trim();
        if (s.StartsWith("/en/", StringComparison.OrdinalIgnoreCase)) s = s[4..];
        s = s.TrimStart('/');
        if (s.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) s = s[..^5];
        return s.Length == 0 ? null : s;
    }

    public static string ToTitleUrl(string slug, int languageId) =>
        languageId == Lang.En ? $"/en/{slug}.html" : $"/{slug}.html";

    /// <summary>Both legacy URL forms a slug could be stored as.</summary>
    public static (string Vi, string En) Candidates(string slug) => ($"/{slug}.html", $"/en/{slug}.html");

    /// <summary>
    /// Public slug for a row. Rows without TitleURL get "{generated}-{id}" so they stay addressable;
    /// <see cref="TrailingId"/> recovers the id when such a slug is requested.
    /// </summary>
    public static string For(string? titleUrl, string? title, int id) =>
        FromTitleUrl(titleUrl) ?? $"{Generate(title)}-{id}".TrimStart('-');

    public static int? TrailingId(string slug)
    {
        var m = TrailingNumber().Match(slug);
        return m.Success && int.TryParse(m.Groups[1].Value, out var id) ? id : null;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"(?:^|-)(\d+)$")]
    private static partial Regex TrailingNumber();
}
