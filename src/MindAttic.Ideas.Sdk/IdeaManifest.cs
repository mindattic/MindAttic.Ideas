using System.Text.Json.Serialization;

namespace MindAttic.Ideas.Sdk;

/// <summary>
/// The <c>idea.json</c> manifest the packer emits and the host reads to register a package.
/// Mirrors the install-package model in docs/IMPLEMENTATION_PLAN.md §6. JSON keys are camelCase.
/// </summary>
public sealed class IdeaManifest
{
    [JsonPropertyName("key")] public string Key { get; init; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; init; } = "";
    /// <summary>Page | Theme | Component | Control — the ContentKind, inferred from the namespace.</summary>
    [JsonPropertyName("kind")] public string Kind { get; init; } = "";
    /// <summary>Idea-Manager filter/grouping category; defaults to <see cref="Kind"/>.</summary>
    [JsonPropertyName("category")] public string Category { get; init; } = "";
    [JsonPropertyName("version")] public int Version { get; init; }
    /// <summary>Abstractions SDK version this package was built against; the host gates loads on it.</summary>
    [JsonPropertyName("sdk")] public int Sdk { get; init; }
    [JsonPropertyName("entryType")] public string EntryType { get; init; } = "";
    [JsonPropertyName("assemblyName")] public string AssemblyName { get; init; } = "";
    [JsonPropertyName("renderMode")] public string RenderMode { get; init; } = "InteractiveServer";
    [JsonPropertyName("scope")] public string Scope { get; init; } = "Placeable";
    [JsonPropertyName("assets")] public IReadOnlyList<string> Assets { get; init; } = [];
    [JsonPropertyName("dependencies")] public IReadOnlyList<string> Dependencies { get; init; } = [];
}
