using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Media;

namespace MindAttic.Ideas.Blazor.Cli;

/// <summary>
/// CLI mode: <c>--extract-media</c>. Lifts inline <c>data:image/…;base64,…</c> images out of page bodies
/// into the managed media store, rewriting each <c>&lt;img&gt;</c> as
/// <c>&lt;Component.MediaImage uid="…" /&gt;</c>.
/// <para>
/// Base64-inlining was how the hand-authored MindAttic sites shipped images, and it is the single biggest
/// reason a page body runs to hundreds of kilobytes. Once an image is a managed asset the browser can cache
/// it, the same bytes can be shared across pages, and the body is markup again.
/// </para>
/// <para>
/// Identical bytes upload once: assets are keyed by SHA-256, so a logo repeated across ten pages becomes one
/// asset with ten references. Re-runnable — a page with no inline images is left untouched.
/// </para>
/// Usage: <c>dotnet run --project src/MindAttic.Ideas.Blazor -- --extract-media [--slug frontpage]
/// [--folder site] [--dry-run]</c>
/// </summary>
public static partial class ExtractMediaCli
{
    /// <summary>An &lt;img&gt; whose src is an inline base64 data URI. Attribute order is not assumed.</summary>
    [GeneratedRegex(
        """<img\b[^>]*?\bsrc\s*=\s*(?<q>["'])data:(?<mime>image/[a-zA-Z0-9.+-]+);base64,(?<data>[A-Za-z0-9+/=\s]+?)\k<q>[^>]*>""",
        RegexOptions.IgnoreCase)]
    private static partial Regex InlineImage();

    /// <summary>One attribute inside a tag, used to carry alt/title/class/width/height across the rewrite.</summary>
    [GeneratedRegex("""(?<name>[A-Za-z_:][-A-Za-z0-9_:.]*)\s*=\s*(?<q>["'])(?<value>[^"']*)\k<q>""")]
    private static partial Regex TagAttribute();

    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var dryRun = args.Contains("--dry-run");
        var onlySlug = ArgValue(args, "--slug");
        var folder = ArgValue(args, "--folder") ?? "pages";

        if (dryRun) Console.WriteLine("[extract-media] DRY RUN — no uploads, no DB writes.");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var media = scope.ServiceProvider.GetRequiredService<IMediaStore>();

        var query = db.Pages.IgnoreQueryFilters()
            .Where(p => p.Kind == PageKind.Data && p.BodyHtml != null && p.BodyHtml.Contains("base64,"));
        if (!string.IsNullOrWhiteSpace(onlySlug))
            query = query.Where(p => p.Slug == onlySlug);

        var pages = await query.ToListAsync();
        if (pages.Count == 0)
        {
            Console.WriteLine("[extract-media] No pages contain inline base64 images.");
            return 0;
        }

