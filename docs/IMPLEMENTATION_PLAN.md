# MindAttic.Ideas — Implementation Plan

A DotNetNuke-inspired, **single-deployment** Blazor CMS for the MindAttic ecosystem.
One Azure App Service, one app pool, one database. Every project frontend
(`MindAttic.Frontend`, `MindAttic.Legion.Frontend`, future ones) becomes an **Idea**
(or a Page composed of Ideas) installed into this CMS instead of a separately
deployed web app.

> **Stack:** .NET 10, Blazor Web App (global `InteractiveServer`), EF Core, Azure SQL,
> Azure Blob Storage. Reuse what already works across MindAttic: cookie auth + roles
> (from `MindAttic.Frontend`), and **`MindAttic.UiUx`** for themes/components
> (the Cyberspace look, delivered via jsDelivr). *(Note: the shared UI library is
> `MindAttic.UiUx` — not "MindAttic.Content".)*
>
> *This plan is a merge of two drafts: the multi-site/rendering/Azure detail from an
> LLM draft, plus the `.idea` package model, the **Idea** vocabulary, and the
> corrected ecosystem facts.*

---

## 0. The one mental model that drives everything

**Pages are data. Functionality is code (an *Idea*). Layout is a Theme.**

| Concept | What it is | Created by | Lives as |
|---|---|---|---|
| **Site** | A tenant/portal, resolved by hostname or path prefix | Admin | Data row |
| **Page** | A composition of Ideas placed into a theme's zones | Admin (no code) | Data rows |
| **Theme** | A layout that declares named zones + a CSS bundle | Developer (once) | RCL layout + css |
| **Idea** | A reusable content/component type (Html, Markdown, ProjectHub, a Legion dashboard…) | Developer (once) | Blazor component in an RCL |
| **IdeaInstance** | One Idea dropped into one zone on one page, with its own settings | Admin | Data row + settings JSON |

You almost never deploy to "add a page." You deploy (or upload a `.idea`) only to ship
a new **Idea type** or **Theme**. The architecture protects that line hard — it's the
whole point of the rebuild, and it's what kills the "~20 Azure apps" problem.

The brand: *MindAttic.Ideas is a CMS of Ideas. A Page is an arrangement of Ideas; the
Idea Catalog is everything you can place; an "app" is just a Page full of Ideas.*

Your "MindAtticPage base object" splits into two real artifacts:
1. **`CmsPageHost`** — the catch-all route component that resolves a slug to a Page and renders it.
2. **`Idea` / `Idea<TSettings>`** — the base every Idea inherits to get its settings, page/site context, render mode, and scoped services for free.

### Vocabulary (DNN → MindAttic.Ideas)

Portal→**Site**, Tab→**Page**, Pane→**Zone**, Module→**Idea**, Module instance→**IdeaInstance**,
Module registry→**Idea Catalog**, Skin→**Theme**, Container→**Container**, Extension package→**`.idea` package**.
Never say "module"; the unit is an **Idea**.

---

## 1. Solution layout

```
MindAttic.Ideas.sln
├─ src/
│  ├─ MindAttic.Ideas.Abstractions      // Idea SDK: Idea base, [Idea], contexts, IIdeaSource. Tiny, few deps.
│  ├─ MindAttic.Ideas.Core              // entities, EF DbContext, services, discovery, IIdeaCatalog
│  ├─ MindAttic.Ideas.Web               // Blazor host + Admin UI + catch-all routing
│  ├─ MindAttic.Ideas.Ideas.Content     // built-in Ideas: Html, Markdown, ComponentEmbed, FileList, LinkOut
│  ├─ MindAttic.Ideas.Themes.Default    // default theme RCL (layout + scoped css; pulls Cyberspace from UiUx)
│  └─ MindAttic.Ideas.Sdk               // MSBuild targets + `dotnet ma-idea pack` (builds .idea packages)
├─ modules/                             // project frontends, refactored into Idea RCLs
│  ├─ MindAttic.Ideas.Ideas.ProjectHub  // ← MindAttic.Frontend's accordion nav, lifted in
│  └─ MindAttic.Ideas.Ideas.Legion
├─ docs/
└─ tests/
```

`Abstractions` is deliberately thin so Idea authors never pull in EF or the web host —
everything an Idea needs at runtime arrives through a **context object**, not a hard
reference. `MindAttic.UiUx` is consumed by `Themes.Default` (and available to Ideas)
for the Cyberspace chrome via jsDelivr; don't reinvent shared chrome.

