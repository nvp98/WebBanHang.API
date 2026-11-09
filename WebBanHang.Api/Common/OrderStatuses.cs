namespace WebBanHang.Api.Common;

/// <summary>Maps the legacy Orders.is_status integer to the frontend OrderStatus strings.</summary>
public static class OrderStatuses
{
    private static readonly string[] Names = ["pending", "confirmed", "processing", "shipping", "delivered", "cancelled"];

    public const int Pending = 0;
    public const int Cancelled = 5;

    public static string ToName(int? status) =>
        status is >= 0 and < 6 ? Names[status.Value] : Names[Pending];

    public static int? FromName(string? name)
    {
        var idx = Array.IndexOf(Names, name?.Trim().ToLowerInvariant());
        return idx < 0 ? null : idx;
    }

    public static IReadOnlyList<string> All => Names;

    /// <summary>Quote code shown to customers, e.g. BG006392.</summary>
    public static string Code(int orderId) => $"BG{orderId:D6}";
}
