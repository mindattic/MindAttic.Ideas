using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Rendering;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// The disabled/missing-dependency render guard: the shared include grammar, plus IncludeExpander
/// degrading an unresolved/disabled tag to a MissingContent placeholder AND raising exactly one alert
/// (never a render crash; a resolved tag raises none).
/// </summary>
[TestFixture]
public class RenderGuardTests
{
    // ---- IncludeReferenceParser grammar ----

    [Test]
    public void Parse_PinnedVersion()
    {
        var refs = IncludeReferenceParser.Parse("{{MindAttic.Ideas.Plugin.Tooltip.V11}}");
        Assert.That(refs, Has.Count.EqualTo(1));
        Assert.That(refs[0], Is.EqualTo((ContentKind.Plugin, "tooltip", (int?)11)));
    }

    [Test]
    public void Parse_BraceToken_WithAttributes_ResolvesReferenceIgnoringAttrs()
    {
        // Tokens carry attributes (e.g. fontSize=0.5rem); they don't affect the (kind,key,version) identity,
        // and surrounding text is ignored. Only the reference is parsed here.
        var refs = IncludeReferenceParser.Parse(
            "before {{ MindAttic.Ideas.Plugin.TableOfContents fontSize=0.5rem class=\"x y\" }} after");
        Assert.That(refs, Has.Count.EqualTo(1));
        Assert.That(refs[0], Is.EqualTo((ContentKind.Plugin, "tableofcontents", (int?)null)));
    }

    [Test]
    public void Parse_FloatingAndLatest_HaveNullVersion()
    {
        Assert.That(IncludeReferenceParser.Parse("{{MindAttic.Ideas.Theme.Cyberspace}}")[0],
            Is.EqualTo((ContentKind.Theme, "cyberspace", (int?)null)));
        Assert.That(IncludeReferenceParser.Parse("{{MindAttic.Ideas.Control.Textbox.Latest}}")[0],
            Is.EqualTo((ContentKind.Control, "textbox", (int?)null)));
    }

    [Test]
    public void Parse_IgnoresPlainHtmlAndMalformed()
    {
        Assert.That(IncludeReferenceParser.Parse("<div><p>hello</p></div>"), Is.Empty);
        Assert.That(IncludeReferenceParser.Parse("{{MindAttic.Ideas.Bogus.Thing}}"), Is.Empty); // kind not a ContentKind
        Assert.That(IncludeReferenceParser.Parse(null), Is.Empty);
    }

    [Test]
    public void BodyPinsVersion_And_BodyReferencesKey()
    {
        const string html = "{{MindAttic.Ideas.Plugin.Tooltip.V11}}{{MindAttic.Ideas.Theme.Cyberspace}}";
        Assert.That(IncludeReferenceParser.BodyPinsVersion(html, ContentKind.Plugin, "tooltip", 11), Is.True);
        Assert.That(IncludeReferenceParser.BodyPinsVersion(html, ContentKind.Plugin, "tooltip", 12), Is.False);
        Assert.That(IncludeReferenceParser.BodyReferencesKey(html, ContentKind.Theme, "cyberspace"), Is.True);
        Assert.That(IncludeReferenceParser.BodyFloatsKey(html, ContentKind.Theme, "cyberspace"), Is.True);
    }

    // ---- IncludeExpander guard behavior ----

    private sealed class DummyComponent : Microsoft.AspNetCore.Components.ComponentBase { }

    private sealed class FakeCatalog : IContentCatalog
    {
        public ContentResolution Outcome = ContentResolution.Missing;
        public IReadOnlyCollection<ContentDescriptor> All => Array.Empty<ContentDescriptor>();
        public ContentDescriptor? Find(ContentKind kind, string key, int version) => null;
        public ContentDescriptor? FindLatest(ContentKind kind, string key) => null;
        public Type? ResolveType(ContentDescriptor descriptor) => typeof(DummyComponent);
        public ResolvedContent ResolveTag(ContentKind kind, string key, int? version) =>
            Outcome == ContentResolution.Resolved
                ? new ResolvedContent(ContentResolution.Resolved, typeof(DummyComponent), null)
                : new ResolvedContent(Outcome, null, null);
    }

    private sealed class RecordingSink : IRenderAlertSink
    {
        public int Missing, Disabled;
        public void RaiseMissing(ContentKind k, string key, int? v, Guid p, string s) => Missing++;
        public void RaiseDisabled(ContentKind k, string key, int? v, Guid p, string s) => Disabled++;
    }

    private sealed class PassGate : IRawContentGate
    {
        public MarkupString Emit(string? html, ContentTrust trust) => new(html ?? "");
    }

    private static (int missing, int disabled, bool placeholder, bool resolvedComponent) RunExpand(ContentResolution outcome)
    {
        var catalog = new FakeCatalog { Outcome = outcome };
        var sink = new RecordingSink();
        var builder = new RenderTreeBuilder();
        builder.OpenElement(0, "div");
        var seq = 1;
        IncludeExpander.Expand(builder, ref seq, "{{MindAttic.Ideas.Plugin.Tooltip.V11}}",
            catalog, new PassGate(), ContentTrust.Author, sink, Guid.NewGuid(), "demo");
        builder.CloseElement();

        var frames = builder.GetFrames();
        bool placeholder = false, resolved = false;
        for (var i = 0; i < frames.Count; i++)
        {
            var f = frames.Array[i];
            if (f.FrameType != RenderTreeFrameType.Component) continue;
            if (f.ComponentType == typeof(MissingContent)) placeholder = true;
            if (f.ComponentType == typeof(DummyComponent)) resolved = true;
        }
        return (sink.Missing, sink.Disabled, placeholder, resolved);
    }

    [Test]
    public void MissingInclude_RendersPlaceholder_AndRaisesOneMissingAlert()
    {
        var (missing, disabled, placeholder, _) = RunExpand(ContentResolution.Missing);
        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.EqualTo(1));
            Assert.That(disabled, Is.EqualTo(0));
            Assert.That(placeholder, Is.True);
        });
    }

    [Test]
    public void DisabledInclude_RendersPlaceholder_AndRaisesOneDisabledAlert()
    {
        var (missing, disabled, placeholder, _) = RunExpand(ContentResolution.Disabled);
        Assert.Multiple(() =>
        {
            Assert.That(disabled, Is.EqualTo(1));
            Assert.That(missing, Is.EqualTo(0));
            Assert.That(placeholder, Is.True);
        });
    }

    [Test]
    public void ResolvedInclude_RendersComponent_AndRaisesNoAlert()
    {
        var (missing, disabled, _, resolved) = RunExpand(ContentResolution.Resolved);
        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True);
            Assert.That(missing, Is.EqualTo(0));
            Assert.That(disabled, Is.EqualTo(0));
        });
    }
}
