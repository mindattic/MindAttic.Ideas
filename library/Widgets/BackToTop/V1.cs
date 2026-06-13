using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.BackToTop;

/// <summary>
/// MindAttic.Ideas.Widget.BackToTop.V1 — the scroll-to-top capability as an asset-only activator.
/// Drop the token once and a floating button appears in the bottom-right corner after the visitor
/// scrolls one screen down; clicking it smooth-scrolls back to the top. Zero author markup — the
/// script creates the button itself (the arrow is a CSS glyph; no images, no requests).
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/backtotop/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/backtotop.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/backtotop.js" };
}
