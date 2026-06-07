namespace MindAttic.Ideas.Abstractions;

// ============================================================================================
//  DISCOVERY — by CONVENTION, with an OPTIONAL override attribute.
//  A type becomes content by deriving from a kind base (PageBase/ThemeBase/WidgetBase/ControlBase).
//  Identity (Kind, Key, Version) is inferred:
//    • Kind    = which base it derives from.
//    • Key     = the namespace tail after MindAttic.Ideas.<Kind>.  (e.g. ...Module.Tooltip -> "tooltip")
//    • Version = the Vn class name.                                 (e.g. class V11           -> 11)
//  Use [Idea] only to OVERRIDE the convention (non-conforming names, or to set Scope=Global).
//  APPEND-ONLY: new optional init-only properties may be added; never remove/rename/retype.
// ============================================================================================

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class IdeaAttribute : Attribute
{
    /// <summary>Override the convention-inferred key (stable lowercase dotted id).</summary>
    public string? Key { get; init; }
    /// <summary>Override the convention-inferred whole-number version. 0 = infer from the Vn class name.</summary>
    public int Version { get; init; }
    public string? DisplayName { get; init; }
    public string? Category { get; init; }
    /// <summary>For Module/Control: Global attaches at theme scope; default Placeable.</summary>
    public PlacementScope Scope { get; init; } = PlacementScope.Placeable;
    public CmsRenderMode RenderMode { get; init; } = CmsRenderMode.InteractiveServer;

    public IdeaAttribute() { }
    public IdeaAttribute(string key) => Key = key;
}

/// <summary>
/// Declares a citizen this COMPILED Page/Theme references BY STRING ID at runtime (via
/// <see cref="CmsInclude"/>) but does NOT compile against. Repeatable. The reflection-only packer emits
/// each one into the manifest's <c>uses[]</c>, so the host can (1) hoist the referenced citizen's css/js
/// into <c>&lt;head&gt;</c> for this compiled page, (2) warn at install when a declared dependency is not
/// installed, and (3) reference-guard it against deletion. This is the declarative dependency model
/// (Orchard-style): a page never compile-references another package, it names what it uses.
/// <paramref name="version"/> 0 = float to the latest enabled version.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class UsesAttribute(ContentKind kind, string key, int version = 0) : Attribute
{
    public ContentKind Kind { get; } = kind;
    public string Key { get; } = key;
    /// <summary>Pinned whole-number version; 0 = float to latest.</summary>
    public int Version { get; } = version;
}

/// <summary>
/// Stamped on a content assembly by the SDK packer; the host reads it to gate package loads against
/// <see cref="Sdk.Version"/>. Whole-number SDK version (no SemVer).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public sealed class IdeaSdkVersionAttribute(int version) : Attribute
{
    public int Version { get; } = version;
}

/// <summary>The frozen Abstractions SDK version. MAJOR pinned at 1 forever; additive-only.</summary>
public static class Sdk
{
    /// <summary>Whole-number SDK version a host advertises and packages gate against.</summary>
    public const int Version = 1;
}
