using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// The version-specific, reference-guarded delete logic (pure FindBlockingPages) and the enable/disable
/// live-apply path (SetEnabledAsync + DiscoveryService.ReloadCatalogAsync makes the catalog report Disabled).
/// </summary>
[TestFixture]
public class ContentLifecycleServiceTests
{
    private static PageRef Pub(string slug, string? body = null, string? themeKey = null, int? themeVer = null) =>
        new(slug, body, themeKey, themeVer, Enabled: true, IsPublished: true);

    // ---- pure delete-guard ----

    [Test]
    public void PinnedVersion_AlwaysBlocks_AndListsSlug()
    {
        var pages = new[] { Pub("home", "{{MindAttic.Ideas.Plugin.Tooltip.V11}}") };
        var blocking = ContentLifecycleService.FindBlockingPages(
            ContentKind.Plugin, "tooltip", 11, pages, otherEnabledVersions: new[] { 12 });
        Assert.That(blocking, Is.EquivalentTo(new[] { "home" }));   // pin blocks even though V12 also exists
    }

    [Test]
    public void FloatingReference_BlocksOnlyWhenItWouldOrphan()
    {
        var pages = new[] { Pub("home", "{{MindAttic.Ideas.Plugin.Tooltip}}") };  // floats to latest
        // V11 is the last enabled version -> deleting it orphans the float -> blocked.
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Plugin, "tooltip", 11, pages, Array.Empty<int>()),
            Is.EquivalentTo(new[] { "home" }));
        // V12 remains enabled -> the float harmlessly moves to V12 -> deleting V11 is allowed.
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Plugin, "tooltip", 11, pages, new[] { 12 }),
            Is.Empty);
    }

    [Test]
    public void DisabledOrUnpublishedPage_IsNotABlockingReference()
    {
        var pages = new[]
        {
            new PageRef("draft", "{{MindAttic.Ideas.Plugin.Tooltip.V11}}", null, null, Enabled: false, IsPublished: true),
            new PageRef("hidden", "{{MindAttic.Ideas.Plugin.Tooltip.V11}}", null, null, Enabled: true, IsPublished: false),
        };
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Plugin, "tooltip", 11, pages, Array.Empty<int>()),
            Is.Empty);
    }

    [Test]
    public void SoftDeletedPage_DoesNotBlockCitizenDeletion()
    {
        // Regression: IsDeleted pages still had Enabled=true+IsPublished=true after SoftDeleteAsync,
        // so they fell through the guard and falsely blocked citizen deletion.
        var pages = new[]
        {
            new PageRef("removed", "{{Plugin.Tooltip.V11}}", null, null, Enabled: true, IsPublished: true, IsDeleted: true),
        };
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Plugin, "tooltip", 11, pages, Array.Empty<int>()),
            Is.Empty, "soft-deleted pages must not block citizen deletion");
    }

    [Test]
    public void ActivePluginsJson_VersionedPin_BlocksDeletion()
    {
        // Regression: only floating refs in ActivePluginsJson were guarded; versioned pins like
        // "Plugin.navmenu@1" were missed, allowing deletion of a citizen still actively used.
        var pages = new[]
        {
            new PageRef("home", null, null, null, Enabled: true, IsPublished: true,
                ActivePluginsJson: """["Plugin.navmenu@1"]"""),
        };
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Plugin, "navmenu", 1, pages, Array.Empty<int>()),
            Is.EquivalentTo(new[] { "home" }), "versioned plugin pin in ActivePluginsJson must block deletion");
    }

    [Test]
    public void ActivePluginsJson_FloatingRef_BlocksOnlyWhenOrphaning()
    {
        var pages = new[]
        {
            new PageRef("p", null, null, null, Enabled: true, IsPublished: true,
                ActivePluginsJson: """["Plugin.navmenu"]"""),
        };
        // Last version → orphan → blocked.
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Plugin, "navmenu", 1, pages, Array.Empty<int>()),
            Is.EquivalentTo(new[] { "p" }));
        // Another version remains → float moves there → allowed.
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Plugin, "navmenu", 1, pages, new[] { 2 }),
            Is.Empty);
    }

    [Test]
    public void ThemePin_Blocks_AndThemeFloatFollowsOrphanRule()
    {
        var pinned = new[] { Pub("a", themeKey: "cyberspace", themeVer: 1) };
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Theme, "cyberspace", 1, pinned, new[] { 2 }),
            Is.EquivalentTo(new[] { "a" }));   // pinned theme version blocks

        var floating = new[] { Pub("b", themeKey: "cyberspace", themeVer: null) };
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Theme, "cyberspace", 1, floating, Array.Empty<int>()),
            Is.EquivalentTo(new[] { "b" }));   // last enabled version -> orphan -> blocked
        Assert.That(ContentLifecycleService.FindBlockingPages(ContentKind.Theme, "cyberspace", 1, floating, new[] { 2 }),
            Is.Empty);                          // another version remains -> allowed
    }

    // ---- enable/disable live-apply over EF InMemory ----

    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
    }

    private sealed class NullResolver : ITypeResolver
    {
        public Type? Resolve(ContentDescriptor descriptor) => null;
    }

    [Test]
    public async Task SetEnabledFalse_ReloadsCatalog_SoResolveTagReportsDisabled()
    {
        var factory = new InMemoryFactory("lc_" + Guid.NewGuid().ToString("N"));
        int id;
        await using (var db = factory.CreateDbContext())
        {
            var def = new CmsContentDefinition
            {
                Kind = ContentKind.Theme, Key = "cyberspace", Version = 1, Origin = ContentOrigin.Compiled,
                DisplayName = "Cyberspace", Enabled = true, IsActive = true, IsShadowed = false,
                DiscoveredUtc = DateTime.UtcNow,
            };
            db.ContentDefinitions.Add(def);
            await db.SaveChangesAsync();
            id = def.Id;
        }

        var catalog = new ContentCatalog(new NullResolver());
        var discovery = new DiscoveryService(factory, Array.Empty<ICmsContentSource>(), catalog);
        var svc = new ContentLifecycleService(factory, discovery);

        var result = await svc.SetEnabledAsync(id, enabled: false);

        Assert.That(result.Ok, Is.True);
        Assert.That(catalog.ResolveTag(ContentKind.Theme, "cyberspace", 1).Outcome,
            Is.EqualTo(ContentResolution.Disabled));   // disabled identity is now known-disabled, not Missing
    }
}
