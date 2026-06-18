using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Tests.Packaging;

/// <summary>
/// The host install tail over EF InMemory: it validates a .idea, upserts the registry + mirrored catalog
/// rows (Origin=Package), is idempotent, retains prior versions on upgrade, soft-disables, and rejects an
/// invalid package without writing anything. No assembly is ever loaded (the ALC loader is Phase-5/B), so
/// the registered descriptor's type stays unresolved until then.
/// </summary>
[TestFixture]
public class PackageInstallServiceTests
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

    private static (PackageInstallService Svc, InMemoryFactory Factory, ContentCatalog Catalog, InMemoryPackageBlobStore Blobs) NewService()
    {
        var factory = new InMemoryFactory("pkg_" + Guid.NewGuid().ToString("N"));
        var catalog = new ContentCatalog(new NullResolver());
        var discovery = new DiscoveryService(factory, Array.Empty<ICmsContentSource>(), catalog);
        var blobs = new InMemoryPackageBlobStore();
        return (new PackageInstallService(factory, discovery, blobs, new NullPackageExtractor(), new NullRenderAlertSink()), factory, catalog, blobs);
    }

    [Test]
    public async Task Install_UpsertsRegistryAndMirroredCatalogRow()
    {
        var (svc, factory, _, blobs) = NewService();
        var plan = await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false);

        Assert.That(plan.Action, Is.EqualTo(InstallAction.Install));
        await using var db = factory.CreateDbContext();

        var pkg = await db.InstalledPackages.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(pkg.Key, Is.EqualTo("ui.tooltip"));
            Assert.That(pkg.IsActiveVersion, Is.True);
            Assert.That(pkg.Sha256, Has.Length.EqualTo(64));
            Assert.That(pkg.ManifestJson, Does.Contain("ui.tooltip"));   // verbatim manifest stored
            Assert.That(pkg.BlobPath, Is.EqualTo("Plugin/ui.tooltip/1.idea"));
            Assert.That(blobs.Saved.ContainsKey(pkg.BlobPath), Is.True, "the .idea bytes are persisted to the store");
        });

        var def = await db.ContentDefinitions.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(def.Origin, Is.EqualTo(ContentOrigin.Package));
            Assert.That(def.Priority, Is.EqualTo(50));
            Assert.That(def.Kind, Is.EqualTo(ContentKind.Plugin));
            Assert.That(def.AssetMount, Is.EqualTo("/_ideas/Plugin/ui.tooltip/1"));
        });
    }

    [Test]
    public async Task Install_CodePackage_ExtractsBinForTheResolver()
    {
        var root = Path.Combine(Path.GetTempPath(), "ma-inst-" + Guid.NewGuid().ToString("N"));
        try
        {
            var factory = new InMemoryFactory("pkg_" + Guid.NewGuid().ToString("N"));
            var catalog = new ContentCatalog(new NullResolver());
            var discovery = new DiscoveryService(factory, Array.Empty<ICmsContentSource>(), catalog);
            var extractor = new PackageExtractor(root);
            var svc = new PackageInstallService(factory, discovery, new InMemoryPackageBlobStore(), extractor, new NullRenderAlertSink());

            await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false);

            // CodePackage ships bin/Demo.dll with manifest AssemblyName "Demo".
            Assert.That(extractor.IsExtracted("Plugin", "ui.tooltip", 1, "Demo"), Is.True);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Test]
    public async Task Install_IsIdempotent_NoDuplicateRow()
    {
        var (svc, factory, _, _) = NewService();
        await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1), allowOverride: false);
        var second = await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1), allowOverride: false);

        Assert.That(second.Action, Is.EqualTo(InstallAction.NoOpAlreadyInstalled));
        await using var db = factory.CreateDbContext();
        Assert.That(await db.InstalledPackages.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task HigherVersion_RetainsPriorVersion_FlipsItInactive()
    {
        var (svc, factory, _, _) = NewService();
        await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1), allowOverride: false);
        await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 2), allowOverride: false);

        await using var db = factory.CreateDbContext();
        var rows = await db.InstalledPackages.OrderBy(p => p.Version).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(2), "the prior version is retained, never deleted");
            Assert.That(rows[0].Version, Is.EqualTo(1));
            Assert.That(rows[0].IsActiveVersion, Is.False);
            Assert.That(rows[0].Enabled, Is.True);          // retained + still enabled, just not the active version
            Assert.That(rows[1].Version, Is.EqualTo(2));
            Assert.That(rows[1].IsActiveVersion, Is.True);
        });
    }

    [Test]
    public async Task Disable_FlipsBothRowsEnabledFalse_BytesRemain_CatalogReportsDisabled()
    {
        var (svc, factory, catalog, _) = NewService();
        await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false);

        await svc.DisableAsync("Plugin", "ui.tooltip", 1);

        await using var db = factory.CreateDbContext();
        Assert.Multiple(() =>
        {
            Assert.That(db.InstalledPackages.Single().Enabled, Is.False);
            Assert.That(db.ContentDefinitions.Single().Enabled, Is.False);
        });
        // Reloaded catalog now reports the disabled identity as Disabled, not Missing.
        Assert.That(catalog.ResolveTag(ContentKind.Plugin, "ui.tooltip", 1).Outcome,
            Is.EqualTo(ContentResolution.Disabled));
    }

    [Test]
    public async Task InvalidPackage_ForbiddenBinAssembly_Throws_NoRowsWritten()
    {
        var (svc, factory, _, blobs) = NewService();
        var bad = IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Plugin", Kind = "code", Key = "evil", Version = 1,
                DisplayName = "Evil", Sdk = 1, EntryType = "MindAttic.Ideas.Plugin.Evil.V1", AssemblyName = "Evil",
            }),
            ["bin/Microsoft.AspNetCore.Components.dll"] = "stowaway",   // host assembly must not ship
        });

        Assert.ThrowsAsync<InstallException>(async () => await svc.InstallAsync(bad, allowOverride: false));

        await using var db = factory.CreateDbContext();
        Assert.Multiple(() =>
        {
            Assert.That(db.InstalledPackages.Count(), Is.EqualTo(0));
            Assert.That(db.ContentDefinitions.Count(), Is.EqualTo(0));
            Assert.That(blobs.Saved, Is.Empty, "an invalid package persists no bytes (validation precedes save)");
        });
    }

    [Test]
    public async Task AfterInstall_CatalogRegistersPackageDescriptor_TypeUnresolvedUntilLoader()
    {
        var (svc, _, catalog, _) = NewService();
        await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false);

        Assert.That(catalog.All.Any(d => d.Origin == ContentOrigin.Package && d.Key == "ui.tooltip"), Is.True);
        // No ALC load yet (Phase-5/B): the descriptor is registered but its CLR type does not resolve.
        Assert.That(catalog.ResolveTag(ContentKind.Plugin, "ui.tooltip", 1).Outcome,
            Is.EqualTo(ContentResolution.Missing));
    }

    [Test]
    public async Task DisableAsync_NotFoundPackageAndDef_IsNoOp_NothingModified()
    {
        // Regression: DisableAsync called SaveChangesAsync and ReloadCatalogAsync unconditionally — even
        // when both the InstalledPackage row and the CmsContentDefinition row were not found. This caused
        // a spurious catalog reload (and a DB round-trip) for every disable call against a non-existent
        // key. The guard `if (pkg is null && def is null) return;` makes the no-op truly zero-cost.
        var (svc, factory, catalog, _) = NewService();
        // Pre-fill catalog with a real entry so we can verify it stays untouched.
        await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false);
        var snapshotBefore = catalog.All.ToList();

        // Disable a key that doesn't exist — must complete without throwing and without modifying DB.
        Assert.DoesNotThrowAsync(() => svc.DisableAsync("Plugin", "nonexistent", 99));

        // Catalog still has the original entry; no stray rows were written.
        Assert.That(catalog.All.Count, Is.EqualTo(snapshotBefore.Count),
            "catalog must be unmodified after a no-op DisableAsync call");
        await using var db = factory.CreateDbContext();
        Assert.That(db.InstalledPackages.Count(), Is.EqualTo(1), "only the pre-existing package row exists");
        Assert.That(db.ContentDefinitions.Count(), Is.EqualTo(1), "only the pre-existing definition row exists");
    }

    private static MemoryStream PackageRequiring(string depRef) =>
        IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Plugin", Kind = "code", Key = "ui.carousel", Version = 1,
                DisplayName = "Carousel", Sdk = 1, EntryType = "MindAttic.Ideas.Plugin.Carousel.V1",
                AssemblyName = "Carousel", Requires = [depRef],
            }),
            ["bin/Carousel.dll"] = "MZ-fake",
        });

    [Test]
    public async Task Requires_AllPresent_InstallSucceeds()
    {
        var (svc, factory, _, _) = NewService();
        // Pre-install the dependency.
        await svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false);

        // Now install the package that requires it.
        var plan = await svc.InstallAsync(PackageRequiring("Plugin.ui.tooltip@1"), allowOverride: false);

        Assert.That(plan.Action, Is.EqualTo(InstallAction.Install));
        await using var db = factory.CreateDbContext();
        Assert.That(await db.InstalledPackages.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Requires_Missing_ThrowsInstallException_NoRowsWritten()
    {
        var (svc, factory, _, blobs) = NewService();

        Assert.ThrowsAsync<InstallException>(async () =>
            await svc.InstallAsync(PackageRequiring("Plugin.ui.tooltip@1"), allowOverride: false));

        await using var db = factory.CreateDbContext();
        Assert.Multiple(() =>
        {
            Assert.That(db.InstalledPackages.Count(), Is.EqualTo(0), "no package row written on rejected install");
            Assert.That(blobs.Saved, Is.Empty, "no bytes persisted on rejected install");
        });
    }
}
