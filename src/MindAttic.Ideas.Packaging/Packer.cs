using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MindAttic.Ideas.Packaging;

public sealed class PackException(string message) : Exception(message);

public sealed class PackRequest
{
    public required string AssemblyPath { get; init; }
    public string? WwwrootDir { get; init; }
    public required string OutputDir { get; init; }
    public string? IconPath { get; init; }
    public int? VersionOverride { get; init; }
    /// <summary>
    /// Extra resolver inputs (files or directories) so the reflection-only context can resolve the
    /// types referenced by the entry assembly's attributes (notably Abstractions, which is host-provided
    /// and never copied beside the build output). Typically the project's reference directories.
    /// </summary>
    public IReadOnlyList<string> ReferenceInputs { get; init; } = [];
}

/// <summary>
/// Turns a built RCL assembly into a <c>.idea</c> package. Reflection-only (MetadataLoadContext) —
/// the target assembly is never executed, only inspected for its convention-named entry type and
/// optional <c>[Idea]</c>/<c>[IdeaSdkVersion]</c> attributes. This packer always produces a
/// <c>kind=code</c> package (it packs a compiled citizen); <c>kind=data</c> packages are author content
/// authored in the CMS, not produced here.
/// </summary>
public static partial class Packer
{
    [GeneratedRegex(@"^V(\d+)$")] private static partial Regex VersionClass();

