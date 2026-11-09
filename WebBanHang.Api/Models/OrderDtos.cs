using System.ComponentModel.DataAnnotations;

namespace WebBanHang.Api.Models;

// Mirrors the Order type in frontend/types/index.ts.

public record AddressDto(string Id, string FullName, string Phone, string Street, string Ward, string District, string City, bool IsDefault);

public record OrderItemProductDto(string Id, string Name, string Slug, IReadOnlyList<ProductImageDto> Images);

public record OrderItemDto(string Id, OrderItemProductDto Product, int Quantity, double UnitPrice, double Subtotal);

public class OrderDto
{
    public required string Id { get; init; }
    public required string OrderCode { get; init; }
    public required string Status { get; init; }
    public string PaymentMethod { get; init; } = "bank_transfer";
    public string PaymentStatus { get; init; } = "unpaid";
    public required AddressDto ShippingAddress { get; init; }
    public required IReadOnlyList<OrderItemDto> Items { get; init; }
    public double Subtotal { get; init; }
    public double ShippingFee { get; init; }
    public double Discount { get; init; }
    public double Total { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    // Contact details of the requester (admin screens).
    public string? Email { get; init; }
    public DateTime? DeliveryDate { get; init; }
}

public class OrderStatusUpdate
{
    /// <summary>pending | confirmed | processing | shipping | delivered | cancelled</summary>
    [Required]
    public string Status { get; set; } = "";
}

public class OrderUpdate
{
    [StringLength(250)] public string? FullName { get; set; }
    [EmailAddress, StringLength(250)] public string? Email { get; set; }
    [StringLength(50)] public string? Phone { get; set; }
    [StringLength(550)] public string? Address { get; set; }
    public string? Note { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Status { get; set; }
}
