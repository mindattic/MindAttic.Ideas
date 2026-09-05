---
codex: 1
project: MindAttic.Ideas
code: MAI
layer: amendments
status: living
updated: 2026-09-04
---

# MindAttic.Ideas — Amendments (append-only; amendment wins over the bible)

These directives were finalized **after** the Legion deliberation produced
[`FOUNDATION_ADR.md`](FOUNDATION_ADR.md). Where an amendment conflicts with the ADR or with
[`BIBLE.md`](BIBLE.md), **the amendment wins** and the bible/ADR are to be read as patched here. All
are part of the ratifiable foundation. IDs are stable; never rewrite an amendment, only
supersede it with a new one.

> **Migration note (2026-06-07):** this file is the Codex L1 home of the former
> `FOUNDATION_AMENDMENTS.md`. Content is preserved verbatim; stable `{#MAI-An}` anchors were added so
> the bible and stories can cite each amendment by ID.

---

## MAI-A1 — Versioning is whole-number, not SemVer (overrides ADR §3 / §2) {#MAI-A1}

Every Page, Theme, and Component version is a **single whole number** (`1`, `2`, `3`). No
dotted/minor/patch versions anywhere.

- `idea.json` → `"version": 1` (integer).
- The package `sdk` gate becomes an **integer minimum** (`"sdk": 1` = "requires host SDK ≥ 1"), not a
  SemVer range. `[assembly: IdeaSdkVersion("1")]` likewise.
- Asset route segment uses the integer: `/_ideas/{key}/{version}/…` → `/_ideas/ui.sacredgeometry/1/…`.
- `Abstractions` MAJOR pinned at **1 forever**, additive-only (unchanged from ADR).

**Rationale:** the owner's explicit instruction — "whole numbers only, no 1.5.11; make it trivially
obvious which version is which." Coexisting integer versions are the never-break mechanism.

## MAI-A2 — Version is part of identity; Pages pin a version (refines ADR §2 identity lock) {#MAI-A2}

A citizen's identity becomes the triple **`(ContentKind Kind, string Key, int Version)`**. The composition
tag pins the version explicitly (`<…Cyberspace.V1/>` / `<ma-component key="cyberspace" v="1" …/>`).

- A Page **pins** the exact Theme/Component versions it references; a new version (`V2`) never affects a
  Page pinned to `V1`.
- There is **no implicit "track latest."** Upgrading a Page to `V2` is a deliberate edit.
- The `<ma-component>` include grammar gains a required-when-ambiguous `v` attribute (whole number). The
  friendly namespaced tag form (`<MindAttic.Ideas.Widgets.SacredGeometry.V1/>`) is the authoring sugar;
  it resolves to `(Component, "ui.sacredgeometry", 1)`.

## MAI-A3 — Disable / delete integrity (adds to ADR §4 data model) {#MAI-A3}

- Every Page/Theme/Component version has an **`Enabled`** flag. **Disabled = exists but cannot be used
  until re-enabled.**
- **Referential guard:** a Theme/Component version **cannot be deleted while any Page references it.**
  Deletion is blocked until every referencing Page is **Disabled or Deleted**. Enforced in the service
  layer over a `PageReference` projection (derived from parsing `BodyHtml` `<ma-component>` tags + Code
  page references), with a confirming DB check.

## MAI-A4 — Temporal history (adds to ADR §4) {#MAI-A4}

Pages (and their Theme/Component pin set) use **SQL Server system-versioned temporal tables** — mirror
Prose's pattern: `SysStart`/`SysEnd` `GENERATED ALWAYS`, an idempotent
`EnableSystemVersioningAsync` at startup, `FOR SYSTEM_TIME AS OF` queries for the wiki-like history view.
A Page version's row records the `(Kind,Key,Version)` set it rendered with, so history is fully
reconstructable and rollback is a row restore.

## MAI-A5 — Disabled-dependency render guard + Admin Inbox (adds to ADR §6) {#MAI-A5}

If a Page resolves a Theme/Component reference that is **Disabled** (or missing), the render **halts**
(shows a clear block to the user instead of partial output) **and** immediately writes an **Admin Inbox**
message. The inbox is DB-backed and patterned on Prose's `FindingRow` + `FindingsService.Upsert`
(hash `DedupKey` unique index, severity/status enums, dedup). Entity: `AdminInboxMessage`
`{ Id, Severity, Category, Subject, Body, DedupKey(unique), Status, CreatedUtc, ResolvedUtc? }`.

This refines the ADR's `CmsMissingContent` "never crash" placeholder: a *missing/stale* type still
degrades to a placeholder, but a *deliberately Disabled* dependency is a halt-and-notify event.

## MAI-A6 — MindAttic.Vault for all credentials (adds to ADR "Stack") {#MAI-A6}