---

## 2. Packaging: the `.idea` file is the *universal* unit

A `.idea` package carries one of **three kinds**, discriminated by **`Category`** in its
manifest. The same upload flow, Idea Manager, loader, and `IIdeaSource` handle all three —
"everything is an Idea package" — so there is **one** install story for the whole ecosystem:

| Category | Contract | Registers into | Used as |
|---|---|---|---|
| **Idea** (Content, General, …) | `[Idea]` + `Idea`/`Idea<T>` | **Idea Catalog** | Placed into a zone as an `IdeaInstance` |
| **Component** | `[CmsComponent]` + a Blazor wrapper | **Component Registry** | Attached at **theme scope** (global) or **page scope** (via `ComponentEmbed`) |
| **Theme** | `[CmsTheme]` + `CmsThemeBase` | **Theme Registry** | Selected as a Page/Site layout (declares zones + CSS) |

**MindAttic.UiUx is the source of Component and Theme packages — not MindAttic.Ideas.**
UiUx is adding Blazor wrappers for every component and theme (the `Tooltip` and
`PageScrollbar` wrappers already exist), so UiUx packs each as a `.idea` (Category=Component
or =Theme) via the SDK packer (§6). The CMS **consumes** them through the Idea Manager —
**zero duplication** of themes/components inside the Ideas solution. The Ideas repo keeps
only a tiny built-in *bootstrap* fallback theme so the host renders before any package is
installed; real themes (Cyberspace, …) arrive as UiUx Theme packages.

### Getting a package into the running app — two loaders

Two ways, **not** mutually exclusive — both feed the same registries via `IIdeaSource`. **Not mutually exclusive** — both feed the
same Idea Catalog and the same renderer via the `IIdeaSource` abstraction, so moving
from the first to the second is **additive, not a rewrite**.

```csharp
interface IIdeaSource { IEnumerable<IdeaDescriptor> Discover(); }

CompiledIdeaSource : scans referenced RCL assemblies for [Idea]   // dev/CI, WASM-capable
PackageIdeaSource  : loads installed .idea packages at runtime    // the upload model
```

**Phase 1 — compiled RCL Ideas (do this first).** Each Idea is a Razor Class Library
referenced by `MindAttic.Ideas.Web`. At startup, discovery scans referenced assemblies
for `[Idea]` types and upserts `IdeaDefinition` rows. Adding an Idea *type* = add a
project reference + redeploy once. This already kills the "20 apps" problem (all Ideas
ship in one deployable) and adding *pages/instances* never needs a deploy.
*Pros:* full type safety; scoped CSS + static assets "just work" via `_content/{PackageId}/`;
no `AssemblyLoadContext` pain; WASM-capable. *Cons:* a new Idea *type* needs a redeploy.

**Phase 5 — `.idea` packages (the headline; harder, deferred but designed-for now).**
Upload a `.idea` zip via the Admin **Idea Manager**; load it at runtime via a collectible
`AssemblyLoadContext`. True DNN "install module" behavior, with **no redeploy**. See §6.

> Why deferred (both source drafts agree): RCL static assets (`_content/…`) are stitched
> into the host manifest **at compile time**, so a runtime-loaded Idea's CSS/JS isn't
> served automatically — you need a custom file provider (§6). Plus ALC unload/leak care.
> Ship Phases 1–4 to prove the model; light up Phase 5 additively.

---

## 3. The Idea contract (`MindAttic.Ideas.Abstractions`)

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class IdeaAttribute(string key) : Attribute
{
    public string Key { get; } = key;                 // stable id, e.g. "content.html", "projecthub"
    public string DisplayName { get; init; } = key;
    public string Category { get; init; } = "General";
    public Type? EditComponent { get; init; }         // optional settings editor component
    public Type? SettingsModel { get; init; }
    public IdeaRenderMode RenderMode { get; init; } = IdeaRenderMode.InteractiveServer;
}

public enum IdeaRenderMode { Static, InteractiveServer }   // (WASM = compile-time only; see §6)
public enum IdeaMode { View, Edit, Preview }

