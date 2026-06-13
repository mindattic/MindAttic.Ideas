---
codex: 1
project: MindAttic.Ideas.Library
code: MAIL
layer: bible
status: living
updated: 2026-06-09
---

# MindAttic.Ideas.Library — Project Bible

> Single source of truth for what MindAttic.Ideas.Library IS, is NOT, and the rules that keep it
> coherent. The [README](../README.md) says how to build/run and pack; this says how to think
> about the system. Where a fact is structured and tabular it lives once in
> [`docs/data/components.json`](data/components.json) (L5) and is cited here by `id`.

## 1. The one sentence {#MAIL-§1}

MindAttic.Ideas.Library is the **first-party catalog of `.idea` components** — Themes, Widgets, and
Controls — that ship *with* the [MindAttic.Ideas](../../MindAttic.Ideas) CMS, each an independently
versioned, independently packable RCL whose **asset bundle is the single source of truth** across all
three of its consumers (raw HTML pages, standalone Blazor apps, and the CMS).

## 2. The product promise {#MAIL-§2}

- **One home, many `.idea`s.** Every first-party Theme/Widget/Control lives here instead of one git
  repo per piece. Each component is its own small project so each `.idea` packs and uploads on its own.
- **The bundle is the interchange format.** A component owns its `assets/` (css/js/html/images) once.
  The exact same bundle is consumed three ways with no duplication and no cross-repo coupling
  ([README consumer table](../README.md)): a raw `.html` page links `assets/*`; a standalone Blazor app
  references the RCL or links `assets/`; the CMS uploads the packed `.idea` with assets bundled into
  `wwwroot/`.
- **Identity by convention, not configuration.** The component's key is its namespace tail (lowercased)
  and its version is the `V{n}` class number — so a `.csproj` stays tiny. The shipped catalog is
  enumerated in [`components.json`](data/components.json).
- **Composition by string id.** A Theme or Widget pulls in other installed components by string key via
  `[Uses(ContentKind.…, "key", n)]` + `<CmsInclude Ref="…">`, never by project reference — the
  "multi-target source" model (see [theme.cyberspace](data/components.json), [widget.frontpage](data/components.json),
  [widget.legionpersonas](data/components.json)).

## 3. What it is NOT {#MAIL-§3}

- **NOT the CMS host.** The CMS (`MindAttic.Ideas`) is standalone and never references anything here; it
  only installs packed `.idea` files as **optional** content. The only thing these projects compile
  against is the frozen `MindAttic.Ideas.Abstractions` SDK in the sibling repo.
- **NOT a NuGet library.** Every component is `IsPackable=false`; the unit of distribution is the
  `.idea` (a guarded zip, [HOUSE-LAW-5](../../MindAttic.HouseRules.md)), never a package.
