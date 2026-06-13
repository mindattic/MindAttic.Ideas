using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.Gallery;

/// <summary>
/// MindAttic.Ideas.Widget.Gallery.V1 — the image-grid capability as an asset-only activator. Drop the
/// token once and ANY <c>&lt;div class="ma-gallery"&gt;</c> becomes a responsive grid of its tiles.
/// Tiles follow the page-authoring convention that images live as inline base64 CSS classes:
/// <code>
///   &lt;div class="ma-gallery"&gt;
///     &lt;div class="ma-gallery-tile img-photo1"&gt;&lt;/div&gt;            (class tile → lightbox)
///     &lt;a class="ma-gallery-tile img-book1" href="https://…"&gt;&lt;/a&gt;  (linked tile → navigates)
///     &lt;img src="data:…" alt="…"&gt;                                  (&lt;img&gt; also works)
///   &lt;/div&gt;
/// </code>
/// Linked tiles make it the mindattic.com books grid (covers → store pages); unlinked tiles get a
/// keyboard-navigable lightbox (Esc / arrows). Tile size tunes via <c>--ma-gallery-min</c>, tile
/// shape via <c>--ma-gallery-ratio</c>.
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/gallery/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/gallery.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/gallery.js" };
}
