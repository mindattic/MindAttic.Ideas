using Microsoft.AspNetCore.Components;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Rendering;

namespace MindAttic.Ideas.Tests.Rendering;

/// <summary>
/// The injection guard for per-page raw Html/Css/Js (Page records). The gate is the SOLE place a
/// MarkupString is built from author content: Author-trust passes raw (intentional admin markup);
/// anything Untrusted is sanitized so script/style/event-handler/javascript: injection can't land.
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
    public void Untrusted_PreservesSafePlainText_SoWidgetTokenLikeTextPassesThrough()
    {
        // Non-admin (Untrusted) page content is sanitized. Token-like text {{…}} is plain text
        // and survives the sanitizer; however, IncludeExpander no longer interprets {{…}} as a
        // component reference — it is treated as literal text. Component embedding requires Author trust.
        const string body = "<p>before</p>{{ MindAttic.Ideas.Plugin.Tooltip }}<p>after</p>";
        var outp = Untrusted(body);
        Assert.That(outp, Does.Contain("{{ MindAttic.Ideas.Plugin.Tooltip }}"),
            "plain text (including token-like strings) survives sanitization");
        // The parser does NOT find it as a component reference (tokens are no longer parsed).
        var refs = IncludeReferenceParser.Parse(outp);
        Assert.That(refs, Is.Empty, "token-like text is not a component reference after the token grammar was removed");
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

    // ---- Hardened config: form tags / phishing ----

    [Test]
    public void Untrusted_StripsFormTag_BlocksPhishing()
    {
        var outp = Untrusted("<form action='//evil.com' method='post'><input name='cc'/></form><p>ok</p>");
        Assert.That(outp, Does.Contain("ok"));
        Assert.That(outp, Does.Not.Contain("<form"));
        Assert.That(outp, Does.Not.Contain("action="));
        Assert.That(outp, Does.Not.Contain("<input"));
    }

    // ---- Hardened config: CSS injection ----

    [Test]
    public void Untrusted_StripsStyleAttribute_BlocksCssOverlay()
    {
        var outp = Untrusted("<div style=\"position:fixed;top:0;left:0;width:100%;height:100%;z-index:9999\">overlay</div>");
        Assert.That(outp, Does.Contain("overlay"));
        Assert.That(outp, Does.Not.Contain("style="));
        Assert.That(outp, Does.Not.Contain("position:fixed"));
    }

    // ---- Hardened config: tab-nabbing ----

    [Test]
    public void Untrusted_StripsTarget_PreventsTabNabbing()
    {
        // Without target="_blank" the opener.location attack vector disappears entirely.
        var outp = Untrusted("<a href=\"https://example.com\" target=\"_blank\">link</a>");
        Assert.That(outp, Does.Contain("https://example.com"));
        Assert.That(outp, Does.Not.Contain("target="));
    }

    // ---- Hardened config: frame injection ----

    [Test]
    public void Untrusted_StripsIframe_BlocksFrameInjection()
    {
        var outp = Untrusted("<iframe src=\"https://evil.com\" sandbox=\"allow-scripts\"></iframe><p>ok</p>");
        Assert.That(outp, Does.Contain("ok"));
        Assert.That(outp, Does.Not.Contain("<iframe"));
    }

    // ---- Hardened config: data: URI ----

    [Test]
    public void Untrusted_StripsDataUri_InImgSrc()
    {
        // data:text/html can smuggle a full HTML document with script.
        var outp = Untrusted("<img src=\"data:text/html,<script>alert(1)</script>\" alt=\"x\" />");
        Assert.That(outp, Does.Not.Contain("data:text/html"));
    }

    // ---- Hardened config: safe content preserved ----

    [Test]
    public void Untrusted_PreservesSafeHtml()
    {
        const string safe = "<h2>Title</h2><p>Hello <strong>world</strong>.</p>" +
                            "<a href=\"https://example.com\">link</a>" +
                            "<img src=\"https://example.com/img.png\" alt=\"photo\" />";
        var outp = Untrusted(safe);
        Assert.That(outp, Does.Contain("<h2>"));
        Assert.That(outp, Does.Contain("<strong>"));
        Assert.That(outp, Does.Contain("href=\"https://example.com\"")
            .Or.Contain("href='https://example.com'"));
        Assert.That(outp, Does.Contain("https://example.com/img.png"));
    }

    // ---- Scheme allow-list ----

    [Test]
    public void Untrusted_StripsVbscriptUri()
    {
        var outp = Untrusted("<a href=\"vbscript:msgbox(1)\">click</a>");
        Assert.That(outp, Does.Not.Contain("vbscript:"));
    }

    [Test]
    public void Untrusted_PermitsMailtoAndTel()
    {
        var outp = Untrusted("<a href=\"mailto:user@example.com\">email</a> <a href=\"tel:+1234567890\">call</a>");
        Assert.That(outp, Does.Contain("mailto:user@example.com"));
        Assert.That(outp, Does.Contain("tel:+1234567890"));
    }
}
