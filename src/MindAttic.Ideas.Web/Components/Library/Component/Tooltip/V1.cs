using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Component.Tooltip;

/// <summary>
/// MindAttic.Ideas.Component.Tooltip.V1 — a capability activator. Dropping this tag onto a page loads
/// the tooltip CSS/JS, so thereafter ANY element with <c>data-tooltip</c>/<c>data-tt</c> shows a
/// tooltip on hover. It renders no widget of its own — it inherits ComponentBase's asset-emitting
/// render. Code-only (no markup) so it keeps that inherited behavior.
/// (Base here is MindAttic.Ideas.Abstractions.ComponentBase — not Blazor's.)
/// </summary>
public sealed class V1 : ComponentBase
{
    private const string Cdn = "https://cdn.jsdelivr.net/gh/mindattic/MindAttic.UiUx@v1.1.0/Components/Tooltip/";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Cdn + "tooltip.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Cdn + "tooltip.js" };
}
