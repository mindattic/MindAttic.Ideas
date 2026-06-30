using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using BlazorComponentBase = Microsoft.AspNetCore.Components.ComponentBase;  // alias: MindAttic's ComponentBase wins the bare name (MAI-A26, MAI-A10)

namespace MindAttic.Ideas.Abstractions;

// ============================================================================================
//  THE INHERITANCE ROOTS — IdeaBase is the shared root every content type derives from; the four
//  kind bases give each kind its shape. The kind is determined by which base you inherit. Inheriting
//  is how a type becomes content; the .idea package carries it. GROW ONLY by adding NON-ABSTRACT
//  members; never add an abstract member (it would break every existing subclass + shipped package).
// ============================================================================================

/// <summary>The shared root. Everything an .idea contains derives (transitively) from this.</summary>
public abstract class IdeaBase : BlazorComponentBase
{
    /// <summary>The render context for this placement (provided by the host via a cascade).</summary>
    [CascadingParameter] protected IRenderContext Context { get; set; } = default!;
    protected bool EditMode => Context?.Mode == ContentMode.Edit;

    // ── URL safety ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the URL unchanged if it is safe to render in an href/src/action attribute.
    /// Returns "#" for javascript:, data:, vbscript: and any other scheme that could execute
    /// client-side code. Relative URLs (/path, #anchor, ./rel), http:, https:, mailto:, tel:
    /// all pass through. Call on every untrusted URL attribute before rendering.
    /// </summary>
    protected static string SafeUrl(string? url) => IsUnsafeUrl(url) ? "#" : (url ?? "#");

    /// <summary>True if the URL could execute script or embed attacker-controlled data.</summary>
    protected static bool IsUnsafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var t = url.TrimStart();
        return t.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase);
    }
}

// ---- Page ----

/// <summary>Base for a compiled (Code) Page. e.g. <c>MindAttic.Ideas.Page.LegionFrontpage.V2</c>.</summary>
public abstract class PageBase : IdeaBase { }

/// <summary>Base for a compiled Page with strongly-typed settings.</summary>
public abstract class PageBase<TSettings> : PageBase where TSettings : class, new()
{
    protected TSettings Settings { get; private set; } = new();
    protected override void OnParametersSet() => Settings = Context?.GetSettings<TSettings>() ?? new TSettings();
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

// ---- Plugin (site-wide .idea: activates a behavior/capability across the whole page) ----

/// <summary>
/// Base for a Plugin: a site-wide .idea that activates a behavior or capability across the entire
/// rendered page without occupying a specific token position. Plugins are selected per-page via the
/// Admin Page Properties Plugin checkbox list; they may also be injected inline via
/// <c>{{Plugin.X}}</c> for one-off pages (non-canonical). e.g. dropping
/// <c>MindAttic.Ideas.Plugin.Tooltip.V1</c> loads tooltip css/js so ANY element with
/// <c>data-tooltip</c>/<c>data-tt</c> shows a tooltip on hover.
///
/// By default renders no markup — emits <see cref="StylesheetUrls"/> as &lt;link&gt; and
/// <see cref="ScriptUrls"/> as &lt;script&gt;. Override <c>BuildRenderTree</c> to add markup.
/// Declare typed <c>[Parameter]</c> properties for configuration; unmatched attributes land in
/// <see cref="Attributes"/>.
/// </summary>
public abstract class PluginBase : IdeaBase
{
    /// <summary>
    /// Config attributes from the include tag that don't match a typed <c>[Parameter]</c> property.
    /// Never throws on an unknown attribute.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? Attributes { get; set; }

    /// <summary>Stylesheets this plugin needs (jsDelivr or host-relative). Emitted once.</summary>
    public virtual IReadOnlyList<string> StylesheetUrls => Array.Empty<string>();

    /// <summary>Scripts this plugin needs (the behavior engine). Emitted once.</summary>
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

// ---- Component (inline-placed .idea: renders at the {{Component.X}} token position; can nest) ----

/// <summary>
/// Base for a Component: an inline-placed .idea that renders at the exact
/// <c>{{Component.X}}</c> token position in the page body. Components can nest other Components,
/// enabling composite UIs — e.g. <c>MindAttic.Ideas.Component.TabControl</c> nests
/// <c>Component.TabButtonContainer</c>, <c>Component.TabButton</c> instances,
/// <c>Component.TabPageContainer</c>, and <c>Component.TabPage</c> instances (each of which may
/// contain <c>Component.Textbox</c> or other children). Declare sub-component dependencies with
/// <c>[Uses]</c>/<c>uses[]</c>.
///
/// NOTE: <c>ComponentBase</c> here is <c>MindAttic.Ideas.Abstractions.ComponentBase</c>, NOT Blazor's
/// <c>Microsoft.AspNetCore.Components.ComponentBase</c> — the MindAttic kind wins the bare name.
/// Blazor's base is aliased as <c>BlazorComponentBase</c> in this file (see MAI-A26, MAI-A10).
/// </summary>
public abstract class ComponentBase : IdeaBase
{
    /// <summary>
    /// Config attributes from the include tag that don't match a typed <c>[Parameter]</c> property.
    /// Never throws on an unknown attribute.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? Attributes { get; set; }

    /// <summary>Stylesheets this component needs (jsDelivr or host-relative). Emitted once.</summary>
    public virtual IReadOnlyList<string> StylesheetUrls => Array.Empty<string>();

    /// <summary>Scripts this component needs. Emitted once.</summary>
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

// ---- (Widget kind RETIRED — MAI-A26: split into Plugin=1 and Component=4. WidgetBase deleted.) ----
// ---- (Control kind REMOVED pre-1.0 — MAI-A19. Author atomic UI as a Component; ordinal 3 reserved.) ----
