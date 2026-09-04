using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Portability;
using MindAttic.Media;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Blazor.Cli;

/// <summary>
/// CLI mode: <c>--import-content &lt;file&gt;</c>. Applies a bundle produced by
/// <see cref="ExportContentCli"/> to this environment.
/// <para>
/// Re-runnable by construction. Pages reconcile on <c>Uid</c> first and <c>(SiteId, Slug)</c> second —
/// the slug fallback is what lets a bundle land on a database that was seeded independently, where
/// <c>frontpage</c> already exists under a different uid. Without it the first import would hit the
/// unique <c>(SiteId, Slug)</c> index instead of updating the page the operator meant.
/// </para>
/// <para>
/// Media is uploaded through <see cref="IMediaStore"/>, which mints the uid, so imported items get new
/// ones and every <c>/_media/{uid}</c> reference is rewritten through an old→new map. Identical bytes
/// (matched by SHA-256) are adopted rather than re-uploaded, so a second import moves no payloads.
/// </para>
/// Usage: <c>dotnet run --project src/MindAttic.Ideas.Blazor -- --import-content site.ideabundle
/// [--dry-run] [--untrusted] [--prune]</c>
/// </summary>
public static class ImportContentCli
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var dryRun = args.Contains("--dry-run");
        var forceUntrusted = args.Contains("--untrusted");
        var prune = args.Contains("--prune");

        var source = ArgValue(args, "--import-content");
        if (string.IsNullOrWhiteSpace(source))
        {
            Console.Error.WriteLine("[import-content] No input file given. Usage: --import-content <file.ideabundle> [--dry-run] [--untrusted] [--prune]");
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
        var manifestEntry = zip.GetEntry(ContentBundle.ManifestEntryName);
        if (manifestEntry is null)
        {
            Console.Error.WriteLine($"[import-content] Not a content bundle: no {ContentBundle.ManifestEntryName} inside {inPath}");
            return 1;
        }

        ContentBundle? bundle;
        await using (var ms = manifestEntry.Open())
            bundle = await JsonSerializer.DeserializeAsync<ContentBundle>(ms, ExportContentCli.JsonOpts);

        if (bundle is null)
        {
            Console.Error.WriteLine("[import-content] Bundle manifest could not be read.");
            return 1;
        }
        if (bundle.FormatVersion > ContentBundle.CurrentFormatVersion)
        {
            Console.Error.WriteLine($"[import-content] Bundle format v{bundle.FormatVersion} is newer than this host understands (v{ContentBundle.CurrentFormatVersion}).");
            return 1;
        }

        Console.WriteLine($"[import-content] {Path.GetFileName(inPath)} — exported {bundle.ExportedUtc:u} from {bundle.ExportedFrom ?? "?"}: "
                        + $"{bundle.Pages.Count} page(s), {bundle.Media.Count} media, {bundle.Settings.Count} setting(s).");
        if (dryRun) Console.WriteLine("[import-content] DRY RUN — nothing is written.");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var media = scope.ServiceProvider.GetRequiredService<IMediaStore>();

        // ---- 1. media, and the uid map every later rewrite depends on -------------------------
        var uidMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var existingByHash = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in await media.ListAsync())
            if (!string.IsNullOrEmpty(m.Sha256))
                existingByHash.TryAdd(m.Sha256, m.Uid);

        int uploaded = 0, reused = 0, mediaFailures = 0;
        foreach (var bm in bundle.Media)
        {
            if (!string.IsNullOrEmpty(bm.Sha256) && existingByHash.TryGetValue(bm.Sha256, out var already))
            {
                if (already != bm.SourceUid) uidMap[bm.SourceUid.ToString("D")] = already.ToString("D");
                reused++;
                continue;
            }

            var entry = zip.GetEntry(bm.EntryName);
            if (entry is null)
            {
                Console.Error.WriteLine($"[import-content]   ! {bm.FileName}: payload {bm.EntryName} missing from the archive");
                mediaFailures++;
                continue;
            }

            if (dryRun)
            {
                Console.WriteLine($"[import-content]   [DRY] would upload {bm.FileName} ({Describe(bm.SizeBytes)})");
                uploaded++;
                continue;
            }

            try
            {
                await using var payload = entry.Open();
                var item = await media.UploadAsync(payload, bm.FileName, bm.ContentType,
                    folder: bm.Folder, mediaType: bm.MediaType,
                    width: bm.Width, height: bm.Height, notes: bm.Notes);
                if (item.Uid != bm.SourceUid) uidMap[bm.SourceUid.ToString("D")] = item.Uid.ToString("D");
                if (!string.IsNullOrEmpty(item.Sha256)) existingByHash.TryAdd(item.Sha256, item.Uid);
                uploaded++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[import-content]   ! {bm.FileName}: {ex.Message}");
                mediaFailures++;
            }
        }
        Console.WriteLine($"[import-content] media: {uploaded} uploaded, {reused} already present, {mediaFailures} failed.");

        // ---- 2. site -------------------------------------------------------------------------
        Site? site = null;
        if (bundle.Site is { } bs)
        {
            // Match on the site KEY, and CREATE when it is absent rather than falling back to the
            // default site. Since A35 a deployment can host several domains, so quietly redirecting
            // another site's pages onto the default one would republish them under the wrong domain.
            // --into-site overrides the target when that is genuinely what you mean.
            var targetKey = (ArgValue(args, "--into-site") ?? bs.Key).Trim().ToLowerInvariant();
            site = await db.Sites.FirstOrDefaultAsync(s => s.Key == targetKey);
            if (site is null)
            {
                var anySites = await db.Sites.AnyAsync();
                Console.WriteLine($"[import-content] no site keyed \"{targetKey}\" here — creating it"
                                + (anySites ? " (it will NOT become the default)." : " as the default site."));
                site = new Site { Key = targetKey, IsDefault = !anySites, CreatedUtc = DateTime.UtcNow };
                if (!dryRun) db.Sites.Add(site);
            }
            site.Name = bs.Name;
            site.HostBindings = bs.HostBindings;
            site.DefaultThemeKey = bs.DefaultThemeKey;
            site.DefaultThemeVersion = bs.DefaultThemeVersion;
            site.SettingsJson = bs.SettingsJson;
            site.ModifiedUtc = DateTime.UtcNow;
            if (!dryRun) await db.SaveChangesAsync();
        }
        else
        {
            site = await db.Sites.OrderBy(s => s.IsDefault ? 0 : 1).ThenBy(s => s.Id).FirstOrDefaultAsync();
        }
        var siteId = site?.Id;

        // ---- 3. settings (values can carry /_media urls, so they are rewritten too) -----------
        foreach (var s in bundle.Settings)
        {
            var scopeId = s.Scope == "Site" ? siteId : null;
            var row = await db.Settings.FirstOrDefaultAsync(
                x => x.Scope == s.Scope && x.ScopeId == scopeId && x.Key == s.Key);
            var value = Remap(s.Value, uidMap);
            if (row is null)
            {
                if (!dryRun) db.Settings.Add(new SettingEntry { Scope = s.Scope, ScopeId = scopeId, Key = s.Key, Value = value });
            }
            else row.Value = value;
        }
        if (!dryRun) await db.SaveChangesAsync();

        // ---- 4. pages ------------------------------------------------------------------------
        var authorTrusted = bundle.Pages.Count(p => string.Equals(p.BodyTrust, "Author", StringComparison.OrdinalIgnoreCase));
        if (authorTrusted > 0)
        {
            Console.WriteLine(forceUntrusted
                ? $"[import-content] --untrusted: {authorTrusted} Author-trust page(s) will be imported as Untrusted (bodies get sanitized; component tags will NOT render)."
                : $"[import-content] {authorTrusted} page(s) import with Author trust — their HTML/JS is written VERBATIM and rendered unsanitized. "
                + "Run with --untrusted if this bundle did not come from you.");
        }

        int created = 0, updated = 0;
        var touched = new Dictionary<Guid, CmsPage>();

        foreach (var bp in bundle.Pages)
        {
            // Uid first (the portable identity), then slug — the slug fallback is what lets a bundle
            // adopt an independently seeded row instead of colliding with it.
            var page = await db.Pages.IgnoreQueryFilters().Include(p => p.MetaTags)
                           .FirstOrDefaultAsync(p => p.Uid == bp.Uid)
                       ?? await db.Pages.IgnoreQueryFilters().Include(p => p.MetaTags)
                           .FirstOrDefaultAsync(p => p.SiteId == siteId && p.Slug == bp.Slug);

            var isNew = page is null;
            if (page is null)
            {
                page = new CmsPage { Uid = bp.Uid, CreatedUtc = DateTime.UtcNow };
                if (!dryRun) db.Pages.Add(page);
                created++;
            }
            else updated++;

            page.SiteId = siteId;
            page.Slug = bp.Slug;
            page.Title = bp.Title;
            page.SeoTitle = bp.SeoTitle;
            page.Kind = Enum.TryParse<PageKind>(bp.Kind, ignoreCase: true, out var k) ? k : PageKind.Data;
            page.BodyHtml = Remap(bp.BodyHtml, uidMap);
            page.PageCss = Remap(bp.PageCss, uidMap);
            page.PageJs = Remap(bp.PageJs, uidMap);
            page.BodyTrust = !forceUntrusted
                             && string.Equals(bp.BodyTrust, "Author", StringComparison.OrdinalIgnoreCase)
                ? ContentTrust.Author
                : ContentTrust.Untrusted;
            if (page.AuthorTrustVersion == 0) page.AuthorTrustVersion = 1;
            page.ThemeKey = bp.ThemeKey;
            page.ThemeVersion = bp.ThemeVersion;
            page.ActivePluginsJson = bp.ActivePluginsJson;
            page.ComponentTypeName = bp.ComponentTypeName;
            page.AssemblyName = bp.AssemblyName;
            page.SettingsJson = bp.SettingsJson;
            page.IsPublished = bp.IsPublished;
            page.Enabled = bp.Enabled;
            page.IsRestricted = bp.IsRestricted;
            page.OpenInNewWindow = bp.OpenInNewWindow;
            page.SortOrder = bp.SortOrder;
            page.WorkflowState = bp.WorkflowState;
            page.ModifiedUtc = DateTime.UtcNow;
            // A bundle is an authoritative statement about the page, so a row that was soft-deleted
            // here comes back rather than staying invisible under a live slug.
            page.IsDeleted = false;
            page.DeletedUtc = null;

            if (!dryRun)
            {
                await db.SaveChangesAsync();

                // Meta tags: the bundle is the whole truth for a page, so replace rather than merge.
                var existingTags = await db.PageMetaTags.Where(t => t.PageId == page.Id).ToListAsync();
                db.PageMetaTags.RemoveRange(existingTags);
                foreach (var (name, content) in bp.MetaTags)
                    db.PageMetaTags.Add(new PageMetaTag { PageId = page.Id, Name = name, Content = content });

                var existingRoles = await db.PageRoleAccess.Where(r => r.PageId == page.Id).ToListAsync();
                db.PageRoleAccess.RemoveRange(existingRoles);
                foreach (var role in bp.RoleAccess.Distinct(StringComparer.OrdinalIgnoreCase))
                    db.PageRoleAccess.Add(new PageRoleAccess { PageId = page.Id, RoleName = role });

                foreach (var alias in bp.SlugHistory)
                {
                    var already = await db.PageSlugHistory
                        .AnyAsync(h => h.PageId == page.Id && h.OldSlug == alias.OldSlug);
                    if (!already)
                        db.PageSlugHistory.Add(new PageSlugHistory
                        {
                            PageId = page.Id, OldSlug = alias.OldSlug, IsVanity = alias.IsVanity,
                            CreatedUtc = DateTime.UtcNow,
                        });
                }
                await db.SaveChangesAsync();
            }

            touched[bp.Uid] = page;
            Console.WriteLine($"[import-content]   {(isNew ? "+" : "~")} /{bp.Slug}");
        }

        // ---- 5. parents, once every page has an id ------------------------------------------
        if (!dryRun)
        {
            foreach (var bp in bundle.Pages)
            {
                if (bp.ParentUid is not { } parentUid) continue;
                if (!touched.TryGetValue(bp.Uid, out var child)) continue;
                var parent = touched.TryGetValue(parentUid, out var p)
                    ? p
                    : await db.Pages.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Uid == parentUid);
                child.ParentId = parent?.Id;
            }
            await db.SaveChangesAsync();
        }

        // ---- 6. per-component metadata -------------------------------------------------------
        int meta = 0;
        foreach (var bm in bundle.ComponentMetadata)
        {
            // The bundle's page uid may have been adopted onto a row that already had a different one;
            // follow the page we actually wrote.
            var pageUid = touched.TryGetValue(bm.PageUid, out var pg) ? pg.Uid : bm.PageUid;
            if (dryRun) { meta++; continue; }

            var row = await db.ComponentMetadata.FirstOrDefaultAsync(
                x => x.PageUid == pageUid && x.ComponentKey == bm.ComponentKey && x.SlotName == bm.SlotName);
            if (row is null)
            {
                db.ComponentMetadata.Add(new ComponentMetadata
                {
                    PageUid = pageUid, ComponentKey = bm.ComponentKey, SlotName = bm.SlotName,
                    MetadataJson = Remap(bm.MetadataJson, uidMap) ?? "{}",
                    CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow,
                });
            }
            else
            {
                row.MetadataJson = Remap(bm.MetadataJson, uidMap) ?? "{}";
                row.ModifiedUtc = DateTime.UtcNow;
            }
            meta++;
        }
        if (!dryRun) await db.SaveChangesAsync();

        // ---- 7. optional prune ---------------------------------------------------------------
        int pruned = 0;
        if (prune)
        {
            var keep = touched.Values.Select(p => p.Id).ToHashSet();
            var strays = await db.Pages.Where(p => p.SiteId == siteId && !keep.Contains(p.Id)).ToListAsync();
            foreach (var stray in strays)
            {
                Console.WriteLine($"[import-content]   - /{stray.Slug} (not in bundle)");
                if (!dryRun) { stray.IsDeleted = true; stray.DeletedUtc = DateTime.UtcNow; }
                pruned++;
            }
            if (!dryRun) await db.SaveChangesAsync();
        }

        Console.WriteLine($"[import-content] pages: {created} created, {updated} updated"
                        + (prune ? $", {pruned} soft-deleted" : "")
                        + $"; {meta} component metadata row(s); {uidMap.Count} media reference(s) remapped.");
        return mediaFailures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Rewrites every media uid the import remapped. Uids are hyphenated GUIDs, so a plain substring
    /// swap is unambiguous and catches all three shapes at once: <c>/_media/{uid}</c>,
    /// <c>&lt;Component.MediaImage uid="{uid}"&gt;</c>, and a uid inside a component's metadata JSON.
    /// </summary>
    internal static string? Remap(string? text, IReadOnlyDictionary<string, string> uidMap)
    {
        if (string.IsNullOrEmpty(text) || uidMap.Count == 0) return text;
        foreach (var (oldUid, newUid) in uidMap)
            text = text.Replace(oldUid, newUid, StringComparison.OrdinalIgnoreCase);
        return text;
    }

    private static string Describe(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024):F1} MB",
    };

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
    }
}
