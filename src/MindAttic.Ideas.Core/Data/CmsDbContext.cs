using Microsoft.EntityFrameworkCore;
using MindAttic.Authentication.Data;
using MindAttic.Authentication.Entities;
using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Data;

/// <summary>
/// The CMS database. One append-only initial migration; all later migrations are additive.
/// <see cref="Page"/> is system-versioned (temporal) for wiki-like history.
/// Also the MindAttic.Authentication data seam (<see cref="IAuthDataContext"/>): the library's
/// identity tables live here in their isolated <c>auth</c> schema (FOUNDATION_AMENDMENTS A16).
/// </summary>
public sealed class CmsDbContext(DbContextOptions<CmsDbContext> options) : DbContext(options), IAuthDataContext
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<PageMetaTag> PageMetaTags => Set<PageMetaTag>();
    public DbSet<CmsContentDefinition> ContentDefinitions => Set<CmsContentDefinition>();
    public DbSet<InstalledPackage> InstalledPackages => Set<InstalledPackage>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<SettingEntry> Settings => Set<SettingEntry>();
    public DbSet<AdminInboxMessage> AdminInbox => Set<AdminInboxMessage>();

    // MindAttic.Authentication identity tables (auth schema) — IAuthDataContext.
    public DbSet<AuthUser> AuthUsers => Set<AuthUser>();
    public DbSet<AuthUserMfa> AuthUserMfa => Set<AuthUserMfa>();
    public DbSet<AuthRecoveryCode> AuthRecoveryCodes => Set<AuthRecoveryCode>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<AuthLoginThrottle> AuthLoginThrottles => Set<AuthLoginThrottle>();
    public DbSet<AuthAuditLog> AuthAuditLog => Set<AuthAuditLog>();
    public DbSet<AuthPasswordHistory> AuthPasswordHistory => Set<AuthPasswordHistory>();
    public DbSet<AuthPasswordResetToken> AuthPasswordResetTokens => Set<AuthPasswordResetToken>();

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
            e.Property(x => x.SeoTitle).HasMaxLength(400);
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

        b.Entity<PageMetaTag>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.PageId, x.Name }).IsUnique();
            e.HasOne<Page>().WithMany(p => p.MetaTags).HasForeignKey(x => x.PageId).OnDelete(DeleteBehavior.Cascade);
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

        // MindAttic.Authentication identity tables — all 8 in the isolated 'auth' schema.
        b.ApplyMindAtticAuthConfiguration();
    }
}
