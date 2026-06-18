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

        // Global CSS = cascade tier 0 (CmsHead emits it before any theme/widget/page CSS): a modern reset
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

        // The bare route ("" slug) no longer renders a page — PageHost forwards it to the Frontpage.
        // Migrate a stock seeded home page to soft-disabled (HOUSE-LAW-2: never hard-delete); an
        // admin-edited body is left untouched (and stays reachable should the forward be re-pointed).
        // IgnoreQueryFilters: soft-deleted pages are still visible in the (SiteId,Slug) unique index.
        // Without this, a lookup would return null, the code would INSERT, and EF would throw a
        // DbUpdateException when the unique constraint blocks the duplicate slug.
        var home = await db.Pages.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.SiteId == site.Id && p.Slug == "", ct);
        static string NormEol(string? s) => s?.Replace("\r\n", "\n") ?? "";
        if (home is not null && NormEol(home.BodyHtml) == NormEol(LegacyHomeBodyHtml) && home.Enabled)
        {
            home.Enabled = false;
            home.ModifiedUtc = now;
            await db.SaveChangesAsync(ct);
        }

        // Front page — the mindattic.com look recreated as a DATA page: minimal free-form markup,
        // reusable widgets (Tabs board, Gallery, Footer), layout by plain flex (no layout system),
        // images as inline base64 CSS classes, page CSS at the top and page JS at the bottom.
        var front = await db.Pages.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.SiteId == site.Id && p.Slug == "frontpage", ct);
        if (front is null)
        {
            db.Pages.Add(new Page
            {
                SiteId = site.Id, Slug = "frontpage", Title = "MindAttic",
                ThemeKey = "cyberspace", ThemeVersion = 1,
                Kind = PageKind.Data,
                BodyHtml = FrontpageComponentToken,
                BodyTrust = ContentTrust.Author,
                AuthoredByUserId = "system-seed",
                IsPublished = true, Enabled = true,
                CreatedUtc = now, ModifiedUtc = now,
            });
            await db.SaveChangesAsync(ct);
        }
        else if (front.Kind == PageKind.Code
                 && front.ComponentTypeName is "MindAttic.Ideas.Page.Frontpage.V1"
                                            or "MindAttic.Ideas.Page.MindAtticFrontpage.V1")
        {
            // Both names are recognized stock: older DBs carry the pre-rename compiled type
            // (MindAtticFrontpage); anything else is admin-authored and untouchable.
            // Migrate the stock compiled frontpage in place to the widget-based Data page.
            front.Kind = PageKind.Data;
            front.ComponentTypeName = null;
            front.AssemblyName = null;
            front.BodyHtml = FrontpageComponentToken;
            front.PageCss = null;
            front.PageJs = null;
            front.BodyTrust = ContentTrust.Author;
            front.AuthoredByUserId = "system-seed";
            front.ModifiedUtc = now;
            await db.SaveChangesAsync(ct);
        }
        else if (front.Kind == PageKind.Data && front.BodyHtml == FrontpageBodyHtml)
        {
            // Migrate the stock inline HTML/CSS/JS data page to the self-contained widget token.
            // The widget carries all CSS (.maf-*) and JS (IIFE) internally.
            front.BodyHtml = FrontpageComponentToken;
            front.PageCss = null;
            front.PageJs = null;
            front.ModifiedUtc = now;
            await db.SaveChangesAsync(ct);
        }

        // Personas page — MindAttic.Legion.Frontend collapsed into a Data page (MAI-A22): the
        // persona gallery ships as the LegionPersonas Component .idea, so the standalone Blazor app
        // reduces to one token through the theme. Upsert by (SiteId, Slug); never clobber.
        if (!await db.Pages.IgnoreQueryFilters().AnyAsync(p => p.SiteId == site.Id && p.Slug == "personas", ct))
        {
            db.Pages.Add(new Page
            {
                SiteId = site.Id, Slug = "personas", Title = "Legion Personas",
                ThemeKey = "cyberspace", ThemeVersion = 1,
                Kind = PageKind.Data,
                BodyHtml = PersonasBodyHtml,
                PageCss = PersonasCss,
                BodyTrust = ContentTrust.Author,
                AuthoredByUserId = "system-seed",
                IsPublished = true, Enabled = true,
                CreatedUtc = now, ModifiedUtc = now,
            });
            await db.SaveChangesAsync(ct);
        }

        // Claudia — Pi Zero 2 WH + WonderEcho smart speaker (Hardware theme + Claudia widget).
        if (!await db.Pages.IgnoreQueryFilters().AnyAsync(p => p.SiteId == site.Id && p.Slug == "claudia", ct))
        {
            db.Pages.Add(new Page
            {
                SiteId = site.Id, Slug = "claudia", Title = "Claudia",
                ThemeKey = "hardware", ThemeVersion = 1,
                Kind = PageKind.Data,
                BodyHtml = "{{ MindAttic.Ideas.Component.Claudia }}",
                BodyTrust = ContentTrust.Author,
                AuthoredByUserId = "system-seed",
                IsPublished = true, Enabled = true,
                CreatedUtc = now, ModifiedUtc = now,
            });
            await db.SaveChangesAsync(ct);
        }

        // Ideas brochure — CMS product page explaining the three-primitive model, token grammar,
        // DNN retrospective, and comparison table. Seeded at /ideas via the IdeasBrochure widget.
        if (!await db.Pages.IgnoreQueryFilters().AnyAsync(p => p.SiteId == site.Id && p.Slug == "ideas", ct))
        {
            db.Pages.Add(new Page
            {
                SiteId = site.Id, Slug = "ideas", Title = "MindAttic.Ideas",
                SeoTitle = "MindAttic.Ideas — The CMS That Gets Out of the Way",
                ThemeKey = "cyberspace", ThemeVersion = 1,
                Kind = PageKind.Data,
                BodyHtml = "{{ MindAttic.Ideas.Component.IdeasBrochure }}",
                BodyTrust = ContentTrust.Author,
                AuthoredByUserId = "system-seed",
                IsPublished = true, Enabled = true,
                CreatedUtc = now, ModifiedUtc = now,
            });
            await db.SaveChangesAsync(ct);
        }

        // ChiMesh — solar RAK4631 LoRa / Meshtastic mesh (Hardware theme + ChiMesh widget).
        if (!await db.Pages.IgnoreQueryFilters().AnyAsync(p => p.SiteId == site.Id && p.Slug == "chimesh", ct))
        {
            db.Pages.Add(new Page
            {
                SiteId = site.Id, Slug = "chimesh", Title = "ChiMesh",
                ThemeKey = "hardware", ThemeVersion = 1,
                Kind = PageKind.Data,
                BodyHtml = "{{ MindAttic.Ideas.Component.ChiMesh }}",
                BodyTrust = ContentTrust.Author,
                AuthoredByUserId = "system-seed",
                IsPublished = true, Enabled = true,
                CreatedUtc = now, ModifiedUtc = now,
            });
            await db.SaveChangesAsync(ct);
        }

        // One-time data upgrade: rewrite any data page still using the retired <MindAttic.Ideas.…/> include
        // tags to the {{ … }} token grammar. Idempotent — the filter excludes already-converted bodies, so
        // this is a no-op once the cutover is done. (SQL has no regex, so the rewrite is done here in code.)
        var legacy = await db.Pages
            .Where(p => p.BodyHtml != null && p.BodyHtml.Contains("<MindAttic.Ideas."))
            .ToListAsync(ct);
        var upgraded = 0;
        foreach (var p in legacy)
        {
            var converted = Rendering.IncludeReferenceParser.UpgradeLegacyTags(p.BodyHtml);
            if (!string.Equals(converted, p.BodyHtml, StringComparison.Ordinal))
            {
                p.BodyHtml = converted;
                p.ModifiedUtc = now;
                upgraded++;
            }
        }
        if (upgraded > 0) await db.SaveChangesAsync(ct);
    }

    // The retired stock home body — kept verbatim so the migration above can recognize an untouched
    // seed and soft-disable it; an admin-edited body never matches and is never touched.
    private const string LegacyHomeBodyHtml =
        """
        <div class="ma-home">
          <h1>MindAttic.Ideas</h1>
          <p>This page is data — free-form HTML rendered through the Cyberspace theme.</p>

          <p>A <strong>Plugin</strong> switches on a capability (it loads the tooltip engine), so any
             element with <code>data-tooltip</code> works. No version = latest:</p>
          {{ MindAttic.Ideas.Plugin.Tooltip }}
          <p><button type="button" data-tooltip="Composed from MindAttic.UiUx — latest version.">Hover me</button></p>

          <p>A <strong>Component</strong> is an atomic element placed by token (attributes flow through):</p>
          <p>{{ MindAttic.Ideas.Component.Textbox placeholder="Type here…" }}</p>
        </div>
        """;

    // The self-contained frontpage component token — no PageCss or PageJs needed; the component carries both.
    private const string FrontpageComponentToken = "{{ MindAttic.Ideas.Component.MindAtticFrontpage }}";

    // ── The Frontpage (legacy inline form, kept for migration recognition only) ────────────────────
    // Recognised in the else-if branch above so an existing DB row using this exact body is migrated
    // to the component token in place. Not used for new installs.
    private const string FrontpageBodyHtml =
        """
        {{ MindAttic.Ideas.Component.Tabs }}
        {{ MindAttic.Ideas.Component.Gallery }}
        {{ MindAttic.Ideas.Plugin.Footer }}

        <div class="fp">
          <header class="fp-header">
            <h1 class="fp-wordmark">MindAttic</h1>
          </header>

          <main class="fp-sections">
            <section class="fp-section">
              <h2>Portfolio</h2>
              <nav class="fp-links" aria-label="Portfolio">
                <a href="https://ryandebraal.com" target="_blank" rel="noopener noreferrer">ryandebraal.com</a>
                <a href="https://github.com/mindattic" target="_blank" rel="noopener noreferrer">github.com/mindattic</a>
                <a href="https://mindatticcares.com" target="_blank" rel="noopener noreferrer">MindAttic Cares</a>
              </nav>
            </section>

            <section class="fp-section">
              <h2>Software</h2>
              <div class="ma-tabs ma-tabs-board" data-closed>
                <section data-title="Ciao-ChatGpt-Bonjour-Claude"><p>JavaScript migration tool exporting full ChatGPT conversation history into Claude Projects via the Anthropic API.</p><p><a href="https://github.com/mindattic/Ciao-ChatGpt-Bonjour-Claude" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="Cursory"><p>Repository on GitHub — see source for details.</p><p><a href="https://github.com/mindattic/Cursory" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="GridGame2026"><p>Unity tactical grid RPG — 2D sprites on a 3D board, drag-and-slide hero movement, pincer-attack combat, Grandia-style timeline.</p><p><a href="https://github.com/mindattic/GridGame2026" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="IdiotProof"><p>Automated stock trading platform — .NET Blazor Server + a console Monitor evaluating active strategies 24/7, scripted by the IdiotScript fluent DSL.</p><p><a href="https://github.com/mindattic/IdiotProof" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="MediaButler"><p>Media library organizer — point it at a folder of messy downloads and it renames, tags, and files everything into place.</p><p><a href="https://github.com/mindattic/MediaButler" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="mindatticcares.com"><p>Repository on GitHub — see source for details.</p><p><a href="https://github.com/mindattic/mindatticcares.com" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="ryandebraal.com"><p>Single-file HTML resume — one hand-authored index.htm, vanilla HTML/CSS/JS, no build step, 15 themes.</p><p><a href="https://github.com/mindattic/ryandebraal.com" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="StreetSamurai"><p>Blazor Server app for authoring long-form fiction — SQL Server canon with vector embeddings and a directional entity graph.</p><p><a href="https://github.com/mindattic/StreetSamurai" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="TaxRateCollector"><p>US sales-tax rate database for 14,000+ jurisdictions — Blazor Server, EF Core, SQL Server, each rate linked to its source.</p><p><a href="https://github.com/mindattic/TaxRateCollector" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="ThinkTank"><p>.NET MAUI + Blazor desktop app for multi-LLM conversations across 11 providers.</p><p><a href="https://github.com/mindattic/ThinkTank" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="Tutor"><p>Converts books and documents (PDF, EPUB, DOCX, …) into structured courses via a multi-LLM pipeline.</p><p><a href="https://github.com/mindattic/Tutor" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="MindAttic.Console"><p>C# CLI for MindAttic orchestration.</p><p><a href="https://github.com/mindattic/MindAttic.Console" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="MindAttic.Helpers"><p>Repository on GitHub — see source for details.</p><p><a href="https://github.com/mindattic/MindAttic.Helpers" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="MindAttic.Legion"><p>.NET library + CLI for multi-LLM consensus — one unified client across 11 providers.</p><p><a href="https://github.com/mindattic/MindAttic.Legion" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="MindAttic.Legion.Frontend"><p>Browse Legion's 1024 psychometrically-scored personas, each with a generated abstract-art portrait. Blazor Server.</p><p><a href="https://github.com/mindattic/MindAttic.Legion.Frontend" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="MindAttic.Psst"><p>CLI notifier for long-running commands — on exit it plays a sound and sends an SMS with the result.</p><p><a href="https://github.com/mindattic/MindAttic.Psst" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="MindAttic.UiUx"><p>Shared front-end assets for MindAttic sites — ships the Console Background effects suite (17 animated backgrounds).</p><p><a href="https://github.com/mindattic/MindAttic.UiUx" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="MindAttic.Vault"><p>Credentials and per-app settings — a unified IConfiguration-backed pipeline over User Secrets, env vars, and Azure.</p><p><a href="https://github.com/mindattic/MindAttic.Vault" target="_blank" rel="noopener noreferrer">Open</a></p></section>
              </div>
            </section>

            <section class="fp-section">
              <h2>Hardware</h2>
              <div class="ma-tabs ma-tabs-board" data-closed>
                <section data-title="ChiMesh"><p>Chicago LoRa Meshtastic mesh — three solar-powered RAK4631 nodes in IP65 enclosures proving multi-hop routing.</p><p><a href="https://github.com/mindattic/ChiMesh" target="_blank" rel="noopener noreferrer">Open</a></p></section>
                <section data-title="Claudia"><p>DIY Claude-powered voice assistant on a Raspberry Pi Zero 2 W + PiSugar Whisplay HAT — build guide, parts catalog, installer.</p><p><a href="https://github.com/mindattic/Claudia" target="_blank" rel="noopener noreferrer">Open</a></p></section>
              </div>
            </section>

            <section class="fp-section">
              <h2>Writing</h2>
              <div class="ma-gallery fp-books">
                <a class="ma-gallery-tile img-book-norp" href="https://www.amazon.com/dp/B0G4NS2WZL" target="_blank" rel="noopener noreferrer"><span>Melody Valkyrie: Huntress of Norp</span></a>
                <a class="ma-gallery-tile img-book-vengeance" href="https://www.amazon.com/dp/B0G4NPLWJY" target="_blank" rel="noopener noreferrer"><span>Melody Valkyrie: Harbinger of Vengeance</span></a>
                <a class="ma-gallery-tile img-book-palus" href="https://www.amazon.com/dp/B0G4NPLKW9" target="_blank" rel="noopener noreferrer"><span>Melody Valkyrie: Harvestman of Palus</span></a>
                <a class="ma-gallery-tile img-book-rime" href="https://www.amazon.com/dp/B0GDYXD6H1" target="_blank" rel="noopener noreferrer"><span>The Rime of Aurora Roe</span></a>
                <a class="ma-gallery-tile img-book-harvest" href="https://www.amazon.com/dp/B0GDYXBYFP" target="_blank" rel="noopener noreferrer"><span>Aurora Roe: Harvest Prime</span></a>
                <a class="ma-gallery-tile img-book-returns" href="https://www.amazon.com/dp/B0GX36R57Z" target="_blank" rel="noopener noreferrer"><span>Diminishing Returns</span></a>
              </div>
            </section>

            <section class="fp-section">
              <h2>Visual Arts</h2>
              <div class="ma-gallery fp-books">
                <a class="ma-gallery-tile img-book-mosaic" href="https://ryandebraal.com/mosaic/" target="_blank" rel="noopener noreferrer"><span>Mosaic</span></a>
              </div>
            </section>
          </main>

          <footer class="ma-footer">© <span class="fp-year">2026</span> Ryan DeBraal · MindAttic</footer>
        </div>
        """;


    // ── The Personas page: MindAttic.Legion.Frontend collapsed into one token (MAI-A22) ──────────
    private const string PersonasBodyHtml =
        """
        <main class="personas">
          <h1>Legion Personas</h1>
          <p>Browse MindAttic.Legion's psychometrically-scored personas, each with a generated
             abstract-art portrait — the whole former standalone frontend, as one widget.</p>
          {{ MindAttic.Ideas.Component.LegionPersonas }}
        </main>
        """;

    private const string PersonasCss =
        """
        .personas { display: flex; flex-direction: column; gap: 1rem; max-width: 64rem; margin: 0 auto; padding: 1.5rem; }
        """;

    // A modern CSS reset (Josh Comeau's custom reset) emitted as cascade tier 0 — everything is reset before
    // any theme/widget/page CSS, so there are no browser-default "ghost" styles to fight. The --ma-ideas-accent
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
