# MindAttic.Ideas

A single-deployment Blazor CMS for the MindAttic ecosystem. **One** Azure App Service, **one** app
pool, **one** database hosts *many* pages — so a project like `MindAttic.Frontpage` or
`MindAttic.Legion.Frontend` no longer needs a whole web app just to serve essentially one page.

You ship capability by **uploading or CLI'ing a `.idea` file** (a plain zip). The CMS reads whether
it's a **Page**, **Plugin**, **Component**, or **Theme**, registers it, and it's live — no redeploy,
no app-pool restart. Plugins, Components, and Themes are **globally available**, so any Page composes
them by dropping a tag:

```razor
<MindAttic.Ideas.Theme.Cyberspace.V1 />
<MindAttic.Ideas.Plugin.Tooltip />        @* no version = latest *@
<MindAttic.Ideas.Component.Textbox.V1 />
```

> **Status:** Phase 0/1 foundation **built and verified end-to-end**. This README is the **living
> feature spec** — every feature is marked ✅ done · 🔨 building · 📋 planned, kept in lockstep with the
> code so the documentation is feature-complete the moment the code is.
>
> **Canonical contracts:** [`docs/BIBLE.md`](docs/BIBLE.md) + [`docs/AMENDMENTS.md`](docs/AMENDMENTS.md)
> are the current source of truth for the foundation (A1..A24). [`docs/FOUNDATION_ADR.md`](docs/FOUNDATION_ADR.md) is the
> Legion deliberation that produced it (its *vocabulary* is superseded by the amendments — see the
> banner at its top). [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) is the original brief.

---

## The one mental model

**A Page is free-form. A Theme wraps it. Plugins and Components drop into it. Inline JS/CSS/HTML is yours.**

There are **no zones, panes, slots, or grids** — DotNetNuke's clunky fixed-layout model is explicitly
rejected. You author a page however you like and place content exactly where you want it in your markup.
The unit you install is a `.idea` zip; the things it contains are one of four **content kinds**.

| Kind | What it is | Base type | Example tag |
|---|---|---|---|
| **Page** | A free-form page (inline HTML/CSS/JS) that picks a Theme and drops tags | `PageBase` | `MindAttic.Ideas.Page.LegionFrontpage.V2` |
| **Plugin** | A site-wide capability activator (loads js/css across the whole page; selected in Page Properties) | `PluginBase` | `MindAttic.Ideas.Plugin.Tooltip.V1` |
| **Component** | An inline-placed UI unit dropped at a `{{Component.X}}` token position | `ComponentBase` | `MindAttic.Ideas.Component.Textbox.V1` |
| **Theme** | A layout (chrome + one `@Body` hole) + a CSS bundle | `ThemeBase` | `MindAttic.Ideas.Theme.Cyberspace.V4` |

All three derive from a shared root, **`IdeaBase`**. "Idea" names that shared base and the `.idea`
package format (and the `/_ideas/...` asset route) — it is **never** a content kind. New kinds can be
**appended** to the set later without breaking anything.

> **Plugin vs Component.** A Plugin is a site-wide *capability activator* (dropping
> `<MindAttic.Ideas.Plugin.Tooltip />` loads the tooltip engine so that thereafter **any** element with
> `data-tooltip`/`data-tt` shows a tooltip on hover — renders no markup of its own). A Component is
> inline-placed at a specific `{{Component.X}}` token position and renders actual markup (e.g.
> `MindAttic.Ideas.Component.Textbox` renders an `<input>`). Both can nest other citizens via `[Uses]`.

### Two ways to author a Page — one render path
- **Data page** (zero deploy): free-form `BodyHtml` / `PageCss` / `PageJs` stored in the DB. Interactivity
  comes from **your inline JS**. Include tags are expanded into live content. *This is the primary path.*
- **Code page** (compiled): a `[CmsPage]`-style `PageBase` subclass for when you genuinely need Blazor C#
  interactivity. Deploys once per *type*, never per page.

Both are first-class `Page` rows resolved by `(SiteId, Slug)` and rendered through the **same** primitive
(`DynamicComponent` / the built-in `FreeFormPage`). A page can **graduate Data ↔ Code as a row edit** —
never a schema change.

---

## The tag convention (locked)

```
<MindAttic.Ideas.{ContentKind}.{Name}.{Version} />
```

