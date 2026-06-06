using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Services;

/// <summary>
/// Idempotent startup seed (ported pattern from MindAttic.Frontpage): upsert by natural key, never
/// clobber admin-edited content. Seeds the default Site, global CSS, an admin user, and a home Data
/// page that composes the Cyberspace theme + a component include — the zero-deploy render proof.
/// </summary>
public sealed class SeedService(IDbContextFactory<CmsDbContext> dbFactory)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        var site = await db.Sites.FirstOrDefaultAsync(s => s.Key == "default", ct);
        if (site is null)
        {
            site = new Site
            {
                Key = "default", Name = "MindAttic", HostBindings = "",
                DefaultThemeKey = "cyberspace", DefaultThemeVersion = 1, IsDefault = true,
                CreatedUtc = now, ModifiedUtc = now,
            };
            db.Sites.Add(site);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Settings.AnyAsync(s => s.Scope == "Host" && s.Key == "css.global", ct))
        {
            db.Settings.Add(new SettingEntry
            {
                Scope = "Host", Key = "css.global",
                Value = ":root{--ma-ideas-accent:#ff8c00}body{margin:0}",
            });
            await db.SaveChangesAsync(ct);
        }

        // Home page — upsert by (SiteId, Slug=""). Never overwrite an admin-edited body.
        if (!await db.Pages.AnyAsync(p => p.SiteId == site.Id && p.Slug == "", ct))
        {
            db.Pages.Add(new Page
            {
                SiteId = site.Id, Slug = "", Title = "Home",
                ThemeKey = "cyberspace", ThemeVersion = 1,
                Kind = PageKind.Data,
                BodyHtml = HomeBodyHtml,
                PageCss = ".ma-home{padding:2rem;font-family:system-ui}",
                BodyTrust = ContentTrust.Author,
                AuthoredByUserId = "system-seed",
                IsPublished = true, Enabled = true,
                CreatedUtc = now, ModifiedUtc = now,
            });
            await db.SaveChangesAsync(ct);
        }

        // Front page — a compiled Code page rendering the MindAttic.Ideas.Page.Frontpage.V1
        // Idea (the whole-site accordion) through the Cyberspace theme. Upsert by (SiteId, Slug).
        if (!await db.Pages.AnyAsync(p => p.SiteId == site.Id && p.Slug == "frontpage", ct))
        {
            db.Pages.Add(new Page
            {
                SiteId = site.Id, Slug = "frontpage", Title = "MindAttic",
                ThemeKey = "cyberspace", ThemeVersion = 1,
                Kind = PageKind.Code,
                ComponentTypeName = "MindAttic.Ideas.Page.Frontpage.V1",
                AssemblyName = "MindAttic.Ideas.Page.Frontpage",
                IsPublished = true, Enabled = true,
                CreatedUtc = now, ModifiedUtc = now,
            });
            await db.SaveChangesAsync(ct);
        }
    }

    // Proves the full path: free-form HTML rendered through the Cyberspace theme, a Module that
    // switches ON the tooltip capability (so the data-tooltip button works), and a Control that
    // renders an atomic element — all placed by the locked tag form.
    private const string HomeBodyHtml =
        """
        <div class="ma-home">
          <h1>MindAttic.Ideas</h1>
          <p>This page is data — free-form HTML rendered through the Cyberspace theme.</p>

          <p>A <strong>Component</strong> switches on a capability (it loads the tooltip engine), so any
             element with <code>data-tooltip</code> works. No version = latest:</p>
          <MindAttic.Ideas.Plugin.Tooltip />
          <p><button type="button" data-tooltip="Composed from MindAttic.UiUx — latest version.">Hover me</button></p>

          <p>A <strong>Control</strong> is an atomic element placed by tag:</p>
          <p><MindAttic.Ideas.Control.Textbox placeholder="Type here…" /></p>
        </div>
        """;
}
