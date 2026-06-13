using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.WebSnapshot;

/// <summary>
/// MindAttic.Ideas.Widget.WebSnapshot.V1 — the UiUx WEBSNAPSHOT bundle as an asset-only activator,
/// extracted VERBATIM from mindattic.com/index.htm. Any <c>&lt;div class="web-snapshot"&gt;</c>
/// container becomes a framed site-screenshot viewer: fetch mode (<c>data-src</c> pointing at a
/// .b64 capture) or inline mode (set the inner <c>&lt;img src&gt;</c> yourself, e.g. a base64 data
/// URI per the page convention). Exposes <c>window.WebSnapshot.autoInit()</c> for containers built
/// after load (the frontpage's tabified Portfolio tiles).
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/websnapshot/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/websnapshot.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/websnapshot.js" };
}
