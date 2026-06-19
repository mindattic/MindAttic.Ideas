# MindAttic.Ideas.Library

The **first-party library of `.idea` citizens** for [MindAttic.Ideas](../README.md) — one repo for every
Theme, Plugin, and Component that ships with the CMS.

The CMS never references this library at compile time. It only installs the packed `.idea` files as
optional content. Each project here compiles against the frozen `MindAttic.Ideas.Abstractions` SDK only.

## The one rule: the asset bundle is the single source of truth

Each citizen **owns its assets** in its own `assets/` folder. That bundle serves all three consumers:

| Consumer | Uses the bundle as… |
|---|---|
| **Raw `.html` pages** | links `assets/*.css` / `assets/*.js` directly (see each `assets/demo.html`) |
| **Standalone Blazor apps** | references the component RCL, or links the same `assets/` |
| **The MindAttic.Ideas CMS** | uploads the packed `.idea` (assets bundled into `wwwroot/`) |

Three packagings of one thing — not three projects.

## Layout

```
library/
  Themes/      Cyberspace, Light, Dark, Spring, Summer, Autumn, Winter, Hardware  (8)
  Plugins/     Tooltip, OutfitFont, AtticFont, SacredGeometry, Cyberspace,
               NavMenu, Breadcrumbs, Footer, PinFooter, BackToTop, BackHomeM,
               SocialLinks  (12)
  Components/  HelloWorld, Textbox, Card, Accordion, Tabs, TabBoard, Gallery,
               Carousel, Callout, CodeBlock, VideoEmbed, ContactForm, ModalPopup,
               Hero, HardwareHero, TableOfContents, LegionPersonas, IdeasBrochure,
               WebSnapshot, Claudia, ChiMesh, MindAtticFrontpage, Frontpage  (23)
  dist/        packed *.idea — seeded into the CMS on startup as optional content
```

**43 `.idea`s total** (MAIL-A6). Each project is its own small csproj so each `.idea` is independently
versioned and uploadable. Common build settings + the Abstractions reference live once in
`Directory.Build.props`.

## Build & pack

Build one citizen:

```pwsh
dotnet build -c Release Plugins/Tooltip
```

Build everything:

```pwsh
dotnet build -c Release MindAttic.Ideas.Library.slnx
```

Pack a citizen to `dist/` (SDK CLI is in the sibling CMS repo):

```pwsh
dotnet run --project ../MindAttic.Ideas/src/MindAttic.Ideas.Sdk -- pack `
  --assembly Plugins/Tooltip/bin/Release/net10.0/MindAttic.Ideas.Plugin.Tooltip.dll `
  --out ./dist `
  --wwwroot Plugins/Tooltip/assets `
  --refs ../MindAttic.Ideas/src/MindAttic.Ideas.Abstractions/bin/Debug/net10.0
```

Then inspect or verify:

```pwsh
dotnet run --project ../MindAttic.Ideas/src/MindAttic.Ideas.Sdk -- inspect ./dist/MindAttic.Ideas.Plugin.Tooltip.V1.idea
dotnet run --project ../MindAttic.Ideas/src/MindAttic.Ideas.Sdk -- verify ./dist
```

See [`docs/AUTHORING.md`](../docs/AUTHORING.md) for the full authoring guide (adding a new citizen, composing,
uploading).

## Codex docs

- [`docs/BIBLE.md`](docs/BIBLE.md) — L0 source of truth (architecture, laws)
- [`docs/AMENDMENTS.md`](docs/AMENDMENTS.md) — L1 append-only change log (amendment wins over the bible)
- [`docs/USER_STORIES.md`](docs/USER_STORIES.md) — L2 test-cited stories
