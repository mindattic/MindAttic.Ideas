# MindAttic.Ideas — Foundation Amendments

These directives were finalized **after** the Legion deliberation produced
[`FOUNDATION_ADR.md`](FOUNDATION_ADR.md). Where an amendment conflicts with the ADR, **the amendment
wins** and the ADR is to be read as patched here. All are part of the ratifiable foundation.

---

## A1 — Versioning is whole-number, not SemVer (overrides ADR §3 / §2)

Every Page, Theme, and Component version is a **single whole number** (`1`, `2`, `3`). No
dotted/minor/patch versions anywhere.

- `idea.json` → `"version": 1` (integer).
- The package `sdk` gate becomes an **integer minimum** (`"sdk": 1` = "requires host SDK ≥ 1"), not a
  SemVer range. `[assembly: IdeaSdkVersion("1")]` likewise.
- Asset route segment uses the integer: `/_ideas/{key}/{version}/…` → `/_ideas/ui.sacredgeometry/1/…`.
- `Abstractions` MAJOR pinned at **1 forever**, additive-only (unchanged from ADR).

**Rationale:** the owner's explicit instruction — "whole numbers only, no 1.5.11; make it trivially
obvious which version is which." Coexisting integer versions are the never-break mechanism.

## A2 — Version is part of identity; Pages pin a version (refines ADR §2 identity lock)

A citizen's identity becomes the triple **`(ContentKind Kind, string Key, int Version)`**. The composition
tag pins the version explicitly (`<…Cyberspace.V1/>` / `<ma-component key="cyberspace" v="1" …/>`).

- A Page **pins** the exact Theme/Component versions it references; a new version (`V2`) never affects a
  Page pinned to `V1`.
- There is **no implicit "track latest."** Upgrading a Page to `V2` is a deliberate edit.
- The `<ma-component>` include grammar gains a required-when-ambiguous `v` attribute (whole number). The
  friendly namespaced tag form (`<MindAttic.Ideas.Components.SacredGeometry.V1/>`) is the authoring sugar;
  it resolves to `(Component, "ui.sacredgeometry", 1)`.

## A3 — Disable / delete integrity (adds to ADR §4 data model)

- Every Page/Theme/Component version has an **`Enabled`** flag. **Disabled = exists but cannot be used
  until re-enabled.**
- **Referential guard:** a Theme/Component version **cannot be deleted while any Page references it.**
  Deletion is blocked until every referencing Page is **Disabled or Deleted**. Enforced in the service
  layer over a `PageReference` projection (derived from parsing `BodyHtml` `<ma-component>` tags + Code
  page references), with a confirming DB check.

## A4 — Temporal history (adds to ADR §4)

Pages (and their Theme/Component pin set) use **SQL Server system-versioned temporal tables** — mirror
StreetSamurai's pattern: `SysStart`/`SysEnd` `GENERATED ALWAYS`, an idempotent
`EnableSystemVersioningAsync` at startup, `FOR SYSTEM_TIME AS OF` queries for the wiki-like history view.
A Page version's row records the `(Kind,Key,Version)` set it rendered with, so history is fully
reconstructable and rollback is a row restore.

## A5 — Disabled-dependency render guard + Admin Inbox (adds to ADR §6)

If a Page resolves a Theme/Component reference that is **Disabled** (or missing), the render **halts**
(shows a clear block to the user instead of partial output) **and** immediately writes an **Admin Inbox**
message. The inbox is DB-backed and patterned on StreetSamurai's `FindingRow` + `FindingsService.Upsert`
(hash `DedupKey` unique index, severity/status enums, dedup). Entity: `AdminInboxMessage`
`{ Id, Severity, Category, Subject, Body, DedupKey(unique), Status, CreatedUtc, ResolvedUtc? }`.

This refines the ADR's `CmsMissingContent` "never crash" placeholder: a *missing/stale* type still
degrades to a placeholder, but a *deliberately Disabled* dependency is a halt-and-notify event.

## A6 — MindAttic.Vault for all credentials (adds to ADR §“Stack”)

