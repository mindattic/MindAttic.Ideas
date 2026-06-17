# Authoring for MindAttic.Ideas

Two things make up a site, and they are **different kinds of thing**:

| | **Page** | **Plugin / Component** (Theme / Plugin / Component) |
|---|---|---|
| What it is | content | a reusable capability |
| Where it lives | a **row in the CMS database**, in the page hierarchy | a compiled **`.idea`** package |
| How you make it | admin UI: type Html / Css / Js + `{{tags}}` | author a tiny RCL, `ma-idea pack`, upload |
| How it composes | drops widgets by `{{tag}}` | nests other widgets by `{{tag}}` / `[Uses]` |

> **The one rule:** a package never references another package's assembly. Everything composes by **stable
> string id** resolved through the host catalog at runtime. The only shared compile-time dependency is the
> frozen **`MindAttic.Ideas.Abstractions`** SDK.

---

## Part A — Authoring a **Page** (no build, no `.idea`)

A page is a database record. You create and edit it entirely in the admin UI:

1. Sign in as an admin → **/admin/pages**.
2. **Add page** (or edit one). Set the slug, title, parent (for the hierarchy), and theme.
3. Fill in the three content sections — they are plain **`<textarea>`s** today:
   - **Body HTML** — your markup plus `{{…}}` widget tokens.
   - **Page CSS** — page-scoped styles (cascade tier 3).
   - **Page JS** — only emitted when the page is saved as **Author-trusted** (admin); untrusted bodies are
     sanitized (script/style/event-handlers/`javascript:` stripped — `{{tags}}` survive).
4. Save. The page is live at its slug, in the hierarchy, wearing its theme.

A page body composes widgets by token:

```html
{{ Theme.Cyberspace }}                         <!-- the page's chrome -->

<h1>Contact</h1>
{{ Plugin.Tooltip }}                           <!-- switch on a capability -->
<button data-tooltip="Resolved at runtime">Hover me</button>

{{ Component.Textbox label="Email" }}          <!-- place a component; attrs flow through -->
```

- **Grammar:** `{{ <Kind>.<Name>[.V<n>|.Latest] [attr=value …] }}`. Omit the version (or use `.Latest`) to
  float to the latest enabled version; pin with `.V<n>` so a later upload can't change a page.
- A missing/disabled reference degrades to a **clickable placeholder** (for admins, it opens the uploader
  prefilled with the missing reference) — never a crash.
- Page nesting is the hierarchy (parent/child + sort order); drag-drop reorder in **/admin/pages**.

---

## Part B — Authoring a **Plugin or Component** (a `.idea` capability)

Plugins and Components live in the **[`library/`](../../library)** directory — one home
for every first-party Theme / Plugin / Component, organized `Themes/` `Plugins/` `Components/`. The CMS ships the
packed `dist/*.idea` and seeds them on startup (optional content). Copy an existing component to start a new one.

### The shape

A component is a tiny RCL. Common props + the Abstractions reference come from the repo
`Directory.Build.props`, so the `.csproj` is ~3 lines:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <StaticWebAssetsEnabled>false</StaticWebAssetsEnabled>
  </PropertyGroup>
