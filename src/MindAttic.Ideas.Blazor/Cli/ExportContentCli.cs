using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Portability;
using MindAttic.Media;

namespace MindAttic.Ideas.Blazor.Cli;

/// <summary>
/// CLI mode: <c>--export-content &lt;file&gt;</c>. Writes every authored page, its settings, its
/// per-component metadata and the media it references into one portable archive.
/// <para>
/// This is the missing half of "upload a .idea and it goes live": a package moves a CITIZEN, this
/// moves what an author BUILT with citizens. A dev database holding a hand-curated site — a composed
/// home page, extracted media, FromMd slots — could previously only reach production by hand, because
/// <c>--seed</c> regenerates shape, not curation.
/// </para>
/// Usage: <c>dotnet run --project src/MindAttic.Ideas.Blazor -- --export-content site.ideabundle
/// [--slug projects/] [--no-media] [--dry-run]</c>
/// </summary>
public static class ExportContentCli
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var dryRun = args.Contains("--dry-run");
        var includeMedia = !args.Contains("--no-media");
        var slugPrefix = ArgValue(args, "--slug");

        var target = ArgValue(args, "--export-content");
        if (string.IsNullOrWhiteSpace(target))
        {
            Console.Error.WriteLine("[export-content] No output file given. Usage: --export-content <file.ideabundle> [--slug prefix] [--no-media] [--dry-run]");
            return 1;
        }
        // `dotnet run --project` runs from the PROJECT directory, so a relative path here is almost
        // never where the caller thinks it is. Resolve it and say where it landed.
        var outPath = Path.GetFullPath(target);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var site = await db.Sites.OrderBy(s => s.IsDefault ? 0 : 1).ThenBy(s => s.Id).FirstOrDefaultAsync();
        var bundle = new ContentBundle
        {
            ExportedUtc = DateTime.UtcNow,
            ExportedFrom = Environment.MachineName,
            Site = site is null ? null : new BundleSite
            {
                Key = site.Key,
                Name = site.Name,
                HostBindings = site.HostBindings,
                DefaultThemeKey = site.DefaultThemeKey,
                DefaultThemeVersion = site.DefaultThemeVersion,
                SettingsJson = site.SettingsJson,
            },
        };

        // Host- and Site-scope settings only: a Page-scope entry keys on a local page id, and a page
        // already carries everything it needs on its own row.
        bundle.Settings = await db.Settings
            .Where(s => s.Scope == "Host" || s.Scope == "Site")
            .OrderBy(s => s.Scope).ThenBy(s => s.Key)
            .Select(s => new BundleSetting { Scope = s.Scope, Key = s.Key, Value = s.Value })
            .ToListAsync();

        var pageQuery = db.Pages.Include(p => p.MetaTags).AsQueryable();
        if (!string.IsNullOrWhiteSpace(slugPrefix))
            pageQuery = pageQuery.Where(p => p.Slug.StartsWith(slugPrefix));

        var pages = await pageQuery.OrderBy(p => p.Slug).ToListAsync();
        var pageIds = pages.Select(p => p.Id).ToHashSet();
        var uidById = pages.ToDictionary(p => p.Id, p => p.Uid);

        var roleAccess = await db.PageRoleAccess.Where(r => pageIds.Contains(r.PageId)).ToListAsync();
        var slugHistory = await db.PageSlugHistory.Where(h => pageIds.Contains(h.PageId)).ToListAsync();

        foreach (var p in pages)
        {
            bundle.Pages.Add(new BundlePage
            {
                Uid = p.Uid,
                Slug = p.Slug,
                Title = p.Title,
                SeoTitle = p.SeoTitle,
                // A parent outside the exported set cannot be expressed, so the child imports as a root.
                ParentUid = p.ParentId is { } pid && uidById.TryGetValue(pid, out var pu) ? pu : null,
                Kind = p.Kind.ToString(),
                BodyHtml = p.BodyHtml,
                PageCss = p.PageCss,
                PageJs = p.PageJs,
                BodyTrust = p.BodyTrust.ToString(),
                ThemeKey = p.ThemeKey,
                ThemeVersion = p.ThemeVersion,
                ActivePluginsJson = p.ActivePluginsJson,
                ComponentTypeName = p.ComponentTypeName,
                AssemblyName = p.AssemblyName,
                SettingsJson = p.SettingsJson,
                IsPublished = p.IsPublished,
                Enabled = p.Enabled,
                IsRestricted = p.IsRestricted,
                OpenInNewWindow = p.OpenInNewWindow,
                SortOrder = p.SortOrder,
                WorkflowState = p.WorkflowState,
                MetaTags = p.MetaTags.ToDictionary(t => t.Name, t => t.Content),
                RoleAccess = roleAccess.Where(r => r.PageId == p.Id).Select(r => r.RoleName).ToList(),
                SlugHistory = slugHistory.Where(h => h.PageId == p.Id)
                    .Select(h => new BundleSlugAlias { OldSlug = h.OldSlug, IsVanity = h.IsVanity })
                    .ToList(),
            });
        }

        var pageUids = pages.Select(p => p.Uid).ToHashSet();
        bundle.ComponentMetadata = (await db.ComponentMetadata.ToListAsync())
            .Where(m => pageUids.Contains(m.PageUid))
            .Select(m => new BundleComponentMetadata
            {
                PageUid = m.PageUid,
                ComponentKey = m.ComponentKey,
                SlotName = m.SlotName,
                MetadataJson = m.MetadataJson,
            })
            .ToList();

        // Media payloads come from the STORE, not the Media table: with the Azure provider the bytes
        // live in a blob and the row carries only a uri.
        var media = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        var items = includeMedia ? await media.ListAsync() : [];

        Console.WriteLine($"[export-content] {bundle.Pages.Count} page(s), {bundle.ComponentMetadata.Count} component metadata row(s), "
                        + $"{bundle.Settings.Count} setting(s), {items.Count} media item(s).");

        if (dryRun)
        {
            Console.WriteLine($"[export-content] DRY RUN — would write {outPath}");
            foreach (var p in bundle.Pages.Take(10)) Console.WriteLine($"[export-content]   page /{p.Slug}");
            if (bundle.Pages.Count > 10) Console.WriteLine($"[export-content]   … and {bundle.Pages.Count - 10} more");
            return 0;
        }

        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await using var file = File.Create(outPath);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);

        var failures = 0;
        foreach (var item in items)
        {
            var entryName = ContentBundle.MediaFolder + item.Uid.ToString("D") + ExtensionFor(item.FileName, item.ContentType);
            try
            {
                var fetched = await media.GetAsync(item.Uid);
                if (fetched is null)
                {
                    Console.Error.WriteLine($"[export-content]   ! {item.FileName}: no payload in the store, skipped");
                    failures++;
                    continue;
                }
                await using var content = fetched.Value.Content;
                var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await content.CopyToAsync(entryStream);

                bundle.Media.Add(new BundleMedia
                {
                    SourceUid = item.Uid,
                    FileName = item.FileName,
                    ContentType = item.ContentType,
                    Folder = item.Folder,
                    MediaType = item.MediaType,
                    SizeBytes = item.SizeBytes,
                    Sha256 = item.Sha256,
                    Width = item.Width,
                    Height = item.Height,
                    Notes = item.Notes,
                    EntryName = entryName,
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[export-content]   ! {item.FileName}: {ex.Message}");
                failures++;
            }
        }

        // The manifest is written LAST so it only ever lists media that actually made it into the archive.
        var manifest = zip.CreateEntry(ContentBundle.ManifestEntryName, CompressionLevel.Optimal);
        await using (var ms = manifest.Open())
            await JsonSerializer.SerializeAsync(ms, bundle, JsonOpts);

        Console.WriteLine($"[export-content] wrote {outPath} ({Describe(new FileInfo(outPath).Length)}, {bundle.Media.Count} media payload(s)).");
        return failures == 0 ? 0 : 1;
    }

    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string ExtensionFor(string fileName, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(ext)) return ext;
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            "video/mp4" => ".mp4",
            "application/pdf" => ".pdf",
            _ => ".bin",
        };
    }

    private static string Describe(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
    };

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
    }
}
