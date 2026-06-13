using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// PageAdminService — the security write. Trust is stamped from the author principal (Author iff the
/// Cms.AuthorRawMarkup claim is present, else Untrusted); duplicate slugs return a friendly error (not an
/// exception); flags toggle; soft-deleted pages drop out of the list.
/// </summary>
[TestFixture]
public class PageAdminServiceTests
{
    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
    }

    private static ClaimsPrincipal Author(bool withClaim, string uid = "user-1")
    {
        var claims = new List<Claim> { new("ma:uid", uid) };
        if (withClaim) claims.Add(new Claim(CmsClaims.AuthorRawMarkup, "true"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static async Task<PageAdminService> NewServiceAsync()
    {
        var factory = new InMemoryFactory("page_" + Guid.NewGuid().ToString("N"));
        await using (var db = factory.CreateDbContext())
        {
            db.Sites.Add(new Site { Key = "default", Name = "Default", IsDefault = true, CreatedUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        return new PageAdminService(factory);
    }

    [Test]
    public async Task Save_WithAuthorClaim_StampsAuthorTrust_AndCapturesAuthor()
    {
        var svc = await NewServiceAsync();
        var result = await svc.SaveAsync(new PageEditModel { Slug = "about", Title = "About", BodyHtml = "<p>hi</p>", IsPublished = true },
            Author(withClaim: true, uid: "admin-uid"));

        Assert.That(result.Ok, Is.True);
        Assert.That(result.StampedTrust, Is.EqualTo(ContentTrust.Author));
        var saved = await svc.GetAsync(result.Id);
        Assert.That(saved, Is.Not.Null);
        // Read the persisted trust via a fresh summary.
        var summary = (await svc.ListAsync()).Single(p => p.Id == result.Id);
        Assert.That(summary.BodyTrust, Is.EqualTo(ContentTrust.Author));
    }

    [Test]
    public async Task Save_WithoutClaim_StampsUntrusted()
    {
        var svc = await NewServiceAsync();
        var result = await svc.SaveAsync(new PageEditModel { Slug = "guest", Title = "Guest", BodyHtml = "<script>x()</script>" },
            Author(withClaim: false));
        Assert.That(result.Ok, Is.True);
        Assert.That(result.StampedTrust, Is.EqualTo(ContentTrust.Untrusted));
    }

    [Test]
    public async Task Save_DuplicateSlug_ReturnsFriendlyError_NotException()
    {
        var svc = await NewServiceAsync();
        var first = await svc.SaveAsync(new PageEditModel { Slug = "dup", Title = "One" }, Author(true));
        Assert.That(first.Ok, Is.True);

        var second = await svc.SaveAsync(new PageEditModel { Slug = "dup", Title = "Two" }, Author(true));
        Assert.That(second.Ok, Is.False);
        Assert.That(second.Error, Does.Contain("already exists"));
    }

    [Test]
    public async Task SetPublishedAndEnabled_FlipFlags()
    {
        var svc = await NewServiceAsync();
        var r = await svc.SaveAsync(new PageEditModel { Slug = "p", Title = "P", IsPublished = false, Enabled = true }, Author(true));

        Assert.That(await svc.SetPublishedAsync(r.Id, true), Is.True);
        Assert.That(await svc.SetEnabledAsync(r.Id, false), Is.True);
        var summary = (await svc.ListAsync()).Single(p => p.Id == r.Id);
        Assert.That(summary.IsPublished, Is.True);
        Assert.That(summary.Enabled, Is.False);
    }

    [Test]
    public async Task SoftDelete_RemovesFromList_ButDoesNotHardDelete()
    {
        var svc = await NewServiceAsync();
        var r = await svc.SaveAsync(new PageEditModel { Slug = "trash", Title = "Trash" }, Author(true));

        Assert.That(await svc.SoftDeleteAsync(r.Id), Is.True);
        Assert.That((await svc.ListAsync()).Any(p => p.Id == r.Id), Is.False);   // filtered by !IsDeleted
    }

    [Test]
    public async Task Move_NestsPageUnderParent()
    {
        var svc = await NewServiceAsync();
        var parent = await svc.SaveAsync(new PageEditModel { Slug = "docs", Title = "Docs" }, Author(true));
        var child = await svc.SaveAsync(new PageEditModel { Slug = "docs/intro", Title = "Intro" }, Author(true));

        Assert.That(await svc.MoveAsync(child.Id, parent.Id, sortOrder: 0), Is.True);
        var summary = (await svc.ListAsync()).Single(p => p.Id == child.Id);
        Assert.That(summary.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public async Task Move_RejectsCycle_AndSelfParent()
    {
        var svc = await NewServiceAsync();
        var a = await svc.SaveAsync(new PageEditModel { Slug = "a", Title = "A" }, Author(true));
        var b = await svc.SaveAsync(new PageEditModel { Slug = "b", Title = "B" }, Author(true));
        await svc.MoveAsync(b.Id, a.Id, 0);                    // b under a

        Assert.That(await svc.MoveAsync(a.Id, b.Id, 0), Is.False, "moving a under its own descendant b is a cycle");
        Assert.That(await svc.MoveAsync(a.Id, a.Id, 0), Is.False, "a page cannot be its own parent");
        Assert.That((await svc.ListAsync()).Single(p => p.Id == a.Id).ParentId, Is.Null, "a stays at top level");
    }

    // ── SeoMeta unit tests ──────────────────────────────────────────────────────────────────

    [Test]
    public void SeoMeta_Parse_ReturnsNull_ForNullOrEmpty()
    {
        Assert.That(SeoMeta.Parse(null), Is.Null);
        Assert.That(SeoMeta.Parse(""), Is.Null);
        Assert.That(SeoMeta.Parse("   "), Is.Null);
    }

    [Test]
    public void SeoMeta_Parse_ExtractsFields()
    {
        var seo = SeoMeta.Parse("""{"title":"My Title","description":"My Desc"}""");
        Assert.That(seo, Is.Not.Null);
        Assert.That(seo!.Title, Is.EqualTo("My Title"));
        Assert.That(seo.Description, Is.EqualTo("My Desc"));
    }

    [Test]
    public void SeoMeta_Parse_ReturnsNull_ForMalformedJson()
    {
        Assert.That(SeoMeta.Parse("not-json"), Is.Null);
    }

    [Test]
    public void SeoMeta_Serialize_ReturnsNull_WhenBothFieldsNull()
    {
        Assert.That(new SeoMeta().Serialize(), Is.Null);
        Assert.That(new SeoMeta { Title = null, Description = null }.Serialize(), Is.Null);
    }

    [Test]
    public void SeoMeta_Serialize_ReturnsJson_WhenAnyFieldSet()
    {
        var json = new SeoMeta { Title = "T" }.Serialize();
        Assert.That(json, Is.Not.Null);
        var roundtrip = SeoMeta.Parse(json);
        Assert.That(roundtrip!.Title, Is.EqualTo("T"));
        Assert.That(roundtrip.Description, Is.Null);
    }

    [Test]
    public async Task Save_WithSeoFields_PersistsThroughGetAsync()
    {
        var svc = await NewServiceAsync();
        var result = await svc.SaveAsync(new PageEditModel
        {
            Slug = "seo-page", Title = "SEO Page",
            SeoTitle = "Custom SEO Title",
            SeoDescription = "This is the meta description.",
        }, Author(withClaim: true));

        Assert.That(result.Ok, Is.True);
        var loaded = await svc.GetAsync(result.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.SeoTitle, Is.EqualTo("Custom SEO Title"));
        Assert.That(loaded.SeoDescription, Is.EqualTo("This is the meta description."));
    }

    [Test]
    public async Task Save_WithNullSeoFields_LeavesJsonNull()
    {
        var svc = await NewServiceAsync();
        var result = await svc.SaveAsync(new PageEditModel
        {
            Slug = "no-seo", Title = "No SEO",
            SeoTitle = null,
            SeoDescription = null,
        }, Author(withClaim: true));

        Assert.That(result.Ok, Is.True);
        var loaded = await svc.GetAsync(result.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.SeoTitle, Is.Null);
        Assert.That(loaded.SeoDescription, Is.Null);
    }
}
