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

        // Global CSS = cascade tier 0 (CmsHead emits it before any theme/plugin/page CSS): a modern reset
        // so every page starts from a clean slate with no browser-default "ghost" styling. Insert on a fresh
        // DB; migrate the old stock default in place; never clobber an admin-customized value.
        var globalCss = await db.Settings.FirstOrDefaultAsync(s => s.Scope == "Host" && s.Key == "css.global", ct);
        if (globalCss is null)
        {
            db.Settings.Add(new SettingEntry { Scope = "Host", Key = "css.global", Value = GlobalCssReset });
            await db.SaveChangesAsync(ct);
        }
        else if (globalCss.Value == LegacyGlobalCss)
        {
            globalCss.Value = GlobalCssReset;
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

          <p>A <strong>Plugin</strong> switches on a capability (it loads the tooltip engine), so any
             element with <code>data-tooltip</code> works. No version = latest:</p>
          {{ MindAttic.Ideas.Plugin.Tooltip }}
          <p><button type="button" data-tooltip="Composed from MindAttic.UiUx — latest version.">Hover me</button></p>

          <p>A <strong>Control</strong> is an atomic element placed by token (attributes flow through):</p>
          <p>{{ MindAttic.Ideas.Control.Textbox placeholder="Type here…" }}</p>
        </div>
        """;

    // A modern CSS reset (Josh Comeau's custom reset) emitted as cascade tier 0 — everything is reset before
    // any theme/plugin/page CSS, so there are no browser-default "ghost" styles to fight. The --ma-ideas-accent
    // token rides along. Adjust in the admin global-CSS setting; this is just the seeded default.
    private const string GlobalCssReset =
        """
        /*
          Josh's Custom CSS Reset
          https://www.joshwcomeau.com/css/custom-css-reset/
        */

        *, *::before, *::after {
          box-sizing: border-box;
        }

        *:not(dialog) {
          margin: 0;
        }

        @media (prefers-reduced-motion: no-preference) {
          html {
            interpolate-size: allow-keywords;
          }
        }

        body {
          line-height: 1.5;
          -webkit-font-smoothing: antialiased;
        }

        img, picture, video, canvas, svg {
          display: block;
          max-width: 100%;
        }

        input, button, textarea, select {
          font: inherit;
        }

        p, h1, h2, h3, h4, h5, h6 {
          overflow-wrap: break-word;
        }

        p {
          text-wrap: pretty;
        }
        h1, h2, h3, h4, h5, h6 {
          text-wrap: balance;
        }

        #root, #__next {
          isolation: isolate;
        }

        /* MindAttic theme token (kept across the reset). */
        :root { --ma-ideas-accent: #ff8c00; }
        """;

    // The previous stock default — recognized so a fresh-install DB migrates to the reset above, while a
    // value an admin has since edited is left untouched.
    private const string LegacyGlobalCss = ":root{--ma-ideas-accent:#ff8c00}body{margin:0}";
}
