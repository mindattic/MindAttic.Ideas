using MindAttic.Ideas.Sdk;

// ma-idea — the .idea packer. Single verb today: `pack`.
//
//   ma-idea pack --assembly <built.dll> --wwwroot <dir> --out <dir> [--icon <file>] [--version <n>]
//
// Reads the entry type's identity (Kind, Key, Version) by CONVENTION from its namespace and
// Vn class name (reflection-only; never executes the assembly), emits idea.json, collects the
// non-host lib/ assemblies, bundles wwwroot/, and zips everything to
// <entryTypeFullName>.idea — e.g. MindAttic.Ideas.Page.MindAtticFrontpage.V1.idea.

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine("""
        ma-idea — MindAttic.Ideas .idea packer

        Usage:
          ma-idea pack --assembly <path> --wwwroot <dir> --out <dir> [--icon <file>] [--version <n>]

        Options:
          --assembly  Path to the built RCL assembly (e.g. bin/Release/net10.0/MyIdea.dll). Required.
          --wwwroot   Path to the RCL's wwwroot static-asset directory. Optional.
          --out       Output directory for the .idea package. Required.
          --icon      Optional icon (png) bundled at the package root.
          --version   Override the convention/attribute version (whole number).
        """);
    return 0;
}

if (!string.Equals(args[0], "pack", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"ma-idea: unknown command '{args[0]}'. Try 'ma-idea --help'.");
    return 2;
}

var opts = ArgParser.Parse(args.AsSpan(1));

string? assembly = opts.GetValueOrDefault("assembly");
string? outDir = opts.GetValueOrDefault("out");
string? wwwroot = opts.GetValueOrDefault("wwwroot");
string? icon = opts.GetValueOrDefault("icon");
int? versionOverride = opts.TryGetValue("version", out var v) && int.TryParse(v, out var vn) ? vn : null;
var refs = (opts.GetValueOrDefault("refs") ?? "")
    .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToList();

if (string.IsNullOrWhiteSpace(assembly) || string.IsNullOrWhiteSpace(outDir))
{
    Console.Error.WriteLine("ma-idea pack: --assembly and --out are required. Try 'ma-idea --help'.");
    return 2;
}

if (!File.Exists(assembly))
{
    Console.Error.WriteLine($"ma-idea pack: assembly not found: {assembly}");
    return 2;
}

try
{
    var path = Packer.Pack(new PackRequest
    {
        AssemblyPath = Path.GetFullPath(assembly),
        WwwrootDir = string.IsNullOrWhiteSpace(wwwroot) ? null : Path.GetFullPath(wwwroot),
        OutputDir = Path.GetFullPath(outDir),
        IconPath = string.IsNullOrWhiteSpace(icon) ? null : Path.GetFullPath(icon),
        VersionOverride = versionOverride,
        ReferenceInputs = refs,
    });
    Console.WriteLine($"ma-idea: packed {Path.GetFileName(path)}");
    return 0;
}
catch (PackException ex)
{
    Console.Error.WriteLine($"ma-idea pack: {ex.Message}");
    return 1;
}
