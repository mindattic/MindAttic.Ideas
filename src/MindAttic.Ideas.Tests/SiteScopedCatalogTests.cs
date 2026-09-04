using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Discovery;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// The showroom is a SANDBOX site whose visitors may upload their own <c>.idea</c> packages — that is
/// the headline claim being demonstrated, so it has to be real. Which means the catalog must answer
/// "who is asking": a visitor's install has to be invisible to every other site, and a site that
/// installed nothing must see exactly the shared catalog it saw before sites could own citizens.
/// These are the isolation rules; getting one wrong lets a stranger change what production renders.
/// </summary>
[TestFixture]
public class SiteScopedCatalogTests
{
    /// <summary>Resolves any descriptor to a stub type, so resolution outcomes are about the catalog only.</summary>
    private sealed class StubResolver : ITypeResolver
    {
        public Type? Resolve(ContentDescriptor descriptor) => typeof(object);
    }

    /// <summary>Never resolves — used to prove a found descriptor still reports Missing when its type is gone.</summary>
    private sealed class NullResolver : ITypeResolver
    {
        public Type? Resolve(ContentDescriptor descriptor) => null;
    }

    private static ContentDescriptor D(string key, int version, int? siteId, string display = "") => new()
    {
        Kind = ContentKind.Component, Key = key, Version = version,
        DisplayName = display.Length > 0 ? display : $"{key} v{version}",
        Origin = ContentOrigin.Package, SiteId = siteId,
    };

    private static ContentCatalog Catalog(params ContentDescriptor[] descriptors)
    {
        var c = new ContentCatalog(new StubResolver());
        c.LoadSnapshot(descriptors, []);
        return c;
    }

    private const int Sandbox = 7;
    private const int Other = 9;

    // ---- isolation -------------------------------------------------------------------------

    [Test]
    public void ASandboxInstallIsInvisibleToOtherSites()
    {
        var c = Catalog(D("visitorthing", 1, Sandbox));

        Assert.Multiple(() =>
        {
            Assert.That(c.FindLatest(ContentKind.Component, "visitorthing", Sandbox), Is.Not.Null,
                "the site that installed it must see it");
            Assert.That(c.FindLatest(ContentKind.Component, "visitorthing", Other), Is.Null,
                "another site must not");
            Assert.That(c.FindLatest(ContentKind.Component, "visitorthing", siteId: null), Is.Null,
                "and it must not be visible as a shared citizen");
        });
    }

    [Test]
    public void TheSiteLessLookupsMeanSharedOnly()
    {
        // Every caller that has no site in hand is a back door: if these matched a sandbox row, a
        // stranger's upload could surface on the real site through one of them.
        var c = Catalog(D("visitorthing", 1, Sandbox));

        Assert.Multiple(() =>
        {
            Assert.That(c.Find(ContentKind.Component, "visitorthing", 1), Is.Null);
            Assert.That(c.FindLatest(ContentKind.Component, "visitorthing"), Is.Null);
            Assert.That(c.ResolveTag(ContentKind.Component, "visitorthing", null).Outcome,
                Is.EqualTo(ContentResolution.Missing));
        });
    }

    [Test]
    public void ASiteThatInstalledNothingSeesExactlyTheSharedCatalog()
    {
        // The pre-A36 world, which every existing deployment lives in.
        var c = Catalog(D("hero", 1, null), D("card", 2, null));

        Assert.Multiple(() =>
        {
            Assert.That(c.FindLatest(ContentKind.Component, "hero", Other)?.Version, Is.EqualTo(1));
            Assert.That(c.FindLatest(ContentKind.Component, "card", Other)?.Version, Is.EqualTo(2));
            Assert.That(c.ResolveTag(ContentKind.Component, "hero", null, Other).Outcome,
                Is.EqualTo(ContentResolution.Resolved));
        });
    }

    // ---- precedence ------------------------------------------------------------------------

