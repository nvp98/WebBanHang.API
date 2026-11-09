using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebBanHang.Api.Common;
using WebBanHang.Api.Models;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers;

/// <summary>Customer authentication — matches frontend/lib/api/auth.ts and the refresh flow in client.ts.</summary>
[ApiController]
[Route("api/auth")]
[Tags("Storefront")]
public class AuthController(AuthService auth, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ApiResponse<LoginResult>> Login(LoginRequest request, CancellationToken ct) =>
        ApiResult.Ok(await auth.CustomerLoginAsync(request, ct));

    [HttpPost("register")]
    [EnableRateLimiting(RateLimits.PublicForms)]
    public async Task<ApiResponse<UserDto>> Register(RegisterRequest request, CancellationToken ct) =>
        ApiResult.Ok(await auth.RegisterAsync(request, ct), "Đăng ký thành công");

    /// <summary>Shared by customers and back-office users.</summary>
    [HttpPost("refresh-token")]
    public async Task<ApiResponse<LoginResult>> Refresh(RefreshRequest request, CancellationToken ct) =>
        ApiResult.Ok(await auth.RefreshAsync(request.RefreshToken, ct));

    /// <summary>Tokens are stateless; the client discards them.</summary>
    [HttpPost("logout")]
    public ApiResponse<object?> Logout() => ApiResult.Ok<object?>(null);

    [HttpGet("me")]
    [Authorize(Roles = Roles.Customer)]
    public async Task<ApiResponse<UserDto>> Me(CancellationToken ct) =>
        ApiResult.Ok(await auth.GetCustomerAsync(currentUser.CustomerId!.Value, ct));

    [HttpPut("password")]
    [Authorize(Roles = Roles.Customer)]
    public async Task<ApiResponse<object?>> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangeCustomerPasswordAsync(currentUser.CustomerId!.Value, request, ct);
        return ApiResult.Ok<object?>(null, "Đã đổi mật khẩu");
    }
}
