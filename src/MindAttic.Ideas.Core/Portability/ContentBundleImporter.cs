using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Media;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Core.Portability;

/// <summary>How an import should behave. The defaults are what <c>--import-content</c> always did.</summary>
public sealed record BundleImportOptions
{
    /// <summary>Report what would happen and write nothing.</summary>
    public bool DryRun { get; init; }

    /// <summary>Force every page to <see cref="ContentTrust.Untrusted"/> — for a bundle that is not yours.</summary>
    public bool ForceUntrusted { get; init; }

    /// <summary>Soft-delete pages on the target site that the bundle does not mention.</summary>
    public bool Prune { get; init; }

    /// <summary>Site key to import into, overriding the one the bundle names. Null uses the bundle's.</summary>
    public string? IntoSiteKey { get; init; }

    /// <summary>
    /// Import into this site id, whatever the bundle says — and never touch the site row itself.
    /// <para>
    /// This is the sandbox-restore door. A showroom's baseline is a bundle exported from a REAL site, so
    /// applying its <c>site</c> block would rename the sandbox, overwrite its host bindings and wipe its
    /// showroom lifecycle flags — turning a routine reset into the loss of the sandbox itself. When this
    /// is set, <see cref="BundleImportOptions.IntoSiteKey"/> is ignored and the site row is left alone.
    /// </para>
    /// </summary>
    public int? IntoSiteId { get; init; }
}

