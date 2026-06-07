using Microsoft.AspNetCore.Components;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Rendering;

namespace MindAttic.Ideas.Tests.Rendering;

/// <summary>
/// The injection guard for per-page raw Html/Css/Js (Page records). The gate is the SOLE place a
/// MarkupString is built from author content: Author-trust passes raw (intentional admin markup);
/// anything Untrusted is sanitized so script/style/event-handler/javascript: injection can't land.
/// These lock that contract AND prove the {{…}} include tokens survive sanitization (so a non-admin
/// page can still compose widgets, but can't inject script).
/// </summary>
[TestFixture]
public class RawContentGateTests
{
    private RawContentGate _gate = null!;

    [SetUp]
    public void SetUp() => _gate = new RawContentGate();

    private string Untrusted(string html) => _gate.Emit(html, ContentTrust.Untrusted).Value;
    private string Author(string html) => _gate.Emit(html, ContentTrust.Author).Value;

    [Test]
    public void Untrusted_StripsScriptTag()
    {
        var outp = Untrusted("<p>hi</p><script>alert('xss')</script>");
        Assert.That(outp, Does.Contain("hi"));
        Assert.That(outp, Does.Not.Contain("<script"));
        Assert.That(outp, Does.Not.Contain("alert"));
    }

    [Test]
    public void Untrusted_StripsInlineEventHandler()
    {
        var outp = Untrusted("<img src=\"x\" onerror=\"alert(1)\" />");
        Assert.That(outp, Does.Not.Contain("onerror"));
        Assert.That(outp, Does.Not.Contain("alert"));
    }

    [Test]
    public void Untrusted_NeutralizesJavascriptUri()
    {
        var outp = Untrusted("<a href=\"javascript:alert(1)\">x</a>");
        Assert.That(outp, Does.Not.Contain("javascript:"));
    }

    [Test]
    public void Untrusted_StripsStyleTag()
    {
        var outp = Untrusted("<style>body{background:url('javascript:alert(1)')}</style><p>ok</p>");
        Assert.That(outp, Does.Contain("ok"));
        Assert.That(outp, Does.Not.Contain("<style"));
    }

    [Test]
    public void Untrusted_PreservesIncludeTokens_SoWidgetsStillCompose()
    {
        // A non-admin page is sanitized, but must still be able to place widgets by token — the {{…}}
        // text survives so IncludeExpander can resolve it. (Injection is blocked; composition is not.)
        const string body = "<p>before</p>{{ MindAttic.Ideas.Widget.Tooltip }}<p>after</p>";
        var outp = Untrusted(body);
        Assert.That(outp, Does.Contain("{{ MindAttic.Ideas.Widget.Tooltip }}"));
        // and the parser still finds the reference in the sanitized output
        var refs = IncludeReferenceParser.Parse(outp);
        Assert.That(refs, Has.Count.EqualTo(1));
        Assert.That(refs[0], Is.EqualTo((ContentKind.Widget, "tooltip", (int?)null)));
    }

    [Test]
    public void Author_PassesRawVerbatim()
    {
        // Admin-authored content is intentional — script/style honored, byte-for-byte.
        const string raw = "<script>console.log('admin')</script><style>.x{color:red}</style><p>k</p>";
        Assert.That(Author(raw), Is.EqualTo(raw));
    }

    [Test]
    public void Emit_NullOrEmpty_IsEmptyMarkup()
    {
        Assert.That(_gate.Emit(null, ContentTrust.Untrusted).Value, Is.Null.Or.Empty);
        Assert.That(_gate.Emit("", ContentTrust.Author).Value, Is.Null.Or.Empty);
    }
}
