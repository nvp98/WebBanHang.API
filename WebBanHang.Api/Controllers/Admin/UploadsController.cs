using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebBanHang.Api.Common;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/uploads")]
[Tags("Admin · Hệ thống")]
[Authorize(Policy = Policies.Staff)]
public class UploadsController(MediaService media, IOptions<MediaOptions> options) : ControllerBase
{
    /// <summary>
    /// Uploads an image/document. Store the returned <c>path</c> in the entity field (Images, BigImages, FileCatalogue…).
    /// </summary>
    /// <param name="kind">product | product-category | manufacturer | news | banner | partner | service | project | document</param>
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ApiResponse<UploadResult>> Upload([FromQuery] string kind, IFormFile file, CancellationToken ct)
    {
        var path = await media.SaveAsync(kind, file, ct);
        return ApiResult.Ok(new UploadResult(path, media.Url(kind, path)!), "Đã tải lên");
    }

    /// <summary>
    /// Lets the admin UI preview stored values: bare file names resolve to {legacyBaseUrl}/{folders[kind]}/{file}.
    /// </summary>
    [HttpGet("config")]
    public ApiResponse<object> Config() => ApiResult.Ok<object>(new
    {
        legacyBaseUrl = options.Value.LegacyBaseUrl,
        publicBaseUrl = options.Value.PublicBaseUrl,
        folders = options.Value.Folders,
        kinds = MediaKind.All,
        maxUploadBytes = options.Value.MaxUploadBytes,
    });

    /// <summary>Resolves a stored value (file name or path) to its public URL.</summary>
    [HttpGet("resolve")]
    public ApiResponse<string?> Resolve([FromQuery] string kind, [FromQuery] string value) => ApiResult.Ok(media.Url(kind, value));
}

public record UploadResult(string Path, string Url);
