using System.ComponentModel.DataAnnotations;

namespace WebBanHang.Api.Models;

// Request bodies of the admin CRUD endpoints. Audit columns (CreateBy/ModifyDate/…), Pk/Id and LanguageID are
// never bound from the body — they are set by the server. String lengths follow the column sizes in the SQL script.

public class AdminListQuery
{
    /// <summary>Free-text search.</summary>
    public string? Q { get; set; }
    /// <summary>vi | en | all (default vi).</summary>
    public string? Lang { get; set; }
    /// <summary>active | deleted | all (default active).</summary>
    public string? Status { get; set; }
    public int? CategoryId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public abstract class CmsInput
{
    /// <summary>Sort order — higher values are listed first.</summary>
    public int Levels { get; set; }
}

public interface ISeoInput
{
    /// <summary>URL slug without "/" or ".html". Generated from the title when empty.</summary>
    string? Slug { get; }
    string? MetaTitle { get; }
    string? MetaDescription { get; }
    string? MetaKeywords { get; }
}

public abstract class SeoInput : CmsInput, ISeoInput
{
    [StringLength(200), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug chỉ gồm chữ thường, số và dấu gạch ngang")]
    public string? Slug { get; set; }
    [StringLength(550)] public string? MetaTitle { get; set; }
    [StringLength(3050)] public string? MetaDescription { get; set; }
    [StringLength(3050)] public string? MetaKeywords { get; set; }
}

public class ProductInput : SeoInput
{
    [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm"), StringLength(250)] public string Title { get; set; } = "";
    [StringLength(50)] public string? Code { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn danh mục")] public int CategoryId { get; set; }
    public int? ManufacturerId { get; set; }
    public string? ShortText { get; set; }
    public string? Description { get; set; }
    public string? StandardIso { get; set; }
    /// <summary>Main image (file name or /uploads/... path).</summary>
    [StringLength(1000)] public string? BigImages { get; set; }
    [StringLength(550)] public string? Images { get; set; }
    [StringLength(100)] public string? FileCatalogue { get; set; }
    [Range(0, double.MaxValue)] public double? Price { get; set; }
    [Range(0, double.MaxValue)] public double? PromotionalPrice { get; set; }
    [Range(0, 100)] public double? PromotionalPercent { get; set; }
    public bool InPromotion { get; set; }
    public bool IsNew { get; set; }
    public bool IsSellers { get; set; }
    public bool IsOnHomepage { get; set; }
    /// <summary>HTML box of commitments (legacy column "Brand").</summary>
    public string? Highlights { get; set; }
    [StringLength(550)] public string? TitleSeo { get; set; }
}

public class ProductCategoryInput : SeoInput
{
    [Required(ErrorMessage = "Vui lòng nhập tên danh mục"), StringLength(4000)] public string Title { get; set; } = "";
    public string? ShortText { get; set; }
    public string? Description { get; set; }
    [StringLength(550)] public string? Images { get; set; }
    [StringLength(100)] public string? FileCatalogue { get; set; }
    public bool IsHome { get; set; }
    public bool IsMenu { get; set; } = true;
    [StringLength(550)] public string? TitleSeo { get; set; }
}

public class ManufacturerInput : CmsInput
{
    [Required(ErrorMessage = "Vui lòng nhập tên thương hiệu"), StringLength(550)] public string Title { get; set; } = "";
    public string? Description { get; set; }
    [StringLength(550)] public string? Images { get; set; }
}

public class StandardIsoInput : CmsInput
{
    [Required, StringLength(550)] public string Title { get; set; } = "";
    public string? Description { get; set; }
    [StringLength(550)] public string? Images { get; set; }
}

public class NewsCategoryInput : SeoInput
{
    [Required, StringLength(4000)] public string Title { get; set; } = "";
    public string? Description { get; set; }
    [StringLength(50)] public string? Images { get; set; }
}

public class NewsInput : SeoInput
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề"), StringLength(250)] public string Title { get; set; } = "";
    public int? CategoryId { get; set; }
    public string? ShortText { get; set; }
    public string? Description { get; set; }
    [StringLength(1000)] public string? Images { get; set; }
    [StringLength(200)] public string? Source { get; set; }
    public bool IsHome { get; set; }
}

public class ServiceInput : CmsInput
{
    [Required, StringLength(550)] public string Title { get; set; } = "";
    public int? CategoryId { get; set; }
    public string? ShortText { get; set; }
    public string? Description { get; set; }
    [StringLength(1025)] public string? Images { get; set; }
    [StringLength(1000)] public string? Video { get; set; }
    [StringLength(1000)] public string? Download { get; set; }
    [StringLength(550)] public string? TitleDownload { get; set; }
    public bool IsHome { get; set; }
}

public class BannerInput : CmsInput
{
    [StringLength(555)] public string? Title { get; set; }
    [StringLength(550)] public string? Subtitle { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn ảnh"), StringLength(255)] public string Images { get; set; } = "";
    [StringLength(250)] public string? BigImages { get; set; }
    [StringLength(250)] public string? Url { get; set; }
    public string? Description { get; set; }
    /// <summary>BannerHomeCategory id (slider group).</summary>
    public int? CategoryId { get; set; }
}

public class PartnerInput : CmsInput
{
    [Required, StringLength(127)] public string Title { get; set; } = "";
    [StringLength(127)] public string? Images { get; set; }
    [StringLength(255), Url] public string? Url { get; set; }
    public string? Description { get; set; }
    public int CategoryId { get; set; }
}

public class ProjectInput : SeoInput
{
    [Required, StringLength(255)] public string Title { get; set; } = "";
    public int? CategoryId { get; set; }
    public string? ShortText { get; set; }
    public string? Description { get; set; }
    [StringLength(255)] public string? Images { get; set; }
    [StringLength(1000)] public string? Video { get; set; }
    [StringLength(1000)] public string? Download { get; set; }
    public DateTime? BeginDay { get; set; }
    public DateTime? EndDay { get; set; }
    public bool IsTypical { get; set; }
    public bool IsNews { get; set; }
}

public class ProjectCategoryInput : SeoInput
{
    [Required, StringLength(255)] public string Title { get; set; } = "";
    [StringLength(127)] public string? Images { get; set; }
    public string? Description { get; set; }
}

public class DocumentInput : CmsInput
{
    [Required, StringLength(50)] public string Title { get; set; } = "";
    public int? CategoryId { get; set; }
    public string? ShortText { get; set; }
    public string? Description { get; set; }
    [StringLength(550)] public string? Images { get; set; }
    public string? FileUpload { get; set; }
}

public class DocumentCategoryInput : CmsInput
{
    [Required, StringLength(255)] public string Title { get; set; } = "";
    public string? Description { get; set; }
    [StringLength(255)] public string? Images { get; set; }
}

public class ConfigInput : CmsInput
{
    [Required, StringLength(50)] public string ConfigName { get; set; } = "";
    public string? ConfigValue { get; set; }
    public int? CategoryId { get; set; }
}

public class ProductImageInput : CmsInput
{
    [Required, StringLength(550)] public string Images { get; set; } = "";
    [StringLength(550)] public string? Title { get; set; }
}

public class ProductStandardsInput
{
    /// <summary>Logical ids (IDStandardISO) of StandardISO.</summary>
    public List<int> StandardIds { get; set; } = [];
}

public class QuoteItemsInput
{
    [Required, MinLength(1)] public List<QuoteLineInput> Items { get; set; } = [];
}

public class QuoteLineInput
{
    [Required] public int ProductId { get; set; }
    [Range(1, 100_000)] public int Quantity { get; set; } = 1;
    [Range(0, double.MaxValue)] public double UnitPrice { get; set; }
}

public class ContactProcessInput
{
    public bool IsProcessed { get; set; } = true;
}

public class BulkIdsInput
{
    [Required, MinLength(1), MaxLength(500)] public List<int> Ids { get; set; } = [];
}

public class AdminUserCreate
{
    [Required, StringLength(50, MinimumLength = 3), RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Tên đăng nhập không hợp lệ")]
    public string Username { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu ít nhất 8 ký tự")]
    public string Password { get; set; } = "";
    [StringLength(50)] public string? FullName { get; set; }
    [EmailAddress, StringLength(50)] public string? Email { get; set; }
    [StringLength(50)] public string? Phone { get; set; }
    /// <summary>admin | staff</summary>
    [Required, RegularExpression("^(admin|staff)$")] public string Role { get; set; } = "staff";
}

public class AdminUserUpdate
{
    [StringLength(50)] public string? FullName { get; set; }
    [EmailAddress, StringLength(50)] public string? Email { get; set; }
    [StringLength(50)] public string? Phone { get; set; }
    [Required, RegularExpression("^(admin|staff)$")] public string Role { get; set; } = "staff";
    public bool IsActive { get; set; } = true;
}

public class ResetPasswordInput
{
    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu ít nhất 8 ký tự")]
    public string NewPassword { get; set; } = "";
}

public class ActiveInput
{
    public bool IsActive { get; set; }
}
