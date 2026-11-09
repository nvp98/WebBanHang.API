namespace WebBanHang.Api.Common;

/// <summary>Values of the legacy LanguageID column.</summary>
public static class Lang
{
    public const int Vi = 0;
    public const int En = 1;

    /// <summary>"vi" | "en" | "0" | "1" → LanguageID; null when not recognised.</summary>
    public static int? Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "vi" or "0" => Vi,
        "en" or "1" => En,
        _ => null,
    };

    /// <summary>?lang=… first, then Accept-Language, defaulting to Vietnamese.</summary>
    public static int FromRequest(HttpRequest request)
    {
        var fromQuery = Parse(request.Query["lang"]);
        if (fromQuery is not null) return fromQuery.Value;

        var header = request.Headers.AcceptLanguage.ToString();
        return header.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? En : Vi;
    }
}