    public static string Pack(PackRequest req)
    {
        var asmDir = Path.GetDirectoryName(req.AssemblyPath)!;
        using var mlc = CreateLoadContext(asmDir, req.ReferenceInputs);
        var asm = mlc.LoadFromAssemblyPath(req.AssemblyPath);

        var identity = ResolveIdentity(asm, req.VersionOverride);

        // ---- bin/: this assembly + every non-host dependency that sits beside it in the output. ----
        // Host-provided / framework assemblies are NEVER shipped (the package's base types must unify by
        // reference identity with the host's — the SharedContracts.DeferToDefaultPrefixes linchpin).
        var binFiles = Directory.EnumerateFiles(asmDir, "*.dll")
            .Where(f => !IsHostAssembly(Path.GetFileNameWithoutExtension(f)))
            .ToList();
        if (!binFiles.Any(f => string.Equals(Path.GetFullPath(f), req.AssemblyPath, StringComparison.OrdinalIgnoreCase)))
            binFiles.Add(req.AssemblyPath);

        // ---- wwwroot/: every static asset, relative paths preserved. ----
        var assets = new List<string>();
        if (req.WwwrootDir is not null && Directory.Exists(req.WwwrootDir))
        {
            foreach (var f in Directory.EnumerateFiles(req.WwwrootDir, "*", SearchOption.AllDirectories))
                assets.Add(Path.GetRelativePath(req.WwwrootDir, f).Replace('\\', '/'));
            assets.Sort(StringComparer.Ordinal);
        }

        var manifest = new IdeaManifest
        {
            ManifestVersion = 1,
            Category = identity.Kind,         // the ContentKind name (Page | Component | Theme | Control)
            Kind = "code",                    // this packer packs a compiled citizen
            Key = identity.Key,
            Version = identity.Version,
            DisplayName = identity.DisplayName,
            Sdk = identity.SdkVersion,
            EntryType = identity.EntryType,
            AssemblyName = asm.GetName().Name ?? "",
            RenderMode = identity.RenderMode,
            Scope = identity.Scope,
            Assets = assets,
            DependsOn = binFiles
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(n => !string.Equals(n, asm.GetName().Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList(),
        };

        Directory.CreateDirectory(req.OutputDir);
        var outPath = Path.Combine(req.OutputDir, $"{identity.EntryType}.idea");
        if (File.Exists(outPath)) File.Delete(outPath);

        using (var zip = ZipFile.Open(outPath, ZipArchiveMode.Create))
        {
            var json = JsonSerializer.Serialize(manifest, IdeaManifest.ManifestJson);
            WriteEntry(zip, "idea.json", json);

            foreach (var lib in binFiles.Distinct(StringComparer.OrdinalIgnoreCase))
                zip.CreateEntryFromFile(lib, "bin/" + Path.GetFileName(lib));

            if (req.WwwrootDir is not null)
                foreach (var rel in assets)
                    zip.CreateEntryFromFile(Path.Combine(req.WwwrootDir, rel), "wwwroot/" + rel);

            if (req.IconPath is not null && File.Exists(req.IconPath))
                zip.CreateEntryFromFile(req.IconPath, Path.GetFileName(req.IconPath));
        }

        return outPath;
    }

    private record Identity(
        string Key, string DisplayName, string Kind, string Category, int Version,
        int SdkVersion, string EntryType, string RenderMode, string Scope);

    private static Identity ResolveIdentity(Assembly asm, int? versionOverride)
    {
        // Entry type: a type named V<n> in a MindAttic.Ideas.<Kind>.<Key> namespace. We match on
        // name + namespace only — touching IsClass/BaseType here would force resolving framework base
        // types (e.g. Blazor's ComponentBase) that don't sit beside the assembly.
        var candidates = asm.GetTypes()
            .Where(t => (t.Namespace?.StartsWith("MindAttic.Ideas.", StringComparison.Ordinal) ?? false)
                        && VersionClass().IsMatch(t.Name))
            .ToList();

        if (candidates.Count == 0)
            throw new PackException(
                "no entry type found. Expected a public class named V<n> in a " +
                "'MindAttic.Ideas.<Kind>.<Key>' namespace (e.g. MindAttic.Ideas.Page.MindAtticFrontpage.V1).");
        if (candidates.Count > 1)
            throw new PackException(
                "multiple entry types found (" + string.Join(", ", candidates.Select(c => c.FullName)) +
                "). One .idea = one citizen; split them into separate projects.");

        var type = candidates[0];
        var ns = type.Namespace!;                       // MindAttic.Ideas.Page.MindAtticFrontpage
        var segs = ns.Split('.');                       // [MindAttic, Ideas, Page, MindAtticFrontpage, ...]
        if (segs.Length < 4)
            throw new PackException($"namespace '{ns}' is too shallow; expected MindAttic.Ideas.<Kind>.<Key>.");

        var kind = segs[2];                             // Page | Theme | Component | Control
        var key = string.Join('.', segs.Skip(3)).ToLowerInvariant();   // mindatticfrontpage
        var convVersion = int.Parse(VersionClass().Match(type.Name).Groups[1].Value);

        // [Idea] / [IdeaSdkVersion] overrides (read as attribute data — no execution). An attribute
        // whose type can't be resolved is simply skipped; identity falls back to the convention.
        var ideaAttr = type.GetCustomAttributesData()
            .FirstOrDefault(a => SafeAttrName(a) == "IdeaAttribute");
        var sdkAttr = asm.GetCustomAttributesData()
            .FirstOrDefault(a => SafeAttrName(a) == "IdeaSdkVersionAttribute");

        string? overrideKey = NamedOrCtorString(ideaAttr, "Key");
        int attrVersion = NamedInt(ideaAttr, "Version") ?? 0;
        string? displayName = NamedOrCtorString(ideaAttr, "DisplayName");
        string? category = NamedOrCtorString(ideaAttr, "Category");
        string? renderMode = NamedEnumName(ideaAttr, "RenderMode");
        string? scope = NamedEnumName(ideaAttr, "Scope");

        var version = versionOverride ?? (attrVersion > 0 ? attrVersion : convVersion);
        var sdkVersion = sdkAttr is not null && sdkAttr.ConstructorArguments.Count > 0
            ? Convert.ToInt32(sdkAttr.ConstructorArguments[0].Value)
            : 1;

        return new Identity(
            Key: string.IsNullOrWhiteSpace(overrideKey) ? key : overrideKey!.ToLowerInvariant(),
            DisplayName: displayName ?? Humanize(segs[^1]),
            Kind: kind,
            Category: category ?? kind,
            Version: version,
            SdkVersion: sdkVersion,
            EntryType: type.FullName!,
            RenderMode: renderMode ?? "InteractiveServer",
            Scope: scope ?? "Placeable");
    }

    private static string? SafeAttrName(CustomAttributeData a)
    {
        try { return a.AttributeType.Name; }
        catch { return null; }
    }

    /// <summary>
    /// A name the package must NOT ship — it is host-provided/framework and must unify by reference
    /// identity with the host. Single source of truth: Abstractions' <see cref="SharedContracts"/>.
    /// </summary>
    public static bool IsHostAssembly(string name) => ManifestValidator.IsHostAssemblyName(name);

    private static MetadataLoadContext CreateLoadContext(string asmDir, IReadOnlyList<string> referenceInputs)
    {
        // One assembly per simple name — a MetadataLoadContext rejects two files with the same identity
        // (e.g. System.Runtime appears in both a ref pack and the runtime dir). First-wins, in priority
        // order: the target's own dir, then caller refs (ref packs + Abstractions), then the packer's
        // runtime dir (supplies System.Private.CoreLib + any BCL the refs don't carry).
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void AddFile(string f) => byName.TryAdd(Path.GetFileNameWithoutExtension(f), f);
        void AddDir(string dir)
        {
            if (Directory.Exists(dir))
                foreach (var dll in Directory.EnumerateFiles(dir, "*.dll")) AddFile(dll);
        }

        AddDir(asmDir);
        foreach (var input in referenceInputs)
        {
            if (Directory.Exists(input)) AddDir(input);
            else if (File.Exists(input) && input.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) AddFile(input);
        }
        AddDir(Path.GetDirectoryName(typeof(object).Assembly.Location)!);

        return new MetadataLoadContext(new PathAssemblyResolver(byName.Values), "System.Private.CoreLib");
    }

    // ---- CustomAttributeData helpers (named args win; fall back to the first matching ctor arg). ----

    // NOTE: CustomAttributeNamedArgument is a struct; LINQ FirstOrDefault returns a `default` whose
    // members throw NRE. Always iterate and read TypedValue from the matched element only.
    private static object? NamedValue(CustomAttributeData? a, string name)
    {
        if (a is null) return null;
        foreach (var n in a.NamedArguments)
            if (n.MemberName == name)
                return n.TypedValue.Value;
        return null;
    }

    private static string? NamedOrCtorString(CustomAttributeData? a, string name)
        => NamedValue(a, name) is string s && s.Length > 0 ? s : null;

    private static int? NamedInt(CustomAttributeData? a, string name)
        => NamedValue(a, name) is { } v ? Convert.ToInt32(v) : null;

    private static string? NamedEnumName(CustomAttributeData? a, string name)
    {
        if (NamedValue(a, name) is not { } raw) return null;
        // Enum typed values surface as the underlying integer; map known enums by ordinal.
        var ordinal = Convert.ToInt32(raw);
        return name switch
        {
            "RenderMode" => ordinal == 0 ? "Static" : "InteractiveServer",
            "Scope" => ordinal == 1 ? "Global" : "Placeable",
            _ => ordinal.ToString(),
        };
    }

    private static string Humanize(string pascal) =>
        VersionClass().Replace(
            string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString())),
            m => m.Value).Trim();

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var s = new StreamWriter(entry.Open());
        s.Write(content);
    }
}
