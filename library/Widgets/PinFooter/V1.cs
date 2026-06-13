using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.PinFooter;

/// <summary>
/// MindAttic.Ideas.Widget.PinFooter.V1 — the UiUx PINFOOTER bundle as an asset-only activator,
/// extracted VERBATIM from mindattic.com/index.htm. Any element with class
/// <c>pin-when-short</c> (canonically the site &lt;footer&gt;) pins to the bottom edge while the
/// document is shorter than the viewport and flows normally once content scrolls — re-evaluated on
/// resize, font load, and (CMS adapter) host DOM swaps. Distinct from the generic
/// <c>Widget.Footer</c> baseline activator: this is the authentic mindattic.com implementation and
/// class contract.
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/pinfooter/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/pinfooter.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/pinfooter.js" };
}
