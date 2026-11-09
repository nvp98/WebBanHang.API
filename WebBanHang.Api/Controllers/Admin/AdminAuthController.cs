using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebBanHang.Api.Common;
using WebBanHang.Api.Models;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers.Admin;

/// <summary>Back-office sign-in against dbo.Logons. Refresh uses the shared /api/auth/refresh-token.</summary>
[ApiController]
[Route("api/admin/auth")]
[Tags("Admin · Hệ thống")]
public class AdminAuthController(AuthService auth, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ApiResponse<LoginResult>> Login(AdminLoginRequest request, CancellationToken ct) =>
        ApiResult.Ok(await auth.AdminLoginAsync(request, ct));

    [HttpGet("me")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ApiResponse<UserDto>> Me(CancellationToken ct) =>
        ApiResult.Ok(await auth.GetAdminAsync(currentUser.AdminId!.Value, ct));

    [HttpPut("password")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ApiResponse<object?>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangeAdminPasswordAsync(currentUser.AdminId!.Value, request, ct);
        return ApiResult.Ok<object?>(null, "Đã đổi mật khẩu");
    }
}
