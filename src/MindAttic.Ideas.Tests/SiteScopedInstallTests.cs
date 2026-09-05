using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;
using MindAttic.Ideas.Core.Sites;
using MindAttic.Ideas.Packaging;
using MindAttic.Ideas.Tests.Packaging;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// MAI-US-M4: a showroom visitor uploads a <c>.idea</c> and watches it render, with the install landing
/// in their site ONLY. <see cref="SiteScopedCatalogTests"/> pins how the catalog answers "who is asking";
/// this pins the write half — that an install owned by a site never touches, is never planned against,
/// and never resolves its dependencies from anything the site does not own or share.
/// <para>
/// The failure this guards is not cosmetic: an install that lands shared is a stranger changing what
/// production renders, and an install that collides on bytes or on the shadow computation is one site
/// silently serving another site's package.
/// </para>
/// </summary>
[TestFixture]
public class SiteScopedInstallTests
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

    private sealed record Harness(
        PackageInstallService Svc, InMemoryFactory Factory, ContentCatalog Catalog, InMemoryPackageBlobStore Blobs);

    private static Harness NewService(IPackageExtractor? extractor = null)
    {
        var factory = new InMemoryFactory("siteinst_" + Guid.NewGuid().ToString("N"));
        var catalog = new ContentCatalog(new NullResolver());
        var discovery = new DiscoveryService(factory, Array.Empty<ICmsContentSource>(), catalog);
        var blobs = new InMemoryPackageBlobStore();
        var svc = new PackageInstallService(
            factory, discovery, blobs, extractor ?? new NullPackageExtractor(), new NullRenderAlertSink());
        return new(svc, factory, catalog, blobs);
    }

    /// <summary>Adds a site and returns its generated id.</summary>
    private static async Task<int> AddSiteAsync(InMemoryFactory factory, string key, bool isDefault = false)
    {
        await using var db = factory.CreateDbContext();
        var site = new Site { Key = key, Name = key, IsDefault = isDefault, IsSandbox = !isDefault };
        db.Sites.Add(site);
        await db.SaveChangesAsync();
        return site.Id;
    }

    // ---- ownership -------------------------------------------------------------------------

    [Test]
    public async Task AnInstallOwnedByASiteStampsBothRowsAndMountsUnderItsOwnPath()
    {
        var h = NewService();
        var sandbox = await AddSiteAsync(h.Factory, "showroom");

        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false, siteId: sandbox);

        await using var db = h.Factory.CreateDbContext();
        var pkg = await db.InstalledPackages.SingleAsync();
        var def = await db.ContentDefinitions.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(pkg.SiteId, Is.EqualTo(sandbox), "the registry row records the owner");
            Assert.That(def.SiteId, Is.EqualTo(sandbox), "and so does the catalog row it mirrors");
            // A SIBLING of the route MAI-LAW-4 locks, never a change to it.
            Assert.That(def.AssetMount, Is.EqualTo($"/_ideas/sites/{sandbox}/Plugin/ui.tooltip/1"));
            Assert.That(pkg.BlobPath, Is.EqualTo($"sites/{sandbox}/Plugin/ui.tooltip/1.idea"));
        });
    }

    [Test]
    public async Task ASharedInstallIsUnchangedByAnyOfThis()
    {
        var h = NewService();
        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false);

        await using var db = h.Factory.CreateDbContext();
        var pkg = await db.InstalledPackages.SingleAsync();
        var def = await db.ContentDefinitions.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(pkg.SiteId, Is.Null, "null means shared — what every caller meant before A36");
            Assert.That(def.SiteId, Is.Null);
            Assert.That(def.AssetMount, Is.EqualTo("/_ideas/Plugin/ui.tooltip/1"), "the locked route, untouched");
            Assert.That(pkg.BlobPath, Is.EqualTo("Plugin/ui.tooltip/1.idea"));
        });
    }

    // ---- isolation -------------------------------------------------------------------------

    [Test]
    public async Task TwoSitesCanHoldTheSameIdentityWithoutCollidingOnBytesOrRows()
    {
        var h = NewService();
        var a = await AddSiteAsync(h.Factory, "showroom-a");
        var b = await AddSiteAsync(h.Factory, "showroom-b");

        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false, siteId: a);
        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false, siteId: b);

        await using var db = h.Factory.CreateDbContext();
        var pkgs = await db.InstalledPackages.OrderBy(p => p.SiteId).ToListAsync();
        var defs = await db.ContentDefinitions.OrderBy(c => c.SiteId).ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(pkgs.Select(p => p.SiteId), Is.EqualTo(new int?[] { a, b }),
                "the same identity in two sites is two rows, not an upsert of one");
            Assert.That(defs.Select(c => c.SiteId), Is.EqualTo(new int?[] { a, b }));
            Assert.That(h.Blobs.Saved.Keys, Is.EquivalentTo(new[]
            {
                $"sites/{a}/Plugin/ui.tooltip/1.idea", $"sites/{b}/Plugin/ui.tooltip/1.idea",
            }), "and two sets of bytes, so one site's upload can never be served for the other's");
        });
    }

    [Test]
    public async Task ASandboxInstallLeavesTheSharedCopyAloneAndBothStayLive()
    {
        var h = NewService();
        var sandbox = await AddSiteAsync(h.Factory, "showroom");

        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false);
        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false, siteId: sandbox);

        await using var db = h.Factory.CreateDbContext();
        var shared = await db.ContentDefinitions.SingleAsync(c => c.SiteId == null);
        var own = await db.ContentDefinitions.SingleAsync(c => c.SiteId == sandbox);

        Assert.Multiple(() =>
        {
            // Shadowing is computed PER SITE. Grouped together, one of these would be shadowed — either the
            // visitor's upload never renders, or it takes over the shared citizen for the whole deployment.
            Assert.That(shared.IsShadowed, Is.False, "the shared citizen is still the live one for every other site");
            Assert.That(own.IsShadowed, Is.False, "and the sandbox's own copy is live inside the sandbox");
            Assert.That(shared.AssetMount, Is.EqualTo("/_ideas/Plugin/ui.tooltip/1"), "shared assets never move");
        });

        // The live catalog agrees: each site resolves to its own row, and a caller with no site gets shared.
        Assert.Multiple(() =>
        {
            Assert.That(h.Catalog.FindLatest(ContentKind.Plugin, "ui.tooltip", sandbox)?.SiteId, Is.EqualTo(sandbox));
            Assert.That(h.Catalog.FindLatest(ContentKind.Plugin, "ui.tooltip", siteId: null)?.SiteId, Is.Null);
        });
    }

    [Test]
    public async Task DisablingInsideASandboxDoesNotReachTheSharedCopy()
    {
        var h = NewService();
        var sandbox = await AddSiteAsync(h.Factory, "showroom");
        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false);
        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false, siteId: sandbox);

        await h.Svc.DisableAsync("Plugin", "ui.tooltip", 1, siteId: sandbox);

        await using var db = h.Factory.CreateDbContext();
        Assert.Multiple(() =>
        {
            Assert.That(db.ContentDefinitions.Single(c => c.SiteId == sandbox).Enabled, Is.False);
            Assert.That(db.ContentDefinitions.Single(c => c.SiteId == null).Enabled, Is.True,
                "a visitor disabling their own copy must not disable it for the real site");
            Assert.That(db.InstalledPackages.Single(p => p.SiteId == null).Enabled, Is.True);
        });
    }

    // ---- planning --------------------------------------------------------------------------

    [Test]
    public async Task ASiteInstallIsPlannedAgainstItsOwnVersionsOnly()
    {
        var h = NewService();
        var sandbox = await AddSiteAsync(h.Factory, "showroom");

        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 2, "Plugin"), allowOverride: false);

        // V1 into the sandbox is a first install there, NOT a downgrade of the shared V2 the visitor
        // never installed and cannot see the registry of.
        var plan = await h.Svc.InstallAsync(
            IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false, siteId: sandbox);

        Assert.That(plan.Action, Is.EqualTo(InstallAction.Install));
    }

    [Test]
    public async Task ASiteInstallNeedsNoOverrideToBeatACompiledCitizen_ButASharedOneStillDoes()
    {
        var h = NewService();
        var sandbox = await AddSiteAsync(h.Factory, "showroom");

        // A compiled citizen of the same key — always shared, since only an install can make a row site-owned.
        await using (var db = h.Factory.CreateDbContext())
        {
            db.ContentDefinitions.Add(new CmsContentDefinition
            {
                Kind = ContentKind.Plugin, Key = "ui.tooltip", Version = 1, Origin = ContentOrigin.Compiled,
                DisplayName = "Tooltip", Category = "Plugin", Priority = 100, IsActive = true, Enabled = true,
            });
            await db.SaveChangesAsync();
        }

        // Shared: the override prompt still guards the deployment-wide replacement it was written for.
        Assert.That(
            async () => await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false),
            Throws.TypeOf<InstallException>());

        // Site-owned: it can only ever win inside its own site, so there is nothing to confirm.
        var plan = await h.Svc.InstallAsync(
            IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false, siteId: sandbox);
        Assert.That(plan.Action, Is.EqualTo(InstallAction.Install));
    }

    // ---- dependencies ----------------------------------------------------------------------

    [Test]
    public async Task ARequiredDependencyResolvesFromSharedPlusOwn_NeverFromAnotherSite()
    {
        var h = NewService();
        var mine = await AddSiteAsync(h.Factory, "showroom-a");
        var theirs = await AddSiteAsync(h.Factory, "showroom-b");

        // The dependency exists, but only inside SOMEBODY ELSE'S site.
        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.base", 1, "Plugin"), allowOverride: false, siteId: theirs);

        Assert.That(
            async () => await h.Svc.InstallAsync(
                RequiringPackage("ui.needy", "Plugin.ui.base"), allowOverride: false, siteId: mine),
            Throws.TypeOf<InstallException>().With.Message.Contains("REQUIRES_UNMET"),
            "another site's citizen is not a dependency this site can satisfy");

        // Shared, it is.
        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.base", 1, "Plugin"), allowOverride: false);
        var plan = await h.Svc.InstallAsync(
            RequiringPackage("ui.needy", "Plugin.ui.base"), allowOverride: false, siteId: mine);
        Assert.That(plan.Action, Is.EqualTo(InstallAction.Install));
    }

    // ---- page seed -------------------------------------------------------------------------

    [Test]
    public async Task ASeededPageLandsInTheOwningSite_WhateverItsManifestAsksFor()
    {
        var h = NewService();
        var real = await AddSiteAsync(h.Factory, "default", isDefault: true);
        var sandbox = await AddSiteAsync(h.Factory, "showroom");

        // The visitor's own file names the REAL site. In the sandbox the manifest is untrusted input, so
        // honouring siteKey would let an upload plant a page on production — the exact leak M4 must close.
        await h.Svc.InstallAsync(SeedingPackage("demo.page", slug: "hello", siteKey: "default"),
            allowOverride: false, siteId: sandbox);

        await using var db = h.Factory.CreateDbContext();
        var page = await db.Pages.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(page.SiteId, Is.EqualTo(sandbox));
            Assert.That(page.SiteId, Is.Not.EqualTo(real));
            Assert.That(page.Slug, Is.EqualTo("hello"));
        });
    }

    [Test]
    public async Task ASharedInstallStillHonoursTheSiteKeyItsManifestNames()
    {
        var h = NewService();
        await AddSiteAsync(h.Factory, "default", isDefault: true);
        var other = await AddSiteAsync(h.Factory, "microsite");

        await h.Svc.InstallAsync(SeedingPackage("demo.page", slug: "hello", siteKey: "microsite"), allowOverride: false);

        await using var db = h.Factory.CreateDbContext();
        Assert.That((await db.Pages.SingleAsync()).SiteId, Is.EqualTo(other),
            "an operator installing shared is trusted, and this behaviour predates A36");
    }

    // ---- extraction / ALC keying -----------------------------------------------------------

    [Test]
    public async Task TwoSitesExtractToSeparateRoots_SoTheirAssembliesCanNeverBeSwapped()
    {
        var root = Path.Combine(Path.GetTempPath(), "ma-siteinst-" + Guid.NewGuid().ToString("N"));
        try
        {
            var extractor = new PackageExtractor(root);
            var h = NewService(extractor);
            var a = await AddSiteAsync(h.Factory, "showroom-a");
            var b = await AddSiteAsync(h.Factory, "showroom-b");

            await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.tooltip", 1, "Plugin"), allowOverride: false, siteId: a);

            Assert.Multiple(() =>
            {
                Assert.That(extractor.IsExtracted("Plugin", "ui.tooltip", 1, "Demo", a), Is.True);
                Assert.That(extractor.IsExtracted("Plugin", "ui.tooltip", 1, "Demo", b), Is.False,
                    "the other site holds nothing at this identity");
                Assert.That(extractor.IsExtracted("Plugin", "ui.tooltip", 1, "Demo"), Is.False,
                    "and neither does the shared root");
                // AlcAwareTypeResolver keys its load contexts by this path, so distinct paths ARE distinct ALCs.
                Assert.That(extractor.EntryDllPath("Plugin", "ui.tooltip", 1, "Demo", a),
                    Is.Not.EqualTo(extractor.EntryDllPath("Plugin", "ui.tooltip", 1, "Demo", b)));
            });

            // Assets resolve per site through the sibling mount, and never across it.
            Assert.Multiple(() =>
            {
                Assert.That(extractor.ResolveAsset("Plugin", "ui.tooltip", 1, "css/x.css", a), Is.Not.Null);
                Assert.That(extractor.ResolveAsset("Plugin", "ui.tooltip", 1, "css/x.css", b), Is.Null);
                Assert.That(extractor.ResolveAsset("Plugin", "ui.tooltip", 1, "css/x.css"), Is.Null);
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    // ---- who owns an upload ----------------------------------------------------------------

    [Test]
    public void OnlyASandboxOwnsItsUploads_AndTheDefaultSiteNeverDoes()
    {
        var real = new Site { Id = 1, Key = "default", IsDefault = true };
        var sandbox = new Site { Id = 2, Key = "showroom", IsSandbox = true };
        var plain = new Site { Id = 3, Key = "microsite" };
        // The state SandboxService.Gate also refuses: someone hand-edited the row in SQL.
        var flaggedDefault = new Site { Id = 4, Key = "default", IsDefault = true, IsSandbox = true };

        Assert.Multiple(() =>
        {
            Assert.That(InstallScope.OwnerFor(sandbox), Is.EqualTo(2), "a showroom upload is the visitor's own");
            Assert.That(InstallScope.OwnerFor(real), Is.Null, "the real site's operator installs for everyone");
            Assert.That(InstallScope.OwnerFor(plain), Is.Null, "so does any ordinary site");
            Assert.That(InstallScope.OwnerFor(null), Is.Null, "and an unresolved site is shared, as it always was");
            Assert.That(InstallScope.OwnerFor(flaggedDefault), Is.Null,
                "the default site is excluded first and independently of the sandbox flag");
        });
    }

    [Test]
    public async Task ThePackageListShowsSharedPlusOwn_AndNeverAnotherSitesInventory()
    {
        var h = NewService();
        var mine = await AddSiteAsync(h.Factory, "showroom-a");
        var theirs = await AddSiteAsync(h.Factory, "showroom-b");

        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.shared", 1, "Plugin"), allowOverride: false);
        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.mine", 1, "Plugin"), allowOverride: false, siteId: mine);
        await h.Svc.InstallAsync(IdeaTestArchive.CodePackage("ui.theirs", 1, "Plugin"), allowOverride: false, siteId: theirs);

        var registry = new PackageRegistryService(h.Factory);
        // Awaited BEFORE the assertion block: an async lambda inside Assert.Multiple is an async void, and
        // a failure raised after it returns is not attributed to this test at all.
        var visible = (await registry.ListAsync(mine)).Select(p => p.Key).ToList();
        var shared = (await registry.ListAsync(siteId: null)).Select(p => p.Key).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(visible, Is.EquivalentTo(new[] { "ui.shared", "ui.mine" }),
                "a visitor sees what their site renders from — never a stranger's uploads");
            Assert.That(shared, Is.EquivalentTo(new[] { "ui.shared" }),
                "and the shared registry is exactly what it was before sites could own packages");
        });
    }

    // ---- fixtures --------------------------------------------------------------------------

    /// <summary>A code package declaring one blocking <c>requires[]</c> entry.</summary>
    private static MemoryStream RequiringPackage(string key, string requires) =>
        IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Plugin", Kind = "code", Key = key, Version = 1,
                DisplayName = key, Sdk = 1, EntryType = $"MindAttic.Ideas.Plugin.{key}.V1",
                AssemblyName = "Demo", Requires = [requires],
            }),
            ["bin/Demo.dll"] = "MZ-fake",
        });

    /// <summary>A code Page package carrying a <c>data/page.json</c> seed aimed at a named site.</summary>
    private static MemoryStream SeedingPackage(string key, string slug, string siteKey) =>
        IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Page", Kind = "code", Key = key, Version = 1,
                DisplayName = key, Sdk = 1, EntryType = $"MindAttic.Ideas.Page.{key}.V1",
                AssemblyName = "Demo",
            }),
            ["bin/Demo.dll"] = "MZ-fake",
            ["data/page.json"] = $$"""{"slug":"{{slug}}","title":"Hello","siteKey":"{{siteKey}}"}""",
        });
}
