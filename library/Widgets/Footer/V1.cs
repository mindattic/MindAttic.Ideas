using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.Footer;

/// <summary>
/// MindAttic.Ideas.Widget.Footer.V1 — the site-footer capability as an asset-only activator. Drop the
/// token once and ANY <c>&lt;footer class="ma-footer"&gt;…&lt;/footer&gt;</c> the author writes gets the
/// footer chrome plus the mindattic.com <em>pin-when-short</em> behavior: when the page content is
/// shorter than the viewport the footer pins to the bottom edge; when content is taller it flows
/// normally after the content (never overlaps). The author keeps full control of the footer's HTML.
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/footer/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/footer.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/footer.js" };
}