- **NOT a place for Pages.** A Page is a CMS **database record** (Html/Css/Js + tags), not a `.idea`.
  The formerly-parked Pages/_wip sources were deleted as no longer applicable
  ([MAIL-A4](AMENDMENTS.md#MAIL-A4)): the frontpage is assembled verbatim from mindattic.com's
  `index.htm` into a Data page, and LegionPersonas ships as `Widget.LegionPersonas`. History stays in git.
- **NOT a multi-DLL bundle.** Abstractions (and the framework Components it carries) are kept out of
  `bin/` (`Private=false` + `ExcludeAssets=runtime`); the validator forbids host assemblies in a packed
  `bin/`, so each packed `bin/` holds exactly one DLL.
- **NOT cross-host static web assets.** Component chrome lives in a plain `assets/` folder (NOT
  `wwwroot/`) so the Razor SDK never registers it as a static web asset — this is what previously caused
  cross-host collisions. `assets/` becomes the package `wwwroot/` only at pack time.

## 4. Architecture canon {#MAIL-§4}

```
   sibling repo (never referenced back) ──────────────┐
   MindAttic.Ideas (CMS host)                          │ installs packed .idea (optional content)
        ▲ compiles against (frozen SDK only)           │
        │                                              │
   MindAttic.Ideas.Abstractions  ◄── ProjectReference (Private=false, ExcludeAssets=runtime)
        │
   ┌────┴───────────────────────── MindAttic.Ideas.Library ──────────────────────────┐
   │  Directory.Build.props  (net10.0, <Version>1.0.0</Version>, the ONE Abstractions ref)
   │                                                                                   │
   │   Themes/      ThemeBase   → ThemeCssUrls; chrome + ONE @Body hole                │
   │   Widgets/     WidgetBase  → StylesheetUrls / inline markup; self-contained       │
   │                (ControlBase in Abstractions; no standalone Controls folder yet)   │
   │                (no Pages/ — pages are CMS DB records; MAIL-LAW-8, MAIL-A4)        │
   │                                                                                   │
   │   each component: assets/ (the bundle) ── pack ──► dist/*.idea                    │
   └───────────────────────────────────────────────────────────────────────────────-─┘
                          composition: [Uses("key", n)] + <CmsInclude Ref="…"> (by string id)
```

### 4.1 Projects {#MAIL-§4.1}
The solution ([`MindAttic.Ideas.Library.slnx`](../MindAttic.Ideas.Library.slnx)) is 36 component RCLs in
two solution folders — **7 Themes, 29 Widgets** (no standalone Controls: Textbox was folded from Control
to Widget in MAI-A19; `ControlBase` remains available in Abstractions for future use). Eleven widgets are
MindAttic-specific, fifteen are the general-purpose **baseline set** ([MAIL-A3](AMENDMENTS.md#MAIL-A3))
that lets the CMS build ordinary websites from reusable parts, and three are the **mindattic.com
verbatim set** ([MAIL-A5](AMENDMENTS.md#MAIL-A5): TabBoard, PinFooter, WebSnapshot). The full
enumeration (key, kind, version, assembly, packed artifact, mount, composition edges) is L5 canon in
[`docs/data/components.json`](data/components.json) — not restated here. Common settings and the single
Abstractions reference live once in [`Directory.Build.props`](../Directory.Build.props).

### 4.2 Domain model (NOUNS) {#MAIL-§4.2}
- **Component** — a single `.idea` citizen: a Theme, Widget, or Control. Identity = `key` (namespace tail)
  + `version` (`V{n}` class). Catalogued in [`components.json`](data/components.json).
- **Theme** — chrome (a `theme.css`) plus exactly one `@Body` hole; derives from `ThemeBase`, exposes
  `ThemeCssUrls`. May compose Widgets.
- **Widget** — a self-contained capability (font, effect, glyph, gallery, greeting); derives from
  `WidgetBase`, exposes `StylesheetUrls` and/or inline Razor markup. May compose other Widgets.
- **Control** — a parameterized UI element configured like a React/Angular component; derives from
  `ControlBase`, typed `[Parameter]` props plus pass-through `Attributes`.
- **Asset bundle** — a component's `assets/` folder (the single source of truth); becomes the package
  `wwwroot/` at pack time and is served under the component **mount** `/_ideas/<Kind>/<key>/<version>/`.
- **`.idea` artifact** — the packed, uploadable zip in [`dist/`](../dist) (one per component).

### 4.3 Key services (VERBS) {#MAIL-§4.3}
- **build** — `dotnet build -c Release <project>` (or the whole `.slnx`); compiles each RCL to a single
  DLL against Abstractions.
- **pack** — `dotnet run --project ../MindAttic.Ideas/src/MindAttic.Ideas.Sdk -- pack …` turns a built DLL
  + its `assets/` into a `dist/*.idea` (see [README](../README.md)).
- **compose** — `[Uses(ContentKind, "key", n)]` declares a dependency on another installed component and
  `<CmsInclude Ref="…">` renders it; resolution is by **string id** at install/render time, never a build
  reference.
- **mount/serve** — the CMS serves a component's bundle under `/_ideas/<Kind>/<key>/<version>/`; a raw page
  links `assets/*` directly to prove the bundle stands alone.

## 5. The Laws {#MAIL-§5}

This project **inherits the org-wide House Rules** at
[`MindAttic.HouseRules.md`](../../MindAttic.HouseRules.md) by reference — they are not restated here.
Most load-bearing for this repo: [HOUSE-LAW-1](../../MindAttic.HouseRules.md) (whole-number versioning),
[HOUSE-LAW-5](../../MindAttic.HouseRules.md) (`.idea` is a guarded, versioned, integrity-checked zip with a
soft-disable lifecycle), and [HOUSE-LAW-8](../../MindAttic.HouseRules.md) (done = verified). The laws below
are **project-specific** to MindAttic.Ideas.Library.

### {#MAIL-LAW-1} The asset bundle is the single source of truth.
A component owns its `assets/` (css/js/html/images) once, copied in here, never staged from another repo.
That one bundle serves all three consumers (raw HTML, standalone Blazor, CMS). No second copy may exist.

### {#MAIL-LAW-2} Identity is convention, never configuration.
A component's **key** is its namespace tail, lowercased; its **version** is the `V{n}` class number. A
`.csproj` declares no key/version. Renaming the tail or class is a breaking identity change.

### {#MAIL-LAW-3} Compose by string id, never by project reference.
The only `ProjectReference` any component may carry is the frozen Abstractions SDK. Dependencies on other
components are declared with `[Uses(ContentKind, "key", n)]` and rendered with `<CmsInclude Ref="…">`,
resolved at install/render time.

### {#MAIL-LAW-4} One DLL per packed `bin/`.
Abstractions and the framework Components it carries stay out of `bin/` (`Private=false` +
`ExcludeAssets=runtime`). A packed `bin/` holds exactly one DLL; host assemblies in `bin/` are forbidden by
the validator.

### {#MAIL-LAW-5} Chrome lives in `assets/`, never `wwwroot/`.
Component css/js live in a plain `assets/` folder with `StaticWebAssetsEnabled=false`, so the Razor SDK
never registers them as static web assets (the cause of past cross-host collisions). `assets/` becomes the
package `wwwroot/` only at pack time; at render it is served under the component mount.

### {#MAIL-LAW-6} Styles are scoped to the component.
Every selector is scoped under a component-specific root (e.g. `.hello-world`, `.theme-light`,
`.ma-field`) so a component never leaks styles into the host theme, the page, or a sibling component.

### {#MAIL-LAW-7} The CMS never references the library.
The CMS is standalone; it installs packed `.idea`s as **optional** content and never takes a code
dependency on anything here. The dependency arrow points one way only.

### {#MAIL-LAW-8} Pages are records, not `.idea`s.
Themes, Widgets, and Controls ship as `.idea`. A Page is a CMS database record. No page source lives in
this repo at all — the once-parked Pages/_wip tree was deleted as no longer applicable
([MAIL-A4](AMENDMENTS.md#MAIL-A4)); history stays in git.

## 6. Verified state {#MAIL-§6}

| Aspect | Status | Evidence (2026-06-09) |
|---|---|---|
| Full solution compiles | ✅ | `dotnet build -c Release MindAttic.Ideas.Library.slnx` → **Build succeeded, 0 Warning(s), 0 Error(s)**; all 36 component DLLs + Abstractions emitted (net10.0). Verified 2026-06-09. |
| Smallest widget builds standalone | ✅ | `dotnet build -c Release Widgets/HelloWorld` → succeeded, 0/0. Verified 2026-06-07. |
| Packed artifacts present | ✅ | [`dist/`](../dist) holds 36 `*.idea` — one per catalogued component in [`components.json`](data/components.json). The 15 baseline-set artifacts were packed (`--wwwroot assets`) and compose-graph verified (`ma-idea verify`) 2026-06-09. |
| Automated tests | ⬜ | No test project exists in the repo (RCL component library; verification is build + the HelloWorld interactive smoke test + per-component `demo.html`). See [MAIL-§8](#MAIL-§8). |
| `pack` round-trip | ✅ | Re-run 2026-06-09: 15 baseline widgets packed (`ma-idea pack --wwwroot assets`); `ma-idea verify ./dist` → "OK — every declared dependency resolves" across all 36 (re-verified with the MAIL-A5 set 2026-06-09). |
| Plugin→Widget rename | ✅ | Rename complete: `Widgets/` + `MindAttic.Ideas.Widget.*` namespaces throughout; solution builds clean 0/0. MAIL-A1. (The frozen Pages/_wip prose that retained "Plugin" was deleted with the tree — MAIL-A4.) |
| Textbox Control→Widget fold | ✅ | Textbox lives under `Widgets/`, `@inherits WidgetBase`, namespace `MindAttic.Ideas.Widget.Textbox`; no `Controls/` folder on disk. `components.json` and `.slnx` both record Widget kind. MAIL-A2. |

## 7. Active frontier {#MAIL-§7}

- [RFC 0001](rfc/0001-component-test-harness.md) — a smoke-test harness so component "done" is build-proven,
  not asserted (closing the ⬜ in [MAIL-§6](#MAIL-§6) against [HOUSE-LAW-8](../../MindAttic.HouseRules.md)).
- [RFC 0002](rfc/0002-uiux-source-to-idea-pipeline.md) — raw UiUx source → `.idea` generator (deferred).
- Backlog and acceptance criteria: [`USER_STORIES.md`](USER_STORIES.md).

## 8. Quality bar {#MAIL-§8}

A component change is **done** only when:
1. `dotnet build -c Release` of the component (and the `.slnx`) is clean — 0 warnings, 0 errors.
2. Its assets live in `assets/` (not `wwwroot/`) and every selector is scoped to the component
   ([MAIL-LAW-5](#MAIL-LAW-5), [MAIL-LAW-6](#MAIL-LAW-6)).
3. Identity is convention-only ([MAIL-LAW-2](#MAIL-LAW-2)); the only `ProjectReference` is Abstractions
   ([MAIL-LAW-4](#MAIL-LAW-4)); any cross-component dependency is by string id ([MAIL-LAW-3](#MAIL-LAW-3)).
4. The catalog row in [`components.json`](data/components.json) is updated (key/version/kind/uses).
5. For interactive widgets, the live behavior is observed (HelloWorld's click counter; per-component
   `demo.html` for the raw-HTML consumer). Mark `✅` only when build/observation proves it, else `🟡`/`⬜`
   ([HOUSE-LAW-8](../../MindAttic.HouseRules.md)).

## 9. Glossary {#MAIL-§9}

- **`.idea`** — a guarded, versioned zip ([HOUSE-LAW-5](../../MindAttic.HouseRules.md)) that is the unit of
  distribution for a component; uploaded to the CMS as optional content.
- **Component** — a Theme, Widget, or Control; one RCL, one `.idea`. Catalog: [`components.json`](data/components.json).
- **Theme / Widget / Control** — see [MAIL-§4.2](#MAIL-§4.2). (Widget was formerly "Plugin".)
- **Asset bundle / `assets/`** — the single source of truth for a component's css/js/html/images; becomes
  the package `wwwroot/` at pack time.
- **Mount** — the served path `/_ideas/<Kind>/<key>/<version>/` for a component's assets.
- **Key** — a component's namespace tail, lowercased; its stable string identity.
- **Version** — the `V{n}` content class number; whole-number only ([HOUSE-LAW-1](../../MindAttic.HouseRules.md)).
- **`[Uses]` / `<CmsInclude>`** — declare/render a dependency on another installed component by string id.
- **Abstractions** — `MindAttic.Ideas.Abstractions`, the frozen SDK (the only thing components compile
  against); supplies `ThemeBase` / `WidgetBase` / `ControlBase`, `[Idea]`, `[Uses]`, `CmsInclude`.
- **Page record** — a CMS database row (Html/Css/Js + tags); NOT a `.idea` ([MAIL-LAW-8](#MAIL-LAW-8), [MAIL-A4](AMENDMENTS.md#MAIL-A4)).
- **RCL** — Razor Class Library, the project type of every component (`Microsoft.NET.Sdk.Razor`).