No secrets in `appsettings`/User Secrets. Wire in `Program.cs`:
`builder.Configuration.AddMindAtticVaultFiles().AddEnvironmentVariables();` then
`builder.Services.AddMindAtticVault(builder.Configuration);`. DB connection string resolves through
`IConfiguration`/env (`ConnectionStrings__Ideas`); LLM/API keys via `LlmCredentialResolver.GetKey(...)`.
Package: `MindAttic.Vault` (local feed `C:\LocalNuGet`, net10.0). **Never** add `<UserSecretsId>`.
(Org-wide form: [HOUSE-LAW-3](../../MindAttic.HouseRules.md#HOUSE-LAW-3).)

## MAI-A7 — MindAttic.Legion for LLM + voting (new optional Core service) {#MAI-A7}

Register `services.AddLLMVoting(new VotingConfiguration())` (zero-config; keys via Vault) and/or
`AddLegionClient()`. Expose `LegionClient` (direct LLM: `CallAsync`) and `LlmVotingService`
(`VoteAsync`/`DecideAsync`/`ScoreAsync`) to Core services. In-proc project/package reference (net10.0);
depends transitively on Vault. Foundation-optional — wired but not load-bearing for Phase 0/1 render.
(Org-wide form: [HOUSE-LAW-4](../../MindAttic.HouseRules.md#HOUSE-LAW-4).)

## MAI-A8 — UiUx three-layer wrapper chain (confirms ADR §7) {#MAI-A8}

Official Components/Themes are sourced from MindAttic.UiUx as: **raw js/css/html → thin UiUx Blazor
wrapper (`.razor`) → CMS citizen (`CmsComponentBase`/`CmsThemeBase`)**. The CMS citizen wraps the UiUx
wrapper and references raw assets by pinned-tag jsDelivr URL mirrored from `deps.json` — **zero
duplication**, UiUx stays build-free source-of-truth.

---

## Open questions resolved by later owner messages
- **Multi-tenancy grain (ADR Open-Q #2):** each project frontend becomes a **Page** (or page subtree) in
  the default Site — *not* its own Site. Sites are reserved for genuinely separate domains. (Owner: "…would
  be converted into a MindAttic.Ideas.Page object…")
- **Frontend collapse path (ADR Open-Q #1):** lean **Data page + inline JS + component tags** (the
  Legion.Frontend example: filters/pagination/modal are inline JS; SacredGeometry/Cyberspace are tags).
  Code pages remain available for genuine Blazor-C# interactivity but are not the default.

## Resolved by ratification
- **Build scope:** foundation first — Phase 0/1 (Abstractions + Core + Web render pipeline), verified
  end-to-end, before Admin/CLI.
- **`.idea` upload collision (ADR Open-Q #3):** **hard-refuse** when an upload's `(Kind,Key)` collides
  with a compiled citizen (compiled is authoritative). Admin-confirmed override is additive later (the
  shadow/priority fields stay reserved).
- **Author demotion (ADR Open-Q #4):** **keep rendering** already-published Author-trust pages (trust
  stamped at write time). A deliberate `AuthorTrustVersion` epoch bump can bulk re-gate if ever needed.

---

# Taxonomy finalization (A9–A14) — supersedes the ADR's vocabulary

These were settled during the Phase-0/1 build and are now the implemented truth. **The ADR's vocabulary
(`Idea` as content noun, `CmsPageBase`/`CmsComponentBase`/`CmsThemeBase`, zone language, `<ma-component>`,
`ContentKind.Widget` meaning a generic widget) is superseded by the below.**

## MAI-A9 — Four content kinds under one shared base `IdeaBase` {#MAI-A9}

The kinds are **Page · Component · Theme · Control** (`ContentKind { Page=0, Component=1, Theme=2,
Control=3 }`, append-only — new kinds may be added later for free). All derive from a shared root
**`IdeaBase`**; each kind has a base: `PageBase` / `ComponentBase` / `ThemeBase` / `ControlBase`. The kind
is determined by which base a type inherits. "Idea" names the shared base and the `.idea` package — never
a kind.

- **Component** = a *capability activator* (e.g. Tooltip): dropping its tag loads its css/js so a behavior
  works page-wide (any `data-tooltip`/`data-tt` element gets a tooltip); renders no widget itself
  (`ComponentBase` emits its `StylesheetUrls`/`ScriptUrls`; activators are code-only classes).
- **Control** = one *atomic placed UI element* (e.g. Textbox → an `<input>`); include-tag attributes flow
  through to the rendered element.
- **Theme** = layout chrome + one `@Body` hole + CSS bundle. **Page** = the page (Data or Code).

> *Superseded:* the kind name "Component" was later renamed to "Plugin" ([A17](#MAI-A17)) and then to
> "Widget" ([A18](#MAI-A18)). The four-kinds-under-`IdeaBase` structure stands.

## MAI-A10 — `ComponentBase` clash resolved by aliasing Blazor's (per [[naming-conflict-aliasing]]) {#MAI-A10}

MindAttic's `ComponentBase` owns the bare name. Blazor's framework base is referenced via
`using BlazorComponentBase = Microsoft.AspNetCore.Components.ComponentBase;` (so `IdeaBase :
BlazorComponentBase`), and Razor `_Imports.razor` aliases the bare name to ours
(`@using ComponentBase = MindAttic.Ideas.Abstractions.ComponentBase`) so `@inherits ComponentBase`
resolves to MindAttic's. Standing rule: on any future framework name clash, surface it and ask before
aliasing — alias the *framework* side so MindAttic's namespace wins the bare name.

> *Superseded:* fully retired by [A17](#MAI-A17) — with no MindAttic type named `ComponentBase`, the
> aliases are deleted. The standing rule for *future* clashes still holds.

## MAI-A11 — Locked tag convention `<MindAttic.Ideas.{ContentKind}.{Name}.{Version} />` {#MAI-A11}

Identity by **convention**: Kind from the base, Name (key, lowercased) from the namespace tail after
`MindAttic.Ideas.{Kind}.`, Version from the `V{n}` class name. Optional `[Idea(key:…, version:…,
scope:Global)]` overrides. The same tag works in **data pages** (the include expander resolves it,
case-insensitively, replacing the earlier `<ma-component>` form) and **code pages** (a real Blazor tag).
Razor forbids lowercase component class names, so the **version token is uppercase `V{n}`** to match the
class exactly.

## MAI-A12 — Version is OPTIONAL; defaults to latest {#MAI-A12}

A tag may omit the version (or use `.Latest`) to resolve the **highest enabled version** from the tables,
or pin exactly with `.V3`. This refines A2's "pin everything": pin when you care, float when you don't, so
composing many co-versioned pieces (e.g. `TabControl` + `TabButton` + `TabPage`) needs no version juggling.
Integrity (A3) is preserved: a version-specific delete is blocked while anything pins it; a floating
reference is valid as long as some enabled version remains.

## MAI-A13 — Self-closing include tags are normalized before parsing {#MAI-A13}

`<MindAttic.Ideas.… />` is not truly self-closing in HTML (the parser would swallow following siblings as
children). The expander normalizes the known `MindAttic.Ideas.*` self-closing tags to explicit paired
tags before AngleSharp parsing, and only passes inner content as `ChildContent` when the resolved type
declares it. A malformed/unresolved/disabled include degrades to a visible placeholder — never a crash.

## MAI-A14 — Vocabulary: no umbrella noun; UiUx is the multi-target source {#MAI-A14}

There is **no umbrella noun** for the four kinds in prose — spell out Page/Component/Theme/Control (the
word "citizen" used during the build is dropped). All official content ultimately lives in **MindAttic.UiUx**
as ONE canonical core distributed as MANY wrappers/exports: raw js/css/html → Blazor wrapper → `.idea`
(MindAttic.Ideas) → later React, Angular, etc. The CMS↔UiUx tie stays thin (load raw assets by pinned-tag
URL; never reimplement). Phase-1 content lives inline in the Web project as a render proof; its permanent
home is UiUx.

> *Updated by [A18](#MAI-A18):* "Widget" is now adopted as the umbrella term for the composable-UI kind.

## MAI-A15 — Deployment: Windows App Service, Prose-style (supersedes IMPLEMENTATION_PLAN §10 "Linux") {#MAI-A15}

MindAttic.Ideas deploys the SAME way Prose does — **NOT Linux**. The plan doc's "App Service
(Linux)" is wrong. Target: a GitHub Actions pipeline on `windows-latest`, **build → migrate → deploy** to
an **Azure App Service (Windows)** + **Azure SQL**:
- **build** — `dotnet publish` the Web host as an artifact; private packages (Vault, Legion, Authentication,
  Psst) restored from a local-packages folder via `NuGet.config` alongside nuget.org.
- **migrate** — apply EF migrations and enable SQL `SYSTEM_VERSIONING` for the temporal `Pages` table,
  authenticated via an **OIDC service principal** with `db_ddladmin` (the App Service managed identity is
  read/write only and cannot run DDL).
- **deploy** — push the artifact to the App Service slot.

Windows hosting means `net10.0-windows` packages (e.g. **MindAttic.Psst**) are fine to depend on. The
auth email channel still uses an `IAuthEmailSender` abstraction (clean packaging/testability + lets Tutor,
if non-Windows, swap the transport), with a Psst-backed implementation for Windows hosts.

## MAI-A16 — Authentication is MindAttic.Authentication, not Ideas-owned (supersedes the ported BCrypt auth) {#MAI-A16}

The canonical auth engine for MindAttic.Ideas is the **[MindAttic.Authentication](https://github.com/mindattic/MindAttic.Authentication)**
Razor Class Library — the same hardened engine Prose and Tutor adopt, so the three authenticate
**identically** instead of each rolling its own. It supersedes the **ported, interim** BCrypt auth now in
Core (`Services/AuthService.cs` + `Entities/AuthEntities.cs`) — the very implementation that package's own
audit flags as "🟡 minimal: BCrypt ✓ but SecurityStamp revalidation unwired, no lockout/MFA." Ideas does
**not** grow its own auth further; new auth capability lands in the package.

**What the package owns** (Argon2id+pepper over a Vault pepper, persistent DB-backed lockout, TOTP +
recovery codes, 8h-absolute/30m-idle `__Host-` cookie, SecurityStamp revalidated ≤60 s, DP key-ring via
Vault, HIBP, audited reset) — built to OWASP ASVS L2/L3 · NIST SP 800-63B AAL2. Its **only** hard
dependency is **MindAttic.Vault** (A6), and its email notices flow through the `IAuthEmailSender` channel
of A15 (Psst-backed on Windows).

**Adoption contract (target shape — the wiring Ideas commits to):**
- `Program.cs`: `builder.Services.AddMindAtticAuthentication(builder.Configuration, o => { o.AppName = "Ideas"; … });`
  then `app.UseMindAtticAuthentication();` (forwarded-headers → authn → authz → antiforgery, order asserted
  by the library's fail-closed `IStartupFilter`) and `app.MapMindAtticAuthEndpoints();` (`/_ma-auth/login`,
  `/logout`, `/change-password`, `/mfa-challenge` — **endpoints own sign-in, never components**).
- `o.AppName = "Ideas"` is the **per-app trust boundary**: per-app `SetApplicationName` + isolated Data
  Protection ⇒ a cookie stolen from Prose/Tutor cannot authenticate to Ideas. **No cross-app SSO in v1.**
- The CMS DbContext applies the package's schema (`b.ApplyMindAtticAuthConfiguration()`), which owns an
  isolated **`auth`** schema; Ideas keeps its own connection and runs `dotnet ef migrations add`. The host
  checks the package's **migration fingerprint** at startup. The interim `Users` table is dropped on adoption.
- Login UI is the package's presentation-only static-SSR `<MaLogin/>` (antiforgery on every POST), branded
  via constrained `AuthUiOptions` (text + allow-listed logo/CSS — never raw markup).

**Mapping onto the existing Ideas trust model:** the `Cms.AuthorRawMarkup` claim and the Admin role
(README "Trust & security" / A-ratification "author demotion") ride on the package's principal — the raw-markup
gate (`IRawContentGate`) keys off that claim exactly as today; only the *issuer* of the principal changes.
The `AuthorTrustVersion` epoch-bump demotion path is unaffected.

**Timing (foundation-optional, like A6/A7):** the package is mid-build (crypto core + canonical EF model
done; DI/middleware/endpoints/components/`MaLogin` not yet shipped) and is **not** in the local feed. Per
its locked order, Ideas adopts **after** the library completes **and after** Prose — at which point
the ported `AuthService`/`User` are deleted and the Phase-2 Admin login wires to the package. Until then the
interim BCrypt auth stands unchanged. This is a ratified direction, not a Phase-0/1 render dependency.
(Org-wide form: [HOUSE-LAW-7](../../MindAttic.HouseRules.md#HOUSE-LAW-7).)

## MAI-A17 — Content kind **Component renamed to Plugin** (supersedes A9's "Component" + all of A10) {#MAI-A17}

The capability-activator kind is **Plugin**, not Component. A Plugin is "a Tooltip Plugin" — code you add
to a page to switch a behavior on. This is a hard rename across the codebase (pre-1.0 foundation, no
back-compat shims):

- Enum member **`ContentKind.Component` → `ContentKind.Plugin`** (ordinal **1** is preserved — the enum is
  append-only on *ordinals*, and this changes only the *name* at the frozen ordinal). `KindNames[1]` and the
  manifest `category` string become **`"Plugin"`**; the include tag/namespace segment is
  **`MindAttic.Ideas.Plugin.{Key}`**; `uses[]` entries read **`Plugin.{key}[@n]`**; the asset mount is
  **`/_ideas/Plugin/{key}/{version}`**.
- Base class **`ComponentBase` → `PluginBase`** (`MindAttic.Ideas.Abstractions`). Library folder
  `Web/Components/Library/Component` → `…/Plugin`.
- **A10 is fully superseded and retired.** With no MindAttic type named `ComponentBase`, the bare name
  `ComponentBase` unambiguously means Blazor's, so the `BlazorComponentBase` alias and the
  `_Imports.razor` `@using ComponentBase = …` alias are **deleted** (`IdeaBase : ComponentBase` now refers to
  Blazor's directly). The [[naming-conflict-aliasing]] standing rule still holds for *future* clashes; this
  particular clash simply no longer exists.

The four kinds are now **Page · Plugin · Theme · Control** under `IdeaBase` (bases
`PageBase` / `PluginBase` / `ThemeBase` / `ControlBase`). All 155 NUnit tests green after the rename.
The frozen `FOUNDATION_ADR.md` still records the original "Component" naming as historical decision text —
this amendment overrides it.

> *Superseded by [A18](#MAI-A18):* "Plugin" was renamed to "Widget".

## MAI-A18 — Content kind **Plugin renamed to Widget** (supersedes A17) {#MAI-A18}

The composable-UI kind is now **Widget**, not Plugin. A Widget spans the full range — from an asset-only
capability activator (Tooltip) up to a complete interactive UI (Frontpage) that **nests other widgets
recursively** via `CmsInclude`. "Plugin" undersold that range; "Widget" is the umbrella term. Hard rename
across **both** repos (pre-1.0 foundation, no back-compat shims):

- Enum member **`ContentKind.Plugin` → `ContentKind.Widget`** (ordinal **1** preserved — name-only change at
  the frozen ordinal). `KindNames[1]` and the manifest `category` string become **`"Widget"`**; the include
  tag/namespace segment is **`MindAttic.Ideas.Widget.{Key}`**; `uses[]` entries read **`Widget.{key}[@n]`**;
  the asset mount is **`/_ideas/Widget/{key}/{version}`**.
- Base class **`PluginBase` → `WidgetBase`** (`MindAttic.Ideas.Abstractions`).
- First-party library (`MindAttic.Ideas.Library`): the `Plugins/` folder → **`Widgets/`**, and every project
  `MindAttic.Ideas.Plugin.{Key}` → **`MindAttic.Ideas.Widget.{Key}`**.
- Data fix: migration **`RenamePluginKindToWidget`** rewrites `ContentDefinitions.Kind`/`Category`,
  `InstalledPackages.Category`, and author include-tags in `Pages.BodyHtml` from `Plugin` to `Widget`
  (forward-only; `Down` is a no-op), mirroring A17's heal.

The four kinds are now **Page · Widget · Theme · Control** under `IdeaBase` (bases
`PageBase` / `WidgetBase` / `ThemeBase` / `ControlBase`). All 166 NUnit tests green after the rename.
This is the **current vocabulary**.

## MAI-A19 — Control kind REMOVED (folded into Widget) (refines A9; breaks the append-only enum) {#MAI-A19}

A `Control` had no behavior a `Widget` lacks: `WidgetBase` exposes the same unmatched-attribute
passthrough (`CaptureUnmatchedValues`), resolves through the identical include path, and can render a
single visible element. So **atomic UI is authored as a Widget**, and `Control` is **removed entirely**
(not merely deprecated) — pre-1.0, before any external package shipped.

- **`ControlBase` / `ControlBase<TSettings>` deleted** from `MindAttic.Ideas.Abstractions`.
- **`ContentKind.Control = 3` removed** from the enum, and `"Control"` dropped from `Packer.KindNames`
  and `CompiledContentSource.KindOf`. This is the **lone, deliberate exception** to the "frozen,
  append-only — never remove" rule (Enums.cs), justified by being pre-1.0 with no shipped packages. The
  ordinal **3 is never reused**; the next new kind appends at 4.
- **Data heal:** migration **`RemoveControlKind`** deletes any `ContentDefinitions`/`InstalledPackages`
  rows with `Kind`/`Category = 'Control'` and rewrites author body tags
  `MindAttic.Ideas.Control.* → MindAttic.Ideas.Widget.*` in `Pages.BodyHtml` (forward-only; `Down` no-op).
- **Library:** `Controls/Textbox` → **`Widgets/Textbox`** (`MindAttic.Ideas.Widget.Textbox`,
  `@inherits WidgetBase`, mount `/_ideas/Widget/textbox/1`); re-packed. The host's bundled seed `.idea`
  and the seeded demo tag now use `{{ MindAttic.Ideas.Widget.Textbox }}`. The duplicate `.idea` projects
  in **MindAttic.UiUx** were removed (the Library is the single home for `.idea`s; UiUx stays raw source).
- Tests updated (the old `Control.Textbox` parse case now asserts a Widget). Theme stays a first-class
  kind (its `@Body` page-wrapper is structural).

**The kinds are now `ContentKind { Page=0, Widget=1, Theme=2 }`** under `IdeaBase`
(`PageBase` / `WidgetBase` / `ThemeBase`).

## MAI-A20 — F7 cross-repo collapse: current-state record (2026-06-08) {#MAI-A20}

MAI-US-F7 ("official content lives in MindAttic.UiUx; MindAttic.Frontpage / MindAttic.Legion.Frontend
collapse into Pages") is cross-repo work that cannot be completed inside `MindAttic.Ideas` alone.
State as of 2026-06-08:

- **MindAttic.Ideas** seeds a `frontpage` Code page and a home Data page; the home page already
  demonstrates zero-deploy widget composition via `{{ MindAttic.Ideas.Widget.Tooltip }}` tokens.
- **MindAttic.Ideas.Library** (sibling repo) is the single `.idea` home for all first-party widgets
  (A19 removed the duplicate `.idea` projects from UiUx; UiUx remains raw multi-target source).
- **MindAttic.Authentication** (F4 ✅) and the **Monaco editor** (F8 ✅) are the in-Ideas
  preconditions for a complete authoring experience; both are now shipped.
- **MindAttic.Frontpage** and **MindAttic.Legion.Frontend** remain standalone apps; they will be
  replaced by Data pages + Widget `.idea` files once UiUx extraction is scheduled.
- No code change in `MindAttic.Ideas` is required for F7 itself: the seed, catalog, widget palette,
  Monaco editor, and upload pipeline are all in place.

## MAI-A21 — The Frontpage: mindattic.com as a Data page + bare-route forward (supersedes A20's seed record) {#MAI-A21}

**What changed (2026-06-09).**
- The seeded `frontpage` page is no longer a compiled Code page (`MindAttic.Ideas.Page.Frontpage.V1`).
  It is now a **Data page** that recreates the mindattic.com look from the baseline widget set
  (MindAttic.Ideas.Library, MAIL-A3): `{{ MindAttic.Ideas.Widget.Tabs }}` (the `ma-tabs-board`
  project boards for Software/Hardware), `{{ MindAttic.Ideas.Widget.Gallery }}` (the Writing books
  grid + Visual Arts), and `{{ MindAttic.Ideas.Widget.Footer }}` (pin-when-short), through the
  Cyberspace theme. Layout is plain flex in author HTML (no layout system); page CSS rides at the
  top and page JS at the bottom of the body; cover images are inline base64 CSS classes.
- **The bare route forwards to the Frontpage.** `PageHost` no longer resolves the `""` slug to a
  page: it forwards to the slug named by the Host setting **`page.frontpage`** (default
  `frontpage`). The seeded `""` home demo page is retired — an untouched stock copy is
  soft-disabled by the seed (HOUSE-LAW-2), an admin-edited one is left untouched (and reachable
  again by re-pointing the setting).
- **Seed migration, never clobber:** a DB still carrying the stock compiled frontpage is migrated
  in place to the Data recreation (a row edit — Data ↔ Code graduation is never a schema change);
  any admin-authored frontpage is not recognized as stock and is never overwritten.

**Why.** The product goal is "recreate whole sites from reusable widgets + a Page record" — the
CMS's own front door should be the proof. A compiled accordion page demonstrated the Code path but
not the product promise; the Data recreation exercises upload → install → token → render end to end
and is editable in Monaco with zero deploys.

**Proof.** `SeededPageRenderTests` (NUnit): `FrontpageBody_AllSeedTokens_ParseFromTheRealSeededPage`,
`Seed_MigratesStockCodeFrontpage_ToDataPage_ButNeverAnAdminPage`,
`Seed_SoftDisablesStockHomePage_AndNeverAnEditedOne`,
`SeedBody_InstalledTabsWidget_ExpandsToResolvedFrame`. Suite: 199 passed / 0 failed (2026-06-09). Live proof: GET / → 302 → /frontpage; the rendered frontpage shows zero ma-missing placeholders with all 33 library .ideas installed (attended run 2026-06-09).

## MAI-A22 — F7 complete + RFC 0001 implemented: the CMS reaches its definition of done (supersedes A20's "pending" items) {#MAI-A22}

**What changed (2026-06-09).**
- **MAI-US-F7 is complete.** Both standalone frontends are collapsed into Pages:
  `MindAttic.Frontpage` → the `frontpage` Data page (mindattic.com recreated verbatim, A21), and
  `MindAttic.Legion.Frontend` → the seeded **`personas`** Data page, whose whole body is one token —
  `{{ MindAttic.Ideas.Component.LegionPersonas }}` — through the Cyberspace theme. Verified live:
  `/personas` renders the full gallery with zero placeholders. "Official content lives in
  MindAttic.UiUx" is restated per A19/A20 reality: **MindAttic.Ideas.Library is the single home of
  first-party `.idea` content** (43 components — 8 Themes + 12 Plugins + 23 Components per [A26](AMENDMENTS.md#MAI-A26)); UiUx remains an upstream raw-source repo and is no
  longer on the Ideas critical path.
- **RFC 0001 is fully implemented** (marked `status: implemented`):
  - *Typed-attribute coercion* — a `{{token}}` attribute matching a declared typed `[Parameter]` on
    the resolved component coerces to bool/int/double/decimal/enum (Nullable unwrapped) in the ONE
    shared `EmitInclude` path; unmatched attributes stay raw for the `CaptureUnmatchedValues` bag; a
    failed conversion falls back to the raw value (a render never throws).
  - *Clickable upload-to-fix placeholders* — `MissingContent` renders as a LINK to
    `/admin/upload?missing=<reference>`, and the admin Upload panel reads `?missing=` and shows
    which `.idea` the page is waiting on.
- **MAI-US-B5 is complete.** The live SQL Server temporal proof ran against the dev LocalDB:
  `GetHistoryAsync` returned multiple ordered temporal versions of the much-edited frontpage row
  (`PageHistorySqlServerTests`, [Explicit], passed 2026-06-09).

**Proof.** 210 NUnit tests green (`IncludeAttributeCoercionTests` ×9,
`RenderGuardTests.MissingPlaceholder_LinksToAdminUpload_WithTheMissingKey`,
`SeededPageRenderTests.Seed_CreatesPersonasPage_CollapsingLegionFrontendIntoOneToken`) plus the
explicit SQL Server temporal test; live render checks for `/personas` and `/frontpage` (0 missing).
With this amendment every MAI user story is ✅ (or 🗑️) — the foundation-era definition of done is met.

## MAI-A23 — Library mono-repo consolidation: `library/` merged into the Ideas repo {#MAI-A23}

**What changed (2026-06-12).**
`MindAttic.Ideas.Library` (the first-party widget/theme library, formerly a sibling repo) was
merged into this repo under the **`library/`** subdirectory. The sibling GitHub repo is retired
and can be deleted. The two halves of the project are now:

- **`src/`** — the CMS engine (unchanged; stand-alone `.slnx`: `MindAttic.Ideas.slnx`).
- **`library/`** — the widget/theme library (stand-alone `.slnx`: `library/MindAttic.Ideas.Library.slnx`).

**Key structural facts:**
- The two solutions are **independent**: the CMS never references the library as a project; it
  only installs packed `dist/*.idea` files as optional content (copied to
  `src/MindAttic.Ideas.Web/library/` on pack). The library references only
  `src/MindAttic.Ideas.Abstractions` as a `Private=false ExcludeAssets=runtime` project reference
  (so Abstractions is not bundled into the `.idea`; the host provides it at runtime).
- **`library/Directory.Build.props`** carries the single, intra-repo path fix:
  `$(MSBuildThisFileDirectory)..\src\MindAttic.Ideas.Abstractions\...` — each widget `.csproj` is
  ~3 lines.
- **`library/.gitignore`** covers library-specific build artifacts (`**/artifacts/`,
  `Themes/**/dist/`, `Widgets/**/dist/`, `/dist/`).
- The CMS Web host ships **43** first-party `.idea` files in `src/MindAttic.Ideas.Web/library/`
  (8 Themes + 12 Plugins + 23 Components — MAIL-A6) — verified by `ma-idea verify` (compose-graph green).

**Why.** Single-repo maintenance: git history, issues, PRs, and CI stay unified while the engine
and the library remain build-independent. No external reference change is needed because the CMS
loads `.idea` blobs at runtime, not project references.

## MAI-A24 — Page Properties panel + SEO metadata wired end-to-end {#MAI-A24}

**What changed (2026-06-12).**

- **Collapsible "Page Properties" panel in the admin page editor** (`Web/Components/Pages/Admin/Pages.razor`):
  the flat property grid is now a `<details>` element with an animated chevron, a hint line
  (`/slug · theme-key`) in the `<summary>`, and a CSS rule set in `app.css`.
- **SEO Title / SEO Description** fields added to the panel. They write to the pre-existing but
  previously unread `Page.SeoMetaJson` JSON column via the new `SeoMeta` helper class in
  `PageAdminService.cs` (serializes `{title,description}` as camelCase JSON; null when both are
  blank — no migration required).
- **`PageEditModel`** gains `SeoTitle` and `SeoDescription` properties; `GetAsync` deserializes
  the JSON column on load; `SaveAsync` serializes it on save.
- **`PageHost.razor`** now renders `<PageTitle>` from `seo.title` (falling back to `Page.Title`)
  and emits a `<meta name="description">` tag when `seo.description` is set, both populated via
  the `IPageContext.Meta` dictionary (the pre-existing-but-empty seam in Abstractions).
- **Theme** dropdown was already implemented via the pre-existing `ThemeKey`/`ThemeVersion` DB
  columns; A24 moves that assignment into the new collapsible panel and labels the route field
  "Route" (was "Slug").

**Proof.** 7 new NUnit tests in `PageAdminServiceTests`:
`SeoMeta_Parse_ReturnsNull_ForNullOrEmpty`, `SeoMeta_Parse_ExtractsFields`,
`SeoMeta_Parse_ReturnsNull_ForMalformedJson`, `SeoMeta_Serialize_ReturnsNull_WhenBothFieldsNull`,
`SeoMeta_Serialize_ReturnsJson_WhenAnyFieldSet`,
`Save_WithSeoFields_PersistsThroughGetAsync`, `Save_WithNullSeoFields_LeavesJsonNull`.
Suite: **224 NUnit green** (7 new tests in `PageAdminServiceTests` +
3 new in `PageTreeFeatureTests` + 4 new in `ArgParserTests`).

## MAI-A25 — DNN-parity features: dependency checks, widget settings versioning, content workflow, slug redirect history {#MAI-A25}

**What changed (2026-06-13).** Four features that restore DNN-era capabilities in the .idea model:

### Feature 1 — Manifest dependency checks at install time

`IdeaManifest` gains two new fields: `minHostVersion` (int?, blocks install if the running host engine
version is below this threshold) and `requires` (string[], same `"Category.key[@n]"` grammar as `uses[]`
but enforced as a **hard install-time gate**). `ManifestValidator.Validate()` accepts an optional
`hostEngine` parameter (defaults to `IdeaManifest.HostEngineVersion = 1`) and emits a
`MIN_HOST_VERSION_UNMET` hard error when `minHostVersion` exceeds it.
`PackageInstallService.InstallAsync()` walks `requires[]` before persisting any bytes: any missing or
disabled dependency throws `InstallException("REQUIRES_UNMET: …")` with zero DB writes. This contrasts
with `uses[]`, which remains advisory-only (raises `AdminInboxMessage` at render time).

**Proof.** `ManifestValidatorTests`: `MinHostVersion_AbsentOrAtHost_IsValid` (×3 cases),
`MinHostVersion_ExceedsHostEngine_IsHardError`; `PackageInstallServiceTests`:
`Requires_AllPresent_InstallSucceeds`, `Requires_Missing_ThrowsInstallException_NoRowsWritten`.

### Feature 2 — Host-managed widget instance-settings versioning with rollback

`WidgetPlacementSettings` (PageId, SlotName, WidgetRef, SettingsJson, SettingsVersion, Uid) stores
per-placement configuration. Every `SaveAsync` call snapshots the current row into
`WidgetPlacementSettingsHistory` before overwriting, so version history is preserved without temporal
tables. `RollbackAsync(pageId, slot, version)` restores a snapshot's JSON while advancing the version
counter (version never decreases). Service: `IWidgetInstanceSettingsService` /
`WidgetInstanceSettingsService`; DI-registered as `AddScoped`.

**Proof.** `WidgetInstanceSettingsServiceTests` (7 NUnit): `Save_Create_PersistsVersionOne`,
`Save_Update_BumpsVersionAndWritesHistory`, `Save_MultipleUpdates_AccumulatesHistory`,
`Rollback_RestoresPreviousSettingsAndBumpsVersion`, `Rollback_UnknownVersion_ReturnsFalse`,
`GetAsync_UnknownSlot_ReturnsNull`, `GetHistoryAsync_UnknownSlot_ReturnsEmpty`.

### Feature 3 — Named-state content workflow with role-gated transitions

`WorkflowDefinition` (Name, InitialState, IsDefault) + `WorkflowTransitionDef` (FromState, ToState,
RequiredRole, Label) define named state machines. Pages carry `WorkflowDefinitionId` (nullable FK) and
`WorkflowState` (nvarchar 64). `WorkflowService.TransitionPageAsync` validates the transition exists,
checks `ClaimsPrincipal` against `RequiredRole` (Admins bypass all role gates via `MaRoles.Admin`), and
syncs `Page.IsPublished` (only the state named `"Published"` sets it true; all others clear it). Creating
a definition with `isDefault: true` atomically demotes the prior default. `IWorkflowService` /
`WorkflowService`; DI-registered as `AddScoped`. Manifests declare `defaultWorkflow` (string, advisory;
not yet enforced by the host at install time — future extension point).

**Proof.** `WorkflowServiceTests` (9 NUnit): `CreateDefinition_Persists_WithInitialStateAndTransitions`,
`CreateDefinition_IsDefault_DemotesPreviousDefault`, `TransitionPage_ValidTransition_ChangesWorkflowState`,
`TransitionPage_ToPublished_SetsIsPublishedTrue`, `TransitionPage_FromPublishedToDraft_SetsIsPublishedFalse`,
`TransitionPage_MissingTransition_ReturnsError`, `TransitionPage_InsufficientRole_ReturnsError`,
`TransitionPage_AdminBypassesRoleGate`, `AssignWorkflow_SetsWorkflowAndInitialState`.

### Feature 4 — Auto-301 slug history and vanity redirects

`PageSlugHistory` (PageId, OldSlug, IsVanity, AddedByUserId, CreatedUtc) records old and vanity slugs.
`PageAdminService.SaveAsync` automatically writes a `PageSlugHistory` row whenever the slug changes
(non-vanity, `IsVanity = false`). `SlugRedirectService.CheckRedirectAsync` looks up the old slug and
returns a `SlugRedirectResult(TargetSlug, StatusCode: 301)` — null when no history row matches, when
the page is unpublished/disabled, or when the old slug is identical to the current slug (no self-redirect).
`AddVanityRedirectAsync` is idempotent (duplicate slug is a no-op returning true). `PageHost.razor` calls
`CheckRedirectAsync` before returning 404 and uses `NavigationManager.NavigateTo` for the redirect.
`ISlugRedirectService` / `SlugRedirectService`; DI-registered as `AddScoped`.

**Proof.** `SlugRedirectServiceTests` (7 NUnit): `CheckRedirect_NoHistory_ReturnsNull`,
`CheckRedirect_MatchingHistory_Returns301ToCurrentSlug`, `CheckRedirect_SameSlugInHistory_ReturnsNull`,
`CheckRedirect_UnpublishedPage_ReturnsNull`, `AddVanityRedirect_WritesIsVanityEntry`,
`AddVanityRedirect_Idempotent_DoesNotDuplicate`, `AddVanityRedirect_UnknownPage_ReturnsFalse`.

**Schema.** Single migration `20260613200000_AddWorkflowSlugHistoryAndWidgetSettings` creates five tables
(`WorkflowDefinitions`, `WorkflowTransitionDefs`, `PageSlugHistory`, `WidgetPlacementSettings`,
`WidgetPlacementSettingsHistory`) and adds `WorkflowDefinitionId` + `WorkflowState` columns to `Pages`.

## MAI-A26 — Widget kind split into Plugin (site-wide) and Component (inline-placed) {#MAI-A26}

**What changed (2026-06-16).** The single `Widget` kind (ordinal 1) conflated two fundamentally
different scoping semantics. Widget is retired and replaced by two distinct kinds:

- **Plugin** (ordinal **1**, frozen ordinal preserved — name-only change, exactly as A17 and A18 did):
  a site-wide `.idea` that *activates* a behavior or capability across the entire rendered page without
  occupying a specific token position. Examples: Tooltip (global `data-tooltip` behavior), OutfitFont
  (loads a font family globally), NavMenu (renders site-wide navigation), SacredGeometry (global
  background animation). Plugins are selected per-page via the Admin Page Properties **Plugin checkbox
  list** (see below). Base: **`PluginBase`**.
- **Component** (ordinal **4**, new — ordinal 3 remains reserved per A19, never reused): an
  inline-placed `.idea` that renders at the exact `{{Component.X}}` token position in the page body.
  Components can nest other Components, enabling composite UIs — e.g., `Component.TabControl` contains
  `Component.TabButtonContainer`, a list of `Component.TabButton` instances, a
  `Component.TabPageContainer`, and a list of `Component.TabPage` instances; each `TabPage` may contain
  `Component.Textbox` or other children. Sub-component dependencies declared via `[Uses]`/`uses[]`. Base:
  **`ComponentBase`** (see alias note below).

**The four kinds are now `ContentKind { Page=0, Plugin=1, Theme=2, Component=4 }`** under `IdeaBase`
(`PageBase` / `PluginBase` / `ThemeBase` / `ComponentBase`). `WidgetBase` is deleted.

**`ComponentBase` namespace alias.** Introducing `ComponentBase` in Abstractions restores the name
collision with Blazor's `Microsoft.AspNetCore.Components.ComponentBase` (dissolved by A17, now
returning). Per the standing rule in A10 (the rule survives even though A10 is otherwise superseded):
the MindAttic kind wins the bare name. Blazor's base is aliased:

```csharp
using BlazorComponentBase = Microsoft.AspNetCore.Components.ComponentBase;
public abstract class IdeaBase : BlazorComponentBase { … }
public abstract class ComponentBase : IdeaBase { … }   // MindAttic's
```

The Web project's `_Imports.razor` also adds
`@using ComponentBase = MindAttic.Ideas.Abstractions.ComponentBase` so `@inherits ComponentBase`
resolves to MindAttic's in all Razor files.

**Admin Page Properties panel.** The collapsible properties panel (A24) gains a **Plugin selection
section** — a checkbox list with a vertical scrollbar — inserted between the Theme dropdown and the SEO
fields. Each checkbox corresponds to an installed, enabled Plugin; checking it activates that Plugin for
the page. The selection is persisted as `Page.ActivePluginsJson` (a new nullable JSON column: an array of
`"Plugin.key[@n]"` strings, e.g. `["Plugin.tooltip", "Plugin.navmenu.V1"]`). `PageHost.razor` reads this
column and emits each selected Plugin's include in the render pipeline alongside the Theme's assets,
before the page body renders.

**Inline override tags (non-canonical path).** Two inline token forms let authors escape the normal
admin-selection paths when needed:
- `{{Plugin.tooltip}}` anywhere in `BodyHtml` activates that Plugin even if it is absent from
  `ActivePluginsJson` — useful for a one-off page-level opt-in. Not the recommended path.
- `{{Theme.cyberspace}}` anywhere in `BodyHtml` overrides the page's Theme for asset injection on that
  page (the tag itself renders no markup; only the asset cascade changes). Allows per-page theme overrides
  without touching the admin properties panel.

Both forms follow the existing `IncludeReferenceParser` grammar (first segment must be a valid
`ContentKind` member name); they are recognized automatically once Plugin and Component are valid enum
members.

**Library reclassification.** All existing Widgets in `library/` are reclassified as Plugin or Component.
Classification criterion: *does the widget activate a behavior across the whole page (Plugin) or does it
render at a specific lexical position (Component)?*

- **Plugins** (site-wide): `tooltip`, `outfitfont`, `atticfont`, `sacredgeometry`, `cyberspace` (widget),
  `navmenu`, `breadcrumbs`, `footer`, `pinfooter`, `backtotop`, `backhomem`, `sociallinks`
- **Components** (inline-placed): `textbox`, `card`, `accordion`, `tabs`, `tabboard`, `gallery`,
  `carousel`, `callout`, `codeblock`, `videoembed`, `contactform`, `modalpopup`, `hero`, `hardwarehero`,
  `tableofcontents`, `legionpersonas`, `ideasbrochure`, `helloworld`, `websnapshot`, `claudia`, `chimesh`,
  `mindatticfrontpage`, `frontpage`

Namespaces and asset mounts change accordingly: `MindAttic.Ideas.Plugin.{Key}` /
`/_ideas/Plugin/{key}/{version}` and `MindAttic.Ideas.Component.{Key}` /
`/_ideas/Component/{key}/{version}`.

**Data migration** `AddPluginComponentKindSplit` (forward-only, `Down` is no-op):
1. Adds `ActivePluginsJson nvarchar(max) NULL` to `Pages`.
2. Renames `"Widget"` → `"Plugin"` or `"Component"` in `ContentDefinitions.Kind`/`Category` and
   `InstalledPackages.Category` per the classification table above.
3. Rewrites `MindAttic.Ideas.Widget.{key}` → `MindAttic.Ideas.Plugin.{key}` or
   `MindAttic.Ideas.Component.{key}` in `Pages.BodyHtml` per the classification table.
4. Rewrites `"Widget."` prefix → `"Plugin."` or `"Component."` in `WidgetPlacementSettings.WidgetRef`
   per the classification table.

## MAI-A27 — Three append-only SDK additions: plugin slots, child identity, batch metadata {#MAI-A27}

**What changed (2026-09-04).** Building the MindAttic site *on* the CMS surfaced three gaps in the
frozen SDK. All three are additive under [MAI-LAW-2](BIBLE.md#MAI-LAW-2) — new enum, new init-only
attribute property, new interface default method — so `MAJOR` stays pinned at 1 and every already-packed
`.idea` keeps its exact meaning.

**1. `PluginSlot` — a Plugin declares where it renders.** Every active plugin rendered *before* the
theme/body, so a footer plugin landed at the top of the page. New append-only enum:

```csharp
public enum PluginSlot { BeforeBody = 0, AfterBody = 1 }
```

surfaced as `IdeaAttribute.Slot`, defaulting to `BeforeBody` — the behavior that already existed, so no
shipped plugin moves. `PageHost` reads it off the **type** (no instantiation) and partitions the active
plugin list into a pass before the theme/body and a pass after, preserving author order within each.
First consumer: `Plugin.poweredby` (MAIL-A7 in the [library amendments](../library/docs/AMENDMENTS.md)).

**2. `ChildPage.PageId` — a listed child is identifiable.** `IPageTree` gave a component a child's slug
and title but not its identity, so a component rendering an index could not look up anything *about* the
pages it listed. `ChildPage` gains a trailing, defaulted `Guid PageId`; the positional record stays
source-compatible and `PageTreeFeature` populates it from `Page.Uid`.

**3. `IComponentMetadataStore.GetManyAsync` — one query for a list.** With (2), an index over N children
would issue N metadata round trips per render. The batch read is a **default method** that loops
`GetAsync`, so any existing host keeps working unchanged, and `ComponentMetadataService` overrides it
with a single query.

**Also corrected: renames now emit a real 301.** `PageHost` claimed "Auto-301" in comment but redirected
via `NavigationManager.NavigateTo`, which can only produce a 302 — telling search engines the *old* URL
was still canonical. `SlugRedirectResult.StatusCode` was, in consequence, dead code. The host now writes
the declared status and `Location` directly when the response has not started, falling back to
`NavigateTo` when it has.

**Site-level default chrome.** A page whose `ActivePluginsJson` selects nothing now inherits the
site setting `plugins.default`, so every page in a site gets its nav/footer without per-page
bookkeeping. A page's own selection always wins.

## MAI-A28 — Brace tokens are retired; the tag form is the composition grammar {#MAI-A28}

**What changed (2026-09-04).** The bible documented composition as `{{Kind.Key[.Vn]}}` in six places,
and the shipped Ideas brochure taught the same. The code had already moved on: `IncludeExpander` and
`IncludeReferenceParser` recognise **only** the PascalCase tag form, and `SeedService` rewrites any
surviving brace token at startup. Canon and code disagreed, and the canon lost.

**The grammar is the tag form**, in author-trusted page bodies:

```html
<Component.Textbox />
<Plugin.Tooltip />
<Theme.Cyberspace />
<Component.TabBoard alwaysShowTabPage="true" />
<Component.TabBoard data-version="2" />      <!-- pin a version; omit to float to latest -->
```

`{{ … }}` is **not** part of the grammar and is never re-added. It survives only as a migration input:
`SeedService.ApplyLegacyMigration` converts brace tokens (parameters included, per
[MAI-A27](#MAI-A27)) into tags, so old content is upgraded in place rather than silently rendering the
token as visible text — which is exactly what had been happening on the MindAttic front page.

**Why the tag form won.** It parses as HTML, so AngleSharp handles nesting, attributes, and quoting
instead of a bespoke regex; the reference guard and the head-asset hoist read the same tree the renderer
does ([MAI-LAW-3](BIBLE.md#MAI-LAW-3)); and untrusted content degrades safely, since an unknown
PascalCase element is lowercased into an inert unknown tag rather than expanded.

> *Corrects prose in [MAI-A26](#MAI-A26) and the bible glossary, both of which described the retired
> brace form. The kinds, ordinals, and everything else in A26 stand unchanged.*

## MAI-A29 — A component can list another page's children; inline media has a migration path {#MAI-A29}

**What changed (2026-09-04).** Rebuilding the MindAttic home page as composition rather than a pasted
blob needed two more capabilities.

**1. `IPageTree.ChildrenOfSlugAsync`.** `IPageTree` could only list the *current* page's children, so a
home page could not show the projects index. The new method takes a **slug**, because a slug is what an
author can type into a tag attribute. Append-only: it is a default method returning empty, so a host that
has not implemented it degrades to an empty list rather than breaking; `PageTreeFeature` overrides it.
Consumed by `Component.projectgrid`'s `From` attribute.

**2. `--extract-media`.** Base64-inlining was the authoring convention of the hand-written MindAttic
sites, and it is why a page body reached hundreds of kilobytes — 96% of the front page's body was seven
book covers encoded into its markup. Inlined bytes cannot be cached, shared between pages, or managed.

The CLI walks Data page bodies, uploads each inline image through `IMediaStore`, and rewrites the
`<img>` as `<Component.MediaImage uid="…" />`. Assets are keyed by SHA-256, so identical bytes upload
once however many pages reference them. Content is never destroyed: data that matches the base64
alphabet but fails to decode is left inline and reported as a failure, and a `src` outside that alphabet
was never an inline image, so it is left untouched and reported as nothing.

Applied to `/frontpage`: **179,321 → 5,202 characters** of body.

**The direction this sets.** A page body is markup; bytes are managed assets served from `/_media/{uid}`.
Large media that should not live in the database (video) is the open case — `MediaStoreOptions.MediaRoot`
is the existing disk seam, and an Azure Blob provider behind the same `IMediaStore` is the intended
route, with the page still referencing an asset rather than a URL.

## MAI-A30 — `--extract-media` covers stylesheets; the home page carries no page JS {#MAI-A30}

**What changed (2026-09-04).** Extends [MAI-A29](#MAI-A29).

**Stylesheets.** `--extract-media` now lifts CSS `url(data:…;base64,…)` out of `PageCss` as well as
`<img>` out of `BodyHtml`. CSS cannot reference a component, so these rewrite to the raw
`/_media/{uid}` endpoint rather than to a `MediaImage` tag, and the asset is named after the custom
property it is assigned to (`--bg-abstract-dark` → `bg-abstract-dark.png`) so Admin → Media reads as
names. Body and stylesheet share one asset when the bytes match, because deduplication is by SHA-256
across the whole run.

**A defect this surfaced, which the CMS did not introduce.** `mindattic.com`'s
`--bg-abstract-light` and `--bg-abstract-dark` hold **truncated** base64 — length not divisible by 4 —
and have **zero `var()` references**. 246KB of broken, unreferenced data has been shipping on every
page load of the live site, and is present in `mindattic.com/index.htm` itself, not just the CMS copy.
Removed from the page here; the source repo still has it. This is why the tool leaves undecodable data
inline and reports a failure rather than dropping it: the failure is the signal.

**Page JS.** `/frontpage` now stores **no `PageJs` at all**. The 144KB it held was already inert —
its `init()` returns early unless `window.TabBoard` is present, and that activator went with the static
board markup the repo grids replaced. The data it carried (portfolio blurbs, book synopses) is markup
now, so it is readable without script and visible to search engines instead of appearing only on click.

**Where the home page landed.** Body 5,202 → 10,032 characters (it gained the recovered blurbs and
synopses), `PageCss` 261,171 → 20,006, `PageJs` 144,332 → 0. Rendered: **624,070 → 57,064 bytes**, zero
inline base64, 13 managed media references.

**The general shape, for the next site.** A page is markup plus a stylesheet; bytes are managed assets;
behavior is a Plugin, not a `<script>` block in a page row. An asset-only activator Plugin must be
removed along with the markup it wires, or it silently loads CSS and JS for nothing.

## MAI-A31 — Media has two backing stores, chosen by config; `/_media/{uid}` is the contract {#MAI-A31}

**What changed (2026-09-04).** Completes the open case [MAI-A29](#MAI-A29) named: *"Large media that
should not live in the database (video) is the open case."*

**The contract.** `/_media/{uid}` is the only thing a page ever references. Behind it, the backing store
is chosen by `Media:Provider` — `local` (default, unchanged) or `azure`. Switching the provider changes
no page markup, no component, and no stored row. A page says *which asset*; it never says *where the
bytes live*.

**How a blob-backed asset is served.** When the store can mint a URL for an item, `/_media/{uid}` answers
**302** to a short-lived SAS URL and Azure serves the bytes; otherwise it streams them through the app.
This is not an optimisation, it is the whole point: a video needs HTTP Range so the player can seek, and
Range is the storage service's job, not the CMS's. A signed URL also means the container stays private —
no anonymous blob access — while a public/CDN container is still available via `PublicRead` +
`PublicBaseUri`, which hands out a plain, cacheable URL with no query string.

**Streaming, both stores.** Neither store buffers a payload to compute its hash any more. `MediaStreams.
CopyAndHashAsync` reads the source **once, sequentially** — the only thing a browser upload stream or a
request body can offer — hashing in flight. `ThresholdSpillStream` keeps a payload in memory only up to
`InlineThresholdBytes` and spills to its destination past that, so inline-vs-blob is decided *without*
knowing the length up front. Memory is bounded by the threshold (2 MB), not by the file. The previous
code did `CopyToAsync(new MemoryStream()).ToArray()`: a 400 MB video meant ~800 MB of transient LOH.

**Three defects this fixed in the pre-existing Azure provider**, none of which would have survived
contact with a real video:
1. **Blobs were uploaded with no `Content-Type`.** Every asset served as `application/octet-stream`, so a
   redirected video downloaded instead of playing.
2. **`GetAsync` re-authenticated the stored URI with a fresh `DefaultAzureCredential`**, ignoring the
   configured connection string — a connection-string deployment could write but never read. Reads now
   resolve through the one configured container client.
3. **The endpoint redirected to any `https://` `BlobUri` verbatim**, which 403s against a private
   container. Redirects now go only to a URL a signer actually minted.

**Endpoint, otherwise.** Streamed responses now carry `Accept-Ranges` (206 on Range), an ETag from the
stored SHA-256 (304 on `If-None-Match`), `Last-Modified`, and `Cache-Control`. `video/*` and `audio/*`
join `image/`, `text/` and PDF as inline dispositions.

**Interface change.** `IMediaStore` gains `GetMetaAsync(uid)` — the row alone, no payload. A caller
deciding *how* to serve an item (redirect, 304, stream) must not pull the whole blob down to find out.
MindAttic.Media and MindAttic.Media.Azure both go to **V2** (HOUSE-LAW-1); the Ideas Core reference moves
to `2.0.0`.

**Credentials.** `Media:Azure:ConnectionString` / `BlobServiceUri` resolve through the Vault chain — a
`"Media"` bucket was added to the Ideas Vault bucket list (HOUSE-LAW-3). `Media:Provider=azure` with
neither credential **fails closed at startup** rather than falling back to disk: a deployment that
believes it is on blob storage and is not would lose every upload on the next app-service restart.

**Getting a video in.** `--upload-media <file…> [--folder site] [--media-type video]` streams local files
straight into the configured store. The Admin Media panel is right for an image and wrong for a 400 MB
video, which would have to cross a SignalR circuit first.

**Verified end-to-end** against the Azurite emulator, through the running app: a 40 MB upload landed in
blob storage with `Bytes` NULL and a SHA-256 matching the source file exactly; `/_media/{uid}` answered
302 to a 30-minute SAS; following it returned all 41,943,040 bytes at the same hash; and a Range request
seeked to byte 20,000,000 for `206 · content-range: bytes 20000000-20000999/41943040 · content-type:
video/mp4`. Pre-existing inline rows still stream unchanged under the Azure provider, so switching a live
deployment does not strand the assets already in the database.

**What this does not do.** Nothing migrates existing inline rows into blob storage, and `DeleteAsync`
stays soft (HOUSE-LAW-2) — the blob is deliberately left behind, so reclaiming storage is a separate,
explicit operation.

## MAI-A32 — The deployment is code: infra, CI, and a vendored feed {#MAI-A32}

**What changed (2026-09-04).** MindAttic.Ideas can be stood up on Azure from this repo. Nothing was
provisioned by this amendment; what landed is the means, verified as far as it can be without
spending money.

**The estate is one deployment.** `infra/main.bicep` provisions sixteen resources: an App Service
plan + web app, an Azure SQL server + `MindAtticIdeas` database, a storage account with `media` and
`dp-keys` containers, a Key Vault with the `dp-protect` RSA key, and five role assignments. That is
the whole thing — the CMS hosts many pages from one deployment ([§1](BIBLE.md#MAI-§1)), so there is
nothing per-page in the infrastructure.

**Everything is passwordless.** The web app has a system-assigned managed identity and reaches SQL,
Blob Storage and Key Vault through it. The SQL server is **Entra-only** (`azureADOnlyAuthentication`),
the storage account has `allowSharedKeyAccess: false` and `allowBlobPublicAccess: false`, and the
connection string carries `Authentication=Active Directory Default` — so there is no database
password, no storage key and no client secret in the repo, in CI, or in app settings
([HOUSE-LAW-3](../../MindAttic.HouseRules.md#HOUSE-LAW-3)).

**Least privilege at the database.** The app identity is granted `db_datareader` + `db_datawriter`
and nothing more. It *cannot* issue DDL, which is the enforcement of what was previously only a
convention: `MigrateAsync` runs in Development only, and schema changes come from the migrate job.

**A CI problem that would have failed confusingly.** Ideas references six private packages from
`C:\LocalNuGet` and `../local-feed`. A GitHub runner has neither — and **NuGet tolerates a missing
local source silently**, so the failure surfaces as `NU1101: package not found` for a package that
plainly exists on the dev box. `lib/local-packages/` now holds a git-tracked copy of each (the
closure is exactly those six; none has a transitive MindAttic dependency), `nuget.config` lists it
first so dev and CI resolve identically, and `.gitignore` re-includes it against the global
`*.nupkg` exclusion. `DeploymentPackagingTests` fails the build when a `PackageReference` is bumped
without vendoring the matching `.nupkg` — the one thing between a one-line version bump and a red
deploy.

**Three gated CI stages** (`.github/workflows/azure-deploy.yml`): **build** (restore → Release build
→ the full NUnit suite → publish → emit the idempotent migration script), **migrate** (apply it
under an Entra token, opening and closing a single-run SQL firewall rule), **deploy** (push the
artifact, then poll `/_health` until 200). Deploy proceeds when migrate is *skipped* but never when
migrate *ran and failed*: shipping code against a schema that did not apply is how a production
database ends up half-migrated.

**A health endpoint, under the reserved prefix.** `/_health` is a liveness probe that deliberately
does **not** touch the database — App Service restarts an instance that fails its health check, and
a transient SQL blip must not become a restart loop. It sits under `/_` with `/_media`, `/_ideas`
and `/_ma-auth` so it can never shadow a page slug.

**Three security advisories cleared on the way.** `System.Security.Cryptography.Xml` 10.0.8 had
picked up five HIGH advisories since it was pinned (it was itself an override for a vulnerable
transitive 9.0.0) → 10.0.11. `AngleSharp` 0.17.1 carried GHSA-pgww-w46g-26qg → 1.7.2, which required
`HtmlSanitizer` 9.0.892 → 9.2.1039. AngleSharp is load-bearing in the render path
(`IncludeExpander`, `IncludeReferenceParser`), so the jump was verified against the whole suite and
a live sweep: **48 pages, all 200, zero `ma-missing` placeholders**. `dotnet list package
--vulnerable --include-transitive` now reports none.

**Verified.** Bicep compiles and **validates against the live subscription** (`provisioningState:
Succeeded`); what-if enumerates the sixteen resources; a Release restore *and* publish succeed
seeing **only** the vendored feed and nuget.org, producing a complete 94 MB artifact with all 51
library `.idea`s; `dotnet ef migrations script --idempotent` generates cleanly; Release build + 370
tests green; `/_health` answers 200 live. Both PowerShell scripts parse under Windows PowerShell 5.1.

**Not verified, because it costs money.** No resources exist. The estate has not been provisioned,
the app has never run on App Service, and the migrate job has never touched an Azure SQL database.
The `ideas` entry in `MindAttic.Deploy/projects.json → apps[]` therefore ships `disabled: true` with
a note ([HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2)) and `/deploy` exits 0 without
half-firing. Runbook: [`docs/DEPLOYMENT.md`](DEPLOYMENT.md).

## MAI-A33 — Deployed. Two library bugs only a Linux host could reveal {#MAI-A33}

**What changed (2026-09-04).** The estate from [A32](#MAI-A32) is provisioned and the CMS is running
at **https://mindattic-ideas.azurewebsites.net**. A32 said "nothing was provisioned"; that is now
superseded. Getting there surfaced three defects and one platform behaviour, none of which any amount
of local testing would have found.

**1. MindAttic.Vault aborted the process on Linux (fixed in Vault V3, VLT-A3).**
`Environment.GetFolderPath` returns an empty string — it does not throw — on a host with no user
profile, and `VaultPaths` converted that into an exception. Vault sits in the `IConfiguration` chain,
so the throw landed during **host construction**: SIGABRT before a line of application code ran, with
a stack trace pointing at `ConfigurationBuilder` rather than at a missing folder. Vault now walks an
ordered chain (override → `SpecialFolder` → platform convention → `$HOME` → application base) and
never throws. Windows still resolves to `%APPDATA%\MindAttic`.

**2. App Service on Linux rewrites application-setting names (fixed in Authentication V4).**
Separators that are illegal in a POSIX variable name are removed or replaced when settings are
injected as environment variables. Every Security secret arrived misspelled:

| Configured | Delivered to the container |
|---|---|
| `…Security__pepper.v1` | `…Security__pepper_v1` |
| `…Security__bootstrap-token` | `…Security__bootstraptoken` |
| `…Security__reset-token-key` | `…Security__resettokenkey` |
| `…Security__dp-kek` | `…Security__dpkek` |

Auth fail-closed on secrets that had been provisioned correctly. The mangling is not invertible, so
`ConfigAuthSecrets` now matches exact-first, then with both sides reduced to letters and digits;
two keys differing only by punctuation raise an ambiguity error rather than a guess, because the
wrong pepper invalidates every stored password hash.

**3. `Compress-Archive` produces a zip Linux cannot unpack.** Windows PowerShell writes `\`
separators into zip entries, so Kudu's rsync failed on every file with a subdirectory
(`library\Foo.idea`) and the deployment 400'd. Build the package with forward slashes.

**Also learned.** First boot installs 51 `.idea`s against a 5-DTU Basic database and exceeds the
default container start limit, so `WEBSITES_CONTAINER_START_TIME_LIMIT=1800` is now part of the
template; later boots are fast because seeding is idempotent. Separately, `az webapp deploy` gives up
polling at ten minutes and reports failure while the container is still starting — the site came up
healthy shortly after a "failed" deploy, so trust `/_health`, not the CLI's verdict.

**Bicep owns every app setting.** `siteConfig.appSettings` is authoritative: anything added
out-of-band is wiped by the next template deployment. The four Key Vault *references* therefore live
in the template, while `provision.ps1` only generates the secret *values* — and restarts the app
afterwards, because a reference cannot resolve until its secret exists.

**Verified live.** `/_health` 200 · `/` 302 → `/frontpage` · `/frontpage` 200 · `/projects` 200 ·
`/ideas` 200. The database holds **53 content definitions** — the full first-party library installed
itself through the real install path on first boot — reached over managed identity with no password
anywhere.

**What is deliberately not there yet.** Production carries **baseline seed content only**: 5 pages,
0 media, 0 component metadata. The mindattic.com rebuild — 41 GitHub-seeded project pages, the
composed home page, 12 media assets — was built in the dev database by CLI runs and admin edits, and
none of that lives in the repo. Moving it over is a content migration (`--seed repos`,
`--upload-media` against the production connection), not a deployment step.

## MAI-A34 — Authored content is portable: the `.ideabundle` {#MAI-A34}

**What changed (2026-09-04).** A `.idea` package moves a **citizen** — a Theme, a Plugin, a
Component, a compiled Page type. Nothing moved what an author **built** with citizens. That gap was
not theoretical: after [A33](#MAI-A33) the production database held the 5-page baseline seed while a
hand-curated 55-page site — a composed home page, 12 extracted media assets, 86 `ComponentMetadata`
rows wiring `frommd`/`fromhtml` slots — existed only in a developer's LocalDB, with no path to
production but re-doing it by hand. `--seed repos` regenerates the *shape* of that site and none of
the curation.

Two CLI verbs close it:

```pwsh
dotnet run --project src/MindAttic.Ideas.Blazor -- --export-content site.ideabundle [--slug projects/] [--no-media] [--dry-run]
dotnet run --project src/MindAttic.Ideas.Blazor -- --import-content site.ideabundle [--dry-run] [--untrusted] [--prune]
```

A bundle is a zip: `bundle.json` (site, Host/Site settings, pages with their bodies, trust, theme
pin, plugin selection, SEO meta, role access and slug aliases, plus per-component metadata) and
`media/` payloads.

**Four decisions worth recording, because each one is a way this could have quietly gone wrong.**

**1. Identity is `Uid` first, `(SiteId, Slug)` second.** Integer ids are environment-local and never
cross. But uid alone is not enough: a production database that booted on its own already has a
`frontpage` under a *different* uid, so a uid-only import would hit the unique `(SiteId, Slug)` index
instead of updating the page the operator meant. The slug fallback is what lets a bundle **adopt** an
independently seeded row. Measured on the real data: importing the dev bundle into a freshly seeded
database gave **50 created, 5 updated** — the five baseline pages adopted, not duplicated.

**2. Media uids are remapped, never forced.** `IMediaStore.UploadAsync` mints the uid, so an imported
item necessarily gets a new one. Writing the row directly to preserve the uid would work for the local
disk store and quietly corrupt the Azure one, where the blob is addressed *by* uid. So import builds
an old→new map and rewrites every reference — `/_media/{uid}`, `<Component.MediaImage uid="…">`, and
uids inside a component's metadata JSON. Uids are hyphenated GUIDs, so a substring swap catches all
three shapes unambiguously.

**3. Re-import moves nothing.** Media is adopted by SHA-256, pages reconcile rather than insert. The
second import of the same bundle reported `0 uploaded, 12 already present` and `0 created, 55 updated`.

**4. Author trust is honoured, loudly, and refusable.** [MAI-LAW-5](BIBLE.md#MAI-LAW-5) stamps trust
at write time from the writer's claim. An import *is* a write, performed by whoever can run a CLI
against the server — strictly more privileged than an Admin — so the bundle's `Author` trust is
honoured, exactly as `--install` already trusts a `.idea` from disk. It is never silent: the import
prints how many pages are being written with raw, unsanitized markup, and `--untrusted` downgrades
them all. `--prune` (soft-delete pages absent from the bundle) is opt-in, per
[HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2).

**Verified end to end**, not just unit-tested: 55 pages / 86 metadata rows / 7 settings / 12 media
exported from the dev database (634 KB), imported into a fresh LocalDB seeded exactly like production
(5 pages, 0 media), then served — `/frontpage`, `/projects`, `/personas`, `/ideas`, `/chimesh` and
the project pages all 200, and every remapped `/_media/{uid}` on the front page resolving 200.

## MAI-A35 — One deployment, many domains: `HostBindings` is finally read {#MAI-A35}

**What changed (2026-09-04).** `Site.HostBindings` has been in the schema since migration #1, and
`Site`'s own XML doc has always described a tenant *"resolved by host header"* — but **nothing read
it**. `PageHost` took `Sites.FirstOrDefault(s => s.IsDefault)` and set `CmsSiteContext.Host = ""`, so
one deployment meant one domain and every project lived as a slug beneath it. The seam is now wired.

**Resolution.** `ISiteResolver` scores the request host against each site's bindings and picks the
best match, falling back to the default site when nothing matches. A binding list is comma /
semicolon / whitespace separated, case-insensitive, tolerant of a pasted URL, and **port-agnostic
unless a port is named** — so a production binding keeps matching on `localhost:5199` without being
rewritten. Precedence, highest first:

| | Binding | Matches |
|---|---|---|
| `HostAndPort` | `localhost:5199` | that host on that port only |
| `Host` | `mindattic.com` | that host on any port |
| `Wildcard` | `*.mindattic.com` | any subdomain — **never the apex** |
| `CatchAll` | `*` | anything |
| *(fallback)* | — | the default site |

A wildcard deliberately excludes the apex so it can never silently claim the bare domain another
site owns; ties are broken by the default flag then the lowest id, so the answer never depends on
row order.

**The host comes from `NavigationManager`, not `IHttpContextAccessor`.** `PageHost` is
`@rendermode InteractiveServer`: `HttpContext` exists during prerender and is **null for every render
after the circuit connects**. Reading the host from it would have resolved the correct site on first
paint and the *default* site on every client-side navigation thereafter — a bug that would look like
"the second domain works until you click a link". `Nav.BaseUri` is set from the originating request
and stays correct for the life of the circuit. Verified in a real browser: after the circuit was
live, `Blazor.navigateTo('/about')` resolved against the correct site, and the same navigation on the
other host correctly 404'd.

**Backwards compatible by construction.** A site with empty bindings answers every hostname it is the
default for, exactly as before; multi-site is opt-in per site by filling bindings in. The existing
single-site shape is pinned by a test, because a regression here would 404 every deployment that
predates this amendment.

**Also changed.**
- **Per-domain front page.** The bare route `/` now prefers the Site-scope `page.frontpage` setting
  over the Host-scope one, so each domain lands on its own page.
- **`CmsSiteContext.Host`** carries the host the request actually arrived on, so a citizen can build
  absolute URLs or vary per domain.
- **Admin → Sites** (`/admin/sites`): create sites, edit bindings, move the default, delete
  (reference-guarded — a site with pages cannot be deleted, since that would orphan them onto
  whatever site resolves next), plus a "which site answers…?" probe that runs the *same* rule the
  render path uses. Without this, a second domain could only be added by hand in SQL, which is
  exactly how `HostBindings` came to sit unread for so long. A binding another site already claims is
  refused: the loser would otherwise be invisible, with no error anywhere.
- **Bundles ([A34](#MAI-A34)) became site-aware**, because multi-site made the old behaviour wrong:
  `--export-content` now carries **one** site (`--site <key>`, default the default site) and scopes
  its pages and Site-scope settings to it, and `--import-content` matches the target site by **key
  and creates it when absent** rather than falling back to the default site — which would have
  republished one domain's pages under another. `--into-site <key>` overrides the target.

**Not changed, deliberately.** `UseForwardedHeaders` still forwards only `X-Forwarded-For` and
`-Proto`. App Service passes the real `Host`, and trusting `X-Forwarded-Host` from an unrestricted
proxy would let a caller choose which site it gets. Revisit only alongside a known proxy allowlist.

**Why `X-Forwarded-Host` stays untrusted — the concrete risk, now that a header picks a tenant.**
It is a client-supplied header: anyone can send it. Trusting it would let the *caller*, rather than
DNS, choose which site they are served — bind a staging or internal hostname to a site, and a request
to the public domain carrying `X-Forwarded-Host: internal.mindattic.com` would be answered by that
site. It is also the classic host-header-poisoning vector for anything that builds absolute URLs from
the host. The exposure is compounded by `Program.cs` clearing `KnownProxies`/`KnownIPNetworks`, which
means forwarded headers are accepted from **any** peer — tolerable for `For`/`Proto` behind App
Service, but it would leave zero authentication on the value that now selects a tenant. App Service
passes the real `Host`, so there is nothing to gain. If a future proxy ever requires it, the safe
order is: populate `KnownProxies` **first**, then enable the header — never the reverse.

## MAI-A36 — Showroom mode: a sandbox site, and a catalog that knows who is asking {#MAI-A36}

**What changed (2026-09-04).** Ideas needs a place a stranger can be handed admin access to a vanilla
deployment, upload a `.idea`, watch it go live, and break nothing — the showroom for people
evaluating Ideas. Demonstrating the product's headline claim requires letting a visitor *actually do*
the thing, which means the deployment must contain a site whose content is disposable.

**A sandbox is a Site, not a new tenancy axis.** [A35](#MAI-A35) made `SiteId` load-bearing: pages,
settings and host resolution already run through it. A parallel `InstanceId` would mean every
tenant-scoped query filtering on two columns that must always agree, with a cross-tenant leak
wherever one is forgotten — and the schema could not enforce the invariant. So `Site` gained a
lifecycle instead: `IsSandbox`, `ResetPolicy`, `IdleGraceMinutes`, `LastResetUtc`. One process can
serve both sites, or two processes can each resolve their own — tenancy lives in the database, not in
the process.

**The catalog now knows who is asking.** A visitor installing a package was the hard part: the
catalog was global, so an upload would have changed what *every* site renders.
`CmsContentDefinition` and `InstalledPackage` gained a nullable `SiteId` — **null means shared**,
which is what every existing row means. Resolution is site-first, then shared: a site's own citizen
wins over the shared one of the same identity, and a site that installed nothing sees exactly the
catalog it saw before. `SiteId` joins both unique indexes, because the shared copy and a sandbox's own
copy of `(Kind, Key, Version, Origin)` are legitimately different rows.

Two details carry most of the safety:

- **The site-less lookups now mean SHARED-ONLY.** `Find`/`FindLatest`/`ResolveTag` without a site
  used to match any row. Every caller holding no site is a back door, so if they matched a sandbox
  row a stranger's upload could surface on the real site through one of them. Shared-only is also
  exactly what they meant before sites could own citizens.
- **The Abstractions surface was appended to, never changed** ([MAI-LAW-2](BIBLE.md#MAI-LAW-2)).
  `ContentDescriptor.SiteId` is a new optional init-only property; the site-aware lookups are default
  interface methods whose defaults ignore the site and fall through, so a catalog written before this
  keeps working untouched — pinned by a test that implements only the frozen members.

**The main site is never reset — structurally, and twice.** Showroom mode contains a routine that
deletes a site's content on a timer, so the safety is not a conditional at the call site.
`SandboxService.Gate` is the only authority for "may this be reset?", and it refuses unless the site
is a sandbox, has a reset policy, **and is not the default site** — the default check runs *first and
independently of the sandbox flag*, so a row hand-edited in SQL to mark the main site as a sandbox is
still refused. `SiteAdminService` refuses to create that state from either direction: the default
site cannot be put into Showroom mode, and a showroom site cannot be promoted to default. The idle
sweep re-gates every candidate rather than trusting its own query predicate, because a predicate that
drifts from the gate is precisely how the wrong site gets wiped.

**Idle, not "the moment they leave."** A visitor between page loads has no live circuit for a beat;
wiping the site under them would read as a crash. Liveness comes from the auth package's
`AuthSession` (`LastSeenUtc`, unrevoked, unexpired) and a per-site grace period. Sessions are not
site-scoped, so "in use" is measured across the deployment — deliberately conservative, since that
can only ever *delay* a reset, never cause one.

**Day Zero is a content bundle.** The baseline a showroom resets to is the `.ideabundle` from
[A34](#MAI-A34): reset = drop the site's own content and packages, then import the baseline scoped to
that site. The export/import path already reconciles by uid-then-slug and remaps media, so the
restore mechanism is one that is already tested rather than a second, parallel one.

**Still to come** (this amendment is the foundation): install scoping through `PackageInstallService`,
per-site asset mounts (additive to the route locked by [MAI-LAW-4](BIBLE.md#MAI-LAW-4) — a sibling
path, never a change to `/_ideas/{Kind}/{key}/{version}/{**path}`), the reset executor and its
background sweep, lazy provisioning on first navigation, the guided tour, and the recomposition of
`/ideas` from a compiled brochure into a Data page built from discrete components.

---

## MAI-A37 — A site-owned install: what the visitor's upload may and may not touch {#MAI-A37}

**What changed (2026-09-04).** [A36](#MAI-A36) made the catalog able to say who is asking and left the
write half to come. This is the write half: `IPackageInstallService.InstallAsync` and `DisableAsync`
take an **owner** — `null` installs shared, exactly what every caller meant before sites could own
citizens, and a site id installs into that site alone. Every lookup the install makes moved with it, so
an upload is planned against, collides with, and resolves its dependencies from only what that site can
actually see. `MAI-US-M4` is met.

**Four decisions this forced, none of them obvious from A36:**

- **The override prompt does not apply to a site-owned install.** *Allow override* exists to stop a
  package silently replacing a **compiled** citizen for the whole deployment. A site's install cannot do
  that — it wins only inside its own site, by the catalog's site-first ordering — so the collision domain
  is the owner's own rows, which for a site never contains a compiled one, because compiled citizens are
  always shared. A visitor may therefore upload a package whose key matches a shipped one and watch their
  copy take over their sandbox, changing nothing anywhere else. Installing *shared* still needs the
  confirmation it always did.

- **Shadowing is computed per site.** This one was a live defect the moment M4 landed, not a preference:
  `ReloadCatalogAsync` grouped by `(Kind, Key, Version)`, so a visitor's upload of an identity the real
  site already had would have been decided against it by Priority — either the upload never renders, or it
  shadows the shared citizen for **every** site. Grouping by `(SiteId, Kind, Key, Version)` is what makes
  the two copies non-competitors, which is what the catalog's site-first lookup already assumed. The same
  correction applies to discovery's identity key and its manifest map.

- **A site-owned install seeds its page into the owning site, whatever `data/page.json` asks for.** In the
  showroom the manifest is a stranger's file. Honouring its `siteKey` would let an upload plant a page on
  the real site — the single thing site-scoping exists to prevent. A shared install still honours it: an
  operator installing for everyone is trusted, and that behaviour predates this.

- **A sandbox owns its uploads; every other site installs shared.** `InstallScope.OwnerFor` is the one
  home for that rule, because a second upload surface re-deriving its own copy is precisely how a
  visitor's package ends up shared with production. It excludes the default site explicitly, mirroring
  `SandboxService.Gate`: a row hand-edited in SQL to flag the main site as a sandbox must not start
  diverting its operator's installs either.

**Bytes, assemblies and assets are keyed by site.** Two sites may legitimately hold the same
`(category, key, version)` of *different* bytes, so identity alone is no longer a unique path. Blobs go
to `sites/{siteId}/…`, extraction to a `sites/{siteId}/` root, and assets mount at
`/_ideas/sites/{siteId}/{category}/{key}/{version}/{**path}` — a **sibling** of the route
[MAI-LAW-4](BIBLE.md#MAI-LAW-4) locks, never a change to it; the shared route answers exactly what it did.
ALC keying comes free rather than as a second mechanism: `AlcAwareTypeResolver` already keys its load
contexts by the entry-assembly path, so site-keying the extraction directory *is* site-keying the ALC —
one site's assembly can never be handed back for another site's descriptor of the same identity.

**A defect A36 left behind, fixed here.** Making `SiteId` part of both unique indexes produced a unique
index over a *nullable* column, which SQL Server filters to `WHERE [SiteId] IS NOT NULL` — so every
SHARED row, meaning every row predating A36 and every row the library seeder installs, silently fell
outside the constraint that had covered it since migration #1. The install path’s concurrency guard is a
caught `DbUpdateException` from precisely that index, so the loss turned a concurrent shared install from
a clean `NoOpAlreadyInstalled` into two live rows of one identity — which `DiscoveryService` then throws
on at the next boot when it keys them. Migration `SharedRowUniqueIndexes` adds the complementary
`WHERE [SiteId] IS NULL` index on each table; the two together cover every row.

**The registry listing became site-visible too.** `IPackageRegistryService.ListAsync` shows shared plus
the asking site's own. A showroom visitor being shown the real site's inventory is a disclosure the
demo has no reason to make, and the real site's operator is not looking at a stranger's uploads either.

---

## MAI-A38 — Day Zero: the reset executor, and what a restore must never touch {#MAI-A38}

**What changed (2026-09-05).** [A36](#MAI-A36) defined when a showroom may be reset;
[A37](#MAI-A37) made a visitor's install land in their site alone. This is the routine that actually
deletes: `SandboxResetService` drops everything a sandbox site owns and restores its baseline, and
`SandboxResetSweep` runs it for every site idle past its grace period. `MAI-US-M5` is met.

**One authority, asked twice.** The sweep does not decide anything — it asks
`ISandboxService.DueForResetAsync` which sites qualify, and the executor asks
`ISandboxService.Gate` **again**, immediately before the first delete, rather than trusting whatever
called it. A caller that gated a minute ago, a sweep predicate that has drifted, the Sites-panel button
and the `--reset-sandbox` CLI verb all meet the same refusal. The sweep is also **off by default**: a
deployment with no showroom must not run a background loop whose job is deleting content.

**A reset hard-deletes, and that is the exception it looks like.** [HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2)
makes disable-not-delete the rule, and soft rows are for content someone might want back. A sandbox
reset exists precisely to make the visitor's work unrecoverable, and rows kept forever would collide
with the baseline's slugs on the very next restore. This is the one routine allowed to do it, which is
why it sits behind three separate flags and the default-site refusal.

**Two things deliberately survive.** *Media* is not site-scoped by the media package, so deleting a
visitor's uploads would mean deleting from a store the real site shares — a stale image in a sandbox is
a far smaller price than a live site losing its pictures. *Shared citizens* — the whole first-party
library — are untouched, because they were never the sandbox's to drop; only what the site itself owns
goes.

**The restore path is the operator's own, not a second one.** The import half of
[A34](#MAI-A34) moved out of `ImportContentCli` into `ContentBundleImporter` in Core; the CLI is now
argument parsing and console reporting over it. A parallel restore mechanism is one that only ever runs
when something has already gone wrong, so the reset takes the path an operator exercises by hand.

Two defects surfaced in doing that, both of which the reset would have triggered on a timer:

- **A restore could steal another site's pages.** Import reconciled a page on `Uid` first with no site
  filter. A uid is portable and therefore GLOBAL, and a showroom's baseline is a bundle exported from
  the *real* site — carrying the real site's uids. Restoring it would have matched production's rows and
  re-pointed their `SiteId` into the sandbox. Both lookups are now scoped to the target site, and a page
  whose uid is already taken elsewhere gets a fresh one, so the sandbox receives a *copy* rather than a
  move.
- **A restore could overwrite the sandbox's own identity.** Applying the bundle's `site` block would
  rename the sandbox, give it production's host bindings, and clear the flags that make it a sandbox —
  losing the showroom on the routine meant to refresh it. `BundleImportOptions.IntoSiteId` pins the
  target by id and leaves the site row alone; it is the sandbox-restore door and nothing else uses it.

**Reachability.** Showroom mode could previously only be entered by editing SQL — `SetSandboxAsync`
existed but nothing exposed it. The Sites panel now carries the toggle, its grace settings, and a
two-click *Reset to baseline now*; `--reset-sandbox <key>` is the operator-facing form of the sweep.
Configuration is `Showroom:{Enabled,IntervalMinutes,BaselineBundle}`, absent by default.

---

## MAI-A39 — Showroom mode is withdrawn: a demo is a DEPLOYMENT, not a feature {#MAI-A39}

**Supersedes [A36](#MAI-A36), [A37](#MAI-A37) and [A38](#MAI-A38)** (2026-09-05). Those three built a
sandbox site *inside* the real deployment: a site lifecycle, a per-site catalog, a reset executor and an
idle sweep. All of it is removed. A showroom is a **separate, vanilla install of Ideas** — its own app,
its own database, reset by its own operator — and needs no product code at all.

**Why the earlier reading was wrong.** The clearest evidence was in the shape of the code A36–A38
produced: three redundant guards, a gate asked twice, a default-site refusal checked first and
independently of every other flag, and a whole test fixture whose stated purpose was to make one
sentence true — *the main site is never reset*. None of that defends against a hazard in the product.
It defends against a hazard **the design introduced**, by putting a routine that deletes content into
the same process and database as the live site. A separate deployment has nothing to defend: there is
no production content in reach.

It is also the weaker demo. Ideas' claim is that one deployment hosts many pages and goes live the
moment a `.idea` is uploaded. A visitor evaluating that should meet an actual Ideas install doing
exactly that — not a tenancy-scoped imitation of it inside somebody's real site, with a subset of
admin and an upload path that behaves differently from the real one.

And the cost was carried by the whole product, not the demo: every catalog lookup gained a site-aware
overload, shadowing became per-site, package bytes and extraction directories and ALC keys all grew a
site segment, and the asset route grew a sibling — for a scenario (two sites holding the same key and
version of *different bytes*) that a single-tenant deployment never reaches.

**What was removed.** `Site.IsSandbox` / `ResetPolicy` / `IdleGraceMinutes` / `LastResetUtc`;
`SandboxService`, `SandboxResetService`, `SandboxResetSweep`, `ISandboxBaselineSource`, `InstallScope`;
`SiteId` on `CmsContentDefinition` and `InstalledPackage` and every lookup, shadow group, blob path,
extraction root, ALC key and asset mount that read it; `SiteAdminService.SetSandboxAsync` and its two
guards; the Sites-panel showroom UI; `--reset-sandbox` and `--install --site`; the `Showroom:*`
configuration. Migration `RemoveSandboxAndSiteScopedCatalog` drops the columns and returns both unique
indexes to their pre-A36 shape.

**A pre-1.0 withdrawal from the frozen SDK.** `ContentDescriptor.SiteId` and the site-aware
`IContentCatalog` default methods were appended under [MAI-LAW-2](BIBLE.md#MAI-LAW-2) and are now
*removed*, which append-only would otherwise forbid. The precedent is [A19](#MAI-A19), which removed the
`Control` kind pre-1.0 and retired its ordinal. The same reasoning holds: MAJOR is still 1, nothing
outside this repo has ever consumed either member, and leaving dead API on a surface that is frozen
*forever* is a worse outcome than withdrawing it in the window where withdrawal is still possible. As
with `Control`, this is a withdrawal, not a licence — the surface remains append-only.

**Three things survive, because they were defects the showroom work merely uncovered:**

- **`--into-site` copies rather than moves.** Import reconciled a page on `Uid` first with no site
  filter. A uid is the portable identity and therefore global, while a page belongs to exactly one site
  — so importing a bundle into site B, on a deployment that already held those pages under site A, would
  re-point site A's rows instead of giving B its own. Both lookups are site-scoped and a page whose uid
  is already taken gets a fresh one. This is a live [A34](#MAI-A34)/[A35](#MAI-A35) bug independent of
  any showroom. *(test: `ContentBundleTests.IntoSite_CopiesThePagesRatherThanMovingThemOffTheSiteThatHasThem`)*
- **`ContentBundleImporter` stays in Core.** The import half no longer lives inside a CLI verb; the verb
  is argument parsing and console reporting over it. Better structure on its own merits.
- **The `SharedRowUniqueIndexes` reasoning is retired, not forgotten.** Those filtered indexes existed
  only because a nullable `SiteId` had joined the key, and a unique index over a nullable column is
  filtered to `IS NOT NULL` on SQL Server — which had silently left every shared row unconstrained. With
  the column gone the plain unique index covers every row again. The lesson stands for the next time a
  nullable column is added to a unique index.

**What a showroom deployment actually needs from this repo:** nothing new. It is an Ideas install; it
gets back to Day Zero the way any environment does — `--import-content <bundle> --prune`, or a database
restore. How and when that runs is a deployment decision, recorded wherever that deployment is, not here.

---

## MAI-A40 — A slug is only unique inside a site: `IPageTree` grows a scoped lookup {#MAI-A40}

**Appends to the frozen SDK under [MAI-LAW-2](BIBLE.md#MAI-LAW-2)** (2026-09-04). `IPageTree` gains one
default method:

```csharp
Task<IReadOnlyList<ChildPage>> ChildrenOfSlugAsync(Guid siteId, string slug, CancellationToken ct = default)
    => ChildrenOfSlugAsync(slug, ct);
```

**The defect.** `ChildrenOfSlugAsync(slug)` resolved its parent with `p.Slug == slug` and nothing else.
But a slug has never been globally unique — the Pages unique index is `(SiteId, Slug)`, and has been
since migration #1. That mismatch was harmless while one deployment meant one site; [A35](#MAI-A35) made
the deployment answer on many domains and turned it into a real bug. `<Component.ProjectGrid
From="projects" />` on site B could list **site A's** child pages, chosen by nothing more principled
than which row the database returned first.

**Why an overload rather than a fix in place.** The slug-only signature carries no site, and the render
context has had one all along — `ISiteContext.SiteId`, the site uid every citizen can already read. So
the host cannot infer the answer; the caller has to say. The new overload is the one to prefer;
`Guid.Empty` means "site unknown" and falls back to the unscoped lookup, which is now at least *ordered*
so its answer is deterministic rather than down to row order.

**This is growth, not a break.** The surface is append-only, and a default method is exactly how
LAW-2 permits an interface to grow: a host that implements only the slug-only form keeps compiling and
keeps answering, through the delegating default. `PageTreeFeature` overrides it; `Component.ProjectGrid`
passes `Context.Site.SiteId` and was repacked. Nothing else in the library consumed the slug-only form —
`Component.TableOfContents` walks by uid, which is global and was never affected.

*(tests: `PageTreeFeatureTests.ChildrenOfSlug_ScopedToSite_ReturnsOnlyThatSitesChildren`,
`.ChildrenOfSlug_SiteWithNoSuchSlug_ReturnsEmpty_NotAnotherSitesPage`,
`.ChildrenOfSlug_UnknownSite_FallsBackToTheUnscopedLookup`,
`.IPageTree_DefaultOverload_DelegatesToTheSlugOnlyForm`)*
