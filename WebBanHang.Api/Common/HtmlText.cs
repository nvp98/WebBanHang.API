using System.Net;
using System.Text.RegularExpressions;

namespace WebBanHang.Api.Common;

public static partial class HtmlText
{
    /// <summary>Strips tags/entities from legacy HTML so it can be shown as a plain-text summary.</summary>
    public static string? ToPlainText(string? html, int maxLength = 300)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var text = Tags().Replace(html, " ");
        text = WebUtility.HtmlDecode(text);
        text = Spaces().Replace(text, " ").Trim();
        if (text.Length == 0) return null;
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
