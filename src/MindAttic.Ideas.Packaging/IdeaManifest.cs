using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Ideas.Packaging;

/// <summary>
/// The <c>idea.json</c> manifest the packer emits and the host reads to register a package. This is a
/// FROZEN WIRE CONTRACT — once a <c>.idea</c> ships, the kernel field names and meanings cannot change,
/// only grow (forward-compat is preserved by <see cref="Extra"/>, which losslessly round-trips any field
/// a newer packer adds that this host doesn't yet model). Mirrors the install-package model in
/// docs/FOUNDATION_ADR.md §5/Appendix E. JSON keys are camelCase.
/// </summary>
public sealed record IdeaManifest
{
    // ---- Kernel: the six fields every package MUST carry. ----

    /// <summary>Schema version of <c>idea.json</c>. The host refuses a manifest newer than it understands.</summary>
    [JsonPropertyName("manifestVersion")] public int ManifestVersion { get; init; } = 1;

    /// <summary>The <see cref="MindAttic.Ideas.Abstractions.ContentKind"/> name: Page | Plugin | Theme | Control.</summary>
    [JsonPropertyName("category")] public string Category { get; init; } = "";

    /// <summary><c>data</c> (free-form author content, no assembly) or <c>code</c> (a compiled citizen in bin/).</summary>
    [JsonPropertyName("kind")] public string Kind { get; init; } = "";

    [JsonPropertyName("key")] public string Key { get; init; } = "";

    /// <summary>Whole-number content version. Coexists with other versions of the same key.</summary>
    [JsonPropertyName("version")] public int Version { get; init; }

    [JsonPropertyName("displayName")] public string DisplayName { get; init; } = "";

    // ---- Optional metadata (code packages and forward-compat). ----

    [JsonPropertyName("description")] public string Description { get; init; } = "";

    /// <summary>Abstractions SDK version this package was built against; the host gates code loads on it.</summary>
    [JsonPropertyName("sdk")] public int? Sdk { get; init; }

    /// <summary>For <c>code</c>: the resolvable CLR entry type full name. MUST be null for <c>data</c>.</summary>
    [JsonPropertyName("entryType")] public string? EntryType { get; init; }

    [JsonPropertyName("assemblyName")] public string AssemblyName { get; init; } = "";
    [JsonPropertyName("renderMode")] public string RenderMode { get; init; } = "InteractiveServer";
    [JsonPropertyName("scope")] public string Scope { get; init; } = "Placeable";

    /// <summary>Stylesheet asset paths, cascade order preserved (pre-shapes the Phase-5 head dedup).</summary>
    [JsonPropertyName("css")] public IReadOnlyList<string> Css { get; init; } = [];
    /// <summary>Script asset paths, load order preserved.</summary>
    [JsonPropertyName("scripts")] public IReadOnlyList<string> Scripts { get; init; } = [];
    /// <summary>Every static asset bundled under wwwroot/, relative paths preserved.</summary>
    [JsonPropertyName("assets")] public IReadOnlyList<string> Assets { get; init; } = [];
    /// <summary>The non-host assembly simple-names bundled in bin/ (informational; the loader audits bin/ itself).</summary>
    [JsonPropertyName("dependsOn")] public IReadOnlyList<string> DependsOn { get; init; } = [];

    /// <summary>
    /// Citizens this COMPILED page/theme references BY STRING ID at runtime (via <c>CmsInclude</c>) but
    /// does NOT bundle — entries are <c>"&lt;Kind&gt;.&lt;key&gt;[@&lt;version&gt;]"</c>, e.g.
    /// <c>"Plugin.tooltip"</c>, <c>"Control.textbox@1"</c>. The packer derives these from
    /// <c>[Uses]</c>. The host hoists their css/js into <c>&lt;head&gt;</c>, warns if one is not installed,
    /// and reference-guards them against deletion. Omitted version = float to latest.
    /// </summary>
    [JsonPropertyName("uses")] public IReadOnlyList<string> Uses { get; init; } = [];

    /// <summary>A package may shadow a compiled key only with this set true PLUS admin confirmation.</summary>
    [JsonPropertyName("allowOverride")] public bool AllowOverride { get; init; }

    /// <summary>Forward-compat: any field a newer packer emits that this host doesn't model is captured here and re-serialized losslessly.</summary>
    [JsonExtensionData] public IDictionary<string, JsonElement> Extra { get; init; } = new Dictionary<string, JsonElement>();

    // ---- Host limits + the one canonical serializer. ----

    /// <summary>The highest <see cref="ManifestVersion"/> this host build can read.</summary>
    public const int HostMaxManifestVersion = 1;

    /// <summary>The Abstractions SDK version this host build provides; a code package's <see cref="Sdk"/> must be ≤ this.</summary>
    public const int HostSdkVersion = 1;

    /// <summary>The single serializer used for every read and write of an <c>idea.json</c>.</summary>
    public static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
