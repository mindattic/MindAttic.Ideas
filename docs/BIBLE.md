---
codex: 1
project: MindAttic.Ideas
code: MAI
layer: bible
status: living
updated: 2026-06-09
---

# MindAttic.Ideas — Project Bible

> Single source of truth for what MindAttic.Ideas IS, is NOT, and the rules that keep it coherent.
> README.md says how to build/run; this says how to think about the system.
>
> **Provenance.** This bible was reformatted from the frozen [`FOUNDATION_ADR.md`](FOUNDATION_ADR.md)
> (the Legion deliberation that produced the foundation) as patched by the append-only
> [`AMENDMENTS.md`](AMENDMENTS.md) (A1..A24). Where this bible and an amendment disagree, **the
> amendment wins** — see [§5](#MAI-§5). The ADR's original vocabulary (`Component` as a kind,
> `Cms`-prefixed bases, `<ma-component>` tags) is **superseded**; current vocabulary is A18/A19 (the
> composable-UI kind is **Widget**; `Control` kind removed, ordinal 3 reserved).

## 1. The one sentence {#MAI-§1}

MindAttic.Ideas is a **single-deployment Blazor CMS** — one Azure App Service, one app pool, one
database — that hosts *many* pages and goes live the moment you upload a `.idea` zip, with **no
redeploy and no app-pool restart**.

## 2. The product promise {#MAI-§2}

You ship capability by **uploading or CLI'ing a `.idea` file** (a plain zip). The CMS reads whether
it contains a **Page**, **Widget**, or **Theme**, registers it by convention, and it
is live. The three kinds derive from one shared root `IdeaBase`; Themes/Widgets are
**globally scoped**, so any Page composes them by dropping a token.

