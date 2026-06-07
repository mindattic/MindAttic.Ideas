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
        var refs = IncludeReferenceParser.Parse("{{MindAttic.Ideas.Widget.Tooltip.V11}}");
        Assert.That(refs, Has.Count.EqualTo(1));
        Assert.That(refs[0], Is.EqualTo((ContentKind.Widget, "tooltip", (int?)11)));
    }

    [Test]
    public void Parse_PrefixIsOptional_ShortFormEqualsFullForm()
    {
        // {{Theme.Cyberspace}} == {{MindAttic.Ideas.Theme.Cyberspace}}; both forms parse identically.
        Assert.That(IncludeReferenceParser.Parse("{{Theme.Cyberspace}}"),
            Is.EqualTo(IncludeReferenceParser.Parse("{{MindAttic.Ideas.Theme.Cyberspace}}")));

        var refs = IncludeReferenceParser.Parse("{{Theme.Cyberspace}}{{Widget.Tooltip}}");
        Assert.That(refs, Is.EqualTo(new[]
        {
            (ContentKind.Theme, "cyberspace", (int?)null),
            (ContentKind.Widget, "tooltip", (int?)null),
        }));
    }

    [Test]
    public void TryParseTag_ShortForm_WithVersionAndDottedKey()
    {
        Assert.That(IncludeReferenceParser.TryParseTag("widget.tooltip.v11", out var k, out var key, out var v), Is.True);
        Assert.That((k, key, v), Is.EqualTo((ContentKind.Widget, "tooltip", (int?)11)));

        // Keys may contain dots; the short form preserves them just like the full form.
        Assert.That(IncludeReferenceParser.TryParseTag("widget.ui.tooltip", out _, out var dotted, out _), Is.True);
        Assert.That(dotted, Is.EqualTo("ui.tooltip"));
    }

    [Test]
    public void TryParseTag_KindAloneOrUnknownKind_Fails()
    {
        // A lone kind has no key, and a first segment that isn't a real kind is not an include
        // (so a stray {{foo.bar}} survives as literal text rather than becoming a placeholder).
        Assert.That(IncludeReferenceParser.TryParseTag("theme", out _, out _, out _), Is.False);
        Assert.That(IncludeReferenceParser.TryParseTag("foo.bar", out _, out _, out _), Is.False);
    }

    [Test]
    public void Parse_BraceToken_WithAttributes_ResolvesReferenceIgnoringAttrs()
    {
        // Tokens carry attributes (e.g. fontSize=0.5rem); they don't affect the (kind,key,version) identity,
        // and surrounding text is ignored. Only the reference is parsed here.
        var refs = IncludeReferenceParser.Parse(
            "before {{ MindAttic.Ideas.Widget.TableOfContents fontSize=0.5rem class=\"x y\" }} after");
        Assert.That(refs, Has.Count.EqualTo(1));
        Assert.That(refs[0], Is.EqualTo((ContentKind.Widget, "tableofcontents", (int?)null)));
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
    public void UpgradeLegacyTags_RewritesOldElementFormToTokens_AndIsIdempotent()
    {
        var html = "<p>x</p><MindAttic.Ideas.Widget.Tooltip /><MindAttic.Ideas.Control.Textbox placeholder=\"Type\" />";
        var up = IncludeReferenceParser.UpgradeLegacyTags(html)!;
        Assert.That(up, Does.Contain("{{MindAttic.Ideas.Widget.Tooltip}}"));
        Assert.That(up, Does.Contain("{{MindAttic.Ideas.Control.Textbox placeholder=\"Type\"}}"));
        Assert.That(up, Does.Not.Contain("<MindAttic.Ideas."));
        // Already-token content is left untouched (idempotent on a second pass / on new content).
        Assert.That(IncludeReferenceParser.UpgradeLegacyTags("{{MindAttic.Ideas.Widget.Tooltip}}"),
            Is.EqualTo("{{MindAttic.Ideas.Widget.Tooltip}}"));
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
        const string html = "{{MindAttic.Ideas.Widget.Tooltip.V11}}{{MindAttic.Ideas.Theme.Cyberspace}}";
        Assert.That(IncludeReferenceParser.BodyPinsVersion(html, ContentKind.Widget, "tooltip", 11), Is.True);
        Assert.That(IncludeReferenceParser.BodyPinsVersion(html, ContentKind.Widget, "tooltip", 12), Is.False);
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
        IncludeExpander.Expand(builder, ref seq, "{{MindAttic.Ideas.Widget.Tooltip.V11}}",
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
