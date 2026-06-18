using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// Tests for <see cref="PageHistoryService"/>. <see cref="IPageHistoryService.GetHistoryAsync"/> uses
/// EF Core <c>TemporalAll()</c>, which requires a SQL Server provider and cannot be tested here with
/// InMemory. <see cref="IPageHistoryService.RestoreAsync"/> takes a pre-fetched snapshot and is DB-agnostic,
/// so it is fully covered below.
/// </summary>
[TestFixture]
public class PageHistoryServiceTests
{
    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
        public Task<CmsDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
    }

    private static IDbContextFactory<CmsDbContext> NewFactory() =>
        new InMemoryFactory("hist_" + Guid.NewGuid().ToString("N"));

    private static async Task<CmsPage> SeedPageAsync(IDbContextFactory<CmsDbContext> factory)
    {
        await using var db = factory.CreateDbContext();
        var page = new CmsPage
        {
            Slug = "about", Title = "Current Title", Kind = PageKind.Data,
            BodyHtml = "<p>current</p>", IsPublished = true, Enabled = true,
            BodyTrust = ContentTrust.Untrusted, CreatedUtc = DateTime.UtcNow,
        };
        db.Pages.Add(page);
        await db.SaveChangesAsync();
        return page;
    }

    [Test]
    public async Task RestoreAsync_CopiesSnapshotContentFields_OntoCurrentPage()
    {
        var factory = NewFactory();
        var page = await SeedPageAsync(factory);
        var snapshot = new PageHistoryEntry(
            page.Id, "about", "Old Title", false, true, PageKind.Data,
            "cyberspace", 2, "<p>old body</p>", "body{color:red}", null,
            ContentTrust.Untrusted,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1));

        var ok = await new PageHistoryService(factory).RestoreAsync(snapshot, new ClaimsPrincipal());

        await using var verify = factory.CreateDbContext();
        var restored = await verify.Pages.FindAsync(page.Id);
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(restored!.Title, Is.EqualTo("Old Title"));
            Assert.That(restored.BodyHtml, Is.EqualTo("<p>old body</p>"));
            Assert.That(restored.PageCss, Is.EqualTo("body{color:red}"));
            Assert.That(restored.ThemeKey, Is.EqualTo("cyberspace"));
            Assert.That(restored.ThemeVersion, Is.EqualTo(2));
            Assert.That(restored.IsPublished, Is.False);
        });
    }

    [Test]
    public async Task RestoreAsync_ReStampsTrust_FromRestoringUserClaims()
    {
        var factory = NewFactory();
        var page = await SeedPageAsync(factory);
        var snapshot = new PageHistoryEntry(
            page.Id, "about", "Old", true, true, PageKind.Data,
            null, null, "<script>alert(1)</script>", null, null,
            ContentTrust.Untrusted,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        var admin = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(CmsClaims.AuthorRawMarkup, "true"),
            new Claim("ma:uid", "admin-42"),
        ], "Test"));

        await new PageHistoryService(factory).RestoreAsync(snapshot, admin);

        await using var verify = factory.CreateDbContext();
        var restored = await verify.Pages.FindAsync(page.Id);
        Assert.Multiple(() =>
        {
            Assert.That(restored!.BodyTrust, Is.EqualTo(ContentTrust.Author));
            Assert.That(restored.AuthoredByUserId, Is.EqualTo("admin-42"));
        });
    }

    [Test]
    public async Task RestoreAsync_NonAdminUser_StampsUntrusted()
    {
        var factory = NewFactory();
        var page = await SeedPageAsync(factory);
        var snapshot = new PageHistoryEntry(
            page.Id, "about", "Old", true, true, PageKind.Data,
            null, null, "<p>safe</p>", null, null,
            ContentTrust.Author,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        // A user without Cms.AuthorRawMarkup
        var viewer = new ClaimsPrincipal(new ClaimsIdentity([new Claim("ma:uid", "viewer-1")], "Test"));

        await new PageHistoryService(factory).RestoreAsync(snapshot, viewer);

        await using var verify = factory.CreateDbContext();
        var restored = await verify.Pages.FindAsync(page.Id);
        Assert.That(restored!.BodyTrust, Is.EqualTo(ContentTrust.Untrusted));
    }

    [Test]
    public async Task RestoreAsync_UnknownPage_ReturnsFalse()
    {
        var snapshot = new PageHistoryEntry(
            999, "missing", "Gone", false, false, PageKind.Data,
            null, null, null, null, null,
            ContentTrust.Untrusted,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        var ok = await new PageHistoryService(NewFactory()).RestoreAsync(snapshot, new ClaimsPrincipal());

        Assert.That(ok, Is.False);
    }

    [Test]
    public async Task RestoreAsync_RestoresMissingFields_SeoTitle_ActivePlugins_IsRestricted_Kind()
    {
        // Regression: RestoreAsync omitted SeoTitle, ActivePluginsJson, IsRestricted, and Kind,
        // so reverting a snapshot left those fields at their current (post-edit) values.
        var factory = NewFactory();
        var page = await SeedPageAsync(factory);
        var snapshot = new PageHistoryEntry(
            page.Id, "about", "Old Title", true, true, PageKind.Code,
            null, null, "<p>body</p>", null, null, ContentTrust.Untrusted,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1),
            SeoTitle: "Old SEO", ActivePluginsJson: """["Plugin.nav"]""", IsRestricted: true);

        var ok = await new PageHistoryService(factory).RestoreAsync(snapshot, new ClaimsPrincipal());

        await using var verify = factory.CreateDbContext();
        var restored = await verify.Pages.FindAsync(page.Id);
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(restored!.Kind, Is.EqualTo(PageKind.Code), "Kind must be restored");
            Assert.That(restored.SeoTitle, Is.EqualTo("Old SEO"), "SeoTitle must be restored");
            Assert.That(restored.ActivePluginsJson, Is.EqualTo("""["Plugin.nav"]"""), "ActivePluginsJson must be restored");
            Assert.That(restored.IsRestricted, Is.True, "IsRestricted must be restored");
        });
    }

    [Test]
    public void GetHistoryAsync_RequiresSqlServer_ThrowsOnInMemoryDb()
    {
        // GetHistoryAsync uses EF Core TemporalAll() which only works with SQL Server temporal
        // tables. On InMemory this throws, documenting the SQL Server requirement for MAI-US-B5.
        var svc = new PageHistoryService(NewFactory());
        Assert.ThrowsAsync<InvalidOperationException>(() => svc.GetHistoryAsync(1));
    }
}
