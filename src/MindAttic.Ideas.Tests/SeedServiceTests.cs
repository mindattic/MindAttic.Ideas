using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// SeedService idempotency: re-running against a DB that already has (possibly soft-deleted) rows
/// at the seeded slugs must not throw or create duplicate rows.
/// </summary>
[TestFixture]
public class SeedServiceTests
{
    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
        public Task<CmsDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
    }

    [Test]
    public async Task SeedAsync_SoftDeletedSeedPage_SkipsReinsert_NoDuplicate()
    {
        // Regression: AnyAsync page-slug guards used the default EF query filter, so a soft-deleted
        // page at e.g. "personas" was invisible → AnyAsync returned false → SeedAsync tried to INSERT
        // a second row → DbUpdateException on the unique (SiteId, Slug) index on real databases.
        // InMemory doesn't enforce the constraint, so the bug is verified by checking that exactly
        // one "personas" row exists after SeedAsync: with IgnoreQueryFilters the AnyAsync finds the
        // soft-deleted row and skips the INSERT; without the fix a second row would be inserted.
        var factory = new InMemoryFactory("seed_soft_" + Guid.NewGuid().ToString("N"));

        int siteId;
        await using (var setup = factory.CreateDbContext())
        {
            var site = new Site
            {
                Key = "default", Name = "MindAttic", HostBindings = "",
                DefaultThemeKey = "cyberspace", DefaultThemeVersion = 1,
                IsDefault = true, CreatedUtc = DateTime.UtcNow,
            };
            setup.Sites.Add(site);
            await setup.SaveChangesAsync();
            siteId = site.Id;

            // Pre-seed a soft-deleted page at "personas" — simulates a slug the admin deleted.
            setup.Pages.Add(new CmsPage
            {
                SiteId = siteId, Slug = "personas", Title = "Old Personas",
                Kind = PageKind.Data, BodyHtml = "<p>old</p>",
                BodyTrust = ContentTrust.Untrusted,
                IsPublished = false, Enabled = true, IsDeleted = true,
                CreatedUtc = DateTime.UtcNow,
            });
            await setup.SaveChangesAsync();
        }

        Assert.DoesNotThrowAsync(async () => await new SeedService(factory).SeedAsync(),
            "SeedAsync must not throw when a soft-deleted page occupies a seeded slug");

        // Verify: exactly one row at "personas", not two.
        await using var verify = factory.CreateDbContext();
        var count = await verify.Pages.IgnoreQueryFilters().CountAsync(p => p.SiteId == siteId && p.Slug == "personas");
        Assert.That(count, Is.EqualTo(1), "soft-deleted 'personas' slug must not receive a second row from SeedAsync");
    }

    [Test]
    public async Task SeedAsync_SoftDeletedPageWithLegacyTag_IsMigrated()
    {
        // Regression: the legacy-tag-upgrade query (BodyHtml.Contains "<MindAttic.Ideas.") used the
        // default EF filter (no IgnoreQueryFilters), so soft-deleted pages with old include tags were
        // silently skipped. After undelete such pages still used the retired XML tag format, causing
        // render failures. IgnoreQueryFilters ensures every page gets migrated regardless of IsDeleted.
        var factory = new InMemoryFactory("seed_legacytag_" + Guid.NewGuid().ToString("N"));
        int siteId;
        await using (var setup = factory.CreateDbContext())
        {
            var site = new Site
            {
                Key = "default", Name = "Test", HostBindings = "",
                DefaultThemeKey = "cyberspace", DefaultThemeVersion = 1,
                IsDefault = true, CreatedUtc = DateTime.UtcNow,
            };
            setup.Sites.Add(site);
            await setup.SaveChangesAsync();
            siteId = site.Id;

            // Insert a soft-deleted page with a legacy XML include tag.
            setup.Pages.Add(new CmsPage
            {
                SiteId = siteId, Slug = "legacy-deleted", Title = "Legacy Deleted",
                Kind = PageKind.Data,
                BodyHtml = "<p><MindAttic.Ideas.Component.Textbox /></p>",
                BodyTrust = ContentTrust.Untrusted,
                IsPublished = false, Enabled = false,
                IsDeleted = true, DeletedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
            });
            await setup.SaveChangesAsync();
        }

        await new SeedService(factory).SeedAsync();

        await using var verify = factory.CreateDbContext();
        var page = await verify.Pages.IgnoreQueryFilters()
            .SingleAsync(p => p.SiteId == siteId && p.Slug == "legacy-deleted");
        Assert.That(page.BodyHtml, Does.Not.Contain("<MindAttic.Ideas."),
            "legacy XML tag must be migrated to {{ }} token grammar even for soft-deleted pages");
        Assert.That(page.BodyHtml, Does.Contain("{{"),
            "migrated body must use the {{ }} token grammar");
    }

    [Test]
    public async Task SeedAsync_FreshDb_CreatesExpectedSeedPages()
    {
        // Smoke test: a fresh DB gets the expected seeded pages from SeedAsync.
        var factory = new InMemoryFactory("seed_fresh_" + Guid.NewGuid().ToString("N"));
        await new SeedService(factory).SeedAsync();

        await using var db = factory.CreateDbContext();
        var slugs = await db.Pages.Select(p => p.Slug).ToListAsync();
        Assert.That(slugs, Does.Contain("frontpage"), "frontpage must be seeded on a fresh DB");
    }
}
