---
codex: 1
project: MindAttic.Ideas.Library
code: MAIL
layer: stories
status: living
updated: 2026-06-09
---

# MindAttic.Ideas.Library — User Stories

> ✅ done (shipped & verified) · 🟡 partial · ⬜ planned · 🗑️ cut. Every ✅ cites the proof.
> This is a component RCL library with **no test project**; "verified" here means a clean
> `dotnet build` and/or an observed interactive/raw-HTML demo (see [MAIL-§8](BIBLE.md#MAIL-§8) and
> [HOUSE-LAW-8](../../MindAttic.HouseRules.md)). Where a story would normally cite a `[Test]`, it cites the
> build target or demo that proves it; absent an automated test those stories stay 🟡.

## Epic A — Authoring a component

- **MAIL-US-A1 ✅** As a component author, I can add a Theme/Widget/Control as a tiny RCL and have its
  identity come from convention (namespace tail = key, `V{n}` = version), so no per-project key/version
  config exists. *Given a new project under `Themes/`/`Widgets/`/`Controls/`, When I build it, Then it
  compiles against Abstractions alone and is identified by convention.* *(verified by
  `dotnet build -c Release MindAttic.Ideas.Library.slnx` → 0/0; identity convention documented at
  [MAIL-LAW-2](BIBLE.md#MAIL-LAW-2).)*
- **MAIL-US-A2 ✅** As a component author, I get common build settings and the one Abstractions reference
  for free, so my `.csproj` stays tiny. *Given `Directory.Build.props`, When I add a component, Then I
  declare only my own asset quirks.* *(verified by [`Directory.Build.props`](../Directory.Build.props) +
  the clean solution build.)*
- **MAIL-US-A3 ✅** As a component author, my css/js live in a plain `assets/` folder (not `wwwroot/`) so
  the Razor SDK never causes cross-host static-asset collisions. *Given `StaticWebAssetsEnabled=false` and
  `assets/`, When the solution builds, Then no static-web-asset collision occurs.* *(verified by the clean
  solution build; rule at [MAIL-LAW-5](BIBLE.md#MAIL-LAW-5).)*
- **MAIL-US-A4 🟡** As a site builder, I can compose ordinary-website UI from a **baseline widget set**
  ([MAIL-A3](AMENDMENTS.md#MAIL-A3)): NavMenu, Breadcrumbs, Hero, Card, Accordion, Tabs (incl. the
  mindattic.com tab-board variant), Gallery (incl. linked books-grid + lightbox), Carousel, Callout,
  CodeBlock, VideoEmbed, ContactForm, SocialLinks, BackToTop, Footer (pin-when-short). *Given the 15
  baseline projects, When the solution builds and each packs, Then 15 `dist/*.idea` exist and every widget
  carries a raw-HTML `demo.html` proving the bundle stands alone.* *(build + pack verified 2026-06-09
  — `dotnet build -c Release` 0/0 and 33 artifacts in `dist/`; 🟡 until the per-widget demos and a live
  CMS render are observed per [MAIL-§8](BIBLE.md#MAIL-§8).)*

## Epic B — The one bundle, three consumers

- **MAIL-US-B1 ✅** As a raw-HTML author, I can link a component's `assets/*` directly and see it render
  with no CMS, build, or Blazor. *Given `Themes/Cyberspace/demo.html`, When I open it, Then the theme
  chrome renders from `assets/theme.css` alone.* *(verified by [`Themes/Cyberspace/demo.html`](../Themes/Cyberspace/demo.html)
  linking the same `theme.css` the `.idea` bundles.)*
- **MAIL-US-B2 🟡** As a standalone Blazor app, I can reference a component RCL (or link the same `assets/`)
  and get identical output to the CMS. *Given the component RCL, When an app references it, Then it renders
  the same bundle.* *(no standalone-app harness in this repo yet; the RCLs build and the bundle is shared by
  construction — not independently observed here.)*
- **MAIL-US-B3 ✅** As the CMS, I can upload a packed `.idea` and serve its bundle under
  the component mount. *Given a packed artifact in [`dist/`](../dist), When the CMS installs it, Then the
  bundle is served at the mount.* *(verified by an observed live run 2026-06-09: all 33 `dist/*.idea` installed through the
  CMS startup library path and the rendered frontpage served `/_ideas/Widget/tabs/1/tabs.css`,
  `/_ideas/Theme/cyberspace/1/theme.css`, … with HTTP 200 — see MAI BIBLE §6 live-render evidence.)*

## Epic C — Composition by string id

- **MAIL-US-C1 ✅** As a Theme author, I can compose installed Widgets by string key without a project
  reference, so themes stay decoupled. *Given `theme.cyberspace` with `[Uses(Widget,"outfitfont",1)]`…
  `[Uses(Widget,"cyberspace",1)]` and matching `<CmsInclude>`, When it builds, Then it carries only its own
  chrome and pulls the rest by id.* *(verified by the clean build of `Themes/Cyberspace`; edges recorded on
  [theme.cyberspace](data/components.json).)*
- **MAIL-US-C2 ✅** As a Widget author, I can compose other Widgets by id (Frontpage→tooltip,
  LegionPersonas→sacredgeometry). *Given those `[Uses]` declarations, When the widgets build, Then the
  edges resolve by id only.* *(verified by the clean solution build; edges on
  [widget.frontpage](data/components.json) and [widget.legionpersonas](data/components.json).)*

## Epic D — Catalog & lifecycle

- **MAIL-US-D1 ✅** As a maintainer, I can read one catalog of every shipped component (key, kind, version,
  assembly, artifact, composition). *Given [`components.json`](data/components.json), When I open it, Then
  all 36 components are enumerated and validate against their schema.* *(verified by
  `tools/codex.ps1 doctor` schema + id-uniqueness checks.)*
- **MAIL-US-D2 ✅** As a maintainer, every component versions by whole numbers only. *Given `<Version>` and
  the `V{n}` class, When I inspect any component, Then the version is a whole number.* *(verified by
  [`Directory.Build.props`](../Directory.Build.props) `<Version>1.0.0</Version>` + `V1` classes;
  [HOUSE-LAW-1](../../MindAttic.HouseRules.md).)*
- **MAIL-US-D3 ✅** As a maintainer, I can re-`pack` any component into a fresh `.idea`. *Given a built DLL
  + `assets/`, When I run the pack command, Then a `dist/*.idea` is produced.* *(verified by the pack re-run 2026-06-09: the 15
  baseline widgets were packed via `ma-idea pack --wwwroot assets` and `ma-idea verify ./dist` reports
  "OK — every declared dependency resolves" across all 33 artifacts.)*

## Epic E — Pages stay records

- **MAIL-US-E1 🗑️** As a maintainer, frozen page source is preserved losslessly without polluting the build.
  *(original spec — audit log: "Given `Pages/_wip/*` with `*.csproj.wip` + an empty `Directory.Build.props`
  and absence from `.slnx`, When the solution builds, Then nothing under `Pages/_wip` compiles or packs.")*
  **Cut 2026-06-09 by [MAIL-A4](AMENDMENTS.md#MAIL-A4):** the parked sources are no longer applicable —
  the frontpage is assembled verbatim from mindattic.com's `index.htm` into a Data page, and
  LegionPersonas ships as `Widget.LegionPersonas`. The `Pages/` tree was deleted; history stays in git.
  [MAIL-LAW-8](BIBLE.md#MAIL-LAW-8) (pages are DB records, never `.idea`s) is unchanged.

## Priority backlog

1. **MAIL-US-B2** — a standalone-Blazor-app smoke harness ([RFC 0001](rfc/0001-component-test-harness.md))
   — the remaining ⬜ test gap in [MAIL-§6](BIBLE.md#MAIL-§6). (D3's pack round-trip and B3's
   install/serve were proven 2026-06-09.)
2. **MAIL-US-A4** — observe the 15 baseline-widget demos interactively (build/pack already proven).

### Audit log

No story has been re-scoped from an original written spec; this file is the first formal statement of the
library's stories, derived from the README, the per-component source/conventions, and the verified build.
When a story is later re-scoped, preserve its original ask verbatim here, marked "(original spec — audit log)".