</Project>
```

**Identity is convention** — no attributes:

| Part | From | Example |
|---|---|---|
| Kind | which base you inherit (`ThemeBase` / `PluginBase` / `ComponentBase`) | `PluginBase` → Plugin |
| Key | the namespace tail after `MindAttic.Ideas.<Kind>.` (lowercased) | `…Plugin.Tooltip` → `tooltip` |
| Version | the `V{n}` class name | `V1` → version 1 |

Ship `V2` **alongside** `V1`; never mutate a shipped version.

### Assets: the bundle is the single source of truth

Each component **owns its assets** in a plain **`assets/`** folder (NOT `wwwroot/`, to avoid the Razor
static-web-asset collision). That same bundle serves all three consumers — no duplication:

- **raw `.html` pages** link `assets/*.css` / `*.js` directly (see e.g. `Themes/Cyberspace/demo.html`);
- **standalone Blazor apps** reference the RCL or link the same `assets/`;
- **the CMS** uploads the `.idea`, whose `wwwroot/` *is* that `assets/` folder, served at
  `/_ideas/{Kind}/{key}/{version}/…`.

A code-only Plugin points its asset URLs at that mount:

```csharp
public sealed class V1 : PluginBase
{
    private const string Mount = "/_ideas/Plugin/tooltip/1";
    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/tooltip.css" };
    public override IReadOnlyList<string> ScriptUrls     { get; } = new[] { Mount + "/tooltip.js" };
}
```

### Composing / nesting Plugins / Components

A Plugin or Component composes others two ways — pick per piece:

- **Compile-in (private):** a sub-component inside the widget's own assembly (e.g. `PersonaCard` inside
  `LegionPersonas`). Not separately deployed. Use when the piece is only ever used here.
- **Reference-by-id (separately deployed):** `<CmsInclude Ref="MindAttic.Ideas.Plugin.SacredGeometry.V1" />`
  in markup **plus** `@attribute [Uses(ContentKind.Plugin, "sacredgeometry", 1)]`. The child ships as its own
  `.idea` and rides along via `uses[]`. Use when the piece is reused or versioned independently.

`[Uses]` → the manifest `uses[]`, which drives: `<head>` asset hoisting, an install-time "missing dependency"
warning, the **delete reference-guard**, and the pre-upload **compose-graph check** (`ma-idea verify`). Nesting
is arbitrary-depth; the page drops only the **top** Plugin or Component's tag.

> Interactive widgets (typed `[Parameter]`s, `@bind`, `@onclick` — e.g. the LegionPersonas gallery) work when
> stamped on a page: `PageHost` renders the content page in one InteractiveServer circuit, so a stamped widget
> is live with **no separate app pool**. Declare it with
> `@attribute [Idea(RenderMode = CmsRenderMode.InteractiveServer)]`.

### Build, pack, verify

From the `MindAttic.Ideas.Library` repo (the CMS SDK CLI is in the sibling repo):

```pwsh
dotnet build -c Release Plugins/Tooltip
dotnet run --project ../MindAttic.Ideas/src/MindAttic.Ideas.Sdk -- pack `
  --assembly Plugins/Tooltip/bin/Release/net10.0/MindAttic.Ideas.Plugin.Tooltip.dll `
  --out ./dist --wwwroot Plugins/Tooltip/assets `
  --refs ../MindAttic.Ideas/src/MindAttic.Ideas.Abstractions/bin/Debug/net10.0

dotnet run --project ../MindAttic.Ideas/src/MindAttic.Ideas.Sdk -- inspect ./dist/MindAttic.Ideas.Plugin.Tooltip.V1.idea
dotnet run --project ../MindAttic.Ideas/src/MindAttic.Ideas.Sdk -- verify ./dist   # whole-library compose-graph
```

`inspect` should show one `bin/` dll (host assemblies excluded) and your `wwwroot/` files; `verify` should
report every declared dependency resolves.

### Upload

In the CMS admin → **/admin/upload**, drop the `.idea`. The host validates it (same gate as `ma-idea install`),
registers the type, extracts its `wwwroot/`, and it's immediately referenceable from any page by its `{{tag}}`.
Install `V2` later and pinned pages keep `V1` until nothing references it.

---

## Cheat sheet

| You want to… | Do this |
|---|---|
| Make a page | admin /admin/pages → add → fill Body HTML/CSS/JS + `{{tags}}` |
| Activate a site-wide Plugin | `{{ Plugin.<Key> }}` in the body |
| Place an inline Component | `{{ Component.<Key> attr="…" }}` |
| Pick a theme | `{{ Theme.<Key> }}` in the body |
| Nest a citizen inside a citizen | `<CmsInclude Ref="…"/>` + `[Uses(...)]` (or a private sub-component) |
| Float vs pin a version | omit `.V{n}` to float; `.V{n}` to pin |
| Ship a new version | add a `V{n+1}` class; never edit `V{n}` |
| Author a new Plugin or Component | copy a folder in `library/Plugins/` or `library/Components/`, build, `ma-idea pack`, upload |
