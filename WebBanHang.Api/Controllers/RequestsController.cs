using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebBanHang.Api.Common;
using WebBanHang.Api.Models;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Controllers;

/// <summary>Public forms — matches frontend/lib/api/requests.ts. Rate-limited per IP (the legacy table is full of spam).</summary>
[ApiController]
[Route("api")]
[Tags("Storefront")]
[EnableRateLimiting(RateLimits.PublicForms)]
public class RequestsController(OrderService orders) : ControllerBase
{
    [HttpPost("contact")]
    public async Task<ApiResponse<object?>> Contact(ContactRequest request, CancellationToken ct)
    {
        await orders.CreateContactAsync(request, ct);
        return ApiResult.Ok<object?>(null, "Cảm ơn bạn đã liên hệ, chúng tôi sẽ phản hồi sớm nhất.");
    }

    [HttpPost("quotes")]
    public async Task<ApiResponse<QuoteCreatedDto>> Quote(QuoteRequest request, CancellationToken ct) =>
        ApiResult.Ok(await orders.CreateQuoteAsync(request, ct), "Đã gửi yêu cầu báo giá");
}

public static class RateLimits
{
    public const string PublicForms = "public-forms";
    public const string Login = "login";
}
