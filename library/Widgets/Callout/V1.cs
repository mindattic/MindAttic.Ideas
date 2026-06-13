using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.Callout;

/// <summary>
/// MindAttic.Ideas.Widget.Callout.V1 — notice/admonition boxes as a CSS-only activator. Drop the
/// token once and ANY <c>&lt;div class="ma-callout"&gt;</c> gets the callout chrome; add a variant
/// class for the accent + icon: <c>ma-callout-info</c> (default), <c>ma-callout-success</c>,
/// <c>ma-callout-warn</c>, <c>ma-callout-error</c>. Pure CSS — icons are drawn glyphs, no images,
/// no script:
/// <code>&lt;div class="ma-callout ma-callout-warn"&gt;&lt;strong&gt;Heads up.&lt;/strong&gt; Versions never mutate.&lt;/div&gt;</code>
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/callout/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/callout.css" };
}
