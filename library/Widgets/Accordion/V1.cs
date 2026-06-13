using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.Accordion;

/// <summary>
/// MindAttic.Ideas.Widget.Accordion.V1 — the collapsible-sections capability as an asset-only
/// activator. Drop the token once and ANY <c>&lt;div class="ma-accordion"&gt;</c> of native
/// <c>&lt;details&gt;/&lt;summary&gt;</c> children gets the accordion chrome (no JS required for
/// open/close — the platform does it). Add <c>data-exclusive</c> on the container and the script
/// keeps at most one item open (the classic FAQ behavior):
/// <code>
///   &lt;div class="ma-accordion" data-exclusive&gt;
///     &lt;details&gt;&lt;summary&gt;Question&lt;/summary&gt;&lt;p&gt;Answer.&lt;/p&gt;&lt;/details&gt;
///   &lt;/div&gt;
/// </code>
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/accordion/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/accordion.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/accordion.js" };
}