/// <summary>What an import did. Counts are reported even for a dry run.</summary>
public sealed record BundleImportResult(
    bool Ok, string? Error,
    int PagesCreated, int PagesUpdated, int PagesPruned,
    int MediaUploaded, int MediaReused, int MediaFailed,
    int ComponentMetadata, int MediaReferencesRemapped)
{
    public static BundleImportResult Failed(string error) => new(false, error, 0, 0, 0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Applies a <see cref="ContentBundle"/> to this environment. Lifted out of the CLI verb so that the
/// showroom reset restores Day Zero through the SAME path an operator's <c>--import-content</c> takes —
/// a second, parallel restore mechanism is one that is only ever exercised by the thing that breaks.
/// <para>
/// Re-runnable by construction. Pages reconcile on <c>Uid</c> first and <c>(SiteId, Slug)</c> second, both
/// WITHIN the target site; the slug fallback is what lets a bundle land on a database seeded
/// independently, where <c>frontpage</c> already exists under a different uid.
/// </para>
/// <para>
/// Media is uploaded through <see cref="IMediaStore"/>, which mints the uid, so imported items get new
/// ones and every <c>/_media/{uid}</c> reference is rewritten through an old→new map. Identical bytes
/// (matched by SHA-256) are adopted rather than re-uploaded, so a second import moves no payloads.
/// </para>
/// </summary>
public sealed class ContentBundleImporter(CmsDbContext db, IMediaStore media)
{
    /// <summary>Progress lines. The CLI prints them; a background reset logs them.</summary>
    public delegate void Log(string message, bool isError = false);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Read a bundle manifest out of an open archive, or explain why it cannot be read.</summary>
    public static async Task<(ContentBundle? Bundle, string? Error)> ReadManifestAsync(ZipArchive zip)
    {
        var entry = zip.GetEntry(ContentBundle.ManifestEntryName);
        if (entry is null)
            return (null, $"Not a content bundle: no {ContentBundle.ManifestEntryName} inside the archive.");

        ContentBundle? bundle;
        try
        {
            await using var ms = entry.Open();
            bundle = await JsonSerializer.DeserializeAsync<ContentBundle>(ms, JsonOpts);
        }
        catch (JsonException ex) { return (null, $"Bundle manifest could not be read: {ex.Message}"); }

        if (bundle is null) return (null, "Bundle manifest could not be read.");
        if (bundle.FormatVersion > ContentBundle.CurrentFormatVersion)
            return (null, $"Bundle format v{bundle.FormatVersion} is newer than this host understands (v{ContentBundle.CurrentFormatVersion}).");
        return (bundle, null);
    }

    public async Task<BundleImportResult> ImportAsync(
        ZipArchive zip, ContentBundle bundle, BundleImportOptions options, Log log, CancellationToken ct = default)
    {
        var dryRun = options.DryRun;

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
                log($"  ! {bm.FileName}: payload {bm.EntryName} missing from the archive", true);
                mediaFailures++;
                continue;
            }

            if (dryRun)
            {
                log($"  [DRY] would upload {bm.FileName} ({Describe(bm.SizeBytes)})");
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
                log($"  ! {bm.FileName}: {ex.Message}", true);
                mediaFailures++;
            }
        }
        log($"media: {uploaded} uploaded, {reused} already present, {mediaFailures} failed.");

        // ---- 2. site -------------------------------------------------------------------------
        Site? site;
        if (options.IntoSiteId is int pinned)
        {
            // Pinned by the caller (the sandbox restore). The site row is deliberately NOT touched.
            site = await db.Sites.FirstOrDefaultAsync(s => s.Id == pinned, ct);
            if (site is null) return BundleImportResult.Failed($"No site with id {pinned} to import into.");
        }
        else if (bundle.Site is { } bs)
        {
            // Match on the site KEY, and CREATE when it is absent rather than falling back to the
            // default site. Since A35 a deployment can host several domains, so quietly redirecting
            // another site's pages onto the default one would republish them under the wrong domain.
            var targetKey = (options.IntoSiteKey ?? bs.Key).Trim().ToLowerInvariant();
            site = await db.Sites.FirstOrDefaultAsync(s => s.Key == targetKey, ct);
            if (site is null)
            {
                var anySites = await db.Sites.AnyAsync(ct);
                log($"no site keyed \"{targetKey}\" here — creating it"
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
            if (!dryRun) await db.SaveChangesAsync(ct);
        }
        else
        {
            site = await db.Sites.OrderBy(s => s.IsDefault ? 0 : 1).ThenBy(s => s.Id).FirstOrDefaultAsync(ct);
        }
        var siteId = site?.Id;

        // ---- 3. settings (values can carry /_media urls, so they are rewritten too) -----------
        foreach (var s in bundle.Settings)
        {
            var scopeId = s.Scope == "Site" ? siteId : null;
            var row = await db.Settings.FirstOrDefaultAsync(
                x => x.Scope == s.Scope && x.ScopeId == scopeId && x.Key == s.Key, ct);
            var value = Remap(s.Value, uidMap);
            if (row is null)
            {
                if (!dryRun) db.Settings.Add(new SettingEntry { Scope = s.Scope, ScopeId = scopeId, Key = s.Key, Value = value });
            }
            else row.Value = value;
        }
        if (!dryRun) await db.SaveChangesAsync(ct);

        // ---- 4. pages ------------------------------------------------------------------------
        var authorTrusted = bundle.Pages.Count(p => string.Equals(p.BodyTrust, "Author", StringComparison.OrdinalIgnoreCase));
        if (authorTrusted > 0)
        {
            log(options.ForceUntrusted
                ? $"--untrusted: {authorTrusted} Author-trust page(s) will be imported as Untrusted (bodies get sanitized; component tags will NOT render)."
                : $"{authorTrusted} page(s) import with Author trust — their HTML/JS is written VERBATIM and rendered unsanitized. "
                  + "Run with --untrusted if this bundle did not come from you.");
        }

        int created = 0, updated = 0;
        var touched = new Dictionary<Guid, CmsPage>();

        foreach (var bp in bundle.Pages)
        {
            // BOTH lookups are scoped to the target site. Uid is portable and therefore GLOBAL, so an
            // unscoped uid match would adopt another site's page and re-point its SiteId — importing a
            // bundle exported from the main site into a sandbox would MOVE production's pages into the
            // sandbox, and the showroom reset does exactly that import on a timer.
            var page = await db.Pages.IgnoreQueryFilters().Include(p => p.MetaTags)
                           .FirstOrDefaultAsync(p => p.Uid == bp.Uid && p.SiteId == siteId, ct)
                       ?? await db.Pages.IgnoreQueryFilters().Include(p => p.MetaTags)
                           .FirstOrDefaultAsync(p => p.SiteId == siteId && p.Slug == bp.Slug, ct);

            var isNew = page is null;
            if (page is null)
            {
                // A uid is unique across the deployment, so a page landing in a DIFFERENT site than one
                // that already holds this uid needs its own — the bundle's copy, not a move of the original.
                var uidTaken = await db.Pages.IgnoreQueryFilters().AnyAsync(p => p.Uid == bp.Uid, ct);
                page = new CmsPage { Uid = uidTaken ? Guid.NewGuid() : bp.Uid, CreatedUtc = DateTime.UtcNow };
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
            page.BodyTrust = !options.ForceUntrusted
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
                await db.SaveChangesAsync(ct);

                // Meta tags: the bundle is the whole truth for a page, so replace rather than merge.
                var existingTags = await db.PageMetaTags.Where(t => t.PageId == page.Id).ToListAsync(ct);
                db.PageMetaTags.RemoveRange(existingTags);
                foreach (var (name, content) in bp.MetaTags)
                    db.PageMetaTags.Add(new PageMetaTag { PageId = page.Id, Name = name, Content = content });

                var existingRoles = await db.PageRoleAccess.Where(r => r.PageId == page.Id).ToListAsync(ct);
                db.PageRoleAccess.RemoveRange(existingRoles);
                foreach (var role in bp.RoleAccess.Distinct(StringComparer.OrdinalIgnoreCase))
                    db.PageRoleAccess.Add(new PageRoleAccess { PageId = page.Id, RoleName = role });

                foreach (var alias in bp.SlugHistory)
                {
                    var already = await db.PageSlugHistory
                        .AnyAsync(h => h.PageId == page.Id && h.OldSlug == alias.OldSlug, ct);
                    if (!already)
                        db.PageSlugHistory.Add(new PageSlugHistory
                        {
                            PageId = page.Id, OldSlug = alias.OldSlug, IsVanity = alias.IsVanity,
                            CreatedUtc = DateTime.UtcNow,
                        });
                }
                await db.SaveChangesAsync(ct);
            }

            touched[bp.Uid] = page;
            log($"  {(isNew ? "+" : "~")} /{bp.Slug}");
        }

        // ---- 5. parents, once every page has an id ------------------------------------------
        if (!dryRun)
        {
            foreach (var bp in bundle.Pages)
            {
                if (bp.ParentUid is not { } parentUid) continue;
                if (!touched.TryGetValue(bp.Uid, out var child)) continue;
                // Scoped to the site for the same reason the page lookup is: a parent is a page.
                var parent = touched.TryGetValue(parentUid, out var p)
                    ? p
                    : await db.Pages.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(x => x.Uid == parentUid && x.SiteId == siteId, ct);
                child.ParentId = parent?.Id;
            }
            await db.SaveChangesAsync(ct);
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
                x => x.PageUid == pageUid && x.ComponentKey == bm.ComponentKey && x.SlotName == bm.SlotName, ct);
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
        if (!dryRun) await db.SaveChangesAsync(ct);

        // ---- 7. optional prune ---------------------------------------------------------------
        int pruned = 0;
        if (options.Prune)
        {
            var keep = touched.Values.Select(p => p.Id).ToHashSet();
            var strays = await db.Pages.Where(p => p.SiteId == siteId && !keep.Contains(p.Id)).ToListAsync(ct);
            foreach (var stray in strays)
            {
                log($"  - /{stray.Slug} (not in bundle)");
                if (!dryRun) { stray.IsDeleted = true; stray.DeletedUtc = DateTime.UtcNow; }
                pruned++;
            }
            if (!dryRun) await db.SaveChangesAsync(ct);
        }

        log($"pages: {created} created, {updated} updated"
            + (options.Prune ? $", {pruned} soft-deleted" : "")
            + $"; {meta} component metadata row(s); {uidMap.Count} media reference(s) remapped.");

        return new BundleImportResult(
            Ok: mediaFailures == 0, Error: null,
            PagesCreated: created, PagesUpdated: updated, PagesPruned: pruned,
            MediaUploaded: uploaded, MediaReused: reused, MediaFailed: mediaFailures,
            ComponentMetadata: meta, MediaReferencesRemapped: uidMap.Count);
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

    internal static string Describe(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024):F1} MB",
    };
}
