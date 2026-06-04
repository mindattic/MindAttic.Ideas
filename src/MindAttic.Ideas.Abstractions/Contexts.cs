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
