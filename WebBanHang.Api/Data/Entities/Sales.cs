namespace WebBanHang.Api.Data.Entities;

/// <summary>dbo.Orders — used as quote requests (B2B). Logical id column is "IDCategory".</summary>
public class Order : CmsEntity
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfDelivery { get; set; }
    /// <summary>Column "is_status": see <see cref="Common.OrderStatuses"/>.</summary>
    public int? Status { get; set; }
    public string? Note { get; set; }
}

/// <summary>dbo.ordersdetailed — CategoryId holds the logical id of the parent Order.</summary>
public class OrderDetail : CmsEntity
{
    public int? OrderId { get; set; }
    /// <summary>Logical product id (IDProduct).</summary>
    public int? ProductId { get; set; }
    public int? Quantity { get; set; }
    public string? Images { get; set; }
    /// <summary>Unit price at the time of the request.</summary>
    public double? Amount { get; set; }
}

/// <summary>dbo.Contacts</summary>
public class Contact : CmsEntity
{
    public int? CategoryId { get; set; }
    public string? Fullname { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Contents { get; set; }
    /// <summary>0 = new, 1 = processed.</summary>
    public int? IsProcess { get; set; }
}
