using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using WebBanHang.Api;
using WebBanHang.Api.Common;
using WebBanHang.Api.Controllers;
using WebBanHang.Api.Controllers.Admin;
using WebBanHang.Api.Data;
using WebBanHang.Api.Services;

// CLI commands (run and exit):
//   dotnet run -- create-admin <username> [password]   tạo/đặt lại tài khoản quản trị
//   dotnet run -- sync-images [--apply] [--download]   sửa tên ảnh sản phẩm/danh mục theo site cũ
string[] commands = ["create-admin", "sync-images"];
var command = commands.Contains(args.FirstOrDefault()) ? args : null;
var builder = WebApplication.CreateBuilder(command is null ? args : []);

// ─── Options ────────────────────────────────────────────────────────────────
builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection("Media"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Jwt:Key must be configured (≥ 32 chars), e.g. env var Jwt__Key.");

    // Development convenience: an ephemeral key, so no secret is committed. Tokens are invalidated on restart.
    jwt.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    Console.WriteLine("warn: Jwt:Key not configured — using an ephemeral development key.");
}
builder.Services.Configure<JwtOptions>(o =>
{
    o.Issuer = jwt.Issuer;
    o.Audience = jwt.Audience;
    o.Key = jwt.Key;
    o.AccessTokenMinutes = jwt.AccessTokenMinutes;
    o.RefreshTokenDays = jwt.RefreshTokenDays;
});

// ─── Services ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<MediaService>();
builder.Services.AddSingleton<ContentSanitizer>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(
                kv => JsonNamingPolicy.CamelCase.ConvertName(kv.Key),
                kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Giá trị không hợp lệ" : e.ErrorMessage).ToArray());
        return new BadRequestObjectResult(new ApiError("Dữ liệu không hợp lệ", StatusCodes.Status400BadRequest, errors));
    });
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = true;
        o.TokenValidationParameters = TokenService.ValidationParameters(jwt);
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                await Http.WriteErrorAsync(ctx.Response, StatusCodes.Status401Unauthorized, "Vui lòng đăng nhập");
            },
            OnForbidden = ctx => Http.WriteErrorAsync(ctx.Response, StatusCodes.Status403Forbidden, "Bạn không có quyền thực hiện thao tác này"),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Staff, p => p.RequireRole(Roles.Admin, Roles.Staff))
    .AddPolicy(Policies.Admin, p => p.RequireRole(Roles.Admin));

builder.Services.AddRateLimiter(o =>
{
    static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    o.AddPolicy(RateLimits.PublicForms, ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy(RateLimits.Login, ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.OnRejected = (ctx, _) => new ValueTask(Http.WriteErrorAsync(ctx.HttpContext.Response,
        StatusCodes.Status429TooManyRequests, "Bạn thao tác quá nhanh, vui lòng thử lại sau ít phút"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "WebBanHang API", Version = "v1", Description = "Storefront + admin CRUD trên CSDL vienvienco_VVNET" });
    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    o.AddSecurityDefinition("Bearer", bearer);
    o.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });

    var xml = Path.Combine(AppContext.BaseDirectory, "WebBanHang.Api.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

var app = builder.Build();

if (command is not null)
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    if (command[0] == "sync-images") await ImageSync.RunAsync(app.Services, command.Skip(1).ToArray());
    else await AdminSeeder.RunAsync(app.Services, command.Skip(1).ToArray());
    return;
}

// ─── Pipeline ───────────────────────────────────────────────────────────────
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, message) = ex switch
    {
        ApiException api => (api.StatusCode, api.Message),
        BadHttpRequestException bad => (bad.StatusCode, "Yêu cầu không hợp lệ"),
        DbUpdateException => (StatusCodes.Status409Conflict, "Không thể lưu dữ liệu (vi phạm ràng buộc CSDL)"),
        OperationCanceledException => (499, "Yêu cầu đã bị huỷ"),
        _ => (StatusCodes.Status500InternalServerError, "Lỗi máy chủ, vui lòng thử lại sau"),
    };
    if (status >= 500 || ex is DbUpdateException)
        app.Logger.LogError(ex, "Unhandled error on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
    await Http.WriteErrorAsync(ctx.Response, status, message);
}));

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "WebBanHang API v1");
    o.DocumentTitle = "WebBanHang API";
});

app.UseStaticFiles();
app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/api/health", async (AppDbContext db, CancellationToken ct) =>
    Results.Ok(ApiResult.Ok(new { status = "ok", database = await db.Database.CanConnectAsync(ct) }))).WithTags("Storefront");

app.Run();

namespace WebBanHang.Api
{
    internal static class Http
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static async Task WriteErrorAsync(HttpResponse response, int status, string message)
        {
            if (response.HasStarted) return;
            response.StatusCode = status;
            response.ContentType = "application/json; charset=utf-8";
            await response.WriteAsync(JsonSerializer.Serialize(new ApiError(message, status), Json));
        }
    }
}
