namespace WebBanHang.Api.Data.Entities;

/// <summary>dbo.News</summary>
public class News : CmsEntity, ISeoEntity
{
    public int? CategoryId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ShortText { get; set; }
    public string? Images { get; set; }
    public string? Source { get; set; }
    public DateTime? ExpireDate { get; set; }
    public int? IsHome { get; set; }
    public int? IsMum { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? TitleUrl { get; set; }
    public string? TitleSeo { get; set; }

    public string SlugSource => Title ?? "";
}

/// <summary>dbo.NewsCategory</summary>
public class NewsCategory : CmsEntity, ISeoEntity
{
    public string Title { get; set; } = "";
    public string? Images { get; set; }
    public string? Description { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? TitleUrl { get; set; }
    public string? TitleSeo { get; set; }

    public string SlugSource => Title;
}

/// <summary>dbo.Services</summary>
public class Service : CmsEntity
{
    public int? ServicesSubId { get; set; }
    public int? CategoryId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ShortText { get; set; }
    public string? Images { get; set; }
    public string? Video { get; set; }
    public string? Download { get; set; }
    public string? TitleDownload { get; set; }
    public int? IsHome { get; set; }
}

/// <summary>dbo.BannerHome — homepage slider.</summary>
public class Banner : CmsEntity
{
    public int? CategoryId { get; set; }
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Images { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
    public string? BigImages { get; set; }
}

/// <summary>dbo.Partner</summary>
public class Partner : CmsEntity
{
    public int CategoryId { get; set; }
    public string? Title { get; set; }
    public string? Images { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
}

/// <summary>dbo.Project</summary>
public class Project : CmsEntity, ISeoEntity
{
    public int? CategoryId { get; set; }
    public string? Title { get; set; }
    public string? ShortText { get; set; }
    public string? Description { get; set; }
    public string? Images { get; set; }
    public string? Video { get; set; }
    public string? Download { get; set; }
    public DateTime? BeginDay { get; set; }
    public DateTime? EndDay { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? TitleUrl { get; set; }
    public string? TitleSeo { get; set; }
    public int? IsTypical { get; set; }
    public int? IsNews { get; set; }

    public string SlugSource => Title ?? "";
}

/// <summary>dbo.ProjectCategory</summary>
public class ProjectCategory : CmsEntity, ISeoEntity
{
    public string? Title { get; set; }
    public string? Images { get; set; }
    public string? Description { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? TitleUrl { get; set; }
    public string? TitleSeo { get; set; }

    public string SlugSource => Title ?? "";
}

/// <summary>dbo.DocumentDownload</summary>
public class Document : CmsEntity
{
    public int? CategoryId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ShortText { get; set; }
    public string? Images { get; set; }
    public string? FileUpload { get; set; }
}

/// <summary>dbo.DocumentDownloadCategory</summary>
public class DocumentCategory : CmsEntity
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Images { get; set; }
}

/// <summary>dbo.Config — key/value site settings.</summary>
public class SiteConfig : CmsEntity
{
    public int? CategoryId { get; set; }
    public string? ConfigName { get; set; }
    public string? ConfigValue { get; set; }
}
