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
        b.OpenElement(0, "span");
        b.AddAttribute(1, "class", "ma-missing-content");
        b.AddAttribute(2, "data-ma-missing-key", Key);
        b.AddAttribute(3, "title", $"Component not available: {Key}");
        b.AddContent(4, $"[unavailable component: {Key}]");
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
