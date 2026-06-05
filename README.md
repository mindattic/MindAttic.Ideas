# MindAttic.Ideas

A single-deployment Blazor CMS for the MindAttic ecosystem. **One** Azure App Service, **one** app
pool, **one** database hosts *many* pages — so a project like `MindAttic.Frontpage` or
`MindAttic.Legion.Frontend` no longer needs a whole web app just to serve essentially one page.

You ship capability by **uploading or CLI'ing a `.idea` file** (a plain zip). The CMS reads whether
it's a **Page**, **Component**, **Theme**, or **Control**, registers it, and it's live — no redeploy,
no app-pool restart. Components, Themes, and Controls are **globally scoped**, so any Page composes
them by dropping a tag:

```razor
<MindAttic.Ideas.Theme.Cyberspace.V1 />
<MindAttic.Ideas.Component.Tooltip />        @* no version = latest *@
<MindAttic.Ideas.Control.Textbox.V21 />
```

> **Status:** Phase 0/1 foundation **built and verified end-to-end**. This README is the **living
> feature spec** — every feature is marked ✅ done · 🔨 building · 📋 planned, kept in lockstep with the
> code so the documentation is feature-complete the moment the code is.
>
> **Canonical contracts:** [`docs/FOUNDATION_AMENDMENTS.md`](docs/FOUNDATION_AMENDMENTS.md) is the
> current source of truth for the foundation. [`docs/FOUNDATION_ADR.md`](docs/FOUNDATION_ADR.md) is the
> Legion deliberation that produced it (its *vocabulary* is superseded by the amendments — see the
> banner at its top). [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) is the original brief.

---

## The one mental model

**A Page is free-form. A Theme wraps it. Components and Controls drop into it. Inline JS/CSS/HTML is yours.**

There are **no zones, panes, slots, or grids** — DotNetNuke's clunky fixed-layout model is explicitly
rejected. You author a page however you like and place content exactly where you want it in your markup.
The unit you install is a `.idea` zip; the things it contains are one of four **content kinds**.

| Kind | What it is | Base type | Example tag |
|---|---|---|---|
| **Page** | A free-form page (inline HTML/CSS/JS) that picks a Theme and drops tags | `PageBase` | `MindAttic.Ideas.Page.LegionFrontpage.V2` |
| **Component** | A **capability** you add to a page — loads its js/css so a behavior works page-wide | `ComponentBase` | `MindAttic.Ideas.Component.Tooltip.V11` |
| **Theme** | A layout (chrome + one `@Body` hole) + a CSS bundle | `ThemeBase` | `MindAttic.Ideas.Theme.Cyberspace.V4` |
| **Control** | One **atomic** placed UI element that renders visibly | `ControlBase` | `MindAttic.Ideas.Control.Textbox.V21` |

All four derive from a shared root, **`IdeaBase`**. "Idea" names that shared base and the `.idea`
package format (and the `/_ideas/...` asset route) — it is **never** a content kind. New kinds can be
**appended** to the set later without breaking anything.

> **Component vs Control.** A **Component** is a *behavior switch*: dropping
> `<MindAttic.Ideas.Component.Tooltip />` loads the tooltip engine so that thereafter **any** element with
> `data-tooltip`/`data-tt` shows a tooltip on hover — it renders no widget of its own. A **Control** is a
> *thing you place*: `<MindAttic.Ideas.Control.Textbox />` renders an actual `<input>` right there.

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

- **`{ContentKind}`** — `Page` · `Component` · `Theme` · `Control`.
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
  — so composing a `TabControl` + `TabButton` + `TabPage` doesn't make you juggle versions.

### Lifecycle & integrity rules 📋 *(data model ✅; enforcement with Phase-2 admin)*
- **A page must never be invalid.** At render, a missing/disabled reference degrades to a visible
  placeholder + fires an **Admin Inbox** alert — never a crash.
- **Disabled = a version that exists but can't be used until re-enabled** (Page, Component, Theme, Control).
- **Delete is version-specific and reference-guarded:** you can't delete `Tooltip.V11` while any page
  pins it. Shipping `V12` doesn't free `V11` — each page must first be migrated (`.V11`→`.V12`) until
  nothing references `V11`. A floating (`latest`) reference is fine as long as *some* enabled version remains.
- **Wiki-like history** via SQL Server **temporal (system-versioned) tables**: every Page version records
  which Component/Theme/Control versions it carried, so you can inspect — and roll back to — any prior state.

---

## How a real page is built (worked example: `MindAttic.Legion.Frontend`) 📋

The current standalone `MindAttic.Legion.Frontend` web app collapses into **one** `MindAttic.Ideas` Page:

```html
<!-- Data page: ThemeKey = cyberspace; inline content + tags. Version omitted = latest. -->
<MindAttic.Ideas.Theme.Cyberspace />          <!-- global look & feel -->
<MindAttic.Ideas.Component.SacredGeometry />  <!-- switch on the geometry engine -->

<div class="legion-layout">
  <aside class="filters"><!-- inline HTML/JS: the left-hand filters --></aside>
  <section class="profiles">
    <!-- paginated profiles on the right; geometry attaches to these via the Component above -->
  </section>
</div>

<style>/* page-level CSS — cascade tier 3 */</style>
<script>/* inline JS: pagination, filtering, and the modal popup (this page's own invention) */</script>
```

