using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Web.Cli;

/// <summary>
/// CLI mode: --seed-hyperspace
/// Upserts a top-level Page record for the Hyperspace game (OpenInNewWindow=true) and a
/// ComponentMetadata record that points the FromHtml component at Hyperspace/index.htm.
/// Usage: dotnet run --project src/MindAttic.Ideas.Web -- --seed-hyperspace [--dry-run]
/// </summary>
public static class SeedHyperspaceCli
{
    const string HtmlPath   = @"D:\Projects\MindAttic\Hyperspace\index.htm";
    const string PageSlug   = "hyperspace";
    const string PageTitle  = "Hyperspace";

    static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var dryRun = args.Contains("--dry-run");
        if (dryRun) Console.WriteLine("[seed-hyperspace] DRY RUN — no DB writes.");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var site = await db.Sites.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (site is null) { Console.Error.WriteLine("[seed-hyperspace] No site found. Create a site first."); return 1; }
        var siteId = site.Id;
        Console.WriteLine($"[seed-hyperspace] Using site: {site.Key} (Id={siteId})");

        var htmlExists = File.Exists(HtmlPath);
        if (!htmlExists) Console.WriteLine($"[seed-hyperspace] WARN: HTML file not found at {HtmlPath}");
        var html = htmlExists ? await File.ReadAllTextAsync(HtmlPath) : "";

        var existingPage = await db.Pages.FirstOrDefaultAsync(p => p.SiteId == siteId && p.Slug == PageSlug);
        Guid pageUid;

        if (existingPage is null)
        {
            if (!dryRun)
            {
                var now = DateTime.UtcNow;
                var newPage = new CmsPage
                {
                    SiteId = siteId,
                    Slug = PageSlug, Title = PageTitle,
                    Kind = PageKind.Data,
                    BodyHtml = "{{Component.fromhtml}}",
                    BodyTrust = ContentTrust.Author,
                    IsPublished = true, Enabled = true,
                    OpenInNewWindow = true,
                    CreatedUtc = now, ModifiedUtc = now,
                };
                db.Pages.Add(newPage);
                await db.SaveChangesAsync();
                pageUid = newPage.Uid;
                Console.WriteLine($"[seed-hyperspace] Created page: /{PageSlug}");
            }
            else
            {
                Console.WriteLine($"[seed-hyperspace] [DRY] Would create page: /{PageSlug}");
                return 0;
            }
        }
        else
        {
            pageUid = existingPage.Uid;
            Console.WriteLine($"[seed-hyperspace] Page exists: /{PageSlug}");
        }

        var meta = await db.ComponentMetadata
            .FirstOrDefaultAsync(m => m.PageUid == pageUid && m.ComponentKey == "fromhtml" && m.SlotName == "main");

        var metadataJson = JsonSerializer.Serialize(new
        {
            localSourceFile = HtmlPath,
            htmlContent = html,
            lastSynced = DateTime.UtcNow,
        }, JsonOpts);

        if (meta is null)
        {
            if (!dryRun)
            {
                var now = DateTime.UtcNow;
                db.ComponentMetadata.Add(new ComponentMetadata
                {
                    PageUid = pageUid, ComponentKey = "fromhtml", SlotName = "main",
                    MetadataJson = metadataJson, CreatedUtc = now, ModifiedUtc = now,
                });
                await db.SaveChangesAsync();
                Console.WriteLine($"[seed-hyperspace]   + ComponentMetadata: {HtmlPath}");
            }
            else Console.WriteLine($"[seed-hyperspace] [DRY]   Would create ComponentMetadata: {HtmlPath}");
        }
        else
        {
            if (!dryRun)
            {
                meta.MetadataJson = metadataJson;
                meta.ModifiedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
                Console.WriteLine($"[seed-hyperspace]   ~ Updated ComponentMetadata: {HtmlPath}");
            }
            else Console.WriteLine($"[seed-hyperspace] [DRY]   Would update ComponentMetadata: {HtmlPath}");
        }

        Console.WriteLine("[seed-hyperspace] Done.");
        return 0;
    }
}
