using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Rendering;

/// <summary>
/// Collects the head assets (css/scripts) a page contributes from the citizens it includes, deduped and in
/// cascade order, for hoisted &lt;head&gt; injection (so two components that use the same stylesheet inject
/// it once). PURE: it resolves include references against the catalog EXACTLY as the renderer does (so the
/// head never disagrees with the body about which version is on the page), and reads each citizen's assets
/// through an injected <paramref name="assetsOf"/> delegate — the only seam, so the package path (live this
/// slot) and the compiled-instance path (attended TODO) compose without changing this code.
/// </summary>
public static class PageAssetCollector
{
    public static HeadAssets Collect(string? pageBodyHtml, IContentCatalog catalog, Func<ContentDescriptor, CitizenAssets> assetsOf)
    {
        var refs = IncludeReferenceParser.Parse(pageBodyHtml);
        if (refs.Count == 0) return HeadAssets.Empty;

        var css = new List<string>();
        var scripts = new List<string>();
        var seenCss = new HashSet<string>(StringComparer.Ordinal);
        var seenScripts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (kind, key, version) in refs)
        {
            // Mirror ContentCatalog.ResolveTag: a pin falls back to latest if that exact version is gone.
            var desc = version is int v
                ? (catalog.Find(kind, key, v) ?? catalog.FindLatest(kind, key))
                : catalog.FindLatest(kind, key);
            if (desc is null) continue;   // missing/disabled — contributes nothing; the renderer alerts, not us

            var assets = assetsOf(desc);
            foreach (var url in assets.Css)
                if (seenCss.Add(url)) css.Add(url);             // first-occurrence-wins, position fixed at first sight
            foreach (var url in assets.Scripts)
                if (seenScripts.Add(url)) scripts.Add(url);
        }

        return new HeadAssets(css, scripts);
    }
}
