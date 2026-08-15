# MindAttic.Ideas

A **single-deployment Blazor CMS** for the MindAttic ecosystem. **One** Azure App Service, **one** app
pool, **one** database hosts *many* pages — so a project like `MindAttic.Frontpage` or
`MindAttic.Legion.Frontend` no longer needs a whole web app just to serve essentially one page.

You ship capability by **uploading or CLI'ing a `.idea` file** (a plain zip). The CMS reads whether
it's a **Page**, **Plugin**, **Component**, or **Theme**, registers it, and it's live — no redeploy,
no app-pool restart. Plugins, Components, and Themes are **globally available**, so any Page composes
them by dropping a tag:

```html
{{ Theme.Cyberspace }}
{{ Plugin.Tooltip }}          <!-- no version = latest -->
{{ Component.Textbox label="Email" }}
```

or, from a compiled page/theme, the equivalent `<CmsInclude Ref="…"/>` primitive (see
[Composing citizens](#composing-citizens-cmsinclude--tags)).

> **Status:** Foundation **built and verified end-to-end** (0 build errors, 224 NUnit tests green as of
> the last recorded run — see [`docs/BIBLE.md §6`](docs/BIBLE.md#MAI-§6)). This README is a **practical
> engineering tour**; [`docs/BIBLE.md`](docs/BIBLE.md) + [`docs/AMENDMENTS.md`](docs/AMENDMENTS.md) are the
> canonical source of truth for what's true today — an amendment always wins over prose here or in the
> bible. [`docs/USER_STORIES.md`](docs/USER_STORIES.md) test-cites every shipped story.
>
> **A note on vocabulary.** The content kinds have been renamed twice as the design settled
> (`Widget`/`Control` → `Plugin`/`Component`, amendments A18/A19/A26). This README uses the **current**
> vocabulary — `Page` · `Plugin` · `Theme` · `Component` — throughout. If you see `Widget` or `Control`
> anywhere in the codebase (a stray file name, an old comment, `docs/FOUNDATION_ADR.md`), it is legacy
> vocabulary from before that split; `docs/BIBLE.md §9` is the authoritative glossary.

---

## Table of contents

- [The one mental model](#the-one-mental-model)
- [The tag convention](#the-tag-convention-locked)
- [Composing citizens: CmsInclude / tags](#composing-citizens-cmsinclude--tags)
- [Versioning & lifecycle](#versioning--lifecycle)
- [The Abstractions SDK — public API](#the-abstractions-sdk--public-api)
- [The `.idea` package format](#the-idea-package-format)
- [Directory layout](#directory-layout)
- [Worked example: authoring a Page from the template](#worked-example-authoring-a-page-from-the-template)
- [Worked example: a Plugin from the first-party library](#worked-example-a-plugin-from-the-first-party-library)
- [The first-party library](#the-first-party-library)
- [CSS cascade](#css-cascade--fixed-order)
- [Trust & security](#trust--security)
- [Ecosystem integration](#ecosystem-integration)
- [Build, test, run](#build-test-run)
- [The `ma-idea` CLI](#the-ma-idea-cli)
- [End-to-end tests](#end-to-end-tests)
- [Feature checklist](#feature-checklist)
- [Stack](#stack)
- [Canon & further reading](#canon--further-reading)

---

## The one mental model

**A Page is free-form. A Theme wraps it. Plugins and Components drop into it. Inline JS/CSS/HTML is yours.**

There are **no zones, panes, slots, or grids** — DotNetNuke's clunky fixed-layout model is explicitly
rejected. You author a page however you like and place content exactly where you want it in your markup.
The unit you install is a `.idea` zip; the thing it contains is one of four **content kinds**, all
deriving from a shared root, **`IdeaBase`** (`src/MindAttic.Ideas.Abstractions/Bases.cs`):

| Kind (`ContentKind`) | Ordinal | What it is | Base type | Example reference |
|---|---|---|---|---|
| **Page** | `0` | A free-form or compiled page — content, resolved by `(SiteId, Slug)` | `PageBase` | `MindAttic.Ideas.Page.HelloWorld.V1` |
| **Plugin** | `1` | A site-wide *capability activator* (loads css/js across the whole page; picked in Page Properties or `{{Plugin.X}}`) | `PluginBase` | `MindAttic.Ideas.Plugin.Tooltip.V1` |
| **Theme** | `2` | Layout chrome + one `@Body` hole + a CSS bundle | `ThemeBase` | `MindAttic.Ideas.Theme.Cyberspace.V4` |
| *(ordinal 3 — removed)* | `3` | `Control` was deleted pre-1.0 (A19); atomic UI is authored as a Component. **Never reused.** | — | — |
| **Component** | `4` | An inline-placed UI unit, rendered at the exact `{{Component.X}}` token position; can nest other Components | `ComponentBase` | `MindAttic.Ideas.Component.Textbox.V1` |

"Idea" names the shared base and the `.idea` package format (and the `/_ideas/...` asset route) — it is
**never** a content kind itself. New kinds can be **appended** to the enum later (new ordinals only,
never renumbered) without breaking anything.

> **Plugin vs Component.** A **Plugin** is a site-wide capability activator — dropping
> `{{Plugin.Tooltip}}` loads the tooltip engine so that thereafter **any** element with
> `data-tooltip`/`data-tt` shows a tooltip on hover; by default it renders no markup of its own (see
> `PluginBase.BuildRenderTree`, which just emits `<link>`/`<script>` tags for its declared
> `StylesheetUrls`/`ScriptUrls`). A **Component** is inline-placed at a specific `{{Component.X}}` token
> position and renders actual markup (e.g. `Component.Textbox` renders an `<input>`). Both can nest
> other citizens via `[Uses]` / `<CmsInclude Ref="…"/>`.

### Two ways to author a Page — one render path

- **Data page** (zero deploy): free-form `BodyHtml` / `PageCss` / `PageJs` stored in the DB.
  Interactivity comes from **your inline JS**. `{{Kind.Name}}` tags are expanded into live content at
  render time by the `IncludeExpander`. *This is the primary path.*
- **Code page** (compiled): a `PageBase` subclass — a `.razor` component shipped as a `.idea` — for
  when you genuinely need Blazor C# interactivity. Deploys once per *type*, never per page instance.

Both are first-class `Page` rows resolved by `(SiteId, Slug)` and rendered through the same primitive
(`PageHost` → `DynamicComponent` / the built-in free-form renderer). A page can **graduate Data ↔ Code
as a row edit** — never a schema change.

---

## The tag convention (locked)

**In a Data page** (stored `BodyHtml`), the include grammar is:

```
{{ <Kind>.<Name>[.V<n>|.Latest] [attr=value …] }}
```

- **`<Kind>`** — `Theme` · `Plugin` · `Component` (first token, case-insensitively matched against the
  `ContentKind` enum member names).
- **`<Name>`** — the content's key (`Cyberspace`, `Tooltip`, `Textbox`, …).
- **`[.V<n>|.Latest]`** — **optional**: omitted or `.Latest` → **latest enabled** version; `.V3` → **pins**
  version 3.
- A missing/disabled reference degrades to a **clickable placeholder** (opens the admin uploader
  prefilled with the missing reference) — never a crash.

**In a compiled Page/Theme/Component** (a `.razor` that compiles against only `Abstractions`), the same
identity is expressed as the fully-qualified string `"MindAttic.Ideas.{Kind}.{Name}.{Version}"` passed to
the `<CmsInclude Ref="…"/>` primitive — see the next section. Both forms resolve through the **same**
catalog, the **same** `Missing`/`Disabled` degradation, and the **same** Admin Inbox alerting.

Identity is inferred by **convention** — Kind from the base type, Key from the namespace tail, Version
from the `V{n}` class name — so no attributes are needed in the normal case. An optional
`[Idea(key:…, version:…, scope:Global)]` attribute overrides the convention when a name can't follow it.

---

## Composing citizens: CmsInclude / tags

A **compiled** Page/Theme/Component references another citizen **by string id**, with **zero
compile-time reference** to that citizen's package — the MindAttic analog of Orchard's `@Display` /
`<zone>`. This is `CmsInclude`, defined once in `src/MindAttic.Ideas.Abstractions/CmsInclude.cs`:

```razor
<CmsInclude Ref="MindAttic.Ideas.Plugin.Tooltip.V1" />
<CmsInclude Ref="MindAttic.Ideas.Component.Textbox.V1" placeholder="Name" />
<CmsInclude Ref="MindAttic.Ideas.Component.Accordion" />   @* no version = float to latest *@
```

`CmsInclude` pulls the cascaded `IRenderContext`, resolves an `IIncludeRenderer` host feature from it,
and delegates rendering — if no host feature is present (e.g. Blazor design time) it silently renders
nothing rather than throwing. Unmatched attributes on the tag flow straight through to the resolved
citizen.

Declare *what* a compiled citizen depends on with the repeatable `[Uses(ContentKind, key, version)]`
class attribute (`version: 0` = float to latest). This is what a Page never compile-referencing another
package's assembly actually means in practice — it *names* what it uses instead:

```csharp
[Uses(ContentKind.Plugin, "tooltip", 1)]
[Uses(ContentKind.Component, "textbox", 1)]
public sealed class V1 : PageBase { … }
```

`[Uses]` feeds the manifest's `uses[]` array, which drives four things at once: (1) `<head>` asset
hoisting for the referenced citizen's css/js, (2) an install-time "missing dependency" warning, (3) the
delete reference-guard, and (4) the pre-upload compose-graph check (`ma-idea verify`).

Themes are **not** placed with `CmsInclude` — a page selects its theme via the `ThemeKey`/`ThemeVersion`
Page Properties (or the `{{Theme.X}}` inline override token), and the host wraps the body in it.

---

## Versioning & lifecycle

Versions are **whole numbers only** (`V1`, `V2`, `V3`) — never SemVer like `1.5.11`. Same scheme for
every kind. This is the heart of **"never change, only enhance"**:

- You **never mutate** `Cyberspace.V1`. You ship `Cyberspace.V2` **alongside** it; versions coexist.
- A reference may **pin** (`.V1`) when a page cares, or **float to latest** (no version / `.Latest`)
  when it doesn't.

Lifecycle rules (data model shipped; full admin enforcement is part of the Phase-2 admin UI, itself
shipped per `docs/BIBLE.md §6`):

- **A page must never be invalid.** At render, a missing/disabled reference degrades to a visible
  placeholder + fires an **Admin Inbox** alert — never a crash.
- **Disabled = a version that exists but can't be used until re-enabled** (Page, Plugin, Component, Theme).
- **Delete is version-specific and reference-guarded:** you can't delete `Tooltip.V11` while any page
  pins it. Shipping `V12` doesn't free `V11` — each page must first be migrated (`.V11`→`.V12`) until
  nothing references `V11`. A floating (`latest`) reference is fine as long as *some* enabled version
  remains.
- **Wiki-like history** via SQL Server **temporal (system-versioned) tables**: every Page version records
  which Plugin/Component/Theme versions it carried, so you can inspect — and roll back to — any prior
  state.

---

## The Abstractions SDK — public API

Everything an author compiles against lives in one frozen project,
**`src/MindAttic.Ideas.Abstractions`**. It references **only** `Microsoft.AspNetCore.Components` +
`System.Text.Json` — nothing host-specific — and its public surface is **append-only forever** (MAJOR
pinned at `1`, `Sdk.Version` constant): new members may be added, nothing is ever removed, renamed, or
made abstract.

| File | What it defines |
|---|---|
| `Bases.cs` | `IdeaBase` (shared root: cascaded `IRenderContext`, `SafeUrl`/`IsUnsafeUrl` XSS guards) and the four kind bases: `PageBase` (+ generic `PageBase<TSettings>`), `ThemeBase` (`Body` render fragment hole, `GlobalCssUrls`/`ThemeCssUrls`/`ScriptUrls`, `BodyPreludeHtml`), `PluginBase` (`StylesheetUrls`/`ScriptUrls`, default render emits `<link>`/`<script>` only), `ComponentBase` (same asset surface, meant to render real markup — aliases Blazor's `ComponentBase` as `BlazorComponentBase` internally so *MindAttic's* `ComponentBase` wins the bare name). |
| `Enums.cs` | `ContentKind` (`Page=0,Plugin=1,Theme=2,Component=4` — ordinal `3` retired, never reused), `PageKind` (`Data`/`Code`), `CmsRenderMode` (`Static`/`InteractiveServer` — **WebAssembly intentionally excluded**, a hard .NET ALC boundary), `ContentMode` (`View`/`Edit`/`Preview`), `ContentOrigin` (`Compiled`/`Package`), `RenderStrategy` (`ClrType`/`RawMarkup`), `PlacementScope` (`Placeable`/`Global`), `ContentTrust` (`Untrusted`/`Author`). |
| `Attributes.cs` | `[Idea(key:, version:, displayName:, category:, scope:, renderMode:)]` — override the naming convention. `[Uses(ContentKind, key, version)]` — repeatable, declares a runtime string-id dependency (see above). `[IdeaSdkVersionAttribute]` — assembly-level, stamped by the packer. `Sdk.Version` — the frozen SDK version constant (`1`). |
| `Contexts.cs` | `IRenderContext` (cascaded to every citizen: `InstanceId`, `Mode`, `RenderMode`, `Page`/`Site` contexts, scoped `Services`, `RawSettingsJson`/`GetSettings<T>()`, and the additive-forever `TryGetFeature<T>()` escape hatch). `IPageContext`, `ISiteContext`, `IInlineMarkup` (a Data page's `Html`/`Css`/`Js` + `Trusted` flag). Optional host features resolved via `TryGetFeature`: `IIncludeRenderer` (renders a string-id reference — what `CmsInclude` delegates to), `IComponentMetadataStore` (per-instance metadata persistence), `IPageTree` (a page's children/descendants, e.g. for a TableOfContents component). |
| `Discovery.cs` | The seams shared by compiled discovery and the runtime `.idea` loader: `ContentDescriptor` (the uniform record every source yields — identity, `Strategy`, `RenderMode`, `AssetMount`, etc.), `ICmsContentSource` (a registration source), `ITypeResolver` (descriptor → `Type`, ALC-aware in the host), `IContentCatalog` (`Find`/`FindLatest`/`ResolveTag`), `ContentResolution` (`Resolved`/`Missing`/`Disabled`), `IRenderAlertSink` (fire-and-forget Admin Inbox alerting), `IRawContentGate` (the sole `MarkupString` chokepoint), `SharedContracts.DeferToDefaultPrefixes` (the ALC unification allow-list: `MindAttic.Ideas.Abstractions`/`.Core`, `Microsoft.*`, `System.*`, …). |
| `CmsInclude.cs` | The `<CmsInclude Ref="…"/>` component itself. |

**Extension points, summarized:**

| To build a… | Derive from | Override |
|---|---|---|
| Page | `PageBase` (or `PageBase<TSettings>` for typed settings) | Razor markup; `[Uses(...)]` for string-id deps |
| Theme | `ThemeBase` | `Body` (render fragment hole), `GlobalCssUrls`/`ThemeCssUrls`/`ScriptUrls`, `BodyPreludeHtml` |
| Plugin | `PluginBase` | `StylesheetUrls`/`ScriptUrls` (default render just emits `<link>`/`<script>`); override `BuildRenderTree` for markup |
| Component | `ComponentBase` | `StylesheetUrls`/`ScriptUrls`, `BuildRenderTree` for its markup; declare typed `[Parameter]`s, unmatched attrs land in `Attributes` |

---

## The `.idea` package format

A `.idea` is **a plain zip**. Its only required member is `idea.json`. The manifest kernel is defined
in `src/MindAttic.Ideas.Packaging/IdeaManifest.cs`; the six required fields never change:

```jsonc
{
  "manifestVersion": 1,            // schema of this file (host-gated integer)
  "category": "Plugin",            // Page | Plugin | Component | Theme   (WHAT it is)
  "kind": "data",                  // data | code             (HOW it renders)
  "key": "tooltip",                // stable identity, never the CLR type name
  "version": 1,                    // whole-number content version (pins + asset URL segment)
  "displayName": "Tooltip"
  // optional, append-only: sdk, entryType, renderMode, css[], scripts[], assets, uses[], uiux[]
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
(`MindAttic.Ideas.Abstractions`, `Microsoft.*`, `System.*`) are **forbidden** in `bin/` — the
`ManifestValidator` audits for this. Data content carries no `bin/` and installs with zero build and
zero recycle. `MindAttic.Ideas.Packaging` (pure, IO-free, NUnit-tested) is the whole wire contract:
manifest kernel + reflection-only `Packer` + zip-slip-guarded `IdeaArchiveReader` + `ManifestValidator`
+ `Sha256Hasher` + `PackageVersionResolver`.

---

## Directory layout

```
MindAttic.Ideas.slnx                  # CMS engine solution
├─ src/
│  ├─ MindAttic.Ideas.Abstractions    # the frozen SDK — see "The Abstractions SDK" above
│  ├─ MindAttic.Ideas.Core            # EF entities, CmsDbContext (SQL Server, temporal Pages),
│  │                                    #   convention discovery, catalog, raw-content gate,
│  │                                    #   FreeFormPage/include expander, ALC loader, auth, seed
│  ├─ MindAttic.Ideas.Packaging       # pure .idea wire contract: manifest kernel, packer, reader,
│  │                                    #   validator, SHA-256, version resolver
│  ├─ MindAttic.Ideas.Rendering       # small rendering-support library (CmsHead.razor, PageHost.razor)
│  ├─ MindAttic.Ideas.Sdk             # the `ma-idea` CLI: pack / inspect / list / verify / install / upgrade
│  ├─ MindAttic.Ideas.Blazor          # the Blazor Web App host (global InteractiveServer): PageHost
│  │                                    #   catch-all, CmsHead cascade, /admin, /_ideas asset route,
│  │                                    #   Vault + Legion + MindAttic.Authentication wiring
│  └─ MindAttic.Ideas.Tests           # NUnit suite
│
├─ library/                          # first-party Theme/Plugin/Component library — see below
│  ├─ Themes/  Plugins/  Components/  # one small csproj per citizen; Directory.Build.props holds
│  │                                    #   the shared settings + the one allowed reference (Abstractions)
│  └─ dist/                          # packed *.idea output
│
├─ samples/MindAttic.Ideas.Page.HelloWorld   # the reference modular Page (not part of the host solution)
├─ templates/maidea-page                     # `dotnet new` template that scaffolds a Page like the sample
├─ dist/                              # packed .idea artifacts at the repo root (ad hoc / import staging)
├─ e2e/                               # Cypress end-to-end suite (upload → reference → render)
├─ docs/                              # Codex canon: BIBLE.md, AMENDMENTS.md, USER_STORIES.md, rfc/, ADRs
└─ tools/                             # codex.ps1 (docs doctor/digest), import-frontpage.ps1, install-library.ps1
```

> **Known drift:** `MindAttic.Ideas.slnx` currently references a project at
> `src/MindAttic.Ideas.Web/MindAttic.Ideas.Web.csproj`, but the host project directory on disk is
> `src/MindAttic.Ideas.Blazor` (csproj `MindAttic.Ideas.Blazor.csproj`, root namespace
> `MindAttic.Ideas.Blazor`). `dotnet build MindAttic.Ideas.slnx` currently fails with **MSB3202** (project
> file not found) until the `.slnx` is updated to point at the renamed project — see
> [Build, test, run](#build-test-run).

---

## Worked example: authoring a Page from the template

The fastest way to build a new Page citizen is `dotnet new` from `templates/maidea-page`, which
scaffolds exactly what [`samples/MindAttic.Ideas.Page.HelloWorld`](samples/MindAttic.Ideas.Page.HelloWorld)
already shows working end-to-end:

```pwsh
# Install the template once, from the repo root
dotnet new install ./templates/maidea-page

# Scaffold a new Page (run from samples/ so the relative Abstractions path resolves)
cd samples
dotnet new maidea-page -n MyPage --slug my-page --theme cyberspace
# -> samples/MyPage/  with MindAttic.Ideas.Page.MyPage.csproj, namespace MindAttic.Ideas.Page.MyPage, class V1
```

| Template parameter | Default | What it does |
|---|---|---|
| `-n` / `--name` | *(required)* | Short name — becomes `MindAttic.Ideas.Page.<Name>` |
| `--slug` | `hello-world` | Route the page is served at after install |
| `--theme` | `cyberspace` | Theme key this page wears (referenced by string, never bundled) |

The generated project (and the `HelloWorld` sample it mirrors) is a **Razor Class Library** that
compiles against **only** `MindAttic.Ideas.Abstractions`:

```xml
<!-- MindAttic.Ideas.Page.HelloWorld.csproj — the whole thing, save comments -->
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Version>1.0.0</Version>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <!-- Private=false + ExcludeAssets=runtime keep Abstractions out of the packed bin/ -->
    <ProjectReference Include="..\..\src\MindAttic.Ideas.Abstractions\MindAttic.Ideas.Abstractions.csproj"
                      Private="false" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

```razor
@* V1.razor — identity by convention: namespace tail "HelloWorld" -> key "helloworld", class "V1" -> version 1 *@
@namespace MindAttic.Ideas.Page.HelloWorld
@inherits PageBase
@attribute [Uses(ContentKind.Plugin, "tooltip", 1)]
@attribute [Uses(ContentKind.Component, "textbox", 1)]

<section class="hello">
    <h1>Hello, world.</h1>

    <CmsInclude Ref="MindAttic.Ideas.Plugin.Tooltip.V1" />
    <p><button type="button" data-tooltip="Resolved at runtime by string id.">Hover me</button></p>

    <CmsInclude Ref="MindAttic.Ideas.Component.Textbox.V1" placeholder="Type here…" />
</section>
```

```json
// data/page.json — the initial Page row seed
{
  "slug": "hello-world",
  "title": "Hello World",
  "themeKey": "cyberspace",
  "themeVersion": 1,
  "published": true
}
```

Build, pack, inspect, and upload it (full detail in [`docs/AUTHORING.md`](docs/AUTHORING.md)):

```pwsh
dotnet build -c Release samples/MyPage

dotnet run --project src/MindAttic.Ideas.Sdk -- pack `
  --assembly samples/MyPage/bin/Release/net10.0/MindAttic.Ideas.Page.MyPage.dll `
  --out ./dist `
  --refs src/MindAttic.Ideas.Abstractions/bin/Debug/net10.0

dotnet run --project src/MindAttic.Ideas.Sdk -- inspect ./dist/MindAttic.Ideas.Page.MyPage.V1.idea
```

Then, in the CMS admin at **/admin/upload**, drop the `.idea`. The host validates it (the same gate as
`ma-idea install`), registers the type, extracts its `wwwroot/`, and it is immediately live at its slug.

---

## Worked example: a Plugin from the first-party library

A **Plugin** is even smaller when it's pure asset-activation with no custom markup. This is the actual,
shipped `library/Plugins/Tooltip/V1.cs` in full:

```csharp
using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Plugin.Tooltip;

public sealed class V1 : PluginBase
{
    private const string Mount = "/_ideas/Plugin/tooltip/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/tooltip.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/tooltip.js" };
}
```

Its assets live in a plain `assets/` folder next to the code (**not** `wwwroot/`, to avoid the Razor
static-web-asset collision):

```
library/Plugins/Tooltip/
 ├─ MindAttic.Ideas.Plugin.Tooltip.csproj   # ~3 lines; settings inherited from Directory.Build.props
 ├─ V1.cs
 └─ assets/
     ├─ tooltip.css
     └─ tooltip.js
```

That same `assets/` bundle serves three consumers with zero duplication: a raw `.html` demo page links
`assets/*.css`/`*.js` directly; a standalone Blazor app references the RCL or the same `assets/`; and the
CMS uploads the packed `.idea` whose `wwwroot/` *is* that folder, served at
`/_ideas/Plugin/tooltip/1/…`.

---

## The first-party library

**`library/`** (`MindAttic.Ideas.Library.slnx`) is the first-party home for every official Theme,
Plugin, and Component — merged into this repo from a former sibling repo. It is **build-independent of
the CMS**: it references only `Abstractions`, and the CMS never compile-references it, only installs
its packed output as optional content. Present on disk today:

| Folder | Citizens present |
|---|---|
| `Themes/` (8) | Autumn, Cyberspace, Dark, Hardware, Light, Spring, Summer, Winter |
| `Plugins/` (12) | AtticFont, BackHomeM, BackToTop, Breadcrumbs, Cyberspace, Footer, Header, NavMenu, OutfitFont, PinFooter, SacredGeometry, SocialLinks |
| `Components/` (27) | Accordion, Callout, Card, Carousel, ChiMesh, Claudia, CodeBlock, ContactForm, FromHtml, FromMd, Frontpage, Gallery, HardwareHero, HelloWorld, Hero, IdeasBrochure, IdeasFrontpage, LegionPersonas, MediaImage, MediaLink, MindAtticFrontpage, ModalPopup, TabBoard, TableOfContents, Tabs, Textbox, VideoEmbed, WebSnapshot |

(`library/README.md` and `library/docs/BIBLE.md` currently cite 43 total citizens / 23 Components as of
amendment MAIL-A6; the counts above are a fresh directory listing at the time this README was written —
several Components (`FromHtml`, `FromMd`, `Header`, `IdeasFrontpage`, `MediaImage`, `MediaLink`) exist on
disk but aren't yet reflected in that older count, so treat `library/docs/` as the tie-breaker for exact
numbers.)

```pwsh
# Build one citizen
dotnet build -c Release library/Plugins/Tooltip

# Build everything in the library
dotnet build -c Release library/MindAttic.Ideas.Library.slnx

# Pack + verify (from the repo root)
dotnet run --project src/MindAttic.Ideas.Sdk -- pack `
  --assembly library/Plugins/Tooltip/bin/Release/net10.0/MindAttic.Ideas.Plugin.Tooltip.dll `
  --out library/dist --wwwroot library/Plugins/Tooltip/assets `
  --refs src/MindAttic.Ideas.Abstractions/bin/Debug/net10.0

dotnet run --project src/MindAttic.Ideas.Sdk -- verify library/dist
```

The library has its own Codex canon at `library/docs/BIBLE.md` / `AMENDMENTS.md` / `USER_STORIES.md` /
`docs/data/components.json` (a machine-readable catalog of every shipped `.idea`) — see
`library/CLAUDE.md` for its working rules.

---

## CSS cascade — fixed order

Locked, enforced in exactly one place (`CmsHead`, `src/MindAttic.Ideas.Rendering/CmsHead.razor`), never
reordered:

```
GLOBAL stylesheet  →  THEME stylesheet (e.g. Cyberspace)  →  PAGE-level stylesheet  →  inline style=""
   (Host setting)        (mirrors UiUx deps.json)              (Page.PageCss)            (by DOM nature)
```

A per-page tweak is either **inline CSS** in the Page definition, or an uploaded **`.idea`**.

---

## Trust & security

Sign-in is delegated to the **[MindAttic.Authentication](https://github.com/mindattic/MindAttic.Authentication)**
package (Argon2id+pepper, Vault-backed, hardened sessions) — `src/MindAttic.Ideas.Blazor/Program.cs`
wires it via `AddMindAtticAuthentication<CmsDbContext>(...)` with `AppName = "Ideas"` (a hard per-app
trust boundary — no cross-app SSO), plus a `Cms.AuthorRawMarkup` policy claim. What stays Ideas-owned is
the *raw-content* trust gate:

- On save, a page is stamped `ContentTrust.Author` **iff** the writer holds the `Cms.AuthorRawMarkup`
  claim (Admin role); otherwise `Untrusted`.
- At render, a single gate (`IRawContentGate`) emits markup: **Author → raw passthrough** (your inline JS
  runs); **Untrusted → sanitized** (HtmlSanitizer strips script/style/event-handlers/`javascript:`;
  `{{tags}}` survive).
- Demoting an author is a deliberate policy action (an `AuthorTrustVersion` epoch bump), never a silent
  re-render of live pages.

You intentionally author **inline JavaScript** in trusted pages — that's a feature, not a leak. The
trust boundary is **author identity at write time**, not content inspection at read time.

---

## Ecosystem integration

MindAttic.Ideas reuses the ecosystem's shared infrastructure (all wired in
`src/MindAttic.Ideas.Blazor/Program.cs`):

- **[MindAttic.Vault](https://github.com/mindattic/MindAttic.Vault)** — all credentials (DB connection
  strings, API keys, admin bootstrap) via `AddMindAtticVaultFiles(...)` + `AddMindAtticVault(...)`. Same
  code locally (`%APPDATA%\MindAttic\…`) and on Azure (App Settings / Key Vault via Managed Identity).
  **No User Secrets.**
- **[MindAttic.Legion](https://github.com/mindattic/MindAttic.Legion)** — LLM calls + multi-model
  voting/consensus/scoring, wired via `AddLegionClient()`.
- **[MindAttic.Authentication](https://github.com/mindattic/MindAttic.Authentication)** — the canonical
  auth engine, wired via `AddMindAtticAuthentication<CmsDbContext>(...)` (see [Trust & security](#trust--security)
  above). Note: `docs/BIBLE.md` currently marks this integration `📋 planned` pending an "interim BCrypt"
  stack, but `Program.cs` as it stands on disk already calls `AddMindAtticAuthentication` — treat the
  code as ahead of that doc line and `docs/AMENDMENTS.md` (A16) as the place that should eventually be
  updated to match.
- **MindAttic.Media** — disk-backed media storage, pointed at `{ContentRoot}/media`.
- **[MindAttic.UiUx](https://github.com/mindattic/MindAttic.UiUx)** — the canonical upstream source for
  official Plugins/Components/Themes' raw js/css/html, from which the `library/` Blazor wrappers are
  authored.

---

## Build, test, run

```pwsh
# Build the CMS engine solution
dotnet build MindAttic.Ideas.slnx -c Debug

# Run the NUnit suite
dotnet test src/MindAttic.Ideas.Tests/MindAttic.Ideas.Tests.csproj

# Run the Blazor host directly (bypassing the stale .slnx — see the drift note above)
dotnet run --project src/MindAttic.Ideas.Blazor
```

> As of this writing, `dotnet build MindAttic.Ideas.slnx` fails immediately with **MSB3202** because the
> `.slnx` still points at `src/MindAttic.Ideas.Web/MindAttic.Ideas.Web.csproj`, a path that no longer
> exists (the project directory is `src/MindAttic.Ideas.Blazor`, csproj `MindAttic.Ideas.Blazor.csproj`).
> Building/running the individual projects (`Abstractions`, `Core`, `Packaging`, `Rendering`, `Sdk`,
> `Tests`, `Blazor`) directly works around this; the `.slnx` itself needs its project path corrected.

Codex docs tooling (see [Canon & further reading](#canon--further-reading)):

```pwsh
powershell -File tools/codex.ps1 digest   # regenerate docs/BIBLE.digest.md
powershell -File tools/codex.ps1 doctor   # validate the canon (must pass)
```

---

## The `ma-idea` CLI

`src/MindAttic.Ideas.Sdk` builds a CLI (`ma-idea`) over the pure `MindAttic.Ideas.Packaging` library.
Every read verb is offline and never touches a database:

| Verb | What it does |
|---|---|
| `pack --assembly <dll> --out <dir> [--wwwroot <dir>] [--data <dir>] [--icon <file>] [--version <n>] [--refs <a;b>]` | Packs a built Page/Theme/Plugin/Component RCL into a `.idea` (reflection-only; identity read from namespace + `Vn` class by convention). |
| `inspect <file.idea>` | Prints the manifest + `bin/`/`wwwroot/`/`data/` file counts. |
| `list [dir]` | Lists every `.idea` in a directory (key, version, category, kind). |
| `verify [dir]` | Checks every package's `uses[]` resolves against the `.idea` files in that directory — the compose-graph check. |
| `install <file.idea> [--allow-override]` | **Offline validation only** — does not install; that is a host operation (`PackageInstallService`). |
| `upgrade <file.idea>` | Validates + previews the install action (`InstallAction`) against the `.idea` files beside it. |
| `disable` | Refuses — disabling a live package is a host database operation, not reachable offline. |

```pwsh
dotnet run --project src/MindAttic.Ideas.Sdk -- pack --assembly bin/Release/net10.0/MyPage.dll --out ./dist
dotnet run --project src/MindAttic.Ideas.Sdk -- inspect ./dist/MindAttic.Ideas.Page.MyPage.V1.idea
dotnet run --project src/MindAttic.Ideas.Sdk -- verify ./dist
```

---

## End-to-end tests

`e2e/` is a Cypress suite covering the core admin flow end to end: **admin logs in → uploads a compiled
`.idea` → creates a page referencing it by `{{tag}}` → the page renders the content with no missing-content
placeholder** (`e2e/cypress/e2e/admin-widget-flow.cy.js`, fixture
`e2e/cypress/fixtures/MindAttic.Ideas.Plugin.Tooltip.V1.idea`). It expects a running instance of the host
(`MindAttic.Ideas.Blazor`) — it does not start the app itself. See `e2e/README.md` for the full
environment-variable table and run instructions:

```pwsh
# 1) start the CMS (from the repo root, separate dev DB)
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS        = "https://localhost:7207"
dotnet run --project src/MindAttic.Ideas.Blazor

# 2) in a second shell, from e2e/
npm install
$env:CYPRESS_BASE_URL       = "https://localhost:7207"
$env:CYPRESS_ADMIN_PASSWORD = "<bootstrap admin password>"
npm run cy:run
```

---

## Feature checklist

The full, kept-in-lockstep checklist (with per-item ✅/🔨/📋 status and the test/story that proves each
`✅`) lives in [`docs/BIBLE.md §6`](docs/BIBLE.md#MAI-§6) and [`docs/USER_STORIES.md`](docs/USER_STORIES.md) —
this README does not duplicate it. In short, per the bible's own "verified state" section: the
Abstractions SDK, Core EF model + convention discovery + catalog, the free-form page renderer + include
expander, the fixed CSS cascade, the raw-content trust gate, disable/enable + reference-guarded delete,
the Admin Inbox, the full `.idea` packaging pipeline (pack/inspect/list/verify/install), host-side
install + collectible-ALC runtime load, the asset-hoisting `<head>` pipeline, the `/_ideas/...` asset
route, and the first-party library (43 citizens per `library/docs/`) are all reported `✅`, each citing
its NUnit test in `docs/USER_STORIES.md`.

---

## Stack

.NET 10 · Blazor Web App (global `InteractiveServer`) · EF Core + SQL Server (temporal tables) ·
Azure Blob · `IDbContextFactory` · MindAttic.Vault · MindAttic.Legion · MindAttic.Authentication ·
MindAttic.Media.

---

## Canon & further reading

| File | Layer | What it is |
|---|---|---|
| [`docs/BIBLE.md`](docs/BIBLE.md) | L0 | Source of truth — what the project IS/is NOT, architecture, the Laws (`{#MAI-LAW-n}`). |
| [`docs/AMENDMENTS.md`](docs/AMENDMENTS.md) | L1 | Append-only change log (`MAI-A1..A26`+). **An amendment wins over the bible.** |
| [`docs/USER_STORIES.md`](docs/USER_STORIES.md) | L2 | Test-cited stories (`MAI-US-<Epic><n>`); every ✅ names its NUnit test. |
| [`docs/AUTHORING.md`](docs/AUTHORING.md) | guide | The full authoring walkthrough: Pages (admin UI) vs Plugins/Components (`.idea` packages), asset bundle rules, composing/nesting, build/pack/verify/upload. |
| [`docs/rfc/0001-unified-page-plan.md`](docs/rfc/0001-unified-page-plan.md) | rfc | The unified page-grammar plan (status: implemented). |
| [`docs/FOUNDATION_ADR.md`](docs/FOUNDATION_ADR.md) | historical | The original Legion deliberation that produced the foundation — vocabulary superseded by the amendments. |
| [`docs/FOUNDATION_AMENDMENTS.md`](docs/FOUNDATION_AMENDMENTS.md) | historical | Preserved for existing links; current truth is `BIBLE.md` + `AMENDMENTS.md`. |
| [`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md) | historical | The original brief. |
| [`docs/BIBLE.digest.md`](docs/BIBLE.digest.md) | generated | Produced by `tools/codex.ps1 digest`; injected at session start. **Never hand-edit.** |
| [`library/README.md`](library/README.md) / [`library/CLAUDE.md`](library/CLAUDE.md) | — | The first-party Theme/Plugin/Component library's own docs and Codex canon (`library/docs/`). |
| [`CLAUDE.md`](CLAUDE.md) | — | How to work in this repo (Codex rules of engagement, build/test commands). |
