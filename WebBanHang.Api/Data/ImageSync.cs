using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebBanHang.Api.Common;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Data;

/// <summary>
/// <c>dotnet run -- sync-images [--apply] [--download]</c>
/// <para>
/// The SQL backup is older than the live site: images were re-uploaded there with a new random suffix
/// ("may-giat-labowash-rf6088w_0c5.jpg" in the DB, "…_cb9.jpg" online), so the stored names 404.
/// For every product/category whose image is missing this finds the current file name on the old site:
/// the product page's og:image first, then the category listings (same name without the "_xxx" suffix,
/// or the only image whose name contains the product code).
/// </para>
/// Without <c>--apply</c> it only prints what would change; with it the old values are saved to a CSV first.
/// <c>--download</c> also copies the images into wwwroot/Photos so the storefront no longer depends on the
/// old site (MediaService serves local copies first).
/// </summary>
public static partial class ImageSync
{
    private const int Concurrency = 4;

    public static async Task RunAsync(IServiceProvider services, string[] args)
    {
        var apply = args.Contains("--apply");
        var download = args.Contains("--download");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var opt = scope.ServiceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        var origin = new Uri(opt.LegacyBaseUrl).GetLeftPart(UriPartial.Authority);
        var productFolder = opt.Folders.GetValueOrDefault(MediaKind.Product, "Products");
        var categoryFolder = opt.Folders.GetValueOrDefault(MediaKind.ProductCategory, "ProductCategory");
        string FileUrl(string folder, string file) => $"{opt.LegacyBaseUrl.TrimEnd('/')}/{folder}/{Uri.EscapeDataString(file)}";

        using var http = NewClient();

        Console.WriteLine($"Nguồn ảnh: {opt.LegacyBaseUrl}  ·  chế độ: {(apply ? "GHI VÀO CSDL" : "xem trước (thêm --apply để ghi)")}{(download ? " + tải ảnh về" : "")}");

        var categories = await db.ProductCategories.Active()
            .Select(c => new { c.Pk, Id = c.Id ?? c.Pk, Lang = c.LanguageId ?? Lang.Vi, c.TitleUrl, c.Images })
            .ToListAsync();

        // ─── 1. Ảnh đang có trên site cũ: trang chủ + mọi trang của từng danh mục ──
        var liveProducts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var liveCategoryFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var listingPages = new[] { "/" }
            .Concat(categories.Where(c => c.Lang == Lang.Vi && !string.IsNullOrEmpty(c.TitleUrl)).Select(c => c.TitleUrl!))
            .Distinct();
        foreach (var page in listingPages)
        {
            await foreach (var html in CrawlListingAsync(origin + page))
            {
                foreach (Match m in PhotoLink().Matches(html))
                {
                    var file = Uri.UnescapeDataString(m.Groups["file"].Value);
                    var folder = m.Groups["folder"].Value;
                    if (folder.Equals(productFolder, StringComparison.OrdinalIgnoreCase)) liveProducts.Add(file);
                    else if (folder.Equals(categoryFolder, StringComparison.OrdinalIgnoreCase)) liveCategoryFiles.TryAdd(BaseName(file), file);
                }
            }
        }
        Console.WriteLine($"Site cũ: {liveProducts.Count} ảnh sản phẩm, {liveCategoryFiles.Count} ảnh danh mục trong các trang danh sách");

        var liveByBase = liveProducts
            .GroupBy(BaseName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Cùng tên khi bỏ đuôi _xxx; nếu không có thì ảnh duy nhất có tên chứa mã sản phẩm (mã ≥ 4 ký tự)
        string? MatchFromListing(string? stored, string? code)
        {
            if (!string.IsNullOrWhiteSpace(stored) && liveByBase.TryGetValue(BaseName(stored), out var same) && same.Count == 1)
                return same[0];
            var key = Normalize(code);
            if (key.Length < 4) return null;
            var byCode = liveProducts.Where(f => Normalize(f).Contains(key)).ToList();
            return byCode.Count == 1 ? byCode[0] : null;
        }

        // ─── 2. Sản phẩm ────────────────────────────────────────────────────
        var productRows = await db.Products.Active()
            .Select(p => new { Id = p.Id ?? p.Pk, Lang = p.LanguageId ?? Lang.Vi, p.TitleUrl, p.BigImages, p.Code })
            .ToListAsync();
        var products = productRows.GroupBy(p => p.Id).Select(g => g.OrderBy(p => p.Lang).First()).ToList();

        var productChanges = new ConcurrentDictionary<int, (string? Old, string New)>();
        var okFiles = new ConcurrentBag<(string Folder, string File)>();
        int alreadyOk = 0, notFound = 0;
        using (var gate = new SemaphoreSlim(Concurrency))
        {
            await Task.WhenAll(products.Select(async p =>
            {
                await gate.WaitAsync();
                try
                {
                    if (!string.IsNullOrWhiteSpace(p.BigImages) && await ImageExistsAsync(http, FileUrl(productFolder, p.BigImages)))
                    {
                        Interlocked.Increment(ref alreadyOk);
                        okFiles.Add((productFolder, p.BigImages));
                        return;
                    }

                    string? file = null;
                    if (!string.IsNullOrEmpty(p.TitleUrl))
                    {
                        var html = await GetStringAsync(http, origin + p.TitleUrl);
                        var og = html is null ? null : OgImage().Match(html);
                        if (og is { Success: true } && og.Groups["url"].Value.Contains($"/Photos/{productFolder}/", StringComparison.OrdinalIgnoreCase))
                            file = Uri.UnescapeDataString(og.Groups["url"].Value.Split('/').Last());
                    }
                    file ??= MatchFromListing(p.BigImages, p.Code);

                    if (file is not null && await ImageExistsAsync(http, FileUrl(productFolder, file)))
                    {
                        productChanges[p.Id] = (p.BigImages, file);
                        okFiles.Add((productFolder, file));
                    }
                    else
                    {
                        Interlocked.Increment(ref notFound);
                        Console.WriteLine($"  ✗ sản phẩm {p.Id} [{p.Code}] {p.TitleUrl}: không tìm thấy ảnh trên site cũ");
                    }
                }
                finally
                {
                    gate.Release();
                }
            }));
        }
        Console.WriteLine($"Sản phẩm: {products.Count} · ảnh đúng sẵn {alreadyOk} · sửa được {productChanges.Count} · không tìm thấy {notFound}");

        // ─── 3. Danh mục (mỗi dòng vi/en có ảnh riêng) ─────────────────────
        var categoryChanges = new Dictionary<int, (string Old, string New)>();
        var categoryImages = categories.Where(c => !string.IsNullOrWhiteSpace(c.Images)).ToList();
        foreach (var c in categoryImages)
        {
            if (await ImageExistsAsync(http, FileUrl(categoryFolder, c.Images!)))
            {
                okFiles.Add((categoryFolder, c.Images!));
                continue;
            }
            if (liveCategoryFiles.TryGetValue(BaseName(c.Images!), out var live) && await ImageExistsAsync(http, FileUrl(categoryFolder, live)))
            {
                categoryChanges[c.Pk] = (c.Images!, live);
                okFiles.Add((categoryFolder, live));
            }
            else Console.WriteLine($"  ✗ danh mục {c.Id} {c.Images}: không tìm thấy ảnh trên site cũ");
        }
        Console.WriteLine($"Danh mục: {categoryImages.Count} ảnh · sửa được {categoryChanges.Count}");

        // ─── 4. Banner trang chủ: tên dạng "ten-banner424_.jpg", site cũ đổi 3 ký tự trước dấu "_" ──
        var bannerFolder = opt.Folders.GetValueOrDefault(MediaKind.Banner, "BannerHome");
        var banners = await db.Banners.Active()
            .Select(b => new { b.Pk, Lang = b.LanguageId ?? Lang.Vi, b.Images, b.BigImages })
            .ToListAsync();
        var bannerChanges = new Dictionary<int, (string Old, string New)>();

        foreach (var (lang, home) in new[] { (Lang.Vi, "/"), (Lang.En, "/en/default.html") })
        {
            var html = await GetStringAsync(http, origin + home);
            var live = html is null ? [] : PhotoLink().Matches(html)
                .Where(m => m.Groups["folder"].Value.Equals(bannerFolder, StringComparison.OrdinalIgnoreCase))
                .Select(m => Uri.UnescapeDataString(m.Groups["file"].Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var unused = new List<string>(live);
            var unmatched = new List<(int Pk, string Stored)>();

            foreach (var b in banners.Where(b => b.Lang == lang))
            {
                var stored = string.IsNullOrWhiteSpace(b.BigImages) ? b.Images : b.BigImages;
                if (string.IsNullOrWhiteSpace(stored) || stored.StartsWith('/') || stored.Contains("://")) continue;  // ảnh tải lên mới
                if (await ImageExistsAsync(http, FileUrl(bannerFolder, stored)))
                {
                    okFiles.Add((bannerFolder, stored));
                    unused.RemoveAll(f => f.Equals(stored, StringComparison.OrdinalIgnoreCase));
                    continue;
                }
                var match = unused.FirstOrDefault(f => BannerBaseName(f).Equals(BannerBaseName(stored), StringComparison.OrdinalIgnoreCase));
                if (match is not null && await ImageExistsAsync(http, FileUrl(bannerFolder, match)))
                {
                    bannerChanges[b.Pk] = (stored, match);
                    okFiles.Add((bannerFolder, match));
                    unused.Remove(match);
                }
                else unmatched.Add((b.Pk, stored));
            }

            // Banner bị đổi tên trên site cũ: ghép lần lượt cặp có nhiều từ chung nhất trong tên
            // (vd. "cong-ty-tnhh-thuong-mai-dich-vu-vien-vien" ↔ "cong-ty-tnhh-tm-dv-vien-vien")
            var pairs = unmatched
                .SelectMany(u => unused.Select(f => (Banner: u, File: f, Score: SharedWords(BannerBaseName(u.Stored), BannerBaseName(f)))))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ToList();
            foreach (var (banner, file, _) in pairs)
            {
                if (!unmatched.Contains(banner) || !unused.Contains(file)) continue;
                if (!await ImageExistsAsync(http, FileUrl(bannerFolder, file))) continue;
                bannerChanges[banner.Pk] = (banner.Stored, file);
                okFiles.Add((bannerFolder, file));
                unmatched.Remove(banner);
                unused.Remove(file);
            }
            foreach (var (pk, stored) in unmatched) Console.WriteLine($"  ✗ banner {pk} {stored}: không tìm thấy ảnh trên site cũ");
        }
        Console.WriteLine($"Banner: {banners.Count} · sửa được {bannerChanges.Count}");

        foreach (var (id, (old, @new)) in productChanges.Take(5))
            Console.WriteLine($"  vd. sản phẩm {id}: {old ?? "(trống)"} → {@new}");

        // ─── Ghi CSDL ───────────────────────────────────────────────────────
        if (apply && (!productChanges.IsEmpty || categoryChanges.Count > 0 || bannerChanges.Count > 0))
        {
            // Mọi dòng ngôn ngữ của sản phẩm dùng chung ảnh chính
            var productIds = productChanges.Keys.ToList();
            foreach (var row in await db.Products.Where(p => productIds.Contains(p.Id ?? p.Pk)).ToListAsync())
                row.BigImages = productChanges[row.Id ?? row.Pk].New;

            var categoryPks = categoryChanges.Keys.ToList();
            foreach (var row in await db.ProductCategories.Where(c => categoryPks.Contains(c.Pk)).ToListAsync())
                row.Images = categoryChanges[row.Pk].New;

            var bannerPks = bannerChanges.Keys.ToList();
            foreach (var row in await db.Banners.Where(b => bannerPks.Contains(b.Pk)).ToListAsync())
            {
                if (string.IsNullOrWhiteSpace(row.BigImages)) row.Images = bannerChanges[row.Pk].New;
                else row.BigImages = bannerChanges[row.Pk].New;
            }

            // Lưu tên cũ để có thể hoàn tác
            var backup = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "Database", $"image-sync-backup-{DateTime.Now:yyyyMMdd-HHmmss}.csv"));
            await File.WriteAllLinesAsync(backup,
                new[] { "table,key,old_value,new_value" }
                    .Concat(productChanges.Select(kv => $"Product(IDProduct),{kv.Key},{kv.Value.Old},{kv.Value.New}"))
                    .Concat(categoryChanges.Select(kv => $"ProductCategory(PK),{kv.Key},{kv.Value.Old},{kv.Value.New}"))
                    .Concat(bannerChanges.Select(kv => $"BannerHome(PK),{kv.Key},{kv.Value.Old},{kv.Value.New}")));

            var saved = await db.SaveChangesAsync();
            Console.WriteLine($"Đã cập nhật {saved} dòng trong CSDL. Tên cũ lưu tại {backup}");
        }

        // ─── Tải ảnh về ─────────────────────────────────────────────────────
        if (download)
        {
            var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            int saved = 0, skipped = 0, failed = 0;
            foreach (var (folder, file) in okFiles.Distinct())
            {
                var target = Path.Combine(webRoot, "Photos", folder, file);
                if (File.Exists(target)) { skipped++; continue; }
                try
                {
                    var bytes = await http.GetByteArrayAsync(FileUrl(folder, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await File.WriteAllBytesAsync(target, bytes);
                    saved++;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    failed++;
                }
            }
            Console.WriteLine($"Tải ảnh về wwwroot/Photos: mới {saved} · đã có {skipped} · lỗi {failed}");
        }

        if (!apply) Console.WriteLine("Chưa ghi gì vào CSDL. Chạy lại với --apply để cập nhật.");
    }

    /// <summary>
    /// Trang danh mục của site cũ (ASP.NET WebForms) phân trang bằng postback: gửi lại form với
    /// __EVENTTARGET = control phân trang và __EVENTARGUMENT = số trang.
    /// </summary>
    private static async IAsyncEnumerable<string> CrawlListingAsync(string url)
    {
        using var http = NewClient(new CookieContainer());

        var html = await GetStringAsync(http, url);
        if (html is null) yield break;
        yield return html;

        var pager = PagerLink().Matches(html);
        if (pager.Count == 0) yield break;
        var target = pager[0].Groups["target"].Value;
        var pages = Math.Min(50, pager.Max(m => int.Parse(m.Groups["page"].Value)));

        for (var page = 2; page <= pages; page++)
        {
            var form = new Dictionary<string, string>();
            foreach (Match m in HiddenInput().Matches(html)) form[m.Groups["name"].Value] = WebUtility.HtmlDecode(m.Groups["value"].Value);
            form["__EVENTTARGET"] = target;
            form["__EVENTARGUMENT"] = page.ToString();

            string? next;
            try
            {
                using var res = await http.PostAsync(url, new FormUrlEncodedContent(form));
                next = res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                next = null;
            }
            if (next is null) yield break;
            html = next;
            yield return html;
        }
    }

    private static HttpClient NewClient(CookieContainer? cookies = null)
    {
        var handler = new HttpClientHandler();
        if (cookies is not null) handler.CookieContainer = cookies;
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WebBanHang-ImageSync/1.0");
        return http;
    }

    private static string Normalize(string? s) =>
        string.IsNullOrEmpty(s) ? "" : new string(s.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    private static int SharedWords(string a, string b) =>
        a.Split('-', StringSplitOptions.RemoveEmptyEntries).Intersect(b.Split('-', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>"may-kiem-tra-noi-that424_.jpg" → "may-kiem-tra-noi-that"</summary>
    private static string BannerBaseName(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        return name.Length > 4 && name.EndsWith('_') ? name[..^4] : name;
    }

    /// <summary>"may-giat_cb9.jpg" → "may-giat"</summary>
    private static string BaseName(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var underscore = name.LastIndexOf('_');
        return underscore > 0 ? name[..underscore] : name;
    }

    private static async Task<bool> ImageExistsAsync(HttpClient http, string url)
    {
        try
        {
            using var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return res.IsSuccessStatusCode && res.Content.Headers.ContentType?.MediaType?.StartsWith("image/") == true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static async Task<string?> GetStringAsync(HttpClient http, string url)
    {
        try
        {
            return await http.GetStringAsync(url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    [GeneratedRegex("""property=["']og:image["'][^>]*content=["'](?<url>[^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex OgImage();

    [GeneratedRegex("""/Photos/(?<folder>[A-Za-z]+)/(?<file>[^"'\s?]+)""")]
    private static partial Regex PhotoLink();

    [GeneratedRegex("""__doPostBack\('(?<target>[^']+)','(?<page>\d+)'\)""")]
    private static partial Regex PagerLink();

    [GeneratedRegex("<input type=\"hidden\" name=\"(?<name>[^\"]+)\" id=\"[^\"]*\" value=\"(?<value>[^\"]*)\"")]
    private static partial Regex HiddenInput();
}