No secrets in `appsettings`/User Secrets. Wire in `Program.cs`:
`builder.Configuration.AddMindAtticVaultFiles().AddEnvironmentVariables();` then
`builder.Services.AddMindAtticVault(builder.Configuration);`. DB connection string resolves through
`IConfiguration`/env (`ConnectionStrings__Ideas`); LLM/API keys via `LlmCredentialResolver.GetKey(...)`.
Package: `MindAttic.Vault` (local feed `C:\LocalNuGet`, net10.0). **Never** add `<UserSecretsId>`.

## A7 — MindAttic.Legion for LLM + voting (new optional Core service)

Register `services.AddLLMVoting(new VotingConfiguration())` (zero-config; keys via Vault) and/or
`AddLegionClient()`. Expose `LegionClient` (direct LLM: `CallAsync`) and `LlmVotingService`
(`VoteAsync`/`DecideAsync`/`ScoreAsync`) to Core services. In-proc project/package reference (net10.0);
depends transitively on Vault. Foundation-optional — wired but not load-bearing for Phase 0/1 render.

## A8 — UiUx three-layer wrapper chain (confirms ADR §7)

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
`ContentKind.Component` meaning a generic widget) is superseded by the below.**

## A9 — Four content kinds under one shared base `IdeaBase`

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

## A10 — `ComponentBase` clash resolved by aliasing Blazor's (per [[naming-conflict-aliasing]])

MindAttic's `ComponentBase` owns the bare name. Blazor's framework base is referenced via
`using BlazorComponentBase = Microsoft.AspNetCore.Components.ComponentBase;` (so `IdeaBase :
BlazorComponentBase`), and Razor `_Imports.razor` aliases the bare name to ours
(`@using ComponentBase = MindAttic.Ideas.Abstractions.ComponentBase`) so `@inherits ComponentBase`
resolves to MindAttic's. Standing rule: on any future framework name clash, surface it and ask before
aliasing — alias the *framework* side so MindAttic's namespace wins the bare name.

## A11 — Locked tag convention `<MindAttic.Ideas.{ContentKind}.{Name}.{Version} />`

Identity by **convention**: Kind from the base, Name (key, lowercased) from the namespace tail after
`MindAttic.Ideas.{Kind}.`, Version from the `V{n}` class name. Optional `[Idea(key:…, version:…,
scope:Global)]` overrides. The same tag works in **data pages** (the include expander resolves it,
case-insensitively, replacing the earlier `<ma-component>` form) and **code pages** (a real Blazor tag).
Razor forbids lowercase component class names, so the **version token is uppercase `V{n}`** to match the
class exactly.

## A12 — Version is OPTIONAL; defaults to latest

A tag may omit the version (or use `.Latest`) to resolve the **highest enabled version** from the tables,
or pin exactly with `.V3`. This refines A2's "pin everything": pin when you care, float when you don't, so
composing many co-versioned pieces (e.g. `TabControl` + `TabButton` + `TabPage`) needs no version juggling.
Integrity (A3) is preserved: a version-specific delete is blocked while anything pins it; a floating
reference is valid as long as some enabled version remains.

## A13 — Self-closing include tags are normalized before parsing

`<MindAttic.Ideas.… />` is not truly self-closing in HTML (the parser would swallow following siblings as
children). The expander normalizes the known `MindAttic.Ideas.*` self-closing tags to explicit paired
tags before AngleSharp parsing, and only passes inner content as `ChildContent` when the resolved type
declares it. A malformed/unresolved/disabled include degrades to a visible placeholder — never a crash.

## A14 — Vocabulary: no umbrella noun; UiUx is the multi-target source

There is **no umbrella noun** for the four kinds in prose — spell out Page/Component/Theme/Control (the
word "citizen" used during the build is dropped). All official content ultimately lives in **MindAttic.UiUx**
as ONE canonical core distributed as MANY wrappers/exports: raw js/css/html → Blazor wrapper → `.idea`
(MindAttic.Ideas) → later React, Angular, etc. The CMS↔UiUx tie stays thin (load raw assets by pinned-tag
URL; never reimplement). Phase-1 content lives inline in the Web project as a render proof; its permanent
home is UiUx.
