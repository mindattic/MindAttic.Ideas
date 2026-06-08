using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Rendering;
using MindAttic.Ideas.Core.Services;
using MindAttic.Ideas.Packaging;
using MindAttic.Ideas.Tests.Packaging;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// Proves the automatable mechanics of MAI-US-A6: a seeded Data page body (HomeBodyHtml in
/// SeedService) uses the correct token grammar, and the install → catalog → IncludeExpander
/// pipeline resolves those tokens to Component frames (not MissingContent placeholders).
///
/// The live render through the running host (Cyberspace theme + Blazor circuit) is not
/// automatable and remains the "attended" portion of A6.
/// </summary>
[TestFixture]
public class SeededPageRenderTests
{
    // Tokens lifted directly from SeedService.HomeBodyHtml — the seeded Cyberspace home page.
    // If these change in SeedService the constants below must be updated to match.
    private const string SeedTooltipToken = "{{ MindAttic.Ideas.Widget.Tooltip }}";
    private const string SeedTextboxToken = "{{ MindAttic.Ideas.Widget.Textbox placeholder=\"Type here…\" }}";

    [TestCase(SeedTooltipToken, "tooltip")]
    [TestCase(SeedTextboxToken, "textbox")]
    public void SeedBodyTokens_ParseToWidgetKind_FloatingVersion(string seedToken, string expectedKey)
    {
        var refs = IncludeReferenceParser.Parse(seedToken);

        Assert.That(refs, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(refs[0].Kind,    Is.EqualTo(ContentKind.Widget));
            Assert.That(refs[0].Key,     Is.EqualTo(expectedKey));
            Assert.That(refs[0].Version, Is.Null, "seed tokens float to latest — no version pin");
        });
    }

    [Test]
    public async Task SeedBody_InstalledTooltipWidget_ExpandsToResolvedFrame()
    {
        // Proves the full pipeline for the A6 scenario: install a widget with key "tooltip"
        // (matching the seed body's Tooltip token), then verify IncludeExpander resolves it.
        var (svc, catalog) = BuildPipeline();

        var archive = IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Widget", Kind = "code",
                Key = "tooltip", Version = 1, DisplayName = "Tooltip", Sdk = 1,
                EntryType    = typeof(WidgetBase).FullName!,
                AssemblyName = "Demo",    // non-host fake; DefaultTypeResolver finds WidgetBase via scan
            }),
            ["bin/Demo.dll"] = "MZ-fake",
        });
        await svc.InstallAsync(archive, allowOverride: false);

        // The floating token "{{ MindAttic.Ideas.Widget.Tooltip }}" (no .VN = float to latest).
        var builder = new RenderTreeBuilder();
        var seq = 0;
        IncludeExpander.Expand(builder, ref seq, SeedTooltipToken, catalog, new PassGate(), ContentTrust.Author);

        var frames = builder.GetFrames();
        bool hasResolved = false, hasMissing = false;
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames.Array[i].FrameType != RenderTreeFrameType.Component) continue;
            if (frames.Array[i].ComponentType == typeof(MissingContent)) hasMissing = true;
            else hasResolved = true;
        }

        Assert.Multiple(() =>
        {
            Assert.That(hasMissing,  Is.False, "seed Tooltip token must not degrade to MissingContent");
            Assert.That(hasResolved, Is.True,  "seed Tooltip token must resolve to a Component frame");
        });
    }

    // ---- shared pipeline infra ----

    private sealed class InMemoryFactory(string db) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(db).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
        public Task<CmsDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class PassGate : IRawContentGate
    {
        public MarkupString Emit(string? html, ContentTrust trust) => new(html ?? "");
    }

    private static (PackageInstallService Svc, ContentCatalog Catalog) BuildPipeline()
    {
        var factory   = new InMemoryFactory("seed_" + Guid.NewGuid().ToString("N"));
        var resolver  = new DefaultTypeResolver();
        var catalog   = new ContentCatalog(resolver);
        var discovery = new DiscoveryService(factory, [], catalog);
        var blobs     = new InMemoryPackageBlobStore();
        var svc       = new PackageInstallService(factory, discovery, blobs, new NullPackageExtractor(), new NullRenderAlertSink());
        return (svc, catalog);
    }
}