// Everything an Idea is handed at render time.
public sealed class IdeaContext
{
    public Guid InstanceId { get; init; }
    public string ZoneName { get; init; } = "";
    public string? RawSettingsJson { get; init; }
    public PageContext Page { get; init; } = default!;
    public SiteContext Site { get; init; } = default!;
    public IServiceProvider Services { get; init; } = default!;  // scoped DI for the circuit
    public IdeaMode Mode { get; init; }
    public T GetSettings<T>() where T : class, new()
        => RawSettingsJson is { Length: > 0 } j ? JsonSerializer.Deserialize<T>(j) ?? new() : new();
}

public abstract class Idea : ComponentBase                      // ← you inherit this
{
    [Parameter] public IdeaContext Context { get; set; } = default!;
    protected bool EditMode => Context.Mode == IdeaMode.Edit;
}

public abstract class Idea<TSettings> : Idea where TSettings : class, new()
{
    protected TSettings Settings { get; private set; } = new();
    protected override void OnParametersSet() => Settings = Context.GetSettings<TSettings>();
}
```

A minimal Idea:

```csharp
[Idea("content.html", DisplayName = "HTML", Category = "Content", EditComponent = typeof(HtmlIdeaEditor))]
public partial class HtmlIdea : Idea<HtmlSettings> { /* razor renders Settings.Html (sanitized) */ }
public sealed class HtmlSettings { public string Html { get; set; } = ""; }
```

**Discovery (`Core`):** at startup reflect over the active `IIdeaSource`(s), find `[Idea]`
types, upsert `IdeaDefinition { Key, DisplayName, Category, ComponentTypeName, AssemblyName,
EditComponentTypeName, DefaultSettings }`. Resolve `Type` from name when rendering.

### Theme & Component contracts (same SDK + `.idea` format, different shape)

Themes and Components are authored in **MindAttic.UiUx** and packed as `.idea` files
(`Category=Theme` / `Category=Component`). Discovery scans for **all three** attributes
and routes each package by category — one package format, one Idea Manager, three registries.

```csharp
[CmsTheme("cyberspace", DisplayName = "Cyberspace")]
public sealed class CyberspaceTheme : CmsThemeBase            // → Theme Registry
{
    public override string[] ZoneNames => ["Header", "Main", "Sidebar", "Footer"];
    // layout markup places <CmsZone Name="..."/>; CSS pulled from UiUx via jsDelivr
}

