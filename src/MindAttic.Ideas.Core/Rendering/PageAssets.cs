using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Core.Rendering;

/// <summary>One citizen's declared head assets, in the citizen's own cascade order.</summary>
public readonly record struct CitizenAssets(IReadOnlyList<string> Css, IReadOnlyList<string> Scripts);

/// <summary>The deduped, cascade-ordered head assets a page contributes (the collector's output).</summary>
public sealed record HeadAssets(IReadOnlyList<string> Css, IReadOnlyList<string> Scripts)
{
    public static readonly HeadAssets Empty = new([], []);
}

/// <summary>
/// Moves a package's ordered css[]/scripts[] across the <see cref="ContentDescriptor.Extra"/> seam, which
/// is <c>string→string</c> only. We join each list with <c>\n</c> on the way in and split on the way out;
/// order is preserved (the manifest lists are ordered) and blank entries are dropped. This is the no-schema
/// data path: a package's assets live in the verbatim <c>InstalledPackage.ManifestJson</c> and are surfaced
/// onto the in-memory descriptor at catalog-reload time — no new EF column.
/// </summary>
public static class ManifestAssetPacker
{
    private const string CssKey = "css";
    private const string ScriptsKey = "scripts";
    private const string UsesKey = "uses";

    public static IReadOnlyDictionary<string, string> PackExtra(IdeaManifest m) => new Dictionary<string, string>
    {
        [CssKey] = string.Join('\n', m.Css),
        [ScriptsKey] = string.Join('\n', m.Scripts),
        [UsesKey] = string.Join('\n', m.Uses),
    };

    public static CitizenAssets FromExtra(IReadOnlyDictionary<string, string>? extra)
    {
        if (extra is null) return new([], []);
        return new(Split(extra.GetValueOrDefault(CssKey)), Split(extra.GetValueOrDefault(ScriptsKey)));
    }

    /// <summary>The declared <c>uses[]</c> string ids a (compiled) citizen carries, surfaced from Extra.</summary>
    public static IReadOnlyList<string> UsesFromExtra(IReadOnlyDictionary<string, string>? extra) =>
        extra is null ? [] : Split(extra.GetValueOrDefault(UsesKey));

    private static IReadOnlyList<string> Split(string? joined) =>
        string.IsNullOrEmpty(joined)
            ? []
            : joined.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>Adapters that hand a descriptor's assets to <see cref="PageAssetCollector"/>.</summary>
public static class PageAssets
{
    /// <summary>
    /// The production adapter for THIS slot: a package citizen's manifest css/scripts (surfaced into Extra
    /// at reload) prefixed with the citizen's <see cref="ContentDescriptor.AssetMount"/> so the emitted
    /// &lt;head&gt; URLs resolve under the <c>/_ideas</c> route. Compiled citizens return none here (their
    /// assets live on the instantiated component and require <c>Activator</c> — the attended host TODO).
    /// Pure and additive: extending it to also harvest compiled citizens later changes no caller.
    /// </summary>
    public static CitizenAssets PackageAssetsOf(ContentDescriptor d)
    {
        if (d.Origin != ContentOrigin.Package) return new([], []);
        var raw = ManifestAssetPacker.FromExtra(d.Extra);
        var mount = (d.AssetMount ?? "").TrimEnd('/');
        return new(Mount(raw.Css, mount), Mount(raw.Scripts, mount));
    }

    // Prefix a relative asset path with the package's mount; leave already-absolute (or unmounted) paths as-is.
    private static IReadOnlyList<string> Mount(IReadOnlyList<string> rels, string mount)
    {
        if (mount.Length == 0) return rels;
        var outp = new List<string>(rels.Count);
        foreach (var r in rels)
            outp.Add(r.StartsWith('/') ? r : $"{mount}/{r}");
        return outp;
    }
}