- **`{ContentKind}`** — `Page` · `Plugin` · `Component` · `Theme`.
- **`{Name}`** — the content's name (`Cyberspace`, `Tooltip`, `TabControl`, …).
- **`{Version}`** — **optional**, uppercase `V{n}`:
  - omitted → **latest** enabled version &nbsp;·&nbsp; `.Latest` → **latest** (explicit) &nbsp;·&nbsp; `.V3` → **pins** version 3.
- The **same tag works in data pages and code pages.** In a data page the include expander resolves it
  (case-insensitively); in a compiled code page it's a real Blazor component tag (matches `V{n}` exactly).

Identity is inferred by **convention** — Kind from the base type, Name (key) from the namespace tail,
Version from the `V{n}` class name — so no attributes are needed in the normal case. An optional
`[Idea(key:…, version:…, scope:Global)]` overrides the convention when a name can't follow it.

---

## Versioning — whole numbers, optional pinning ✅ *(model)* / 📋 *(lifecycle)*

Versions are **whole numbers only** (`V1`, `V2`, `V3`) — never SemVer like `1.5.11`. Same scheme for
every kind, so it's always obvious which version is which. This is the heart of **"never change, only
enhance"**:

- You **never mutate** `Cyberspace.V1`. You ship `Cyberspace.V2` **alongside** it; versions coexist.
- A tag may **pin** (`.V1`) when you care, or **float to latest** (no version / `.Latest`) when you don't
  — so composing a `TabWidget` + `TabButton` + `TabPage` doesn't make you juggle versions.

### Lifecycle & integrity rules 📋 *(data model ✅; enforcement with Phase-2 admin)*
- **A page must never be invalid.** At render, a missing/disabled reference degrades to a visible
  placeholder + fires an **Admin Inbox** alert — never a crash.
- **Disabled = a version that exists but can't be used until re-enabled** (Page, Widget, Theme).
- **Delete is version-specific and reference-guarded:** you can't delete `Tooltip.V11` while any page
  pins it. Shipping `V12` doesn't free `V11` — each page must first be migrated (`.V11`→`.V12`) until
  nothing references `V11`. A floating (`latest`) reference is fine as long as *some* enabled version remains.
- **Wiki-like history** via SQL Server **temporal (system-versioned) tables**: every Page version records
  which Widget/Theme versions it carried, so you can inspect — and roll back to — any prior state.

---

## How a real page is built (worked example: `MindAttic.Legion.Frontend`) ✅

The former standalone `MindAttic.Legion.Frontend` web app is now the seeded **`personas`** Data page
in the CMS. Its body is exactly one token — the rest is free-form markup:

```html
<!-- Data page body.  Theme is set in Page Properties (ThemeKey = "cyberspace") — not a tag. -->
<MindAttic.Ideas.Widget.SacredGeometry />     <!-- capability activator: loads the geometry engine -->

<div class="legion-layout">
  <aside class="filters"><!-- inline HTML/JS: the left-hand filters --></aside>
  <section class="profiles">
    <!-- paginated profiles on the right; geometry attaches to these via the Widget above -->
  </section>
</div>

<style>/* page-level CSS — cascade tier 3 */</style>
<script>/* inline JS: pagination, filtering, and the modal popup (this page's own invention) */</script>
```

> **Theme is a Page Property, not a token.** In the admin Page Properties panel you pick a Theme
> from a dropdown (`ThemeKey` / `ThemeVersion` DB columns); the include expander never sees a
> `<MindAttic.Ideas.Theme.…>` tag in the body. The body is free-form widget composition only.

- **Theme** (`Cyberspace`) is wired via the Page Properties dropdown — supplies the chrome.
- **Widget** (`SacredGeometry`) is a capability activator — loads the geometry engine page-wide.
- **Inline JS/CSS/HTML** handles filters, pagination, and the modal popup — no compiled code.
- The modal is a great **future Widget `.idea`** candidate: extract once, reuse everywhere.

This single page (plus its seed) replaces an entire deployed web app. Verified live 2026-06-09:
`/personas` renders the full gallery with zero `ma-missing` placeholders.

---

## The `.idea` package 📋

A `.idea` is **a plain zip**. Its only required member is `idea.json`. The six-field kernel never changes:

