namespace WebBanHang.Api.Data.Entities;

/// <summary>dbo.Product</summary>
public class Product : CmsEntity, ISeoEntity
{
    /// <summary>Logical id (IDCategory) of ProductCategory.</summary>
    public int CategoryId { get; set; }
    public int? SubProductId { get; set; }
    public int? SubId { get; set; }
    public string? Title { get; set; }
    /// <summary>Product code / SKU.</summary>
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? ShortText { get; set; }
    /// <summary>Free-text list of test standards, e.g. "ISO 9237, ASTM D1776".</summary>
    public string? StandardIso { get; set; }
    public string? FileCatalogue { get; set; }
    public string? Images { get; set; }
    /// <summary>Main image file name (folder Photos/Products).</summary>
    public string? BigImages { get; set; }
    public string? Url { get; set; }
    public double? Price { get; set; }
    public double? PriceNew { get; set; }
    public int? InPromotion { get; set; }
    public double? PromotionalPrice { get; set; }
    public double? PromotionalPercent { get; set; }
    public int? AvailableDays { get; set; }
    public int? SEvaluation { get; set; }
    public int? PEvaluation { get; set; }
    public int? PCount { get; set; }
    public DateTime? ExpireDate { get; set; }
    public int? IsSellers { get; set; }
    public int? IsNew { get; set; }
    public int? IsOnHomepage { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? TitleUrl { get; set; }
    public string? TitleSeo { get; set; }
    public string? Quote { get; set; }
    /// <summary>Logical id (IDManufacturer) of ProductManufacturer.</summary>
    public int? ManufacturerId { get; set; }
    /// <summary>Column "Brand": HTML block of commitments (warranty, shipping, standards) shown beside the product.</summary>
    public string? Highlights { get; set; }

    public string SlugSource => Title ?? "";
}

/// <summary>dbo.ProductCategory</summary>
public class ProductCategory : CmsEntity, ISeoEntity
{
    public string Title { get; set; } = "";
    public string? FileCatalogue { get; set; }
    public string? Images { get; set; }
    public int? IsHome { get; set; }
    public int? IsMenu { get; set; }
    public int? PCount { get; set; }
    public string? ShortText { get; set; }
    public string? Description { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? TitleUrl { get; set; }
    public string? TitleSeo { get; set; }

    public string SlugSource => Title;
}

/// <summary>dbo.ProductManufacturer — used as "Brand" by the frontend.</summary>
public class ProductManufacturer : CmsEntity
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Images { get; set; }
}

/// <summary>dbo.ImagesProduct — extra gallery images. ProductId = logical product id.</summary>
public class ProductImage : CmsEntity
{
    public int? ProductId { get; set; }
    public string? Title { get; set; }
    public string? Images { get; set; }
}

/// <summary>dbo.StandardISO</summary>
public class StandardIso : CmsEntity
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Images { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? TitleSeo { get; set; }
}

/// <summary>dbo.ProductStandardISO — link table (no logical id column).</summary>
public class ProductStandardIso
{
    public int Pk { get; set; }
    public int? ProductId { get; set; }
    public int? StandardIsoId { get; set; }
    public int? CreateBy { get; set; }
    public DateTime? CreateDate { get; set; }
    public int? ModifyBy { get; set; }
    public DateTime? ModifyDate { get; set; }
    public int? LanguageId { get; set; }
    public int? Levels { get; set; }
    public int? IsDeleted { get; set; }
}