[CmsComponent("ui.tooltip", DisplayName = "Tooltip", Scope = ComponentScope.Global)]
public sealed class TooltipComponent : ComponentBase { }      // → Component Registry (the UiUx wrapper)
```

- **Theme** → **Theme Registry**; selectable per Site/Page (§5).
- **Component, Scope=Global** → injected at theme scope (PageScrollbar, fonts, console-bg…).
- **Component, Scope=Placeable** → offered in the page designer, dropped via the
  `ComponentEmbed` Idea, which resolves a Component by key from the Component Registry.

---

## 4. Rendering pipeline (the heart)

1. **Catch-all route.** `@page "/{*slug}"` on `CmsPageHost`. **Reserve `/admin` and `/_*` first.**
2. **Resolve Site** from `HttpContext` host header or path prefix.
3. **Resolve Page** by `(SiteId, Slug)`. 404 → CMS 404 page (itself a Page). 403 if no view perm.
4. **Load instances** for the page, grouped by `ZoneName`, ordered.
5. **Resolve Theme** (page override else site default) and its layout component.
6. Push a cascading `PageRenderContext`; render the theme layout.
7. The layout contains `<CmsZone Name="Main" />` markers; `CmsZone` pulls its instances and renders each:

```razor
@foreach (var inst in Zone.Instances)
{
    <CmsContainer Instance="inst">
        @if (inst.ResolvedType is null)
        {
            <CmsMissingIdea Instance="inst" />   @* stale type → placeholder, never a hard crash *@
        }
        else
        {
            <DynamicComponent Type="inst.ResolvedType"
                              Parameters="@(new Dictionary<string,object> { ["Context"] = inst.BuildContext(Mode) })" />
        }
    </CmsContainer>
}
```

- **`DynamicComponent` by `Type`** (not routing) is the key primitive — it's what lets a
  Type from a *dynamically loaded* assembly (§6) render with **no router changes**.
- **`CmsContainer`** is optional DNN-style chrome (title bar, edit affordances in `Edit` mode).
- The **same render path serves View and Edit** (toggle chrome by `IdeaMode`).
- **Security:** any Idea emitting raw HTML (`MarkupString`) must run through an HTML
  sanitizer (e.g. `Ganss.Xss`). Treat Idea HTML as untrusted by default (admin ≠ developer).

---

## 5. Theming & components

A **Theme** = a layout component that declares zones + a scoped CSS bundle.

```razor
@* DefaultTheme.razor *@
<header class="site-header"><CmsZone Name="Header" /></header>
<main class="site-main"><CmsZone Name="Main" /></main>
<aside><CmsZone Name="Sidebar" /></aside>
<footer><CmsZone Name="Footer" /></footer>
```

- **Assignment cascades:** Host default → Site default → Page override.
- **Theme declares `ZoneNames`.** `IdeaInstance.ZoneName` is validated against them; instances
  in a zone the active theme lacks render into a **fallback zone with an admin warning**
  (keeps pages portable across themes).
- **CSS:** theme RCL static assets via `_content/MindAttic.Ideas.Themes.Default/`; the
  Cyberspace look pulls from **MindAttic.UiUx** via jsDelivr (existing pattern). Containers
  are themeable too (an instance can pick a container style), matching DNN.

**Components vs Ideas (your "add components to pages or themes"):** a UiUx *component*
(Tooltip, PageScrollbar, SacredGeometry, console-bg) attaches at **theme scope** (global,
bundled by the theme) **or** at **page scope** via the built-in **`ComponentEmbed`** Idea
(renders a registered UiUx component by key). That single distinction satisfies "add
components to pages, or to themes."

---

## 6. `.idea` packages + the Idea Manager (Phase 5 headline)

### The `.idea` file (a zip)
```
ProjectHub.idea
├─ idea.json                 # manifest (generated from [Idea] via reflection by the SDK)
├─ lib/  <assembly + only deps NOT already in the host>   ← NEVER ship Abstractions.dll
├─ wwwroot/  <the Idea's css/js/assets>
├─ data/     <optional EF migration / seed SQL for the Idea's own tables>
└─ icon.png  screenshots/  LICENSE
```
```jsonc
// idea.json
{ "key": "projecthub", "displayName": "Project Hub", "category": "Idea", "version": 1,
  "sdk": 1, "entryType": "MindAttic.Ideas.Ideas.ProjectHub.ProjectHub",
  "renderMode": "InteractiveServer", "assets": ["projecthub.css"],
  "settingsSchema": { /* json schema → admin auto-form */ }, "dependencies": [] }
```

> `category` is the discriminator (`Idea` | `Component` | `Theme`). Theme manifests also
> carry `zoneNames` + `cssBundle`; Component manifests carry `scope` (`Global`|`Placeable`).
> The same loader installs all three; the Idea Manager lists them filterable by category.

### `PackageIdeaSource` load sequence (per package)
1. Pull `.idea` blob from **Azure Blob** → extract to local temp. *(App Service disk is
   ephemeral; Blob is the source of truth, so every instance/scale-out unit loads the same set.)*
2. Load `lib/` into a **collectible `AssemblyLoadContext`** + `AssemblyDependencyResolver`.
   **CRITICAL:** defer shared contracts (`MindAttic.Ideas.Abstractions`, ASP.NET Core) to
   the host's default context so the package's `Idea` base **unifies** with the host's —
   otherwise casts fail. This one detail makes or breaks plugin loading.
3. Resolve `entryType` → add to the Idea Catalog. Rendering uses the existing `DynamicComponent` path.
4. Serve `wwwroot/` via a CMS route `/_ideas/{key}/{version}/…` backed by a `PhysicalFileProvider`
   (uploaded assets can't use the build-time `_content/` manifest).

### Idea Manager (`/admin`)
Upload `.idea` (validate manifest + SDK gate + checksum → store blob → register `InstalledIdea`
→ load into catalog), list installed, enable/disable, **upgrade** (newer version → fresh ALC),
uninstall (block if still placed; offer force), inspect manifest/permissions/settings schema.
`InstalledIdea(Key, Version, BlobPath, ManifestJson, Enabled, InstalledUtc, Sha256)` is the registry.

### Build tooling
`MindAttic.Ideas.Sdk` (MSBuild + `dotnet ma-idea pack`) turns an Idea RCL into `<Key>.idea`:
collect assembly, **exclude host-provided deps**, bundle `wwwroot`, emit `idea.json` from `[Idea]`.

### Honest constraints (accept up front)
1. **Runtime Ideas are Static or InteractiveServer only.** Interactive **WebAssembly** Ideas
   must be compile-time referenced (the assembly must reach the browser's WASM runtime).
2. **`.idea` upload = arbitrary in-process code at full host trust** — .NET has no in-proc
   sandbox. Admin-only + checksum/signature; it's *your* trusted tooling, not a public marketplace.
3. **Live unload/upgrade is best-effort** (collectible ALC unloads only when refs drain);
   install-then-recycle is the reliable path. Design for "load on boot."
4. **Blob-backed, not disk-backed.**

---

## 7. Data model (EF Core, Azure SQL; settings as JSON columns)

```
Site            (Id, Key, Name, HostBindings[], DefaultThemeKey, SettingsJson)
Page            (Id, SiteId, ParentId?, Slug, Title, ThemeOverrideKey?, IsPublished,
                 SortOrder, SeoMetaJson, CreatedUtc, ModifiedUtc)         ← tree via ParentId → nav menu
