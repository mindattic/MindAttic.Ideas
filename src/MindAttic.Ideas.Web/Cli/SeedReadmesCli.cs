using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Web.Cli;

/// <summary>
/// CLI mode: --seed-readmes
/// For every MindAttic project, upserts a Page record (under a "projects" parent) and a
/// ComponentMetadata record that points the FromMd component at its local README.md.
/// Usage: dotnet run --project src/MindAttic.Ideas.Web -- --seed-readmes [--dry-run]
/// </summary>
public static class SeedReadmesCli
{
    record ProjectDef(string Slug, string Title, string ReadmePath);

    static readonly ProjectDef[] Projects =
    [
        // ---- Original 9 ----
        new("ideas",            "MindAttic Ideas",        @"D:\Projects\MindAttic\MindAttic.Ideas\README.md"),
        new("idiotproof",       "IdiotProof",             @"D:\Projects\MindAttic\IdiotProof\README.md"),
        new("vault",            "MindAttic Vault",        @"D:\Projects\MindAttic\MindAttic.Vault\README.md"),
        new("legion",           "MindAttic Legion",       @"D:\Projects\MindAttic\MindAttic.Legion\README.md"),
        new("thinktank",        "ThinkTank",              @"D:\Projects\MindAttic\ThinkTank\README.md"),
        new("tutor",            "Tutor",                  @"D:\Projects\MindAttic\Tutor\README.md"),
        new("taxrate",          "TaxRateCollector",       @"D:\Projects\MindAttic\TaxRateCollector\README.md"),
        new("psst",             "MindAttic Psst",         @"D:\Projects\MindAttic\MindAttic.Psst\README.md"),
        new("streetsamurai",    "StreetSamurai",          @"D:\Projects\MindAttic\StreetSamurai\README.md"),
        // ---- New projects ----
        new("bugoutbag",        "BugOutBag",              @"D:\Projects\MindAttic\BugOutBag\Readme.md"),
        new("chimesh",          "ChiMesh",                @"D:\Projects\MindAttic\ChiMesh\Readme.md"),
        new("claudia",          "Claudia",                @"D:\Projects\MindAttic\Claudia\Readme.md"),
        new("cursory",          "Cursory",                @"D:\Projects\MindAttic\Cursory\Readme.md"),
        new("fractionsofacent", "FractionsOfACent",       @"D:\Projects\MindAttic\FractionsOfACent\Readme.md"),
        new("gridgame2026",     "GridGame 2026",          @"D:\Projects\MindAttic\GridGame2026\Readme.md"),
        new("hyperspace",       "Hyperspace",             @"D:\Projects\MindAttic\Hyperspace\Readme.md"),
        new("mediabutler",      "MediaButler",            @"D:\Projects\MindAttic\MediaButler\Readme.md"),
        new("authentication",   "MindAttic Authentication", @"D:\Projects\MindAttic\MindAttic.Authentication\Readme.md"),
        new("mindattic-com",    "mindattic.com",          @"D:\Projects\MindAttic\mindattic.com\Readme.md"),
        new("deploy",           "MindAttic Deploy",       @"D:\Projects\MindAttic\MindAttic.Deploy\Readme.md"),
        new("helpers",          "MindAttic Helpers",      @"D:\Projects\MindAttic\MindAttic.Helpers\Readme.md"),
        new("launcher",         "MindAttic Launcher",     @"D:\Projects\MindAttic\MindAttic.Launcher\Readme.md"),
        new("mobile",           "MindAttic Mobile",       @"D:\Projects\MindAttic\MindAttic.Mobile\Readme.md"),
        new("uiux",             "MindAttic UiUx",         @"D:\Projects\MindAttic\MindAttic.UiUx\Readme.md"),
        new("mindatticcares-com", "mindatticcares.com",   @"D:\Projects\MindAttic\mindatticcares.com\Readme.md"),
        new("ryandebraal-com",  "ryandebraal.com",        @"D:\Projects\MindAttic\ryandebraal.com\Readme.md"),
        new("skindeep",         "SkinDeep",               @"D:\Projects\MindAttic\SkinDeep\Readme.md"),
    ];

    static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var dryRun = args.Contains("--dry-run");
        if (dryRun) Console.WriteLine("[seed-readmes] DRY RUN — no DB writes.");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var site = await db.Sites.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (site is null) { Console.Error.WriteLine("[seed-readmes] No site found. Create a site first."); return 1; }
        var siteId = site.Id;
        Console.WriteLine($"[seed-readmes] Using site: {site.Key} (Id={siteId})");

