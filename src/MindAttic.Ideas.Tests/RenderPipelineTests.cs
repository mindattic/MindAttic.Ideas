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
/// End-to-end render pipeline: install a package → catalog reload → IncludeExpander resolves the
/// token to a Component frame (Resolved outcome, not a MissingContent placeholder). This is the
/// automatable portion of MAI-US-F5/A6; the Blazor-host HTTP layer still needs an attended run.
///
/// The test uses <see cref="DefaultTypeResolver"/> so that a type already in the test process
/// (WidgetBase, reachable by CLR name) resolves without ALC extraction — proving that the
/// install → discovery → catalog → include-expander chain works end-to-end.
/// </summary>
[TestFixture]
public class RenderPipelineTests
{
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
        var factory = new InMemoryFactory("pipeline_" + Guid.NewGuid().ToString("N"));
        var resolver = new DefaultTypeResolver();
        var catalog  = new ContentCatalog(resolver);
        var discovery = new DiscoveryService(factory, [], catalog);
        var blobs = new InMemoryPackageBlobStore();
        var svc = new PackageInstallService(factory, discovery, blobs, new NullPackageExtractor(), new NullRenderAlertSink());
        return (svc, catalog);
    }

    // WidgetBase is in MindAttic.Ideas.Abstractions — always loadable by DefaultTypeResolver in the test process.
    // We use a fake assembly name "Demo" in the manifest/bin so the validator doesn't reject it as a
    // host assembly (MindAttic.Ideas.* is on the FORBIDDEN_BIN list). DefaultTypeResolver falls back
    // to scanning all loaded assemblies by type name, so it still resolves WidgetBase at runtime.
    private static readonly string WidgetBaseTypeName = typeof(WidgetBase).FullName!;
    private const string FakeAsmName = "Demo";

    [Test]
    public async Task Install_ThenReload_ThenExpand_ProducesResolvedFrame()
    {
        // Arrange: package whose EntryType is WidgetBase (resolvable by DefaultTypeResolver)
        var (svc, catalog) = BuildPipeline();
        var archive = IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Widget", Kind = "code",
                Key = "test.widget", Version = 1,
                DisplayName = "Test Widget", Sdk = 1,
                EntryType = WidgetBaseTypeName,
                AssemblyName = FakeAsmName,
            }),
            [$"bin/{FakeAsmName}.dll"] = "MZ-fake",
        });

        // Act: install (includes ReloadCatalogAsync internally)
        var plan = await svc.InstallAsync(archive, allowOverride: false);

        // Catalog should now have the descriptor
        var desc = catalog.FindLatest(ContentKind.Widget, "test.widget");
        Assert.That(desc, Is.Not.Null, "catalog should contain the installed widget");

        // IncludeExpander should produce a Component frame, not a Missing placeholder
        var builder = new RenderTreeBuilder();
        var seq = 0;
        IncludeExpander.Expand(builder, ref seq,
            "{{Widget.test.widget.V1}}",
            catalog, new PassGate(), ContentTrust.Author);

        var frames = builder.GetFrames();
        bool hasComponent = false, hasMissing = false;
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames.Array[i].FrameType != RenderTreeFrameType.Component) continue;
            if (frames.Array[i].ComponentType == typeof(MissingContent)) hasMissing = true;
            else hasComponent = true;
        }

        Assert.Multiple(() =>
        {
            Assert.That(plan.Action, Is.EqualTo(InstallAction.Install));
            Assert.That(hasMissing, Is.False, "no MissingContent placeholder expected");
            Assert.That(hasComponent, Is.True,  "a Component frame for the resolved type is expected");
        });
    }

    [Test]
    public async Task Install_ThenExpand_UnknownToken_ProducesMissingFrame()
    {
        var (svc, catalog) = BuildPipeline();
        var archive = IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Widget", Kind = "code",
                Key = "known.widget", Version = 1, DisplayName = "Known", Sdk = 1,
                EntryType = WidgetBaseTypeName, AssemblyName = FakeAsmName,
            }),
            [$"bin/{FakeAsmName}.dll"] = "MZ-fake",
        });
        await svc.InstallAsync(archive, allowOverride: false);

        // Reference a widget that was NOT installed
        var builder = new RenderTreeBuilder();
        var seq = 0;
        IncludeExpander.Expand(builder, ref seq,
            "{{Widget.not.installed.V1}}",
            catalog, new PassGate(), ContentTrust.Author);

        var frames = builder.GetFrames();
        bool hasMissing = false;
        for (var i = 0; i < frames.Count; i++)
        {
            if (frames.Array[i].FrameType == RenderTreeFrameType.Component &&
                frames.Array[i].ComponentType == typeof(MissingContent))
                hasMissing = true;
        }
        Assert.That(hasMissing, Is.True, "unknown token should degrade to MissingContent");
    }
}