```jsonc
{
  "manifestVersion": 1,            // schema of this file (host-gated integer)
  "category": "Widget",            // Page | Widget | Theme   (WHAT it is)
  "kind": "data",                  // data | code             (HOW it renders)
  "key": "tooltip",                // stable identity, never the CLR type name
  "version": 1,                    // whole-number content version (pins + asset URL segment)
  "displayName": "Tooltip"
  // optional, append-only: sdk, entryType, renderMode, css[], scripts[], assets, dependsOn[], uiux[]
}
```

```
tooltip.idea (a zip)
 ├─ idea.json                 # required
 ├─ wwwroot/                  # css/js/assets → served at /_ideas/{category}/{key}/{version}/...
 ├─ bin/                      # kind=code ONLY: the compiled assembly + non-host deps
 ├─ data/                     # optional idempotent seed
 └─ icon.png  README  LICENSE # never parsed
```

Unknown fields/folders are **ignored** (forward-compatible). Host-provided assemblies
(`MindAttic.Ideas.Abstractions`, `Microsoft.*`, `System.*`) are **forbidden** in `bin/`. Data content
carries no `bin/` and installs with zero build and zero recycle.

---

## CSS cascade — fixed order ✅

Locked, enforced in exactly one place (`CmsHead`), never reordered:

```
GLOBAL stylesheet  →  THEME stylesheet (e.g. Cyberspace)  →  PAGE-level stylesheet  →  inline style=""
   (Host setting)        (mirrors UiUx deps.json)              (Page.PageCss)            (by DOM nature)
```

A per-page tweak is either **inline CSS** in the Page definition, or an uploaded **`.idea`**.

---

## Trust & security ✅ *(raw-content gate)* / 📋 *(auth via MindAttic.Authentication)*