- **A Page is free-form. A Theme wraps it. Widgets drop into it. Inline JS/CSS/HTML is
  yours.** There are no zones, panes, slots, or grids (DotNetNuke's fixed-layout model is rejected).
- **Two authoring paths, one render path.** A *Data page* (free-form `BodyHtml`/`PageCss`/`PageJs`
  in the DB, zero deploy — the primary path) and a *Code page* (a compiled `PageBase` subclass for
  genuine Blazor interactivity). A page can graduate Data ↔ Code as a **row edit**, never a schema
  change.
- **Never change, only enhance.** Versions are whole numbers (`V1`, `V2`); you ship `V2` *alongside*
  `V1` and never mutate a shipped version. Coexisting versions are the never-break mechanism.
- **A page must never be invalid.** A missing/disabled reference degrades to a visible placeholder
  and fires an Admin Inbox alert — never a crash.

## 3. What it is NOT {#MAI-§3}

- **NOT a zone/pane/slot/grid layout engine.** Composition is *lexical* — the author places tokens
  in free-form markup. DNN-style fixed layout is explicitly rejected ([A14](AMENDMENTS.md#MAI-A14)).
- **NOT SemVer.** No `1.5.11` anywhere; whole-number versions only ([A1](AMENDMENTS.md#MAI-A1),
  [HOUSE-LAW-1](../../MindAttic.HouseRules.md#HOUSE-LAW-1)).
- **NOT one-web-app-per-page.** A whole standalone frontend (e.g. `MindAttic.Legion.Frontend`)
  collapses into a single Page, not its own Site. Sites are reserved for genuinely separate domains.
- **NOT a per-page router.** Pages resolve by `(SiteId, Slug)` data lookup through one catch-all
  `PageHost`, never per-page routing — so a runtime-loaded `.idea` type renders with zero router
  changes.
- **NOT the owner of sign-in.** Auth is delegated to MindAttic.Authentication
  ([A16](AMENDMENTS.md#MAI-A16)); the interim BCrypt `AuthService`/`User` in Core is a temporary
  port. What stays Ideas-owned is the *raw-content* trust gate.
- **NOT hard-delete by default.** Disable = exists-but-unusable; a version-specific delete is
  reference-guarded ([A3](AMENDMENTS.md#MAI-A3), [HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2)).
- **NOT WebAssembly for uploaded packages.** Uploaded `.idea` types are Static or InteractiveServer
  only (a hard .NET boundary).
- **"Idea" is NOT a content kind.** It names the shared base `IdeaBase`, the `.idea` package, and
  the `/_ideas/...` asset route — never one of the content kinds.

## 4. Architecture canon {#MAI-§4}

```
                upload/CLI a .idea (plain zip)
                          │
                          ▼
   ┌──────────────────────────────────────────────────────────────┐
   │  MindAttic.Ideas.Web   (Blazor Web App, global InteractiveServer)
   │   PageHost  /{*slug}  ──►  resolve (SiteId,Slug) ─► Theme ─► render fork
   │   CmsHead (FIXED CSS cascade)   /_ideas/{Kind}/{key}/{ver}/… asset route
   │   /admin (Admin policy)         Vault + Legion wired                   │
   └───────────┬──────────────────────────────────┬───────────────────────┘
               │ uses                              │ uses
   ┌───────────▼───────────┐          ┌────────────▼─────────────────────┐
   │ MindAttic.Ideas.Core   │          │ MindAttic.Ideas.Packaging         │
   │  CmsDbContext (EF,SQL,  │          │  manifest kernel + reader +       │
   │   temporal Pages)       │          │  validator + packer + SHA-256     │
   │  CompiledContentSource  │          │  (pure, IO-free)                  │
   │  ContentCatalog         │          └────────────┬──────────────────────┘
   │  IncludeExpander        │                       │
   │  RawContentGate (gate)  │          ┌────────────▼─────────────────────┐
   │  collectible ALC load   │          │ MindAttic.Ideas.Sdk  (ma-idea CLI)│
   └───────────┬─────────────┘          │  pack / inspect / list / verify   │
               │ references             └───────────────────────────────────┘
   ┌───────────▼───────────────────────────────────────────────────────────┐
   │ MindAttic.Ideas.Abstractions   (frozen v1 SDK, MAJOR pinned at 1)       │
   │  IdeaBase + PageBase/WidgetBase/ThemeBase, [Idea],                      │
   │  IRenderContext, ICmsContentSource/ITypeResolver/IRawContentGate seams  │
   │  refs ONLY Microsoft.AspNetCore.Components + System.Text.Json           │
   └─────────────────────────────────────────────────────────────────────────┘
   First-party content (Themes/Widgets) lives in the `library/` directory of
   this repo (merged from the former sibling repo, A23), packed to dist/*.idea.
   (MindAttic.Ideas.Rendering is a small rendering-support project.)
```

### 4.1 Projects
- **`src/MindAttic.Ideas.Abstractions`** — the frozen v1 SDK: `IdeaBase` + the three kind bases
  (`PageBase`/`WidgetBase`/`ThemeBase`), `[Idea]`, `IRenderContext`, `CmsInclude`,
  discovery/catalog seams. References ONLY `Microsoft.AspNetCore.Components` + `System.Text.Json`.
- **`src/MindAttic.Ideas.Core`** — EF entities, `CmsDbContext` (SQL Server, temporal `Pages`),
  convention discovery, persisted catalog, raw-content gate, `FreeFormPage`/include expander, the
  collectible-ALC type resolver, interim auth, seed.
- **`src/MindAttic.Ideas.Packaging`** — pure `.idea` wire contract: manifest kernel, reflection-only
  packer, zip-slip-guarded reader, validator (incl. host-assembly `bin/` audit), SHA-256, version
  resolver.
- **`src/MindAttic.Ideas.Rendering`** — rendering-support library.
- **`src/MindAttic.Ideas.Sdk`** — the `ma-idea` CLI (pack / inspect / list / install / upgrade / verify).
- **`src/MindAttic.Ideas.Web`** — the Blazor Web App host: `PageHost` catch-all, `CmsHead` cascade,
  render fork, `/admin`, `/_ideas` route, Vault+Legion wiring; Phase-1 proof content under
  `Components/Library/Theme` (Bootstrap theme only — Widget/Control inline content removed with A19).
- **`src/MindAttic.Ideas.Tests`** — NUnit suite (224 tests).
- **`library/`** — first-party widget/theme library (`library/MindAttic.Ideas.Library.slnx`);
  independent of the CMS, references only `src/MindAttic.Ideas.Abstractions`. Merged from the
  former sibling repo ([A23](AMENDMENTS.md#MAI-A23)).

### 4.2 Domain model — the NOUNS
- **`IdeaBase`** — shared root of all four content kinds.
- **`ContentKind`** — `Page=0 · Widget=1 · Theme=2` (append-only on *ordinals*; `Control=3` removed pre-1.0 per A19, never reused;
  [A18](AMENDMENTS.md#MAI-A18) renamed ordinal 1 to Widget).
- **`Page`** (`src/MindAttic.Ideas.Core/Entities/Page.cs`) — one durable EF row, `PageKind {Data,Code}`;
  Data columns `BodyHtml`/`PageCss`/`PageJs`/`BodyTrust`; shared `SiteId`/`ParentId`/`Slug`/`ThemeKey`;
  `SeoMetaJson` (JSON, `{title,description}` via `SeoMeta` — wired in A24).
  System-versioned temporal table.
- **`CmsContentDefinition`** — the persisted catalog row: `UNIQUE(Kind,Key,Origin)`, `Priority`,
  `IsShadowed`, asset mount, raw bundle.
- **`InstalledPackage`** — `.idea` registry row: `(Category,Key,Version)` unique, verbatim manifest,
  blob path, SHA-256, `Enabled`.
- **`AdminInboxMessage`** — dedup-by-`DedupKey`, severity/status, reopen-on-recurrence.
- Identity is the triple **`(ContentKind Kind, string Key, int Version)`** ([A2](AMENDMENTS.md#MAI-A2)).

### 4.3 Key services — the VERBS
- **`CompiledContentSource`** (`Core/Discovery`) — convention discovery of compiled citizens into the catalog.
- **`ContentCatalog`** (`Core/Discovery`) — the one catalog; ordered `ICmsContentSource` providers feed it.
- **`AlcAwareTypeResolver` / `CmsPackageLoadContext`** (`Core/Discovery`) — load `.idea` citizens through a
  per-package collectible `AssemblyLoadContext`; host types unify by reference identity (the ALC linchpin).
- **`IncludeExpander` / `IncludeReferenceParser`** (`Core/Rendering`) — resolve `{{Kind.Name[.Vn]}}` tokens
  against the catalog; unresolved/disabled → placeholder.
- **`RawContentGate`** (`Core/Rendering`) — the sole `MarkupString` chokepoint: Author → raw passthrough,
  Untrusted → sanitized.
- **`PageAssetCollector`** (`Core/Rendering`) — cascade-orders/dedupes a page's citizen css/scripts into `<head>`.
- **`PackageInstallService`** (`Core/Services`) — validate → register `InstalledPackage` + mirrored catalog
  row, idempotent, soft-disable, reload catalog.
- **`ContentLifecycleService`** (`Core/Services`) — enable/disable + reference-guarded version-specific delete.
- **`AdminInboxService` / `RenderAlertSink`** (`Core/Services`) — DB-backed dedup alerting; the render thread
  fire-and-forgets and never throws.
- **`PageAdminService` / `PageAuthoring`** (`Core/Services`) — page CRUD, soft-delete, publish, trust stamping,
  SEO metadata round-trip (`SeoMeta` serialize/parse, `PageEditModel.SeoTitle`/`SeoDescription`).
- **`SeedService`** (`Core/Services`) — idempotent upsert-by-key seed that never clobbers admin edits.

## 5. The Laws {#MAI-§5}

> **Inherited.** MindAttic.Ideas inherits every org-wide law in
> [`MindAttic.HouseRules.md`](../../MindAttic.HouseRules.md) by reference (HOUSE-LAW-1 … HOUSE-LAW-9).
> Do not restate them here. Of particular weight for this project: whole-number versioning
> ([HOUSE-LAW-1](../../MindAttic.HouseRules.md#HOUSE-LAW-1)), soft-disable
> ([HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2)), Vault credentials
> ([HOUSE-LAW-3](../../MindAttic.HouseRules.md#HOUSE-LAW-3)), Legion LLMs
> ([HOUSE-LAW-4](../../MindAttic.HouseRules.md#HOUSE-LAW-4)), guarded packaging
> ([HOUSE-LAW-5](../../MindAttic.HouseRules.md#HOUSE-LAW-5)), one-engine-many-front-doors
> ([HOUSE-LAW-6](../../MindAttic.HouseRules.md#HOUSE-LAW-6)), MindAttic.Authentication
> ([HOUSE-LAW-7](../../MindAttic.HouseRules.md#HOUSE-LAW-7)), and verified-DoD
> ([HOUSE-LAW-8](../../MindAttic.HouseRules.md#HOUSE-LAW-8)).
>
> **Amendment supremacy.** The append-only [`AMENDMENTS.md`](AMENDMENTS.md) (A1..A24) patches both the
> ADR and this bible. Where an amendment conflicts with bible prose, the amendment wins.

These are the **project-specific** laws (the cross-cutting invariants the foundation may never change):

- **{#MAI-LAW-1} Naming & identity lock.** A citizen's forever-identity is `(ContentKind Kind, string Key,
  int Version)` — never the CLR type name. The three kinds are Page · Widget · Theme under
  `IdeaBase`; "Idea" is never a kind. ([A2](AMENDMENTS.md#MAI-A2), [A9](AMENDMENTS.md#MAI-A9),
  [A18](AMENDMENTS.md#MAI-A18))
- **{#MAI-LAW-2} Frozen SDK, MAJOR=1 forever.** `Abstractions` references only
  `Microsoft.AspNetCore.Components` + `System.Text.Json`; the context/enum/attribute/descriptor surface is
  append-only (enums grow by appending ordinals; interfaces grow by default methods). MAJOR is pinned at 1.
- **{#MAI-LAW-3} One render primitive, no zones, no routing.** All rendering is `DynamicComponent` /
  `FreeFormPage` through the single `PageHost` catch-all resolving `(SiteId, Slug)`. No zones/panes/slots/grids;
  no per-page routes.
- **{#MAI-LAW-4} Fixed CSS cascade, enforced in one place (`CmsHead`).** Ordinal 0 GLOBAL → 100 THEME →
  200 PAGE → 300+DOM INLINE; reserved gaps allow additive tiers; never reorder. Asset route is locked at
  `/_ideas/{Kind}/{key}/{version}/{**path}`.
- **{#MAI-LAW-5} Trust at write time, gated at render.** On save, `BodyTrust = Author` iff the writer holds
  `Cms.AuthorRawMarkup` (Admin), else `Untrusted`. The single `IRawContentGate` is the only place a
  `MarkupString` is born: Author → raw, Untrusted → sanitized. Demotion is a deliberate `AuthorTrustVersion`
  epoch bump, never a silent re-render. ([A16](AMENDMENTS.md#MAI-A16) moves the *issuer* to the auth package;
  the claim and gate are unchanged.)
- **{#MAI-LAW-6} ALC defer-to-default.** A per-`.idea` collectible `AssemblyLoadContext` defers
  `SharedContracts.DeferToDefaultPrefixes` (Abstractions, Core, Microsoft.*, System.*, …) to the default
  context so a package's base types unify by reference identity with the host's. Host assemblies are
  forbidden in a package `bin/`.
- **{#MAI-LAW-7} A page is never invalid.** A missing/stale reference degrades to a placeholder; a
  *deliberately disabled* dependency halts-and-notifies via the Admin Inbox. The render thread never throws.
  ([A5](AMENDMENTS.md#MAI-A5))
- **{#MAI-LAW-8} Two version axes, never conflated.** `manifestVersion` (file-format host gate) is distinct
  from `sdk` (runtime contract floor); both are integers. ([A1](AMENDMENTS.md#MAI-A1))
- **{#MAI-LAW-9} Port, don't reinvent.** Auth/seed/concurrency were ported verbatim from
  MindAttic.Frontpage and are being migrated onto the shared ecosystem packages (Vault, Legion,
  Authentication), not re-rolled. ([A6](AMENDMENTS.md#MAI-A6), [A7](AMENDMENTS.md#MAI-A7),
  [A16](AMENDMENTS.md#MAI-A16))

## 6. Verified state {#MAI-§6}

**Build/test evidence (2026-06-12):** `dotnet build MindAttic.Ideas.slnx -c Debug` — Build succeeded,
0 errors (0 CS warnings; pre-existing ASP0006 analyzer notes in Core rendering are tracked, not new).
`dotnet test src/MindAttic.Ideas.Tests --no-build` → **Passed: 224, Failed: 0, Skipped: 0**, plus the
[Explicit] SQL Server temporal proof (`PageHistorySqlServerTests`) run against LocalDB — passed.
**Live render evidence (2026-06-09, attended):** with all 37 library `.idea`s installed, `GET /` →
302 → `/frontpage`; the rendered HTML contains the mindattic.com recreation (wordmark, tab boards,
books grid, footer) with **zero** `ma-missing` placeholders, `/_ideas/...` mounts serve 200, and
`/personas` (the collapsed Legion.Frontend) renders the full gallery with zero placeholders.

Proven working (each cited in [`USER_STORIES.md`](USER_STORIES.md)):
- ✅ Abstractions SDK frozen v1 (`IdeaBase` + four kind bases, `[Idea]`, seams).
- ✅ Core EF model, one initial migration, reserved columns, temporal `Pages`, ported auth/seed.
- ✅ `CompiledContentSource` convention discovery + persisted catalog + type resolver.
- ✅ `FreeFormPage` + include expander (AngleSharp) + missing-content placeholder.
- ✅ Fixed CSS cascade and render fork (unit-asserted).
- ✅ Raw-content trust gate (Author raw / Untrusted sanitized).
- ✅ Disable/enable + reference-guarded version-specific delete (Core-tested).
- ✅ Admin Inbox dedup + reopen-on-recurrence; render guard wiring.
- ✅ `.idea` packaging (manifest kernel, packer, zip-slip-guarded reader, validator, SHA-256, version
  resolver) — pure, NUnit-tested.
- ✅ Host-side install (`PackageInstallService`) + collectible-ALC load + unification (NUnit-verified).
- ✅ `PageAssetCollector` cascade ordering + `/_ideas/{Kind}/{key}/{version}/…` asset route.
- ✅ `ma-idea` CLI: pack / inspect / list / install / verify.

**The former "not yet verified" list is closed ([A22](AMENDMENTS.md#MAI-A22)):** the live packed-`.idea`
render, the admin lifecycle UI, MindAttic.Authentication adoption, the frontend collapse
(`frontpage` = mindattic.com per A21, `personas` = Legion.Frontend per A22), and the full Unified
Page Plan (Monaco + typed-attribute coercion + clickable upload-to-fix placeholders) are all shipped
and story-cited.

**Post-A22 enhancements verified (2026-06-12):**
- ✅ Library mono-repo consolidation: `library/` merged into this repo ([A23](AMENDMENTS.md#MAI-A23)).
  37 `.idea`s (7 Themes + 30 Widgets); `ma-idea verify` compose-graph green.
- ✅ Page Properties collapsible panel with SEO Title, SEO Description, Theme picker, Route field
  ([A24](AMENDMENTS.md#MAI-A24)). 7 new tests in `PageAdminServiceTests`.
- ✅ `PageTreeFeature` (IPageTree) tested directly (3 tests in `PageTreeFeatureTests`): ordered
  children, disabled/deleted filtering, unknown-page empty return.
- ✅ `ArgParser` tested (4 tests in `ArgParserTests`): key-value, flag-only, case-insensitive lookup, empty args.

Every story in [`USER_STORIES.md`](USER_STORIES.md) is ✅; the priority backlog is empty.

## 7. Active frontier {#MAI-§7}

The foundation-era frontier is **closed** — RFC 0001 is implemented
([`rfc/0001-unified-page-plan.md`](rfc/0001-unified-page-plan.md), `status: implemented`) and every
user story is ✅ ([A22](AMENDMENTS.md#MAI-A22)). The CMS is in **enhance-only** mode per
[§2](#MAI-§2) (never change, only enhance): new capability arrives as new stories, new whole-number
content versions, and new Library `.idea`s. Known nice-to-haves live outside this repo's stories —
e.g. the Library's component smoke-test harness (MAIL RFC 0001) and observing its baseline-widget
demos interactively (MAIL-US-A4).

## 8. Quality bar {#MAI-§8}

Definition of done (a feature is `✅` only when *verified*, never merely asserted — see
[HOUSE-LAW-8](../../MindAttic.HouseRules.md#HOUSE-LAW-8)):

1. Clean `dotnet build` (0 warnings) on `MindAttic.Ideas.slnx`.
2. Green NUnit tests; a `✅` story names the test that proves it.
3. For anything user-facing, an e2e or lifecycle assertion (mechanics-only proofs stay `🟡` until an
   attended run confirms them — e.g. live packed-`.idea` render).
4. No new fact duplicated: a fact lives in exactly one layer and is cited by `{#id}` elsewhere.
5. Whole-number versioning; soft-disable over hard-delete; secrets via Vault.

## 9. Glossary {#MAI-§9}

- **.idea** — a plain zip whose only required member is `idea.json`; the install unit.
- **Idea / IdeaBase** — the shared base of all content kinds and the package format; never a kind.
- **Page** — content, a CMS DB row (Data or Code), resolved by `(SiteId, Slug)`.
- **Widget** — the composable-UI kind ([A18](AMENDMENTS.md#MAI-A18)): from an asset-only capability
  activator (Tooltip) up to a full interactive UI that nests other widgets via `CmsInclude`. Formerly
  "Component" (A9) then "Plugin" (A17).
- **Theme** — layout chrome + one `@Body` hole + a CSS bundle.
- **Control** — *removed ([A19](AMENDMENTS.md#MAI-A19)): atomic UI is authored as a Widget.* The `Control` kind and `ControlBase` were deleted; ordinal 3 is retired and never reused.
- **ContentKind** — `Page=0 · Widget=1 · Theme=2` (append-only ordinals; `Control=3` removed pre-1.0, never reused).
- **Data page / Code page** — free-form DB body (zero deploy) vs a compiled `PageBase` subclass.
- **Catalog (`CmsContentDefinition`)** — the one persisted registry of all citizens.
- **ALC** — the per-package collectible `AssemblyLoadContext` used to load `.idea` citizens.
- **Raw-content gate (`IRawContentGate`)** — the sole `MarkupString` chokepoint; trust-keyed.
- **Admin Inbox** — DB-backed dedup alert surface for render-time degradation.
- **Trust (`ContentTrust`)** — `Author` (raw passthrough) vs `Untrusted` (sanitized), set at write time.
- **MindAttic.Ideas.Library** — the `library/` directory in this repo (merged from the former
  sibling repo per [A23](AMENDMENTS.md#MAI-A23)): the single home of all first-party Themes/Widgets.
  Build-independent from the CMS; references only `Abstractions`; packs to `dist/*.idea`.
- **UiUx** — MindAttic.UiUx, the build-free canonical source for official content (consumed by pinned-tag URL).
