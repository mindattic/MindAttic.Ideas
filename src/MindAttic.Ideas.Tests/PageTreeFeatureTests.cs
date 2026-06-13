using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Rendering;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// PageTreeFeature — IPageTree host impl used by the TableOfContents widget (and any nav citizen) to
/// list a page's published+enabled children without a compile-time host reference. Covers: ordered
/// results, disabled/deleted filtering, and the unknown-page empty-return guarantee.
/// </summary>
[TestFixture]
public class PageTreeFeatureTests
{
    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
    }

    private static async Task<(PageTreeFeature Feature, Guid ParentUid)> SeedAsync()
    {
        var factory = new InMemoryFactory("tree_" + Guid.NewGuid().ToString("N"));
        await using var db = factory.CreateDbContext();

        var site = new Site { Key = "s", Name = "S", IsDefault = true, CreatedUtc = DateTime.UtcNow };
        db.Sites.Add(site);
        await db.SaveChangesAsync();

        var parent = new CmsPage
        {
            SiteId = site.Id, Slug = "parent", Title = "Parent",
            Kind = PageKind.Data, IsPublished = true, Enabled = true,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Pages.Add(parent);
        await db.SaveChangesAsync();

        // child-b SortOrder=0 → appears first; child-a SortOrder=1 → appears second
        db.Pages.Add(new CmsPage { SiteId = site.Id, ParentId = parent.Id, Slug = "child-b", Title = "Child B", Kind = PageKind.Data, IsPublished = true, Enabled = true, SortOrder = 0, CreatedUtc = DateTime.UtcNow });
        db.Pages.Add(new CmsPage { SiteId = site.Id, ParentId = parent.Id, Slug = "child-a", Title = "Child A", Kind = PageKind.Data, IsPublished = true, Enabled = true, SortOrder = 1, CreatedUtc = DateTime.UtcNow });
        // disabled child — excluded because Enabled=false
        db.Pages.Add(new CmsPage { SiteId = site.Id, ParentId = parent.Id, Slug = "disabled", Title = "Disabled", Kind = PageKind.Data, IsPublished = true, Enabled = false, SortOrder = 2, CreatedUtc = DateTime.UtcNow });
        // soft-deleted child — excluded because IsDeleted=true
        db.Pages.Add(new CmsPage { SiteId = site.Id, ParentId = parent.Id, Slug = "deleted", Title = "Deleted", Kind = PageKind.Data, IsPublished = true, Enabled = true, IsDeleted = true, SortOrder = 3, CreatedUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        return (new PageTreeFeature(factory), parent.Uid);
    }

    [Test]
    public async Task ChildrenOfAsync_ReturnsPublishedEnabled_OrderedBySortOrder()
    {
        var (feature, parentUid) = await SeedAsync();
        var children = await feature.ChildrenOfAsync(parentUid);
        Assert.That(children.Select(c => c.Slug), Is.EqualTo(new[] { "child-b", "child-a" }));
    }

    [Test]
    public async Task ChildrenOfAsync_ExcludesDisabledAndDeletedChildren()
    {
        var (feature, parentUid) = await SeedAsync();
        var children = await feature.ChildrenOfAsync(parentUid);
        Assert.That(children.Any(c => c.Slug == "disabled"), Is.False);
        Assert.That(children.Any(c => c.Slug == "deleted"), Is.False);
    }

    [Test]
    public async Task ChildrenOfAsync_ReturnsEmpty_ForUnknownPageId()
    {
        var factory = new InMemoryFactory("tree_unknown_" + Guid.NewGuid().ToString("N"));
        var feature = new PageTreeFeature(factory);
        var children = await feature.ChildrenOfAsync(Guid.NewGuid());
        Assert.That(children, Is.Empty);
    }
}