Theme           (Id, Key, Name, LayoutComponentTypeName, ZoneNames[])
IdeaDefinition  (Id, Key, DisplayName, Category, Source, ComponentTypeName, AssemblyName,
                 EditComponentTypeName?, DefaultSettingsJson)
IdeaInstance    (Id, PageId, ZoneName, SortOrder, IdeaDefinitionId, Title?, ContainerKey?,
                 SettingsJson, Visibility, CreatedUtc)
InstalledIdea   (Key, Version, BlobPath, ManifestJson, Enabled, InstalledUtc, Sha256)
Asset           (Id, SiteId, Folder, FileName, BlobUri, ContentType, SizeBytes, Sha256, CreatedUtc)
SettingEntry    (Id, Scope, ScopeId, Key, Value)             // Scope: Host|Site|Page|Idea  (override chain)
User/Role/PagePermission/IdeaPermission                       // lift Frontend's cookie + SecurityStamp + roles
```
- **Settings as JSON columns** keeps Ideas schema-free; index into JSON where you query it.
- **Zones aren't stored per page** — they come from the theme; `IdeaInstance.ZoneName` is
  validated against the theme's `ZoneNames` (fallback for unknown zones, §5).
- `MindAttic.Frontend` already has ~80% of Page/Media(→Asset)/User-Role/Setting + idempotent
  seeding + admin scaffolding — **port it, don't reinvent it.**

---

## 8. Multi-site / multi-tenant

Each **domain** (mindattic.com, mindatticcares.com, ryandebraal.com, or `legion.mindattic.io`)
is a **Site**, resolved by host header or path prefix — one app, many portals, like DNN.
Shared Ideas/Themes are available to every Site; content/config isolate by `SiteId`. Launch a
new project frontend by creating a Site + Pages composed of Ideas — **no deployment**.

> Mapping note: per your intent, a *project frontend* is normally a **Page (or page subtree)
> of Ideas**, not necessarily its own Site. Sites are for genuinely separate domains/tenants.

---

## 9. Admin UI (`/admin`, InteractiveServer)

Page tree editor (create/reorder/nest/publish) · **Page designer** (pick theme, see zones,
add Idea from the catalog into a zone, reorder, open each instance's `EditComponent`) ·
Idea Catalog (auto-populated from discovery) · **Idea Manager** (`.idea` install/upgrade, §6) ·
File manager (Blob: upload/folder/copy-URL) · Theme manager + assignment · Roles & permission grants.
Build the designer on the **same `DynamicComponent` path** in `Mode = Edit` (chrome toggled by mode).

---

## 10. Azure / deployment

- **One** App Service (Linux, .NET 10) = one app pool. **One** Azure SQL (EF migrations on deploy).
  **Azure Blob** for assets and `.idea` packages.
- Blazor Server circuits are **stateful**: enable **ARR affinity**; if you scale past one
  instance, add **Azure SignalR Service** as the backplane.
- Watch **circuit memory** (Server holds per-user state); add health checks. CI/CD: GitHub
  Actions → build → migrate → deploy slot → swap (mirror `MindAttic.Frontend/azure-deploy.yml`).

---

## 11. Build order

- **P0 Foundation** — `Abstractions` (Idea SDK) + `Core` (entities, `CmsDbContext`, discovery /
  `CompiledIdeaSource`, `IIdeaCatalog`) + initial migration + idempotent seed.
- **P1 Render pipeline** — `CmsPageHost` + `CmsZone` + `CmsContainer` + `DynamicComponent` loop +
  `Themes.Default` (Cyberspace) + built-in Ideas (Html, Markdown, ComponentEmbed). Reserve `/admin`,`/_*`.
  **Goal:** a seeded Page renders an Idea end-to-end.
- **P2 Admin** — page tree CRUD, Idea placement, settings editors, Idea Catalog, file manager.
- **P3 Theming** — theme engine + containers + asset manager + settings override chain.
- **P4 Security + multi-site** — roles/permission grid + Site resolution by host/path.
- **P5 `.idea` packages (headline)** — `PackageIdeaSource` (collectible ALC) + Idea Manager +
  `MindAttic.Ideas.Sdk` packer + custom static-asset serving.
- **P6 Migrate** — `MindAttic.Frontend` → `ProjectHub` + `Markdown` Ideas (do the smaller frontend
  first to validate the contract), then `Legion`. Heavy standalone apps (Unity GridGame2026, MAUI
  ThinkTank, StreetSamurai, Cursory) stay external, surfaced as Pages with a `LinkOut` Idea.

---

## 12. Known risks / gotchas

- **Dynamic asset loading** (P5) is the real hazard — RCL static assets are compile-time stitched;
  runtime-loaded Ideas need a custom file provider. Deferred for this reason.
- **XSS** from Html/Markdown Ideas → sanitize all `MarkupString`.
- **ALC contract unification** (§6.2) → defer shared assemblies to the host or casts fail.
- **Circuit memory** with Blazor Server at scale → acceptable now; revisit on heavy concurrency.
- **Stale type resolution** from stored `ComponentTypeName` → degrade to a placeholder, not a crash.
- **Theme/zone drift** → render unknown-zone instances into a fallback zone with an admin warning.

---

## 13. Claude Code kickoff prompt (Phase 0 → 1)

Open a Claude Code window rooted in this repo and paste:

```
Read docs/IMPLEMENTATION_PLAN.md. Before writing files, list the solution + any .csproj,
and confirm conventions against the sibling repos D:/Projects/MindAttic/MindAttic.Frontend
(EF/seed/auth patterns to port) and D:/Projects/MindAttic/MindAttic.UiUx (Cyberspace theme,
jsDelivr). Report what exists before generating.

