---
codex: 1
project: MindAttic.Ideas.Library
code: MAIL
layer: amendments
status: living
updated: 2026-09-04
---

# MindAttic.Ideas.Library — Amendments (append-only; amendment wins over the bible)

> Newest at the bottom. Never rewrite an amendment; supersede it with a new one. Beyond ~25, fold into
> [BIBLE.md](BIBLE.md) and start a new epoch (note the git tag); history stays in git.

## MAIL-A1 — Plugin → Widget rename (supersedes —)

**What changed.** The component kind formerly called "Plugin" is now **"Widget"** throughout: the
`Plugins/` directory is now `Widgets/`, and namespaces/types/csproj/`.idea` names moved from
`MindAttic.Ideas.Plugin.*` to `MindAttic.Ideas.Widget.*`. The composition attribute now uses
`ContentKind.Widget`.

**Why.** "Widget" describes a self-contained UI capability more accurately than "Plugin" (which implied a
host-extension contract the library never had), and aligns the catalog vocabulary across the CMS.

**Migration / status.** The source rename is complete and the full solution builds clean
([MAIL-§6](BIBLE.md#MAIL-§6)). Residual historical prose in
[`Pages/_wip/README.md`](../Pages/_wip/README.md) retains "Plugin" as a deliberate historical record
(frozen source; not updated). The bible and stories use **Widget** as the canonical term; this amendment
records the prior name for history.

## MAIL-A2 — Textbox: Control kind folded to Widget (supersedes §4.1 "1 Control") {#MAIL-A2}

**What changed.** `Textbox` was originally authored as a `Control` (inheriting `ControlBase`, living in a
`Controls/` folder) but was folded into the **Widget** kind before any release: it now lives under
`Widgets/Textbox/`, inherits `WidgetBase`, and is named `MindAttic.Ideas.Widget.Textbox`. The solution
has no `Controls/` folder. The `components.json` catalog records its kind as `Widget`. `ControlBase`
remains available in Abstractions for future use.

**Why.** Textbox is a self-contained, configurable UI element that fits the Widget contract (typed
`[Parameter]` props + pass-through `Attributes` are fully expressible on `WidgetBase`). A separate
Control tier adds complexity without benefit until a real distinction emerges (e.g. headless form
primitives that must not carry markup). MAI-A19 tracked this decision in the CMS backlog.

**Impact on §4.1.** The solution count is **7 Themes, 11 Widgets, 0 Controls** (not "7 Themes, 10 Widgets,
1 Control" as previously stated). Bible §4.1 updated accordingly. No breaking change: the `.idea`
artifact key/version and mount path are identical (`textbox`, V1, `/_ideas/Component/textbox/1`). ([MAIL-A6](AMENDMENTS.md#MAIL-A6) reclassified this as Component.)

## MAIL-A3 — The baseline widget set (supersedes §4.1 "7 Themes, 11 Widgets") {#MAIL-A3}

**What changed.** Fifteen general-purpose **baseline widgets** were added so the CMS can build and
maintain ordinary websites from reusable parts instead of bespoke markup: NavMenu, Breadcrumbs, Hero,
Card, Accordion, Tabs, Gallery, Carousel, Callout, CodeBlock, VideoEmbed, ContactForm, SocialLinks,
BackToTop, and Footer. The solution count is now **7 Themes, 26 Widgets**. All are catalogued in
[`components.json`](data/components.json) (notes prefixed "Baseline set (MAIL-A3)").

**Why.** The prior catalog was MindAttic-specific (fonts, effects, persona gallery). The product goal —
many sites from one CMS, no monolith web apps — needs a vanilla kit: navigation, banners, cards,
collapsibles, tab boards, image grids, sliders, notices, code chrome, video, contact, social icons,
scroll-to-top, and a footer. The set was sized against recreating **mindattic.com** as a Data page:
Tabs (`ma-tabs-board`) is its project board, Gallery its books grid, Footer its pin-when-short bar.

**Design rules the set introduced (now load-bearing for future widgets):**
- **Activator-first.** Where possible a widget is an asset-only *capability activator* (the Tooltip
  model): drop the token once and plain author HTML gains the behavior via `ma-*` classes /
  `data-*` attributes. Free-form pages stay free-form; layout stays plain flex in author HTML.
- **String parameters only.** Razor-widget `[Parameter]`s are strings (data-page token attributes
  arrive as strings; typed coercion is RFC 0001 roadmap in the CMS). No widget depends on
  `ChildContent` (data-page tokens never carry it).
- **Images are inline base64 CSS classes.** A widget that shows an image accepts the page-convention
  class (`imageclass=`, `.ma-gallery-tile img-*`, `.ma-slide img-*`) and never requires a file URL;
  icons/arrows are inline SVG path data or CSS glyphs. No external requests, no icon files.
- **Per-site reuse via settings.** Site-chrome widgets (NavMenu, ContactForm, SocialLinks) read
  `ISiteContext.GetSetting` fallbacks (`nav.*`, `contact.action`, `social.*`) so one theme token
  serves every site.

**Migration / status.** Additive only — no existing component changed identity. All 33 components
build clean (0/0) and pack to [`dist/`](../dist).

## MAIL-A4 — `Pages/_wip` deleted: the frozen page sources are no longer applicable (refines MAIL-LAW-8) {#MAIL-A4}

**What changed (2026-06-09).** The `Pages/` tree (frozen `Pages/_wip/LegionPersonas` and
`Pages/_wip/MindAtticFrontpage` sources, kept since the repo's founding as "source to be ported into
Page records later") was **deleted**. The rule itself stands — Pages are CMS database records, never
`.idea`s (MAIL-LAW-8) — only the parked source is gone.

**Why no longer applicable.**
- The MindAttic frontpage now exists as a **Data page** in the CMS, assembled *verbatim* from the
  live `mindattic.com/index.htm` by the CMS repo's `tools/import-frontpage.ps1` (MAI-A21). The
  authoritative source is the real site's single file, not a parked Blazor port — so the frozen
  `MindAtticFrontpage` copy can never be the porting source again.
- LegionPersonas already lives on as the compiled **`Component.LegionPersonas`** `.idea`
  ([component.legionpersonas](data/components.json)); a page that wants it drops the token.
- History is preserved in git (this deletion is one commit; the sources remain retrievable at any
  prior ref). Nothing in the solution referenced the tree (`*.csproj.wip`, never in `.slnx`).

**Doc impact.** Bible §3/§4/glossary and story MAIL-US-E1 (🗑️ cut) updated; the "port `Pages/_wip`"
backlog item is retired.

## MAIL-A5 — The mindattic.com verbatim set: TabBoard, PinFooter, WebSnapshot (supersedes §4.1 "26 Widgets") {#MAIL-A5}

**What changed (2026-06-09).** Three widgets were extracted **verbatim** from `mindattic.com/index.htm`
so the frontpage Data page composes reusable `.idea`s instead of carrying engine code inline:
- **`widget.tabboard`** — the project-board engine (`mindattic-tabs-css` + the board script: panel
  lift, stable procedural tile art, per-section localStorage persistence, single-active click
  handling) exposing `window.TabBoard.build/art/images/refresh` for boards built from page data.
- **`widget.pinfooter`** — the UiUx PINFOOTER bundle (`.pin-when-short`). The authentic
  implementation and class contract; the generic baseline `widget.footer` (`.ma-footer`) remains
  for non-mindattic sites.
- **`widget.websnapshot`** — the UiUx WEBSNAPSHOT bundle (`.web-snapshot` framed screenshot viewer,
  fetch or inline-base64 mode).

The solution count is now **7 Themes, 29 Widgets** (36 components). Adaptations are confined to:
TabBoard's empty `PROJECT_IMAGES` map became the `TabBoard.images` registry; each bundle gained an
idempotence guard + a DOM-swap re-init adapter (Blazor hosts replace the prerendered DOM). The
frontpage page record drops the corresponding inline CSS/JS and places three component tags instead —
its PageJs keeps only CONTENT (synopses, URLs, tabify converters); Theme + fonts + effects continue
to come from the installed Theme.Cyberspace / Plugin.AtticFont / Plugin.OutfitFont /
Plugin.Cyberspace `.idea`s. ([MAIL-A6](AMENDMENTS.md#MAIL-A6) reclassified `pinfooter` as Plugin, `tabboard` and `websnapshot` as Components.)

## MAIL-A6 — Widget kind split into Plugin and Component (follows MAI-A26) {#MAIL-A6}

**What changed (2026-06-16).** All Widget `.idea`s are reclassified as either **Plugin** (site-wide)
or **Component** (inline-placed), per [MAI-A26](../../docs/AMENDMENTS.md#MAI-A26).

**Classification.**

Plugins (12 — activate behavior across the whole page; selected via Admin Page Properties):

| Key | Assembly |
|---|---|
| `tooltip` | MindAttic.Ideas.Plugin.Tooltip |
| `outfitfont` | MindAttic.Ideas.Plugin.OutfitFont |
| `atticfont` | MindAttic.Ideas.Plugin.AtticFont |
| `sacredgeometry` | MindAttic.Ideas.Plugin.SacredGeometry |
| `cyberspace` | MindAttic.Ideas.Plugin.Cyberspace |
| `navmenu` | MindAttic.Ideas.Plugin.NavMenu |
| `breadcrumbs` | MindAttic.Ideas.Plugin.Breadcrumbs |
| `footer` | MindAttic.Ideas.Plugin.Footer |
| `pinfooter` | MindAttic.Ideas.Plugin.PinFooter |
| `backtotop` | MindAttic.Ideas.Plugin.BackToTop |
| `backhomem` | MindAttic.Ideas.Plugin.BackHomeM |
| `sociallinks` | MindAttic.Ideas.Plugin.SocialLinks |

Components (23 — render at a specific `<Component.X />` tag position):

| Key | Assembly |
|---|---|
| `textbox` | MindAttic.Ideas.Component.Textbox |
| `card` | MindAttic.Ideas.Component.Card |
| `accordion` | MindAttic.Ideas.Component.Accordion |
| `tabs` | MindAttic.Ideas.Component.Tabs |
| `tabboard` | MindAttic.Ideas.Component.TabBoard |
| `gallery` | MindAttic.Ideas.Component.Gallery |
| `carousel` | MindAttic.Ideas.Component.Carousel |
| `callout` | MindAttic.Ideas.Component.Callout |
| `codeblock` | MindAttic.Ideas.Component.CodeBlock |
| `videoembed` | MindAttic.Ideas.Component.VideoEmbed |
| `contactform` | MindAttic.Ideas.Component.ContactForm |
| `modalpopup` | MindAttic.Ideas.Component.ModalPopup |
| `hero` | MindAttic.Ideas.Component.Hero |
| `hardwarehero` | MindAttic.Ideas.Component.HardwareHero |
| `tableofcontents` | MindAttic.Ideas.Component.TableOfContents |
| `legionpersonas` | MindAttic.Ideas.Component.LegionPersonas |
| `ideasbrochure` | MindAttic.Ideas.Component.IdeasBrochure |
| `helloworld` | MindAttic.Ideas.Component.HelloWorld |
| `websnapshot` | MindAttic.Ideas.Component.WebSnapshot |
| `claudia` | MindAttic.Ideas.Component.Claudia |
| `chimesh` | MindAttic.Ideas.Component.ChiMesh |
| `mindatticfrontpage` | MindAttic.Ideas.Component.MindAtticFrontpage |
| `frontpage` | MindAttic.Ideas.Component.Frontpage |

**Structural changes.** `library/Widgets/` folder → `library/Plugins/` and `library/Components/`.
Every csproj renames from `MindAttic.Ideas.Widget.{Key}` to `MindAttic.Ideas.Plugin.{Key}` or
`MindAttic.Ideas.Component.{Key}`. Every `V1.cs`/`V1.razor` changes from `@inherits WidgetBase` to
`@inherits PluginBase` or `@inherits ComponentBase` (with `@using ComponentBase =
MindAttic.Ideas.Abstractions.ComponentBase` in `_Imports.razor` to alias Blazor's base). The
`components.json` catalog is updated with the new `kind` values.

**Solution count.** **8 Themes + 12 Plugins + 23 Components = 43 `.idea`s.**

> *Supersedes MAIL-A1 (Widget vocabulary) and MAIL-A2 (Textbox as Widget); the Plugin/Component
> classification is the current vocabulary.*

## MAIL-A7 — ProjectGrid and PoweredBy join the library (45 `.idea`s) {#MAIL-A7}

**What changed.** Two new first-party citizens, both built while making the MindAttic site run *on*
the CMS rather than beside it:

| Kind | Key | Assembly | What it is |
|---|---|---|---|
| Component | `projectgrid` | `MindAttic.Ideas.Component.ProjectGrid` | Card grid over the current page's child pages, enriched from the `repo` metadata slot. |
| Plugin | `poweredby` | `MindAttic.Ideas.Plugin.PoweredBy` | Site footer + the "Powered by MindAttic.Ideas" badge linking to the brochure page. |

**Why they needed host changes.** Both depend on additions to the frozen SDK made under
[MAI-A27](../../docs/AMENDMENTS.md#MAI-A27):

- `ProjectGrid` joins each child page to its metadata, which needs `ChildPage.PageId` (children
  previously carried only slug + title) and `IComponentMetadataStore.GetManyAsync` (an index over 41
  children would otherwise issue 41 round trips per render).
- `PoweredBy` is the first citizen to declare `[Idea(Slot = PluginSlot.AfterBody)]`. Every plugin
  used to render *before* the theme/body, so a footer plugin landed at the top of the page.

**Composition note.** `PoweredBy` emits a real `<footer class="ma-footer">` rather than duplicating
the `footer` plugin, which is asset-only by design ([MAIL-A5](#MAIL-A5)) and styles whatever footer
markup exists. The two compose: activate both and the badge footer gets the pin-when-short behavior.

**Solution count.** **8 Themes + 13 Plugins + 24 Components = 45 `.idea`s** *(superseded by [MAIL-A8](#MAIL-A8), which catalogues six citizens that already shipped uncatalogued: 51).*

> *Supersedes the counts in [MAIL-A6](#MAIL-A6); the Plugin/Component classification itself is unchanged.*

## MAIL-A8 — Six shipped-but-uncatalogued citizens enter the canon (51 `.idea`s) {#MAIL-A8}

**What changed.** `dist/` held 51 packed `.idea`s while
[`components.json`](data/components.json) listed 45. Six citizens had been built, packed and
installed without ever entering the L5 catalog, so canon-as-data understated what the library
actually ships:

| Kind | Key | Why it matters |
|---|---|---|
| Component | `frommd` | Renders Markdown from component metadata. Every project page in the MindAttic site is this component. |
| Component | `fromhtml` | The HTML counterpart of `frommd`. |
| Component | `ideasfrontpage` | Landing page for the CMS itself (distinct from the longer-form `ideasbrochure`). |
| Component | `mediaimage` | A managed MindAttic.Media asset as an `<img>` — the DB-backed alternative to base64-inlining. |
| Component | `medialink` | A managed media asset as a hyperlink; images/PDFs inline, everything else downloads. |
| Plugin | `header` | Three-column fixed header with server-side auth. |

**Solution count.** **8 Themes + 14 Plugins + 29 Components = 51 `.idea`s.**

**Tooling, so this stops recurring.** `dist/` drifting from source is what let the Ideas brochure go
on teaching a token grammar the CMS had already retired. `tools/pack-all.ps1` builds every citizen and
repacks any whose assembly is newer than its `.idea` (`-Force` for all, `-Install` to copy them into the
CMS host's `library/`), so a full, consistent repack is one command rather than 51 hand-written ones.

> *Supersedes the counts in [MAIL-A7](#MAIL-A7) and [MAIL-A6](#MAIL-A6).*

## MAIL-A9 — AppLaunch: a landing page can open an app borderless (52 `.idea`s) {#MAIL-A9}

**What changed (2026-09-04).** Some MindAttic projects are applications, not documents —
ExperimentRTS and Hyperspace exist to be entered, not read. Their landing pages needed to hand the
visitor a borderless, chrome-free surface, and nothing in the library did that. `Component.AppLaunch`
does.

**The constraint that shaped it.** There is no single call that yields "a separate borderless
fullscreen window". A page **cannot** put a window it opened into fullscreen: calling
`requestFullscreen()` on a popup's document from the opener is rejected with *"Permissions check
failed"* even when the two are same-origin, because the activating gesture must occur inside the
target window. Verified against Chromium rather than assumed. So AppLaunch is a ladder:

| Mode | Result | Requires |
|---|---|---|
| `fullscreen` *(default)* | Overlay iframe in this window + the Fullscreen API — zero browser chrome. | One click. No permission prompt. |
| `window` | A real separate window that goes borderless on **its own** first gesture. | The opened page must carry `applaunch.js` — i.e. be an Ideas page with an AppLaunch on it. |
| `inline` | Embedded iframe with a fullscreen affordance. | — |

Rungs below: `requestFullscreen` missing or refused leaves the overlay a `position:fixed` cover
(borderless within the tab); a blocked popup falls back to the overlay rather than doing nothing.

**Why the arming curtain, and not a click listener.** An app-host page is mostly a full-bleed
`<iframe>`, so the visitor's first click lands *inside* it — and a click in a nested browsing context
never reaches the host document. A bare `document.addEventListener('click', …)` therefore never fires
and the window stays chrome'd forever; the failure is the common case, not the edge one. `?ma-fs=1`
raises a curtain that intercepts exactly one click, then removes itself (and self-removes after 15s,
so it can never trap the page).

**Ideas hosts the apps too.** A built bundle packs as an asset-only `.idea` — a `ComponentBase` that
declares no `StylesheetUrls`/`ScriptUrls`, so a 6.6 MB game bundle is never hoisted into a landing
page's `<head>` — and serves from `/_ideas/Component/{key}/{version}/…` with correct MIME types.
Proven with ExperimentRTS: `index.html` 200, the 6.65 MB entry chunk 200 as `text/javascript`,
Babylon booted, canvas live at 1280×720 inside a fullscreen overlay, zero page errors.
Two limits of that route are worth knowing: **no default document** (a directory URL returns 400 —
link `index.html` explicitly) and **no SPA fallback** (client-side deep links 404). Neither affects a
canvas app; both would affect a router-based SPA.

**Solution count.** **8 Themes + 14 Plugins + 30 Components = 52 `.idea`s.**

> *Supersedes the count in [MAIL-A8](#MAIL-A8); the Plugin/Component classification is unchanged.*

## MAIL-A10 — ProjectBrochure: one shell, 34 projects (53 `.idea`s) {#MAIL-A10}

**What changed (2026-09-04).** Every MindAttic project needs a brochure page, and the projects have
almost nothing in common: a Three.js gallery, a Babylon.js world, an ASP.NET log view, and NuGet
libraries with no screen at all. `Component.ProjectBrochure` is the one thing they *do* share — an
identity: name, claim, status, what it is built from, where to get it, and one lead image.

**It is a shell, not a page.** It renders the masthead and the lead figure, then hands `ChildContent`
back so the author composes the rest from what that project actually needs — `<Component.FromMd />`
for a README, `<Component.AppLaunch />` for something runnable, a rendered flowchart for a library
with no UI. Trying to make one component render all 34 would have meant a parameter per project.

**`widehero` exists because evidence has two shapes.** A 16:9 screenshot is happy inside the reading
column; a flowchart is a 5:1 banner and becomes an unreadable smear there. `widehero` breaks the
figure out to viewport width while keeping the *caption* in the column, where prose belongs.

**Two bugs worth recording, both found by looking at the page rather than the exit code.**

1. **A rendered diagram displayed as alt text.** The `.svg` served `200 image/svg+xml` and the
   capture tool said `ok`, but `<img src="…svg">` parses SVG as **strict XML**, where the file was
   invalid twice over: the driver injected a second `style` attribute onto an `<svg>` that already
   had one, and mermaid's `htmlLabels` (which must be disabled at the ROOT, not just per-diagram)
   emitted `<foreignObject>` HTML containing unclosed `<br>`. Both are legal HTML and fatal XML,
   which is why every preview looked right. The driver now **parses its own output as XML before
   writing it**, in the same browser that will later render it.
2. **Mojibake in every em-dash.** `sqlcmd` reads a UTF-8 script as ANSI unless told otherwise, so
   `—` became `â€"` throughout the page copy. Write the file with a BOM, or pass `-f 65001`.

**Solution count.** **8 Themes + 14 Plugins + 31 Components = 53 `.idea`s.**

> *Supersedes the count in [MAIL-A9](#MAIL-A9).*
