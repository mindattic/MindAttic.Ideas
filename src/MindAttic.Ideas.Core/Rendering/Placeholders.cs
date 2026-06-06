using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Rendering;

/// <summary>Visible placeholder for an unresolved/stale include (never a crash). A plain Blazor
/// component — MindAttic's content bases are the four <see cref="IdeaBase"/> kinds, not this.</summary>
public sealed class MissingContent : ComponentBase
{
    [Parameter] public string Key { get; set; } = "";

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        // A visible placeholder box for an unresolved {{ … }} token (missing/disabled .idea). Styled inline
        // so it shows even before any theme/app CSS loads; .ma-missing-content lets a theme restyle it.
        b.OpenElement(0, "span");
        b.AddAttribute(1, "class", "ma-missing-content");
        b.AddAttribute(2, "data-ma-missing-key", Key);
        b.AddAttribute(3, "title", $"Not found: {Key}");
        b.AddAttribute(4, "style",
            "display:inline-block;padding:.15rem .5rem;border:1px dashed #c0392b;border-radius:6px;" +
            "background:rgba(192,57,43,.08);color:#c0392b;font:600 .8rem/1.4 ui-monospace,monospace;");
        b.AddContent(5, $"⚠ {Key} not found");
        b.CloseElement();
    }
}

/// <summary>Rendered when a Code page's type can't be resolved (stale/renamed). Never a crash.</summary>
public sealed class MissingPageHost : PageBase
{
    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        b.OpenElement(0, "div");
        b.AddAttribute(1, "class", "ma-missing-page");
        b.AddContent(2, "This page's component is not available.");
        b.CloseElement();
    }
}
