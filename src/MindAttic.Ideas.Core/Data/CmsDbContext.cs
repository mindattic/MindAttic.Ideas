using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Data;

/// <summary>
/// The CMS database. One append-only initial migration; all later migrations are additive.
/// <see cref="Page"/> is system-versioned (temporal) for wiki-like history.
/// </summary>
public sealed class CmsDbContext(DbContextOptions<CmsDbContext> options) : DbContext(options)
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<CmsContentDefinition> ContentDefinitions => Set<CmsContentDefinition>();
    public DbSet<InstalledPackage> InstalledPackages => Set<InstalledPackage>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<SettingEntry> Settings => Set<SettingEntry>();
    public DbSet<AdminInboxMessage> AdminInbox => Set<AdminInboxMessage>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Site>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => x.Uid);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.HostBindings).HasMaxLength(1000);
            e.Property(x => x.DefaultThemeKey).HasMaxLength(120);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        b.Entity<Page>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => x.Uid);
            // Free-form pages are resolved by (SiteId, Slug) — never per-page routing.
            e.HasIndex(x => new { x.SiteId, x.Slug }).IsUnique();
            e.HasIndex(x => x.ThemeKey);
            e.HasIndex(x => new { x.IsPublished, x.Enabled });
            e.Property(x => x.Slug).HasMaxLength(400).IsRequired();
            e.Property(x => x.Title).HasMaxLength(400);
            e.Property(x => x.ThemeKey).HasMaxLength(120);
            e.Property(x => x.ComponentTypeName).HasMaxLength(512);
            e.Property(x => x.AssemblyName).HasMaxLength(256);
            e.Property(x => x.AuthoredByUserId).HasMaxLength(64);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.BodyTrust).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.RowVersion).IsRowVersion();
            // Self-referencing tree for nav; never cascade-delete a subtree implicitly.
            e.HasOne<Page>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.NoAction);
            e.HasQueryFilter(x => !x.IsDeleted);
            // Wiki-like history: SQL Server system-versioned temporal table.
            e.ToTable("Pages", t => t.IsTemporal());
        });

        b.Entity<CmsContentDefinition>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => x.Uid);
            e.HasIndex(x => new { x.Kind, x.Key, x.Version, x.Origin }).IsUnique();
            e.Property(x => x.Key).HasMaxLength(120).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.ClrTypeName).HasMaxLength(512);
            e.Property(x => x.AssemblyName).HasMaxLength(256);
            e.Property(x => x.AssetMount).HasMaxLength(256);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Origin).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Strategy).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.RenderMode).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Scope).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<InstalledPackage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => x.Uid);
            e.HasIndex(x => new { x.Category, x.Key, x.Version }).IsUnique();
            e.Property(x => x.Category).HasMaxLength(16);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Key).HasMaxLength(120).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.BlobPath).HasMaxLength(1024);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<Asset>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => x.Uid);
            e.HasIndex(x => new { x.SiteId, x.Folder, x.FileName });
            e.HasIndex(x => x.Sha256);
            e.Property(x => x.Folder).HasMaxLength(400);
            e.Property(x => x.FileName).HasMaxLength(400).IsRequired();
            e.Property(x => x.BlobUri).HasMaxLength(1024);
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        b.Entity<SettingEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Scope, x.ScopeId, x.Key }).IsUnique();
            e.Property(x => x.Scope).HasMaxLength(16).IsRequired();
            e.Property(x => x.Key).HasMaxLength(200).IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        b.Entity<AdminInboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DedupKey).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.Severity).HasMaxLength(16);
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.Subject).HasMaxLength(400);
            e.Property(x => x.DedupKey).HasMaxLength(450).IsRequired();
            e.Property(x => x.Status).HasMaxLength(16);
        });

        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(120);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(40);
            e.Property(x => x.SecurityStamp).HasMaxLength(64);
        });
    }
}
