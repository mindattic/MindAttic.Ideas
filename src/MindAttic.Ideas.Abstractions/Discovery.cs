using Microsoft.AspNetCore.Components;

namespace MindAttic.Ideas.Abstractions;

// ============================================================================================
//  THE DISCOVERY / CATALOG SEAMS — the ONE set of contracts Phase-1 compiled discovery and the
//  Phase-5 runtime .idea loader BOTH implement, so they feed one catalog and one renderer with no
//  rework. APPEND-ONLY records/interfaces (new init props, new default methods only).
// ============================================================================================

/// <summary>
/// The uniform record every source yields for one citizen. Type is resolved LATE (never stored as
/// identity) via <see cref="ITypeResolver"/>, so a stale/renamed type degrades to a placeholder
/// instead of crashing. Identity is (<see cref="Kind"/>, <see cref="Key"/>, <see cref="Version"/>).
/// </summary>
public sealed record ContentDescriptor
{
    public required ContentKind Kind { get; init; }
    public required string Key { get; init; }
    /// <summary>Whole-number version. Coexists with other versions of the same Key.</summary>
    public required int Version { get; init; }
    public required string DisplayName { get; init; }

    public ContentOrigin Origin { get; init; } = ContentOrigin.Compiled;
    /// <summary>Higher wins a (Kind,Key,Version) collision. Compiled=100, Package=50 by convention.</summary>
    public int Priority { get; init; } = 100;
    public RenderStrategy Strategy { get; init; } = RenderStrategy.ClrType;
    public CmsRenderMode RenderMode { get; init; } = CmsRenderMode.InteractiveServer;
    public PlacementScope Scope { get; init; } = PlacementScope.Placeable;
    public string Category { get; init; } = "General";

    /// <summary>For Strategy=ClrType: the resolvable CLR type name. Null/empty =&gt; pure RawMarkup citizen.</summary>
    public string? ClrTypeName { get; init; }
    public string AssemblyName { get; init; } = "";

    /// <summary>For Strategy=RawMarkup: the inline content bundle (e.g. a pure-CDN UiUx component).</summary>
    public RawContentBundle? RawBundle { get; init; }

    /// <summary>Base URL/path under which this citizen's assets are served (opaque to callers).</summary>
    public string? AssetMount { get; init; }

    /// <summary>A package may shadow a compiled key only with this set true PLUS admin confirmation.</summary>
    public bool AllowOverride { get; init; }

    /// <summary>Reserved extensibility bag for additive fields that don't yet warrant a property.</summary>
    public IReadOnlyDictionary<string, string>? Extra { get; init; }
}

/// <summary>Raw author markup for a RawMarkup citizen.</summary>
public sealed record RawContentBundle(string? Html, string? Css, string? Js, bool Trusted = false);

/// <summary>A source of content registrations. Phase 1 ships exactly the compiled-reflection source.</summary>
public interface ICmsContentSource
{
    string Name { get; }
    ContentOrigin Origin { get; }
    int Priority { get; }
    IEnumerable<ContentDescriptor> Discover();
}

/// <summary>
/// The ONE class that absorbs all CLR/ALC/framework churn: resolve a descriptor to a Type.
/// Phase-1 impl uses the default load context; the Phase-5 impl is ALC-aware behind this same
/// interface — no other code learns about assembly loading. Returns null =&gt; render a placeholder.
/// </summary>
public interface ITypeResolver
{
    Type? Resolve(ContentDescriptor descriptor);
}

/// <summary>
/// Serves a runtime package's <c>wwwroot</c> asset. RESERVED — the Phase-1 implementation returns
/// null for everything; the Phase-5 implementation reads from extracted package files.
/// </summary>
public interface IPackageAssetSource
{
    Stream? Open(string key, int version, string path);
}

/// <summary>The unified, queryable catalog of all registered citizens.</summary>
public interface IContentCatalog
{
    IReadOnlyCollection<ContentDescriptor> All { get; }
    /// <summary>Find a specific pinned version.</summary>
    ContentDescriptor? Find(ContentKind kind, string key, int version);
    /// <summary>Find the highest enabled version of a key (when a tag doesn't pin one).</summary>
    ContentDescriptor? FindLatest(ContentKind kind, string key);
    Type? ResolveType(ContentDescriptor descriptor);
}

/// <summary>
/// The SOLE place a <see cref="MarkupString"/> is constructed from author content (analyzer-enforced
/// in Core). Author trust =&gt; raw passthrough; Untrusted =&gt; sanitized.
/// </summary>
public interface IRawContentGate
{
    MarkupString Emit(string? html, ContentTrust trust);
}

/// <summary>
/// The ALC unification linchpin for Phase 5: a per-package collectible AssemblyLoadContext MUST
/// defer these assembly-name prefixes to the host's default context so a package's CmsPageBase
/// unifies by reference identity with the host's. The .idea packer is forbidden from shipping them.
/// </summary>
public static class SharedContracts
{
    public static readonly IReadOnlyList<string> DeferToDefaultPrefixes = new[]
    {
        "MindAttic.Ideas.Abstractions",
        "MindAttic.Ideas.Core",
        "Microsoft.AspNetCore.",
        "Microsoft.Extensions.",
        "Microsoft.EntityFrameworkCore.",
        "Microsoft.JSInterop.",
        "System.",
        "netstandard",
        "mscorlib",
    };
}
