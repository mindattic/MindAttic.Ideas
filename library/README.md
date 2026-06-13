# MindAttic.Ideas.Library

The **first-party library of `.idea` components** for [MindAttic.Ideas](../MindAttic.Ideas) — one home
for every Theme and Widget that ships *with* the CMS, instead of one git repo per piece.

This repo is **separate from the CMS host**. The CMS (`MindAttic.Ideas`) is standalone and never references
anything here — it only installs the packed `.idea` files as **optional** content. The only thing these
projects compile against is the frozen `MindAttic.Ideas.Abstractions` SDK in the sibling CMS repo.

## The one rule: the asset bundle is the single source of truth

Each component **owns its assets** (js/css/html) in its own `assets/` folder — copied here once, never
staged from another repo. That single bundle serves all three consumers:

| Consumer | Takes the bundle as… |
|---|---|
| **Raw `.html` pages** | links `assets/*.css` / `assets/*.js` directly (see each component's `assets/demo.html`) |
| **Standalone Blazor apps** (IdiotProof, …) | references the component RCL, or links the same `assets/` |
| **The MindAttic.Ideas CMS** | uploads the packed `.idea` (assets bundled into `wwwroot/`) |

Three packagings of one thing — not three projects. No duplication, no cross-repo coupling.

## Layout

```
Themes/      Cyberspace, Light, Dark, Spring, Summer, Autumn, Winter
Widgets/     MindAttic-specific: OutfitFont, AtticFont, SacredGeometry, Tooltip, BackHomeM,
             TableOfContents, Cyberspace (effects), LegionPersonas, Frontpage, HelloWorld, Textbox
             Baseline set (MAIL-A3): NavMenu, Breadcrumbs, Hero, Card, Accordion, Tabs, Gallery,
             Carousel, Callout, CodeBlock, VideoEmbed, ContactForm, SocialLinks, BackToTop, Footer
dist/        packed *.idea — the CMS seeds these as optional installable content
             (no Pages/ — pages are CMS DB records, MAIL-LAW-8; the parked _wip sources were
              deleted as no longer applicable, MAIL-A4)
```

Each component is its own small project (so each `.idea` is independently versioned and uploadable).
Common build settings + the Abstractions reference live once in `Directory.Build.props`.

## Build & pack a component

```pwsh
dotnet build -c Release Themes/Cyberspace
dotnet run --project ../MindAttic.Ideas/src/MindAttic.Ideas.Sdk -- pack `
  --assembly Themes/Cyberspace/bin/Release/net10.0/MindAttic.Ideas.Theme.Cyberspace.dll `
  --out ./dist `
  --refs ../MindAttic.Ideas/src/MindAttic.Ideas.Abstractions/bin/Debug/net10.0
```
