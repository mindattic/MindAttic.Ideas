using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.Tabs;

/// <summary>
/// MindAttic.Ideas.Widget.Tabs.V1 — the tabbed-content capability as an asset-only activator (the
/// Tooltip model). Drop the token once and ANY <c>&lt;div class="ma-tabs"&gt;</c> whose child sections
/// carry <c>data-title</c> becomes a wired, accessible tab control — the script builds the tablist
/// from the titles, so the page author writes only content:
/// <code>
///   &lt;div class="ma-tabs"&gt;
///     &lt;section data-title="One"&gt;…&lt;/section&gt;
///     &lt;section data-title="Two"&gt;…&lt;/section&gt;
///   &lt;/div&gt;
/// </code>
/// Add <c>ma-tabs-board</c> for the mindattic.com tab-board look: a wrapping grid of fixed-size tiles
/// with the open panel claiming the full row below.
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/tabs/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/tabs.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/tabs.js" };
}