    [Test]
    public void ASitesOwnCopyWinsOverTheSharedOneOfTheSameIdentity()
    {
        // The whole point of the sandbox: upload your own "hero" and watch YOUR page change.
        var c = Catalog(D("hero", 1, null, "shared hero"), D("hero", 1, Sandbox, "visitor hero"));

        Assert.Multiple(() =>
        {
            Assert.That(c.Find(ContentKind.Component, "hero", 1, Sandbox)?.DisplayName, Is.EqualTo("visitor hero"));
            Assert.That(c.Find(ContentKind.Component, "hero", 1, Other)?.DisplayName, Is.EqualTo("shared hero"),
                "the override must not escape the site that made it");
            Assert.That(c.Find(ContentKind.Component, "hero", 1)?.DisplayName, Is.EqualTo("shared hero"));
        });
    }

    [Test]
    public void AHigherSharedVersionStillWinsOnVersion_ThenOwnershipBreaksTheTie()
    {
        var c = Catalog(D("hero", 1, Sandbox, "visitor v1"), D("hero", 2, null, "shared v2"));

        Assert.That(c.FindLatest(ContentKind.Component, "hero", Sandbox)?.DisplayName, Is.EqualTo("shared v2"),
            "version is the primary ordering; ownership only breaks a tie within one version");
    }

    [Test]
    public void APinnedVersionIsNeverPromotedToAnotherVersion()
    {
        var c = Catalog(D("hero", 2, Sandbox));

        Assert.That(c.Find(ContentKind.Component, "hero", 1, Sandbox), Is.Null,
            "a pinned reference resolves exactly or not at all");
    }

    // ---- resolution outcomes ---------------------------------------------------------------

    [Test]
    public void ADisabledCitizenStillReportsDisabledForTheSiteThatOwnsIt()
    {
        // A page must be able to tell "turned off" from "never existed" (MAI-LAW-7).
        var c = new ContentCatalog(new StubResolver());
        c.LoadSnapshot([], [(ContentKind.Component, "gone", 1)]);

        Assert.Multiple(() =>
        {
            Assert.That(c.ResolveTag(ContentKind.Component, "gone", 1, Sandbox).Outcome,
                Is.EqualTo(ContentResolution.Disabled));
            Assert.That(c.ResolveTag(ContentKind.Component, "never", 1, Sandbox).Outcome,
                Is.EqualTo(ContentResolution.Missing));
        });
    }

    [Test]
    public void AFoundDescriptorWhoseTypeWillNotLoadReportsMissing_NotResolved()
    {
        var c = new ContentCatalog(new NullResolver());
        c.LoadSnapshot([D("hero", 1, Sandbox)], []);

        var r = c.ResolveTag(ContentKind.Component, "hero", 1, Sandbox);
        Assert.Multiple(() =>
        {
            Assert.That(r.Outcome, Is.EqualTo(ContentResolution.Missing));
            Assert.That(r.Descriptor, Is.Not.Null, "the descriptor is still reported so the alert can name it");
        });
    }

    [Test]
    public void TheDefaultInterfaceOverloadsIgnoreTheSite_SoOtherCatalogsKeepWorking()
    {
        // MAI-LAW-2 freezes the Abstractions surface, so the site-aware lookups were APPENDED as
        // default methods. A catalog that does not override them must behave exactly as before.
        IContentCatalog legacy = new LegacyCatalog();

        Assert.Multiple(() =>
        {
            Assert.That(legacy.FindLatest(ContentKind.Component, "hero", Sandbox)?.Version, Is.EqualTo(1));
            Assert.That(legacy.ResolveTag(ContentKind.Component, "hero", null, Sandbox).Outcome,
                Is.EqualTo(ContentResolution.Resolved));
        });
    }

    /// <summary>A catalog written before site-scoping existed: implements only the frozen members.</summary>
    private sealed class LegacyCatalog : IContentCatalog
    {
        private readonly ContentDescriptor _only = D("hero", 1, null);
        public IReadOnlyCollection<ContentDescriptor> All => [_only];
        public ContentDescriptor? Find(ContentKind kind, string key, int version) =>
            key == _only.Key && version == _only.Version ? _only : null;
        public ContentDescriptor? FindLatest(ContentKind kind, string key) => key == _only.Key ? _only : null;
        public Type? ResolveType(ContentDescriptor descriptor) => typeof(object);
    }
}