Sign-in is **not** Ideas-owned: it comes from the **[MindAttic.Authentication](https://github.com/mindattic/MindAttic.Authentication)**
package (Argon2id+pepper, Vault-backed, lockout, TOTP/MFA, hardened sessions) — the same engine
StreetSamurai and Tutor use. The BCrypt `AuthService`/`User` in Core today is an **interim port**, replaced
on adoption (AMENDMENTS **A16**). What stays Ideas-owned is the *raw-content* trust gate below.

You intentionally author **inline JavaScript** in trusted pages — that's a feature, not a leak. The trust
boundary is **author identity at write time**:

- On save, a page is stamped `Author` trust **iff** the writer holds the `Cms.AuthorRawMarkup` claim
  (Admin role); otherwise `Untrusted`.
- At render, a single gate (`IRawContentGate`) emits markup: **Author → raw passthrough** (your inline JS
  runs); **Untrusted → sanitized** (HtmlSanitizer). Author-trusted responses carry a per-response CSP
  nonce; everything else is `script-src 'self'`. *(CSP wiring lands with the Phase-2 admin.)*
- Demoting an author is a deliberate policy action (an `AuthorTrustVersion` epoch bump), never a silent
  re-render of live pages.

---

## Ecosystem integration ✅ *(Vault/Legion wired)* / 📋 *(UiUx extraction)*

MindAttic.Ideas reuses the ecosystem's shared infrastructure:

- **[MindAttic.Vault](https://github.com/mindattic/MindAttic.Vault)** — all credentials (DB connection
  strings, API keys, admin bootstrap). Wired via `builder.Configuration.AddMindAtticVaultFiles()` +
  `services.AddMindAtticVault(...)`. Same code locally (`%APPDATA%\MindAttic\…`) and on Azure (App
  Settings / Key Vault via Managed Identity). **No User Secrets** (retired ecosystem-wide).
- **[MindAttic.Legion](https://github.com/mindattic/MindAttic.Legion)** — LLM calls + multi-model
  *voting / consensus / scoring*. In-proc library: `services.AddLegionClient()` / `AddLLMVoting(...)`;
  call `LegionClient.CallAsync(...)`, `LlmVotingService.VoteAsync/DecideAsync/ScoreAsync`. Keys via Vault.
- **[MindAttic.Authentication](https://github.com/mindattic/MindAttic.Authentication)** 📋 — the canonical
  auth engine for all three apps (Argon2id+pepper over a Vault pepper, persistent lockout, TOTP/MFA,
  `__Host-` cookies, SecurityStamp ≤60 s, HIBP). Target wiring: `services.AddMindAtticAuthentication(cfg, o
  => o.AppName = "Ideas")` + `app.UseMindAtticAuthentication()` + `MapMindAtticAuthEndpoints()`; the CMS
  DbContext applies its isolated `auth` schema (`b.ApplyMindAtticAuthConfiguration()`). `AppName = "Ideas"`
  is a hard per-app trust boundary (no cross-app SSO). **Supersedes** the interim BCrypt auth in Core; the
  `Cms.AuthorRawMarkup` claim rides on its principal. Adopted once the package ships (AMENDMENTS **A16**).
- **[MindAttic.UiUx](https://github.com/mindattic/MindAttic.UiUx)** — the **single canonical source** for
  all official Widgets and Themes. UiUx has **no build**: one source distributed as **many
  wrappers/exports**:

  ```
  raw js/css/html  →  Blazor wrapper  →  .idea (MindAttic.Ideas)  →  (later) React, Angular, …
  ```

  The CMS↔UiUx tie stays **thin**: a CMS content type loads UiUx's **raw assets by pinned-tag jsDelivr
  URL** (mirrored from `deps.json`, CI-verified) rather than reimplementing them — **zero duplication**.
  *(Phase-1 content lives inline in the Web project as a render proof; its permanent home is UiUx.)*

---

## Architecture 🔨

```
MindAttic.Ideas.slnx                 # CMS engine
├─ src/
│  ├─ MindAttic.Ideas.Abstractions   # the frozen SDK: IdeaBase + PageBase/PluginBase/ThemeBase/ComponentBase,
│  │                                  #   [Idea], IRenderContext, discovery/catalog seams.
│  │                                  #   refs ONLY Microsoft.AspNetCore.Components + System.Text.Json.
│  ├─ MindAttic.Ideas.Core           # EF entities, CmsDbContext (SQL Server, temporal Pages), convention
│  │                                  #   discovery, catalog, raw-content gate, FreeFormPage, auth, seed.
│  ├─ MindAttic.Ideas.Packaging      # pure .idea wire contract: manifest kernel, packer, reader, SHA-256.
│  ├─ MindAttic.Ideas.Rendering      # rendering-support library.
│  ├─ MindAttic.Ideas.Sdk            # ma-idea CLI: pack / inspect / list / install / verify.
│  └─ MindAttic.Ideas.Web            # Blazor Web App (global InteractiveServer): PageHost catch-all,
│                                     #   CmsHead cascade, render fork, /admin, /_ideas route, Vault+Legion.
├─ tools/  codex.ps1                 # Codex doctor + digest
├─ docs/   BIBLE.md + AMENDMENTS.md (A1..A24) · FOUNDATION_ADR.md (deliberation) · IMPLEMENTATION_PLAN.md
│
library/MindAttic.Ideas.Library.slnx # first-party plugin/component/theme library (build-independent of CMS)
├─ Themes/     # 8 themes (Cyberspace, …)
├─ Plugins/    # 12 plugins (Tooltip, NavMenu, AtticFont, …)
└─ Components/ # 23 components (LegionPersonas, Textbox, Tabs, Gallery, …)
               # packed to dist/*.idea → copied to src/MindAttic.Ideas.Web/library/ at pack time
```

**Hot-load 📋.** New `.idea` packages load into a **collectible `AssemblyLoadContext`** with no app-pool
restart. The coexisting-versions + disable-don't-delete model makes this the safe *additive-load* case.
Shared contracts (Abstractions, ASP.NET Core, EF) are host-owned and deferred to the default load context
so a package's base types unify with the host's. Uploaded packages are **Static or Interactive Server**
only — never Interactive WebAssembly (a hard .NET boundary).

---

## Feature checklist

### Foundation (Phase 0/1) — ✅ built & verified end-to-end
- ✅ `Abstractions` SDK (frozen v1: `IdeaBase` + `PageBase`/`PluginBase`/`ThemeBase`/`ComponentBase`, `[Idea]`, `IRenderContext`, seams)
- ✅ `Core` EF model (one initial migration, reserved columns, temporal `Pages`) + ported auth/seed
- ✅ `CompiledContentSource` convention discovery + persisted catalog + type resolver
- ✅ `FreeFormPage` + `<MindAttic.Ideas.…>` include expander (AngleSharp) + missing-content placeholder
- ✅ `PageHost` catch-all + `CmsHead` fixed cascade + render fork
- ✅ Bootstrap theme (inline proof content under `Components/Library/Theme`)
- ✅ End-to-end: a seeded Data page renders through the theme (mechanics unit-proven; live e2e pending — see docs/USER_STORIES.md MAI-US-F5)

### Versioning, lifecycle & history
- ✅ Whole-number versions; optional / `.Latest` / pinned `.V{n}` resolution
- ✅ Temporal (system-versioned) `Pages` table (history substrate)
- ✅ Disable/enable + version-specific delete-guard enforcement (compiled content degrades to disable;
  pinned/sole-float references block deletion) — Core-tested
- ✅ Admin Inbox (dedup by key, reopen-on-recurrence) + disabled/missing-dependency render guard wiring
  (render thread fire-and-forget, never throws)

### Admin & CLI (Phase 2)
- ✅ Admin: page CRUD + soft-delete + publish/enable, trust stamping on save (raw-markup claim);
  content-definition enable/disable/guarded-delete; Admin Inbox triage — all under `MaPolicies.Admin`
- ✅ **Page Properties panel** (collapsible `<details>` in the page editor): Route, Title, Theme
  dropdown (catalog-driven), Theme version, SEO Title, SEO Description, Published/Enabled flags
- ✅ **SEO metadata** (`SeoMetaJson` column → `PageHost` renders `<title>` + `<meta name="description">`)
- ✅ **Monaco editor** for page body (catalog-driven `{{ }}` autocomplete, RFC 0001 typed-attribute
  coercion, clickable upload-to-fix placeholders) — replaces the plain textarea
- ✅ Theme/widget assignment UI, file manager, roles management (`/users`)
- 📋 Login / sign-out / SecurityStamp via the **MindAttic.Authentication** package (A16) — not Ideas-owned (package mid-build; interim BCrypt stands)
- ✅ `ma-idea` CLI: pack / inspect / list / install (offline validate) / verify

### Packages & migration (Phase 5/6)
- ✅ `.idea` format frozen as a wire contract: `MindAttic.Ideas.Packaging` (manifest kernel + lossless
  forward-compat, reflection-only packer, zip-slip-guarded reader, validator incl. the host-assembly
  bin/ audit, SHA-256, whole-number version/collision resolver) — pure, NUnit-tested
- ✅ Host-side install: `PackageInstallService` validates a `.idea` and registers the `InstalledPackage`
  registry row + a mirrored `Origin=Package` catalog row, idempotent, prior versions retained on upgrade,
  soft-disable — then reloads the live catalog. **No assembly is loaded yet** (see below)
- ✅ Runtime `.idea` load via collectible ALC: install extracts `bin/`, and `AlcAwareTypeResolver` loads
  package citizens through a per-package `CmsPackageLoadContext` (host types unify by reference identity;
  others delegate to the default resolver). `Unload` is never called (soft, effective-on-restart). The
  load + unification mechanics are NUnit-verified; ⚠️ **end-to-end render of a real packed `.idea` through
  the running host is not yet verified** (needs an attended run)
- ✅ `PageAssetCollector` (pure, Core) + `<CmsHead>` binding: a page's package-citizen css/scripts are
  cascade-ordered, deduped, and hoisted into `<head>` (band: Global → Theme → **Plugin/Component** → Page →
  inline), fed by a no-schema manifest→`ContentDescriptor.Extra` data path at catalog reload
- ✅ `/_ideas/{category}/{key}/{version}/…` asset route serves a package's extracted `wwwroot/`
  (category-qualified to disambiguate kinds; path-traversal guarded). Install extracts `wwwroot/` too, and
  `PackageAssetsOf` prefixes the collected `<head>` URLs with the citizen's `AssetMount`
- ✅ Compiled-citizen asset harvest (`Activator` on `PluginBase`/`ComponentBase`) — `PageAssets.AllAssetsOf` hoists
  declared `StylesheetUrls`/`ScriptUrls` via the same `PageAssetCollector` delegate (NUnit-verified)
- ✅ Official content in the first-party library (`library/` in this repo, A23): `MindAttic.Frontpage`
  → `frontpage` Data page (A21); `MindAttic.Legion.Frontend` → `personas` Data page (A22).
  43 `.idea`s: 8 Themes + 12 Plugins + 23 Components ([MAIL-A6](library/docs/AMENDMENTS.md#MAIL-A6))

---

## Stack

.NET 10 · Blazor Web App (global `InteractiveServer`) · EF Core + SQL Server (temporal tables) ·
Azure Blob · `IDbContextFactory`. Auth/seed/concurrency ported from `MindAttic.Frontpage`.