Then implement Phase 0 + Phase 1 using the Idea vocabulary (never "module"):

1. src/MindAttic.Ideas.Abstractions (net10.0): IdeaAttribute, IdeaRenderMode, IdeaMode,
   IdeaContext, PageContext, SiteContext, Idea, Idea<TSettings>, IIdeaSource, IIdeaCatalog,
   IdeaDescriptor. No EF/web deps.
2. src/MindAttic.Ideas.Core (net10.0): entities (Site, Page, Theme, IdeaDefinition,
   IdeaInstance, Asset, SettingEntry, Role, PagePermission, IdeaPermission), CmsDbContext
   (SQL Server), CompiledIdeaSource (reflect referenced assemblies for [Idea]) + IdeaCatalog,
   initial migration, idempotent SeedService (port Frontend's pattern).
3. src/MindAttic.Ideas.Ideas.Content (RCL): HtmlIdea, MarkdownIdea, ComponentEmbedIdea, each
   with strongly-typed settings + an EditComponent. Sanitize all raw HTML output.
4. src/MindAttic.Ideas.Themes.Default (RCL): DefaultTheme.razor with CmsZone markers
   (Header, Main, Sidebar, Footer) + scoped CSS; load Cyberspace from MindAttic.UiUx via jsDelivr.
5. src/MindAttic.Ideas.Web (Blazor Web App, InteractiveServer global): wire DI, run discovery on
   startup, implement CmsPageHost catch-all (reserve /admin and /_*), CmsZone, CmsContainer,
   CmsMissingIdea, and the DynamicComponent render loop. Seed one Site + one Page placing HtmlIdea.

Keep Abstractions dependency-light. Build and report errors after each project. Do not scaffold
the Admin UI yet (Phase 2). Structure so PackageIdeaSource + the Idea Manager (Phase 5) drop in
later as another IIdeaSource feeding the same catalog — no rework.
```
