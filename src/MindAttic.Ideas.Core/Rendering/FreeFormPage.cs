using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Rendering;

/// <summary>
/// The built-in renderer for a Data page (RenderStrategy.RawMarkup): emits the page's free-form
/// author content — page-level CSS (cascade tier 3), the expanded HTML body (with &lt;ma-component&gt;
/// includes turned into live components), and the author's inline JS (only when Author-trusted).
/// </summary>
public sealed class FreeFormPage : PageBase
{
    [Inject] public IContentCatalog Catalog { get; set; } = default!;
    [Inject] public IRawContentGate Gate { get; set; } = default!;
    [Inject] public IRenderAlertSink Alerts { get; set; } = default!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var inline = Context.Page.Inline;
        var trust = inline.Trusted ? ContentTrust.Author : ContentTrust.Untrusted;
        var seq = 0;

        // Cascade tier 3: page-level stylesheet.
        if (!string.IsNullOrWhiteSpace(inline.Css))
        {
            builder.OpenElement(seq++, "style");
            builder.AddMarkupContent(seq++, inline.Css);
            builder.CloseElement();
        }

        // Free-form body with <MindAttic.Ideas.{Kind}.{Name}.V{n}> includes. A missing/disabled
        // reference degrades to a placeholder AND raises an Admin Inbox alert (a page must never be invalid).
        IncludeExpander.Expand(builder, ref seq, inline.Html, Catalog, Gate, trust,
            Alerts, Context.Page.PageId, Context.Page.Slug);

        // Intentional author JS — emitted only for Author-trusted pages.
        if (trust == ContentTrust.Author && !string.IsNullOrWhiteSpace(inline.Js))
        {
            builder.OpenElement(seq++, "script");
            builder.AddMarkupContent(seq++, inline.Js);
            builder.CloseElement();
        }
    }
}
