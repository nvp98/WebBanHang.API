using System.ComponentModel.DataAnnotations;

namespace WebBanHang.Api.Models;

/// <summary>Mirrors the User type in frontend/types/index.ts.</summary>
public record UserDto(string Id, string Email, string FullName, string? Phone, string Role, DateTime? CreatedAt, string? Username = null);

public record LoginResult(string AccessToken, string RefreshToken, int ExpiresIn, UserDto User);

public class LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email"), StringLength(500)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu"), StringLength(100)]
    public string Password { get; set; } = "";
}

public class AdminLoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập"), StringLength(50)]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu"), StringLength(100)]
    public string Password { get; set; } = "";
}

public class RegisterRequest
{
    [Required, EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(50)]
    public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu ít nhất 6 ký tự")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(500)]
    public string FullName { get; set; } = "";

    [StringLength(50)]
    public string? Phone { get; set; }
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = "";
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu ít nhất 6 ký tự")]
    public string NewPassword { get; set; } = "";
}
