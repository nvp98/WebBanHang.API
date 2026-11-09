using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanHang.Api.Common;
using WebBanHang.Api.Data;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Models;

namespace WebBanHang.Api.Controllers.Admin;

[Route("api/admin/news")]
[Tags("Admin · Nội dung")]
public class NewsAdminController(AppDbContext db) : CmsCrudController<News, NewsInput>(db)
{
    protected override void Apply(NewsInput i, News e)
    {
        e.Title = i.Title.Trim();
        e.CategoryId = i.CategoryId;
        e.ShortText = Html.Clean(i.ShortText);
        e.Description = Html.Clean(i.Description);
        e.Images = i.Images?.Trim();
        e.Source = i.Source?.Trim();
        e.IsHome = Flag(i.IsHome);
    }

    protected override IQueryable<News> Search(IQueryable<News> q, string pattern) =>
        q.Where(n => EF.Functions.Like(n.Title!, pattern, "\\"));

    protected override IQueryable<News> FilterByCategory(IQueryable<News> q, int categoryId) =>
        q.Where(n => n.CategoryId == categoryId);

    protected override IOrderedQueryable<News> Sort(IQueryable<News> q) =>
        q.OrderByDescending(n => n.CreateDate).ThenByDescending(n => n.Pk);
}

[Route("api/admin/news-categories")]
[Tags("Admin · Nội dung")]
public class NewsCategoriesAdminController(AppDbContext db) : CmsCrudController<NewsCategory, NewsCategoryInput>(db)
{
    protected override void Apply(NewsCategoryInput i, NewsCategory e)
    {
        e.Title = i.Title.Trim();
        e.Description = Html.Clean(i.Description);
        e.Images = i.Images?.Trim();
    }

    protected override IQueryable<NewsCategory> Search(IQueryable<NewsCategory> q, string pattern) =>
        q.Where(c => EF.Functions.Like(c.Title, pattern, "\\"));

    protected override async Task EnsureCanDeleteAsync(int logicalId, CancellationToken ct)
    {
        var count = await Db.News.Active().CountAsync(n => n.CategoryId == logicalId, ct);
        if (count > 0) throw ApiException.Conflict($"Chuyên mục còn {count} bài viết");
    }
}

[Route("api/admin/services")]
[Tags("Admin · Nội dung")]
public class ServicesAdminController(AppDbContext db) : CmsCrudController<Service, ServiceInput>(db)
{
    protected override void Apply(ServiceInput i, Service e)
    {
        e.Title = i.Title.Trim();
        e.CategoryId = i.CategoryId;
        e.ShortText = Html.Clean(i.ShortText);
        e.Description = Html.Clean(i.Description);
        e.Images = i.Images?.Trim();
        e.Video = i.Video?.Trim();
        e.Download = i.Download?.Trim();
        e.TitleDownload = i.TitleDownload;
        e.IsHome = Flag(i.IsHome);
    }

    protected override IQueryable<Service> Search(IQueryable<Service> q, string pattern) =>
        q.Where(s => EF.Functions.Like(s.Title!, pattern, "\\"));
}

[Route("api/admin/banners")]
[Tags("Admin · Nội dung")]
public class BannersAdminController(AppDbContext db) : CmsCrudController<Banner, BannerInput>(db)
{
    protected override void Apply(BannerInput i, Banner e)
    {
        e.Title = i.Title?.Trim();
        e.Subtitle = i.Subtitle?.Trim();
        e.Images = i.Images.Trim();
        e.BigImages = i.BigImages?.Trim();
        e.Url = i.Url?.Trim();
        e.Description = Html.Clean(i.Description);
        e.CategoryId = i.CategoryId;
    }

    protected override IQueryable<Banner> Search(IQueryable<Banner> q, string pattern) =>
        q.Where(b => EF.Functions.Like(b.Title!, pattern, "\\"));

    protected override IQueryable<Banner> FilterByCategory(IQueryable<Banner> q, int categoryId) =>
        q.Where(b => b.CategoryId == categoryId);
}

[Route("api/admin/partners")]
[Tags("Admin · Nội dung")]
public class PartnersAdminController(AppDbContext db) : CmsCrudController<Partner, PartnerInput>(db)
{
    protected override void Apply(PartnerInput i, Partner e)
    {
        e.Title = i.Title.Trim();
        e.Images = i.Images?.Trim();
        e.Url = i.Url?.Trim();
        e.Description = Html.Clean(i.Description);
        e.CategoryId = i.CategoryId;
    }

    protected override IQueryable<Partner> Search(IQueryable<Partner> q, string pattern) =>
        q.Where(p => EF.Functions.Like(p.Title!, pattern, "\\"));
}

