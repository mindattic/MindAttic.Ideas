using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.TabBoard;

/// <summary>
/// MindAttic.Ideas.Widget.TabBoard.V1 — the mindattic.com project-board engine as an asset-only
/// activator, extracted VERBATIM from mindattic.com/index.htm (the `.tabButton`/`.tabPage` styles
/// and the board script). Drop the token once and authored board markup is wired — tiles in a
/// wrapping grid, single-active full-row panels, procedural tile art for placeholders, per-section
/// localStorage persistence:
/// <code>
///   &lt;div class="board-section"&gt;&lt;div class="board-grid"&gt;
///     &lt;button type="button" class="tabButton" data-target="x"&gt;
///       &lt;div class="tabButton-name"&gt;Name&lt;/div&gt;&lt;/button&gt;
///     &lt;div class="tabPage" id="x"&gt;…&lt;/div&gt;
///   &lt;/div&gt;&lt;/div&gt;
/// </code>
/// Page scripts can also BUILD boards from data via <c>window.TabBoard.build(items)</c> /
/// <c>TabBoard.art(name)</c> / <c>TabBoard.images</c> (how the frontpage tabifies its Portfolio
/// links and books grids).
///
/// PLACEMENT OPTIONS: <c>{{ MindAttic.Ideas.Widget.TabBoard alwaysShowTabPage=true }}</c> keeps a
/// selected item always visible — re-clicking the open tab no longer collapses it, and a board with
/// no saved selection opens its first tab. The selection persists per section in localStorage
/// either way (the engine's verbatim behavior).
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/tabboard/1";

    /// <summary>
    /// "true" =&gt; every board keeps one tab open (no fully-collapsed state). Declared as object so
    /// both token forms bind: <c>alwaysShowTabPage=true</c> (string) and a bare attribute (bool).
    /// </summary>
    [Parameter] public object? AlwaysShowTabPage { get; set; }

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/tabboard.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/tabboard.js" };

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        base.BuildRenderTree(builder);   // the asset-emitting render (link + script)

        // Hand the placement option to the engine. The engine reads window.TabBoardConfig lazily
        // (at wire time), so the relative order of this inline script and the engine script never
        // matters. Emitted only when the author set the attribute.
        if (AlwaysShowTabPage is not null)
        {
            var on = string.Equals(AlwaysShowTabPage.ToString(), "true", StringComparison.OrdinalIgnoreCase);
            builder.OpenElement(100, "script");
            builder.AddMarkupContent(101,
                "window.TabBoardConfig = Object.assign(window.TabBoardConfig || {}, { alwaysShowTabPage: "
                + (on ? "true" : "false") + " });");
            builder.CloseElement();
        }
    }
}
