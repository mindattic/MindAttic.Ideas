---
codex: 1
project: MindAttic.Ideas
code: MAI
layer: amendments
status: living
updated: 2026-06-09
---

# MindAttic.Ideas — Amendments (append-only; amendment wins over the bible)

These directives were finalized **after** the Legion deliberation produced
[`FOUNDATION_ADR.md`](FOUNDATION_ADR.md). Where an amendment conflicts with the ADR or with
[`BIBLE.md`](BIBLE.md), **the amendment wins** and the bible/ADR are to be read as patched here. All
are part of the ratifiable foundation. IDs `A1..A19` are stable; never rewrite an amendment, only
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
StreetSamurai's pattern: `SysStart`/`SysEnd` `GENERATED ALWAYS`, an idempotent
`EnableSystemVersioningAsync` at startup, `FOR SYSTEM_TIME AS OF` queries for the wiki-like history view.
A Page version's row records the `(Kind,Key,Version)` set it rendered with, so history is fully
reconstructable and rollback is a row restore.

## MAI-A5 — Disabled-dependency render guard + Admin Inbox (adds to ADR §6) {#MAI-A5}

If a Page resolves a Theme/Component reference that is **Disabled** (or missing), the render **halts**
(shows a clear block to the user instead of partial output) **and** immediately writes an **Admin Inbox**
message. The inbox is DB-backed and patterned on StreetSamurai's `FindingRow` + `FindingsService.Upsert`
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

## MAI-A15 — Deployment: Windows App Service, StreetSamurai-style (supersedes IMPLEMENTATION_PLAN §10 "Linux") {#MAI-A15}

MindAttic.Ideas deploys the SAME way StreetSamurai does — **NOT Linux**. The plan doc's "App Service
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
Razor Class Library — the same hardened engine StreetSamurai and Tutor adopt, so the three authenticate
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
  Protection ⇒ a cookie stolen from StreetSamurai/Tutor cannot authenticate to Ideas. **No cross-app SSO in v1.**
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
its locked order, Ideas adopts **after** the library completes **and after** StreetSamurai — at which point
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
  `{{ MindAttic.Ideas.Widget.LegionPersonas }}` — through the Cyberspace theme. Verified live:
  `/personas` renders the full gallery with zero placeholders. "Official content lives in
  MindAttic.UiUx" is restated per A19/A20 reality: **MindAttic.Ideas.Library is the single home of
  first-party `.idea` content** (36 components); UiUx remains an upstream raw-source repo and is no
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
- The CMS Web host ships **37** first-party `.idea` files in `src/MindAttic.Ideas.Web/library/`
  (7 Themes + 30 Widgets) — verified by `ma-idea verify` (compose-graph green).

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
Suite: **217 NUnit green**.