[Route("api/admin/projects")]
[Tags("Admin · Nội dung")]
public class ProjectsAdminController(AppDbContext db) : CmsCrudController<Project, ProjectInput>(db)
{
    protected override void Apply(ProjectInput i, Project e)
    {
        e.Title = i.Title.Trim();
        e.CategoryId = i.CategoryId;
        e.ShortText = Html.Clean(i.ShortText);
        e.Description = Html.Clean(i.Description);
        e.Images = i.Images?.Trim();
        e.Video = i.Video?.Trim();
        e.Download = i.Download?.Trim();
        e.BeginDay = i.BeginDay;
        e.EndDay = i.EndDay;
        e.IsTypical = Flag(i.IsTypical);
        e.IsNews = Flag(i.IsNews);
    }

    protected override IQueryable<Project> Search(IQueryable<Project> q, string pattern) =>
        q.Where(p => EF.Functions.Like(p.Title!, pattern, "\\"));

    protected override IQueryable<Project> FilterByCategory(IQueryable<Project> q, int categoryId) =>
        q.Where(p => p.CategoryId == categoryId);
}

[Route("api/admin/project-categories")]
[Tags("Admin · Nội dung")]
public class ProjectCategoriesAdminController(AppDbContext db) : CmsCrudController<ProjectCategory, ProjectCategoryInput>(db)
{
    protected override void Apply(ProjectCategoryInput i, ProjectCategory e)
    {
        e.Title = i.Title.Trim();
        e.Images = i.Images?.Trim();
        e.Description = Html.Clean(i.Description);
    }

    protected override IQueryable<ProjectCategory> Search(IQueryable<ProjectCategory> q, string pattern) =>
        q.Where(c => EF.Functions.Like(c.Title!, pattern, "\\"));

    protected override async Task EnsureCanDeleteAsync(int logicalId, CancellationToken ct)
    {
        var count = await Db.Projects.Active().CountAsync(p => p.CategoryId == logicalId, ct);
        if (count > 0) throw ApiException.Conflict($"Nhóm còn {count} dự án");
    }
}

[Route("api/admin/documents")]
[Tags("Admin · Nội dung")]
public class DocumentsAdminController(AppDbContext db) : CmsCrudController<Document, DocumentInput>(db)
{
    protected override void Apply(DocumentInput i, Document e)
    {
        e.Title = i.Title.Trim();
        e.CategoryId = i.CategoryId;
        e.ShortText = Html.Clean(i.ShortText);
        e.Description = Html.Clean(i.Description);
        e.Images = i.Images?.Trim();
        e.FileUpload = i.FileUpload?.Trim();
    }

    protected override IQueryable<Document> Search(IQueryable<Document> q, string pattern) =>
        q.Where(d => EF.Functions.Like(d.Title!, pattern, "\\"));

    protected override IQueryable<Document> FilterByCategory(IQueryable<Document> q, int categoryId) =>
        q.Where(d => d.CategoryId == categoryId);
}

[Route("api/admin/document-categories")]
[Tags("Admin · Nội dung")]
public class DocumentCategoriesAdminController(AppDbContext db) : CmsCrudController<DocumentCategory, DocumentCategoryInput>(db)
{
    protected override void Apply(DocumentCategoryInput i, DocumentCategory e)
    {
        e.Title = i.Title.Trim();
        e.Description = Html.Clean(i.Description);
        e.Images = i.Images?.Trim();
    }

    protected override IQueryable<DocumentCategory> Search(IQueryable<DocumentCategory> q, string pattern) =>
        q.Where(c => EF.Functions.Like(c.Title, pattern, "\\"));

    protected override async Task EnsureCanDeleteAsync(int logicalId, CancellationToken ct)
    {
        var count = await Db.Documents.Active().CountAsync(d => d.CategoryId == logicalId, ct);
        if (count > 0) throw ApiException.Conflict($"Nhóm còn {count} tài liệu");
    }
}

/// <summary>Site settings. Admin only — the table also stores SMTP credentials.</summary>
[Route("api/admin/configs")]
[Tags("Admin · Hệ thống")]
[Authorize(Policy = Policies.Admin)]
public class ConfigsAdminController(AppDbContext db) : CmsCrudController<SiteConfig, ConfigInput>(db)
{
    protected override void Apply(ConfigInput i, SiteConfig e)
    {
        e.ConfigName = i.ConfigName.Trim();
        e.ConfigValue = i.ConfigValue;
        e.CategoryId = i.CategoryId;
    }

    protected override IQueryable<SiteConfig> Search(IQueryable<SiteConfig> q, string pattern) =>
        q.Where(c => EF.Functions.Like(c.ConfigName!, pattern, "\\") || EF.Functions.Like(c.ConfigValue!, pattern, "\\"));

    protected override IOrderedQueryable<SiteConfig> Sort(IQueryable<SiteConfig> q) => q.OrderBy(c => c.ConfigName).ThenBy(c => c.Pk);
}