- **Theme** (`Cyberspace`) supplies the chrome and the global → theme CSS tiers.
- **Component** (`SacredGeometry`) is a capability — it loads the engine that draws the geometric avatars.
- **Inline JS/CSS/HTML** does the filters, pagination, and the modal popup — no compiled code required.
- The modal is a great **future Component `.idea`** candidate: extract it once, reuse it everywhere.
  Nothing about the page breaks when you do — the inline version simply becomes a tag.

This single page, plus its seed data, replaces an entire deployed web app.

---

## The `.idea` package 📋

A `.idea` is **a plain zip**. Its only required member is `idea.json`. The six-field kernel never changes:

```jsonc
{
  "manifestVersion": 1,            // schema of this file (host-gated integer)
  "category": "Component",         // Page | Component | Theme | Control   (WHAT it is)
  "kind": "data",                  // data | code                          (HOW it renders)
  "key": "tooltip",                // stable identity, never the CLR type name
  "version": 1,                    // whole-number content version (pins + asset URL segment)
  "displayName": "Tooltip"
  // optional, append-only: sdk, entryType, renderMode, css[], scripts[], assets, dependsOn[], uiux[]
}
```

```
tooltip.idea (a zip)
 ├─ idea.json                 # required
 ├─ wwwroot/                  # css/js/assets → served at /_ideas/{key}/{version}/...
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

## Trust & security ✅

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
- **[MindAttic.UiUx](https://github.com/mindattic/MindAttic.UiUx)** — the **single canonical source** for
  all official Components, Themes, and Controls. UiUx has **no build**: one source distributed as **many
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
MindAttic.Ideas.sln
├─ src/
│  ├─ MindAttic.Ideas.Abstractions   # the frozen SDK: IdeaBase + PageBase/ComponentBase/ThemeBase/
│  │                                  #   ControlBase, [Idea], IRenderContext, discovery/catalog seams.
│  │                                  #   refs ONLY Microsoft.AspNetCore.Components + System.Text.Json.
│  ├─ MindAttic.Ideas.Core           # EF entities, CmsDbContext (SQL Server, temporal Pages), convention
│  │                                  #   discovery, catalog, raw-content gate, FreeFormPage, auth, seed.
│  └─ MindAttic.Ideas.Web            # Blazor Web App (global InteractiveServer): PageHost catch-all,
│                                     #   CmsHead cascade, render fork, /admin, /_ideas route, Vault+Legion.
│                                     #   Phase-1 content under Components/Library/{Theme,Component,Control}.
├─ tools/  MindAttic.Ideas.Cli       # `ma-idea` pack/install/list/upgrade/disable  (Phase 2) 📋
└─ docs/   FOUNDATION_AMENDMENTS.md (current) · FOUNDATION_ADR.md (deliberation) · IMPLEMENTATION_PLAN.md
```

**The `ComponentBase` name.** MindAttic's `ComponentBase` owns the bare name; Blazor's framework base is
referenced via an alias (`using BlazorComponentBase = Microsoft.AspNetCore.Components.ComponentBase`), and
`_Imports.razor` aliases the bare `ComponentBase` to MindAttic's — so `@inherits ComponentBase` is yours.

**Hot-load 📋.** New `.idea` packages load into a **collectible `AssemblyLoadContext`** with no app-pool
restart. The coexisting-versions + disable-don't-delete model makes this the safe *additive-load* case.
Shared contracts (Abstractions, ASP.NET Core, EF) are host-owned and deferred to the default load context
so a package's base types unify with the host's. Uploaded packages are **Static or Interactive Server**
only — never Interactive WebAssembly (a hard .NET boundary).

---

## Feature checklist

### Foundation (Phase 0/1) — ✅ built & verified end-to-end
- ✅ `Abstractions` SDK (frozen v1: `IdeaBase` + four kind bases, `[Idea]`, `IRenderContext`, seams)
- ✅ `Core` EF model (one initial migration, reserved columns, temporal `Pages`) + ported auth/seed
- ✅ `CompiledContentSource` convention discovery + persisted catalog + type resolver
- ✅ `FreeFormPage` + `<MindAttic.Ideas.…>` include expander (AngleSharp) + missing-content placeholder
- ✅ `PageHost` catch-all + `CmsHead` fixed cascade + render fork
- ✅ Cyberspace + Bootstrap themes, Tooltip component, Textbox control (inline proof content)
- ✅ End-to-end: a seeded Data page renders a Component capability + a Control through the Cyberspace theme

### Versioning, lifecycle & history
- ✅ Whole-number versions; optional / `.Latest` / pinned `.V{n}` resolution
- ✅ Temporal (system-versioned) `Pages` table (history substrate)
- 📋 Disable/enable + version-specific delete-guard enforcement (with Phase-2 admin)
- 📋 Admin Inbox + disabled-dependency render guard wiring

### Admin & CLI (Phase 2)
- 📋 Admin: page CRUD, theme/component/control assignment, file manager, roles, login + SecurityStamp
- 📋 `ma-idea` CLI: pack / install / list / upgrade / disable

### Packages & migration (Phase 5/6)
- 📋 `PackageContentSource` (runtime `.idea` via collectible ALC) + installer + asset file provider
- 📋 SDK packer (`dotnet ma-idea pack`); per-page Component-asset de-duplication into `<head>`
- 📋 Move official content into MindAttic.UiUx (canonical source); then collapse `MindAttic.Frontpage`
  and `MindAttic.Legion.Frontend` into Pages

---

## Stack

.NET 10 · Blazor Web App (global `InteractiveServer`) · EF Core + SQL Server (temporal tables) ·
Azure Blob · `IDbContextFactory`. Auth/seed/concurrency ported from `MindAttic.Frontpage`.
