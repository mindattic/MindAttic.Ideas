using System.IO.Compression;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Portability;
using MindAttic.Media;

namespace MindAttic.Ideas.Blazor.Cli;

/// <summary>
/// CLI mode: <c>--import-content &lt;file&gt;</c>. Applies a bundle produced by
/// <see cref="ExportContentCli"/> to this environment.
/// <para>
/// Argument parsing and console reporting only — the work is
/// <see cref="ContentBundleImporter"/> in Core.
/// </para>
/// Usage: <c>dotnet run --project src/MindAttic.Ideas.Blazor -- --import-content site.ideabundle
/// [--into-site &lt;key&gt;] [--dry-run] [--untrusted] [--prune]</c>
/// </summary>
public static class ImportContentCli
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var source = ArgValue(args, "--import-content");
        if (string.IsNullOrWhiteSpace(source))
        {
            Console.Error.WriteLine("[import-content] No input file given. Usage: --import-content <file.ideabundle> [--into-site <key>] [--dry-run] [--untrusted] [--prune]");
            return 1;
        }
        var inPath = Path.GetFullPath(source);
        if (!File.Exists(inPath))
        {
            Console.Error.WriteLine($"[import-content] File not found: {inPath}");
            return 1;
        }

        ZipArchive zip;
        try
        {
            zip = ZipFile.OpenRead(inPath);
        }
        catch (InvalidDataException ex)
        {
            // A CLI reports a bad file; it does not stack-trace at the operator.
            Console.Error.WriteLine($"[import-content] Not a readable archive: {inPath} ({ex.Message})");
            return 1;
        }
        using var _ = zip;

        var (bundle, manifestError) = await ContentBundleImporter.ReadManifestAsync(zip);
        if (bundle is null)
        {
            Console.Error.WriteLine($"[import-content] {manifestError}");
            return 1;
        }

        var options = new BundleImportOptions
        {
            DryRun = args.Contains("--dry-run"),
            ForceUntrusted = args.Contains("--untrusted"),
            Prune = args.Contains("--prune"),
            IntoSiteKey = ArgValue(args, "--into-site"),
        };

        Console.WriteLine($"[import-content] {Path.GetFileName(inPath)} — exported {bundle.ExportedUtc:u} from {bundle.ExportedFrom ?? "?"}: "
                        + $"{bundle.Pages.Count} page(s), {bundle.Media.Count} media, {bundle.Settings.Count} setting(s).");
        if (options.DryRun) Console.WriteLine("[import-content] DRY RUN — nothing is written.");

        await using var scope = services.CreateAsyncScope();
        var importer = new ContentBundleImporter(
            scope.ServiceProvider.GetRequiredService<CmsDbContext>(),
            scope.ServiceProvider.GetRequiredService<IMediaStore>());

        var result = await importer.ImportAsync(zip, bundle, options,
            (msg, isError) =>
            {
                if (isError) Console.Error.WriteLine($"[import-content] {msg}");
                else Console.WriteLine($"[import-content] {msg}");
            });

        if (result.Error is { Length: > 0 } error)
        {
            Console.Error.WriteLine($"[import-content] {error}");
            return 1;
        }
        return result.Ok ? 0 : 1;
    }

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
    }
}
