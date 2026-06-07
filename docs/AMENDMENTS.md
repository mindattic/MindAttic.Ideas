---
codex: 1
project: MindAttic.Ideas
code: MAI
layer: amendments
status: living
updated: 2026-06-07
---

# MindAttic.Ideas — Amendments (append-only; amendment wins over the bible)

These directives were finalized **after** the Legion deliberation produced
[`FOUNDATION_ADR.md`](FOUNDATION_ADR.md). Where an amendment conflicts with the ADR or with
[`BIBLE.md`](BIBLE.md), **the amendment wins** and the bible/ADR are to be read as patched here. All
are part of the ratifiable foundation. IDs `A1..A18` are stable; never rewrite an amendment, only
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
