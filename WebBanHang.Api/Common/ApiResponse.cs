namespace WebBanHang.Api.Common;

/// <summary>Envelope expected by the frontend: <c>{ success, data, message }</c>.</summary>
public record ApiResponse<T>(bool Success, T Data, string? Message = null);

/// <summary>Error shape expected by the frontend: <c>{ success: false, message, errors, statusCode }</c>.</summary>
public record ApiError(string Message, int StatusCode, IDictionary<string, string[]>? Errors = null)
{
    public bool Success => false;
}

public static class ApiResult
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null) => new(true, data, message);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>Thrown by services; rendered as an <see cref="ApiError"/> with the given status code.</summary>
public class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static ApiException NotFound(string what = "Không tìm thấy dữ liệu") => new(StatusCodes.Status404NotFound, what);
    public static ApiException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);
    public static ApiException Conflict(string message) => new(StatusCodes.Status409Conflict, message);
}
