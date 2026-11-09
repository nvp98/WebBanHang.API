using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebBanHang.Api.Data.Entities;
using WebBanHang.Api.Common;
using WebBanHang.Api.Services;

namespace WebBanHang.Api.Data;

/// <summary>
/// Database-first mapping onto the existing "vienvienco_VVNET" schema.
/// No migrations are generated: the schema is owned by the SQL script in /SQL.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductManufacturer> Manufacturers => Set<ProductManufacturer>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<StandardIso> StandardIsos => Set<StandardIso>();
    public DbSet<ProductStandardIso> ProductStandardIsos => Set<ProductStandardIso>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<News> News => Set<News>();
    public DbSet<NewsCategory> NewsCategories => Set<NewsCategory>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectCategory> ProjectCategories => Set<ProjectCategory>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<SiteConfig> Configs => Set<SiteConfig>();
    public DbSet<Logon> Logons => Set<Logon>();
    public DbSet<Assign> Assigns => Set<Assign>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        var product = Cms<Product>(mb, "Product", "PKProduct", "IDProduct");
        product.Property(x => x.CategoryId).HasColumnName("CategoryID");
        product.Property(x => x.SubProductId).HasColumnName("SubProductID");
        product.Property(x => x.SubId).HasColumnName("SubID");
        product.Property(x => x.StandardIso).HasColumnName("StandardISO");
        product.Property(x => x.Url).HasColumnName("URL");
        product.Property(x => x.PriceNew).HasColumnName("Pricenew");
        product.Property(x => x.PromotionalPercent).HasColumnName("Promotionalpercent");
        product.Property(x => x.SEvaluation).HasColumnName("S_Evaluation");
        product.Property(x => x.PEvaluation).HasColumnName("P_Evaluation");
        product.Property(x => x.PCount).HasColumnName("P_Count");
        product.Property(x => x.TitleUrl).HasColumnName("TitleURL");
        product.Property(x => x.TitleSeo).HasColumnName("TitleSEO");
        product.Property(x => x.ManufacturerId).HasColumnName("ManufacturerID");
        product.Property(x => x.Highlights).HasColumnName("Brand");
        NText(product, x => x.Description, x => x.ShortText, x => x.Highlights);

        var category = Cms<ProductCategory>(mb, "ProductCategory", "PKProductCategory", "IDCategory");
        category.Property(x => x.IsHome).HasColumnName("Ishome");
        category.Property(x => x.IsMenu).HasColumnName("Is_menu");
        category.Property(x => x.PCount).HasColumnName("pcount");
        category.Property(x => x.TitleUrl).HasColumnName("TitleURL");
        category.Property(x => x.TitleSeo).HasColumnName("TitleSEO");
        NText(category, x => x.Title, x => x.ShortText, x => x.Description);

        NText(Cms<ProductManufacturer>(mb, "ProductManufacturer", "PKProductManufacturer", "IDManufacturer"), x => x.Description);

        Cms<ProductImage>(mb, "ImagesProduct", "PKImagesProduct", "IDImagesProduct")
            .Property(x => x.ProductId).HasColumnName("ProductID");

        var iso = Cms<StandardIso>(mb, "StandardISO", "PKStandardISO", "IDStandardISO");
        iso.Property(x => x.TitleSeo).HasColumnName("TitleSEO");
        NText(iso, x => x.Description);

        var link = mb.Entity<ProductStandardIso>();
        link.ToTable("ProductStandardISO");
        link.HasKey(x => x.Pk);
        link.Property(x => x.Pk).HasColumnName("PKProductStandardISO");
        link.Property(x => x.ProductId).HasColumnName("ProductID");
        link.Property(x => x.StandardIsoId).HasColumnName("StandardISOID");
        link.Property(x => x.LanguageId).HasColumnName("LanguageID");

        var order = Cms<Order>(mb, "Orders", "PKOrders", "IDCategory");
        order.Property(x => x.DateOfDelivery).HasColumnName("Dateofdelivery");
        order.Property(x => x.Status).HasColumnName("is_status");
        NText(order, x => x.Note);

        var detail = Cms<OrderDetail>(mb, "ordersdetailed", "PKordersdetailed", "IDordersdetailed");
        detail.Property(x => x.OrderId).HasColumnName("CategoryID");
        detail.Property(x => x.ProductId).HasColumnName("IDProduct");
        detail.Property(x => x.Quantity).HasColumnName("Number");

        var contact = Cms<Contact>(mb, "Contacts", "PKContacts", "IDContact");
        contact.Property(x => x.CategoryId).HasColumnName("CategoryID");
        NText(contact, x => x.Contents);

        var news = Cms<News>(mb, "News", "PKNews", "IDNews");
        news.Property(x => x.CategoryId).HasColumnName("CategoryID");
        news.Property(x => x.IsMum).HasColumnName("Ismum");
        news.Property(x => x.TitleUrl).HasColumnName("TitleURL");
        news.Property(x => x.TitleSeo).HasColumnName("TitleSEO");
        NText(news, x => x.Description, x => x.ShortText);

        var newsCategory = Cms<NewsCategory>(mb, "NewsCategory", "PKNewsCategory", "IDCategory");
        newsCategory.Property(x => x.TitleUrl).HasColumnName("TitleURL");
        newsCategory.Property(x => x.TitleSeo).HasColumnName("TitleSEO");
        NText(newsCategory, x => x.Title, x => x.Description);

        var service = Cms<Service>(mb, "Services", "PKServices", "IDServices");
        service.Property(x => x.ServicesSubId).HasColumnName("ServicesSubID");
        service.Property(x => x.CategoryId).HasColumnName("CategoryID");
        NText(service, x => x.Description, x => x.ShortText);

        var banner = Cms<Banner>(mb, "BannerHome", "PKBannerHome", "IDBannerHome");
        banner.Property(x => x.CategoryId).HasColumnName("CategoryID");
        banner.Property(x => x.Subtitle).HasColumnName("Title1");
        NText(banner, x => x.Description);

        var partner = Cms<Partner>(mb, "Partner", "PKPartner", "IDPartner");
        partner.Property(x => x.CategoryId).HasColumnName("CategoryID");
        partner.Property(x => x.Url).IsUnicode(false);
        NText(partner, x => x.Description);

        var project = Cms<Project>(mb, "Project", "PKProject", "IDProject", isDeletedColumn: "Isdeleted");
        project.Property(x => x.CategoryId).HasColumnName("CategoryID");
        project.Property(x => x.IsTypical).HasColumnName("Istypical");
        project.Property(x => x.IsNews).HasColumnName("Isnews");
        project.Property(x => x.TitleUrl).HasColumnName("TitleURL");
        project.Property(x => x.TitleSeo).HasColumnName("TitleSEO");
        NText(project, x => x.ShortText, x => x.Description);

        var projectCategory = Cms<ProjectCategory>(mb, "ProjectCategory", "PKProjectCategory", "IDCategory", isDeletedColumn: "Isdeleted");
        projectCategory.Property(x => x.TitleUrl).HasColumnName("TitleURL");
        projectCategory.Property(x => x.TitleSeo).HasColumnName("TitleSEO");
        NText(projectCategory, x => x.Description);

        var document = Cms<Document>(mb, "DocumentDownload", "PKDocumentDownload", "IDDocumentDownload");
        document.Property(x => x.CategoryId).HasColumnName("CategoryID");
        NText(document, x => x.Description, x => x.ShortText, x => x.FileUpload);

        NText(Cms<DocumentCategory>(mb, "DocumentDownloadCategory", "PKDocumentDownloadCategory", "IDCategory"), x => x.Description);

        var config = Cms<SiteConfig>(mb, "Config", "PKConfig", "IDConfig");
        config.Property(x => x.CategoryId).HasColumnName("CategoryID");
        NText(config, x => x.ConfigValue);

        Audit<Logon>(mb, "Logons", "PKLogons", "IDLogons").Property(x => x.IsSeo).HasColumnName("IsSEO");

        var assign = Cms<Assign>(mb, "Assigns", "PKAssigns", "IDAssigns");
        assign.Property(x => x.LogonsId).HasColumnName("LogonsID");
        assign.Property(x => x.GroupsId).HasColumnName("GroupsID");

        NText(Cms<Group>(mb, "Groups", "PKGroups", "IDGroups"), x => x.GroupName, x => x.Description);

        var member = Cms<Member>(mb, "Members", "PKMembers", "IDMembers");
        member.Property(x => x.LogonsId).HasColumnName("LogonsID");
        member.Property(x => x.Birthday).HasColumnName("Brithday");
        NText(member, x => x.Description);

        Audit<Account>(mb, "Account", "PKAccount", "IDAccount").Property(x => x.Cmnd).HasColumnName("CMND");
    }

    private static EntityTypeBuilder<T> Audit<T>(ModelBuilder mb, string table, string pk, string id, string isDeletedColumn = "IsDeleted")
        where T : AuditEntity
    {
        var e = mb.Entity<T>();
        e.ToTable(table);
        e.HasKey(x => x.Pk);
        e.Property(x => x.Pk).HasColumnName(pk).ValueGeneratedOnAdd();
        e.Property(x => x.Id).HasColumnName(id);
        e.Property(x => x.IsDeleted).HasColumnName(isDeletedColumn);
        return e;
    }

    private static EntityTypeBuilder<T> Cms<T>(ModelBuilder mb, string table, string pk, string id, string isDeletedColumn = "IsDeleted")
        where T : CmsEntity
    {
        var e = Audit<T>(mb, table, pk, id, isDeletedColumn);
        e.Property(x => x.LanguageId).HasColumnName("LanguageID");
        return e;
    }

    // ntext columns cannot be compared/sorted and need matching parameter types.
    private static void NText<T>(EntityTypeBuilder<T> e, params System.Linq.Expressions.Expression<Func<T, string?>>[] props)
        where T : class
    {
        foreach (var p in props) e.Property(p).HasColumnType("ntext");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAudit();
        return base.SaveChanges();
    }

    /// <summary>Fills the audit/NOT NULL columns the legacy schema expects on every row.</summary>
    private void StampAudit()
    {
        var now = DateTime.Now;
        var userId = currentUser.AdminId ?? 0;

        foreach (var entry in ChangeTracker.Entries<AuditEntity>())
        {
            var e = entry.Entity;
            if (entry.State == EntityState.Added)
            {
                e.CreateDate ??= now;
                e.CreateBy ??= userId;
                e.Levels ??= 0;
                e.IsDeleted ??= 0;
                e.Id ??= 0;
                if (e is CmsEntity cms) cms.LanguageId ??= Lang.Vi;
            }
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                e.ModifyDate = now;
                e.ModifyBy = userId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ProductStandardIso>())
        {
            if (entry.State != EntityState.Added) continue;
            var e = entry.Entity;
            e.CreateDate ??= now;
            e.CreateBy ??= userId;
            e.ModifyDate = now;
            e.ModifyBy = userId;
            e.Levels ??= 0;
            e.IsDeleted ??= 0;
            e.LanguageId ??= Lang.Vi;
        }
    }
}
