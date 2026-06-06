using Microsoft.AspNetCore.Components;

namespace MindAttic.Ideas.Abstractions;

// ============================================================================================
//  RENDER CONTEXT — the single object handed to every citizen at render time.
//  It is an INTERFACE delivered as a [CascadingParameter], NOT a sealed class as a [Parameter].
//  This is the most load-bearing forward-compat choice in the foundation: an interface grows
//  append-only via DEFAULT methods AND unifies cleanly across the Phase-5 collectible-ALC boundary
//  (a sealed class passed by Parameter cannot). GROW ONLY by adding members WITH default
//  implementations; never remove/rename/reorder.
// ============================================================================================

/// <summary>Everything a Page/Component/Theme is handed at render time.</summary>
public interface IRenderContext
{
    /// <summary>Unique id for this placement/render instance.</summary>
    Guid InstanceId { get; }
    ContentMode Mode { get; }
    CmsRenderMode RenderMode { get; }
    IPageContext Page { get; }
    ISiteContext Site { get; }
    /// <summary>Scoped DI for this circuit/request.</summary>
    IServiceProvider Services { get; }
    /// <summary>Raw settings JSON for this placement, if any.</summary>
    string? RawSettingsJson { get; }
    /// <summary>Deserialize settings using the host's pinned serializer options.</summary>
    T GetSettings<T>() where T : class, new();

    /// <summary>
    /// Additive-forever escape hatch: resolve an optional host feature without ever changing this
    /// interface. Default implementation pulls from <see cref="Services"/>.
    /// </summary>
    bool TryGetFeature<T>(out T? feature) where T : class
    {
        feature = Services.GetService(typeof(T)) as T;
        return feature is not null;
    }
}

/// <summary>The page being rendered.</summary>
public interface IPageContext
{
    Guid PageId { get; }
    string Slug { get; }
    string Title { get; }
    string? ThemeKey { get; }
    /// <summary>The page's pinned theme version, if it overrides the site default.</summary>
    int? ThemeVersion { get; }
    /// <summary>Free-form author HTML/CSS/JS for this page (cascade tier 3) — Data pages.</summary>
    IInlineMarkup Inline { get; }
    IReadOnlyDictionary<string, string?> Meta { get; }
}

/// <summary>The site/tenant the page belongs to. Multi-site is a nullable seam from day one.</summary>
public interface ISiteContext
{
    Guid SiteId { get; }
    string Key { get; }
    string Host { get; }
    string DefaultThemeKey { get; }
    /// <summary>Read a setting through the Host -> Site -> Page override chain.</summary>
    string? GetSetting(string key);
}

/// <summary>
/// The free-form author content for a Data page: cascade tier 3. <see cref="Trusted"/> is the
/// render-time projection of the page's stored, write-time trust stamp.
/// </summary>
public interface IInlineMarkup
{
    string? Html { get; }
    string? Css { get; }
    string? Js { get; }
    /// <summary>True =&gt; host emits raw (intentional admin JS); false =&gt; sanitized.</summary>
    bool Trusted { get; }
}

/// <summary>
/// Host-provided render seam (resolved via <see cref="IRenderContext.TryGetFeature{T}"/>): turns a
/// string-id include reference — e.g. <c>"MindAttic.Ideas.Plugin.Tooltip.V1"</c> (omit the version or
/// use <c>.Latest</c> to float) — into a render fragment, using the SAME catalog resolution, Missing/
/// Disabled degradation, and Admin-Inbox alerting as a data page's <c>&lt;MindAttic.Ideas.…/&gt;</c>
/// include tag. This is what lets a COMPILED Page/Theme compose other citizens BY STRING ID with no
/// compile-time package reference — the <see cref="CmsInclude"/> primitive delegates to it. The host impl
/// MUST never throw into a render (degrade to a placeholder). APPEND-ONLY interface (default methods only).
/// </summary>
public interface IIncludeRenderer
{
    RenderFragment Render(
        IRenderContext context, string reference,
        IReadOnlyDictionary<string, object?>? attributes = null, RenderFragment? childContent = null);
}

/// <summary>One child page in the site tree (for nav / the TableOfContents plugin).</summary>
public sealed record ChildPage(string Slug, string Title);

/// <summary>
/// Host-provided render seam (resolved via <see cref="IRenderContext.TryGetFeature{T}"/>): the published,
/// enabled child pages of a page, ordered by sort order. Lets a Plugin (e.g. TableOfContents) render the
/// CURRENT page's children with NO compile-time host reference — it asks the context for this feature and,
/// if present, lists <see cref="ChildPage"/>s; a Plugin renders nothing when a page has no children. The
/// host impl MUST never throw into a render. APPEND-ONLY interface (default methods only).
/// </summary>
public interface IPageTree
{
    Task<IReadOnlyList<ChildPage>> ChildrenOfAsync(Guid pageId, CancellationToken ct = default);
}