        // Ensure the "projects" parent page exists.
        var parentPage = await db.Pages.FirstOrDefaultAsync(p => p.SiteId == siteId && p.Slug == "projects");
        if (parentPage is null)
        {
            if (!dryRun)
            {
                var now = DateTime.UtcNow;
                parentPage = new CmsPage
                {
                    SiteId = siteId, Slug = "projects", Title = "Projects",
                    Kind = PageKind.Data,
                    BodyHtml = "<h1>MindAttic Projects</h1>",
                    BodyTrust = ContentTrust.Author,
                    IsPublished = true, Enabled = true,
                    CreatedUtc = now, ModifiedUtc = now,
                };
                db.Pages.Add(parentPage);
                await db.SaveChangesAsync();
                Console.WriteLine("[seed-readmes] Created parent page: /projects");
            }
            else Console.WriteLine("[seed-readmes] [DRY] Would create parent page: /projects");
        }
        else Console.WriteLine("[seed-readmes] Parent page exists: /projects");

        foreach (var proj in Projects)
        {
            var slug = $"projects/{proj.Slug}";
            var readmeExists = File.Exists(proj.ReadmePath);
            var markdown = readmeExists ? await File.ReadAllTextAsync(proj.ReadmePath) : "";

            if (!readmeExists)
                Console.WriteLine($"[seed-readmes] WARN: README not found at {proj.ReadmePath}");

            var existingPage = await db.Pages.FirstOrDefaultAsync(p => p.SiteId == siteId && p.Slug == slug);
            Guid pageUid;

            if (existingPage is null)
            {
                if (!dryRun)
                {
                    var now = DateTime.UtcNow;
                    var newPage = new CmsPage
                    {
                        SiteId = siteId, ParentId = parentPage?.Id,
                        Slug = slug, Title = proj.Title,
                        Kind = PageKind.Data,
                        BodyHtml = "{{Component.frommd}}",
                        BodyTrust = ContentTrust.Author,
                        IsPublished = true, Enabled = true,
                        CreatedUtc = now, ModifiedUtc = now,
                    };
                    db.Pages.Add(newPage);
                    await db.SaveChangesAsync();
                    pageUid = newPage.Uid;
                    Console.WriteLine($"[seed-readmes] Created page: /{slug} ({proj.Title})");
                }
                else
                {
                    Console.WriteLine($"[seed-readmes] [DRY] Would create page: /{slug} ({proj.Title})");
                    continue;
                }
            }
            else
            {
                pageUid = existingPage.Uid;
                Console.WriteLine($"[seed-readmes] Page exists: /{slug}");
            }

            // Upsert ComponentMetadata for the frommd slot.
            var meta = await db.ComponentMetadata
                .FirstOrDefaultAsync(m => m.PageUid == pageUid && m.ComponentKey == "frommd" && m.SlotName == "main");

            var metadataJson = JsonSerializer.Serialize(new
            {
                localSourceFile = proj.ReadmePath,
                markdown,
                lastSynced = DateTime.UtcNow,
            }, JsonOpts);

            if (meta is null)
            {
                if (!dryRun)
                {
                    var now = DateTime.UtcNow;
                    db.ComponentMetadata.Add(new ComponentMetadata
                    {
                        PageUid = pageUid, ComponentKey = "frommd", SlotName = "main",
                        MetadataJson = metadataJson, CreatedUtc = now, ModifiedUtc = now,
                    });
                    await db.SaveChangesAsync();
                    Console.WriteLine($"[seed-readmes]   + ComponentMetadata: {proj.ReadmePath}");
                }
                else Console.WriteLine($"[seed-readmes] [DRY]   Would create ComponentMetadata: {proj.ReadmePath}");
            }
            else
            {
                if (!dryRun)
                {
                    meta.MetadataJson = metadataJson;
                    meta.ModifiedUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    Console.WriteLine($"[seed-readmes]   ~ Updated ComponentMetadata: {proj.ReadmePath}");
                }
                else Console.WriteLine($"[seed-readmes] [DRY]   Would update ComponentMetadata: {proj.ReadmePath}");
            }
        }

        Console.WriteLine("[seed-readmes] Done.");
        return 0;
    }
}
