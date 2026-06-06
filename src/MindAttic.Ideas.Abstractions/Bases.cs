using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace MindAttic.Ideas.Abstractions;

// ============================================================================================
//  THE INHERITANCE ROOTS — IdeaBase is the shared root every content type derives from; the four
//  kind bases give each kind its shape. The kind is determined by which base you inherit. Inheriting
//  is how a type becomes content; the .idea package carries it. GROW ONLY by adding NON-ABSTRACT
//  members; never add an abstract member (it would break every existing subclass + shipped package).
// ============================================================================================

/// <summary>The shared root. Everything an .idea contains derives (transitively) from this.</summary>
public abstract class IdeaBase : ComponentBase
{
    /// <summary>The render context for this placement (provided by the host via a cascade).</summary>
    [CascadingParameter] protected IRenderContext Context { get; set; } = default!;
    protected bool EditMode => Context?.Mode == ContentMode.Edit;
}

// ---- Page ----

/// <summary>Base for a compiled (Code) Page. e.g. <c>MindAttic.Ideas.Page.LegionFrontpage.V2</c>.</summary>
public abstract class PageBase : IdeaBase { }

/// <summary>Base for a compiled Page with strongly-typed settings.</summary>
public abstract class PageBase<TSettings> : PageBase where TSettings : class, new()
{
    protected TSettings Settings { get; private set; } = new();
    protected override void OnParametersSet() => Settings = Context.GetSettings<TSettings>();
}

// ---- Theme ----

/// <summary>
/// Base for a Theme: layout chrome with a SINGLE <see cref="Body"/> hole — NO zones. CSS/script URL
/// lists map onto the fixed cascade (Global -> Theme tiers). e.g. <c>MindAttic.Ideas.Theme.Cyberspace.V4</c>.
/// </summary>
public abstract class ThemeBase : IdeaBase
{
    /// <summary>The page's free-form content renders here. The only hole; never a zone grid.</summary>
    [Parameter] public RenderFragment? Body { get; set; }

    /// <summary>Cascade tier "global" CSS (e.g. fonts), emitted before <see cref="ThemeCssUrls"/>.</summary>
    public virtual IReadOnlyList<string> GlobalCssUrls => Array.Empty<string>();

    /// <summary>Cascade tier "theme" CSS. Mirror the UiUx deps.json css[] at a pinned tag.</summary>
    public virtual IReadOnlyList<string> ThemeCssUrls => Array.Empty<string>();

    /// <summary>Theme scripts (e.g. Cyberspace effect loaders). Mirror deps.json scripts[].</summary>
    public virtual IReadOnlyList<string> ScriptUrls => Array.Empty<string>();

    /// <summary>Optional raw HTML injected at the top of the theme body (e.g. effect layers).</summary>
    public virtual string? BodyPreludeHtml => null;
}

// ---- Plugin (a capability you add to a page) ----

/// <summary>
/// Base for a Plugin: a piece of CODE you add to a page to switch ON a capability — it loads its
/// assets so a behavior works page-wide. e.g. dropping <c>MindAttic.Ideas.Plugin.Tooltip.V11</c>
/// loads tooltip css/js so ANY element with <c>data-tooltip</c>/<c>data-tt</c> shows a tooltip on hover.
/// By default it renders no widget of its own — it emits its <see cref="StylesheetUrls"/> as
/// &lt;link&gt; and <see cref="ScriptUrls"/> as &lt;script&gt;. Such activators are normally code-only
/// classes (no markup) so they inherit this asset-emitting render; override only to add markup.
/// </summary>
public abstract class PluginBase : IdeaBase
{
    /// <summary>Stylesheets this capability needs (jsDelivr or host-relative). Emitted once.</summary>
    public virtual IReadOnlyList<string> StylesheetUrls => Array.Empty<string>();

    /// <summary>Scripts this capability needs (the behavior engine, e.g. tooltip.js). Emitted once.</summary>
    public virtual IReadOnlyList<string> ScriptUrls => Array.Empty<string>();

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var seq = 0;
        foreach (var css in StylesheetUrls)
        {
            builder.OpenElement(seq++, "link");
            builder.AddAttribute(seq++, "rel", "stylesheet");
            builder.AddAttribute(seq++, "href", css);
            builder.CloseElement();
        }
        foreach (var js in ScriptUrls)
        {
            builder.OpenElement(seq++, "script");
            builder.AddAttribute(seq++, "src", js);
            builder.CloseElement();
        }
    }
}

// ---- Control (one atomic placed UI element) ----

/// <summary>
/// Base for a Control: a single atomic UI element you place and that renders visibly at that spot
/// (e.g. <c>MindAttic.Ideas.Control.Textbox.V21</c> -&gt; an &lt;input&gt;). Unmatched include-tag
/// attributes flow straight through to the rendered element.
/// </summary>
public abstract class ControlBase : IdeaBase
{
    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? Attributes { get; set; }
}

/// <summary>Base for a Control with strongly-typed settings.</summary>
public abstract class ControlBase<TSettings> : ControlBase where TSettings : class, new()
{
    protected TSettings Settings { get; private set; } = new();
    protected override void OnParametersSet() => Settings = Context?.GetSettings<TSettings>() ?? new();
}
