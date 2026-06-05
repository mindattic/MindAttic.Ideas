using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Rendering;
using MindAttic.Ideas.Core.Services;
using MindAttic.Ideas.Packaging;
using MindAttic.Ideas.Tests.Packaging;

namespace MindAttic.Ideas.Tests.Rendering;

/// <summary>
/// The full no-schema data path end to end: a package's manifest css[]/scripts[] → verbatim
/// InstalledPackage.ManifestJson → (catalog reload) → ContentDescriptor.Extra → ManifestAssetPacker.FromExtra.
/// Plus the resilience guarantee: a corrupt stored manifest degrades only that citizen, never the reload.
/// </summary>
[TestFixture]
public class AssetDataPathTests
{
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
    public async Task Install_Then_Reload_SurfacesManifestCssScripts_OntoDescriptorExtra()
    {
        var factory = new InMemoryFactory("assets_" + Guid.NewGuid().ToString("N"));
        var catalog = new ContentCatalog(new NullResolver());
        var discovery = new DiscoveryService(factory, Array.Empty<ICmsContentSource>(), catalog);
        var svc = new PackageInstallService(factory, discovery, new InMemoryPackageBlobStore());

        var idea = IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Component", Kind = "code", Key = "ui.tooltip", Version = 1,
                DisplayName = "Tooltip", Sdk = 1, EntryType = "MindAttic.Ideas.Component.Tooltip.V1",
                AssemblyName = "Tooltip", Css = ["tip.css", "tip-theme.css"], Scripts = ["tip.js"],
            }),
            ["bin/Tooltip.dll"] = "MZ",
        });

        await svc.InstallAsync(idea, allowOverride: false);

        var desc = catalog.Find(ContentKind.Component, "ui.tooltip", 1);
        Assert.That(desc, Is.Not.Null, "the package descriptor should be in the reloaded catalog");
        var assets = ManifestAssetPacker.FromExtra(desc!.Extra);
        Assert.Multiple(() =>
        {
            Assert.That(assets.Css, Is.EqualTo(new[] { "tip.css", "tip-theme.css" }));   // order preserved
            Assert.That(assets.Scripts, Is.EqualTo(new[] { "tip.js" }));
        });
    }

    [Test]
    public async Task CorruptManifestJson_DoesNotAbortReload_LeavesThatExtraNull()
    {
        var factory = new InMemoryFactory("assets_" + Guid.NewGuid().ToString("N"));
        await using (var db = factory.CreateDbContext())
        {
            db.InstalledPackages.Add(new InstalledPackage
            {
                Category = "Component", Kind = "code", Key = "broken", Version = 1, DisplayName = "Broken",
                ManifestJson = "{ this is not valid json", Enabled = true, IsActiveVersion = true,
                InstalledUtc = DateTime.UtcNow,
            });
            db.ContentDefinitions.Add(new CmsContentDefinition
            {
                Kind = ContentKind.Component, Key = "broken", Version = 1, Origin = ContentOrigin.Package,
                DisplayName = "Broken", Category = "Component", Priority = 50, IsActive = true, Enabled = true,
                DiscoveredUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var catalog = new ContentCatalog(new NullResolver());
        var discovery = new DiscoveryService(factory, Array.Empty<ICmsContentSource>(), catalog);

        Assert.DoesNotThrowAsync(async () => await discovery.ReloadCatalogAsync());

        var desc = catalog.Find(ContentKind.Component, "broken", 1);
        Assert.That(desc, Is.Not.Null, "the citizen is still registered despite the corrupt manifest");
        Assert.That(desc!.Extra, Is.Null, "its head assets degrade to none, not a crash");
    }
}