        // Reuse an asset when the same bytes appear again, here or on another page.
        var byHash = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in await media.ListAsync(folder: folder))
            if (!string.IsNullOrEmpty(existing.Sha256))
                byHash[existing.Sha256] = existing.Uid;

        int extracted = 0, reused = 0, failed = 0, pagesChanged = 0;
        long bytesLifted = 0;

        foreach (var page in pages)
        {
            var before = page.BodyHtml!;
            var index = 0;

            var after = await ReplaceAsync(before, InlineImage(), async match =>
            {
                index++;
                byte[] bytes;
                try
                {
                    // Data URIs are often wrapped across lines in an authored file.
                    bytes = Convert.FromBase64String(Regex.Replace(match.Groups["data"].Value, @"\s+", ""));
                }
                catch (FormatException)
                {
                    Console.Error.WriteLine($"[extract-media]   ! {page.Slug} image {index}: unreadable base64 — left inline.");
                    failed++;
                    return match.Value;
                }

                var mime = match.Groups["mime"].Value.ToLowerInvariant();
                var attrs = ReadAttributes(match.Value);
                var alt = attrs.GetValueOrDefault("alt") ?? attrs.GetValueOrDefault("title") ?? "";
                var hash = Convert.ToHexString(SHA256.HashData(bytes));

                Guid uid;
                if (byHash.TryGetValue(hash, out var known))
                {
                    uid = known;
                    reused++;
                }
                else if (dryRun)
                {
                    Console.WriteLine($"[extract-media] [DRY] {page.Slug} image {index}: {bytes.Length / 1024} KB {mime} \"{Truncate(alt)}\"");
                    extracted++;
                    bytesLifted += bytes.Length;
                    return match.Value;
                }
                else
                {
                    var fileName = FileNameFor(alt, page.Slug, index, mime);
                    await using var stream = new MemoryStream(bytes);
                    var item = await media.UploadAsync(stream, fileName, mime, folder: folder, mediaType: "image",
                        notes: $"Extracted from page /{page.Slug}");
                    uid = item.Uid;
                    byHash[hash] = uid;
                    extracted++;
                    bytesLifted += bytes.Length;
                    Console.WriteLine($"[extract-media]   + {fileName} ({bytes.Length / 1024} KB) -> {uid}");
                }

                return BuildTag(uid, alt, attrs);
            });

            if (dryRun || ReferenceEquals(after, before) || after == before) continue;

            page.BodyHtml = after;
            page.ModifiedUtc = DateTime.UtcNow;
            pagesChanged++;
            Console.WriteLine($"[extract-media]   ~ /{page.Slug}: {before.Length:N0} -> {after.Length:N0} chars");
        }

        if (!dryRun && pagesChanged > 0) await db.SaveChangesAsync();

        Console.WriteLine($"[extract-media] Done. pages={pagesChanged} extracted={extracted} reused={reused} " +
                          $"failed={failed} lifted={bytesLifted / 1024:N0} KB");
        return failed > 0 ? 1 : 0;
    }

    /// <summary>Regex.Replace with an async replacement callback (uploading is I/O).</summary>
    private static async Task<string> ReplaceAsync(string input, Regex regex, Func<Match, Task<string>> replacer)
    {
        var sb = new System.Text.StringBuilder();
        var last = 0;
        foreach (Match m in regex.Matches(input))
        {
            sb.Append(input, last, m.Index - last);
            sb.Append(await replacer(m));
            last = m.Index + m.Length;
        }
        sb.Append(input, last, input.Length - last);
        return sb.ToString();
    }

    private static Dictionary<string, string> ReadAttributes(string tag)
    {
        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in TagAttribute().Matches(tag))
        {
            var name = m.Groups["name"].Value;
            if (name.Equals("src", StringComparison.OrdinalIgnoreCase)) continue;   // that is what we are replacing
            attrs[name] = m.Groups["value"].Value;
        }
        return attrs;
    }

    /// <summary>
    /// The replacement tag. Only attributes MediaImage actually understands are carried over — anything else
    /// would be silently dropped by the component, so leaving it out keeps the markup honest.
    /// </summary>
    private static string BuildTag(Guid uid, string alt, Dictionary<string, string> attrs)
    {
        var sb = new System.Text.StringBuilder($"""<Component.MediaImage uid="{uid}" """);
        if (!string.IsNullOrEmpty(alt)) sb.Append($"""alt="{Escape(alt)}" """);
        foreach (var name in new[] { "width", "height", "class" })
        {
            if (!attrs.TryGetValue(name, out var v) || string.IsNullOrWhiteSpace(v)) continue;
            var attr = name == "class" ? "cssClass" : name;
            sb.Append($"""{attr}="{Escape(v)}" """);
        }
        return sb.Append("/>").ToString();
    }

    private static string FileNameFor(string alt, string slug, int index, string mime)
    {
        var stem = string.IsNullOrWhiteSpace(alt) ? $"{slug}-{index}" : alt;
        var safe = new string(stem.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (safe.Contains("--")) safe = safe.Replace("--", "-");
        safe = safe.Trim('-');
        if (safe.Length == 0) safe = $"image-{index}";
        if (safe.Length > 60) safe = safe[..60].TrimEnd('-');
        return safe + ExtensionFor(mime);
    }

    private static string ExtensionFor(string mime) => mime switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        "image/svg+xml" => ".svg",
        "image/avif" => ".avif",
        _ => ".bin",
    };

    private static string Escape(string s) => s.Replace("\"", "&quot;");

    private static string Truncate(string s) => s.Length <= 40 ? s : s[..40] + "…";

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
