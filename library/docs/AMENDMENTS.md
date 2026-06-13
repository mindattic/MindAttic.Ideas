---
codex: 1
project: MindAttic.Ideas.Library
code: MAIL
layer: amendments
status: living
updated: 2026-06-07
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
artifact key/version and mount path are identical (`widget.textbox`, V1, `/_ideas/Widget/textbox/1`).

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
- LegionPersonas already lives on as the compiled **`Widget.LegionPersonas`** `.idea`
  ([widget.legionpersonas](data/components.json)); a page that wants it drops the token.
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
frontpage page record drops the corresponding inline CSS/JS and places three `{{tokens}}` instead —
its PageJs keeps only CONTENT (synopses, URLs, tabify converters); Theme + fonts + effects continue
to come from the installed Theme.Cyberspace / Widget.AtticFont / Widget.OutfitFont /
Widget.Cyberspace `.idea`s.
