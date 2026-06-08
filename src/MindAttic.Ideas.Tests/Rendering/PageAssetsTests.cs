using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Rendering;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Tests.Rendering;

/// <summary>
/// Unit-tests for <see cref="PageAssets.AllAssetsOf"/>: the unified delegate that harvests assets from
/// both package citizens (manifest → Extra) and compiled widgets (Activator instantiation).
/// </summary>
[TestFixture]
public class PageAssetsTests
{
    private sealed class FakeCatalog(Type? type) : IContentCatalog
    {
        public IReadOnlyCollection<ContentDescriptor> All => [];
        public ContentDescriptor? Find(ContentKind kind, string key, int version) => null;
        public ContentDescriptor? FindLatest(ContentKind kind, string key) => null;
        public Type? ResolveType(ContentDescriptor descriptor) => type;
    }

    private sealed class StubWidget : WidgetBase
    {
        public override IReadOnlyList<string> StylesheetUrls => ["tooltip.css"];
        public override IReadOnlyList<string> ScriptUrls => ["tooltip.js"];
    }

    [Test]
    public void CompiledWidget_AllAssetsOf_HarvestsViaActivator()
    {
        var desc = new ContentDescriptor
        {
            Kind = ContentKind.Widget, Key = "tooltip", Version = 1, DisplayName = "Tooltip",
            Origin = ContentOrigin.Compiled, Strategy = RenderStrategy.ClrType,
        };
        var assets = PageAssets.AllAssetsOf(desc, new FakeCatalog(typeof(StubWidget)));

        Assert.Multiple(() =>
        {
            Assert.That(assets.Css, Is.EqualTo(new[] { "tooltip.css" }));
            Assert.That(assets.Scripts, Is.EqualTo(new[] { "tooltip.js" }));
        });
    }

    [Test]
    public void CompiledWidget_UnresolvableType_ReturnsEmpty()
    {
        var desc = new ContentDescriptor
        {
            Kind = ContentKind.Widget, Key = "gone", Version = 1, DisplayName = "Gone",
            Origin = ContentOrigin.Compiled, Strategy = RenderStrategy.ClrType,
        };
        var assets = PageAssets.AllAssetsOf(desc, new FakeCatalog(null));

        Assert.Multiple(() =>
        {
            Assert.That(assets.Css, Is.Empty);
            Assert.That(assets.Scripts, Is.Empty);
        });
    }

    [Test]
    public void PackageWidget_AllAssetsOf_DelegatesToMountedManifestAssets()
    {
        const string mount = "/_ideas/Widget/tooltip/1";
        var desc = new ContentDescriptor
        {
            Kind = ContentKind.Widget, Key = "tooltip", Version = 1, DisplayName = "Tooltip",
            Origin = ContentOrigin.Package, AssetMount = mount,
            Extra = ManifestAssetPacker.PackExtra(new IdeaManifest
            {
                Category = "Widget", Kind = "code", Key = "tooltip", Version = 1,
                DisplayName = "Tooltip",
                Css = ["tooltip.css"], Scripts = ["tooltip.js"], Uses = [],
            }),
        };
        var assets = PageAssets.AllAssetsOf(desc, new FakeCatalog(null));

        Assert.Multiple(() =>
        {
            Assert.That(assets.Css, Contains.Item($"{mount}/tooltip.css"));
            Assert.That(assets.Scripts, Contains.Item($"{mount}/tooltip.js"));
        });
    }
}
