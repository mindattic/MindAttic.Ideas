# Authoring a `.idea` — build a modular page in 5 minutes

This is the definitive how-to for shipping capability to MindAttic.Ideas as a `.idea` package: author a
compiled `.razor`, build it in Release, pack it, and drop it into the CMS — where it picks up a **Theme**
and **Plugins/Controls that live in other packages, referenced by a string id**, never bundled and
never compile-time referenced.

The worked example is [`samples/MindAttic.Ideas.Page.HelloWorld`](../samples/MindAttic.Ideas.Page.HelloWorld) —
copy it to start a new page.

> **The one idea (Orchard Core's model, which we follow):** a package never references another package's
> assembly. It references **stable string ids** resolved through the host's global catalog at runtime. The
> only thing every package compiles against is the frozen **`MindAttic.Ideas.Abstractions`** SDK. Composition
> is **declarative**.

---

## 1. The project (a Razor Class Library)

A content package is an RCL whose **only** reference is the Abstractions SDK:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Version>1.0.0</Version>            <!-- whole-number versioning; the .idea content version is the V{n} class -->
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <!-- The ONLY reference. Private=false + ExcludeAssets=runtime keep Abstractions (and the framework
         Components it carries) OUT of bin/ — the validator FORBIDS host assemblies in bin/. -->
    <ProjectReference Include="..\..\src\MindAttic.Ideas.Abstractions\MindAttic.Ideas.Abstractions.csproj"
                      Private="false" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

`_Imports.razor`:

```razor
@using Microsoft.AspNetCore.Components
@using MindAttic.Ideas.Abstractions
```

`AssemblyInfo.cs`:

```csharp
using MindAttic.Ideas.Abstractions;
[assembly: IdeaSdkVersion(1)]   // the SDK version the host gates code loads against
```

---

## 2. Identity is convention — no attributes needed

| Part | Where it comes from | Example |
|---|---|---|
| **Kind** | which base you inherit (`PageBase` / `ThemeBase` / `PluginBase` / `ControlBase`) | `PageBase` → Page |
| **Key** | the namespace tail after `MindAttic.Ideas.<Kind>.` (lowercased) | `…Page.HelloWorld` → `helloworld` |
| **Version** | the `V{n}` class name | `V1` → version 1 |

So a page lives in `V1.razor` with `@namespace MindAttic.Ideas.Page.HelloWorld` and `@inherits PageBase`.
Ship `V2` **alongside** `V1` — never mutate a shipped version.

---

## 3. Compose other packages BY STRING ID

There are exactly two reference surfaces, and neither is a compile-time reference:

### Theme — by `ThemeKey` (page metadata)
A page does not place its theme; it **names** it, and the host wraps the page body in it. You declare the
theme in `data/page.json` (below). At render, `ThemeKey` → the global catalog → the resolved `ThemeBase`.

### Plugins / Controls — by `<CmsInclude>` (the SDK primitive)
`<CmsInclude Ref="…" />` is the MindAttic analog of Orchard's `@Display`/`<zone>`. It resolves the string
id through the global catalog at runtime and renders the result — the *same* resolution a data page's
`<MindAttic.Ideas.…/>` include tag uses:

```razor
@namespace MindAttic.Ideas.Page.HelloWorld
@inherits PageBase
@attribute [Uses(ContentKind.Plugin, "tooltip", 1)]
@attribute [Uses(ContentKind.Control, "textbox", 1)]

<h1>Hello, world.</h1>

<CmsInclude Ref="MindAttic.Ideas.Plugin.Tooltip.V1" />            @* switch on a capability *@
<button data-tooltip="Resolved by string id at runtime.">Hover me</button>

<CmsInclude Ref="MindAttic.Ideas.Control.Textbox.V1" placeholder="Type here…" />  @* place a control; attrs flow through *@
```

- Omit the `.V{n}` (or use `.Latest`) to **float** to the latest enabled version.
- A missing/disabled reference **degrades to a placeholder** + an Admin Inbox alert — never a crash.

### `[Uses(...)]` — declare what you compose
A compiled page has no HTML body for the host to scan, so it **declares** the citizens it composes with
`[Uses(kind, key, version)]` (repeatable; version `0`/omitted = float). The packer turns these into the
manifest's `uses[]`, which the host uses to:
1. **hoist** the referenced citizens' css/js into `<head>` (correct cascade tier), and
2. **warn** at install if a declared dependency isn't installed yet, and
3. **reference-guard** them — you can't delete a Plugin/Theme a compiled page still uses.

> Keep `[Uses]` in sync with your `<CmsInclude>` calls — it's the manifest of what your page depends on.

---

## 4. Make it routable on install — `data/page.json`

A Page package carries an optional seed so it's **live the moment it's uploaded** (idempotent upsert by
`(SiteId, Slug)`; never clobbers a slug another package or an admin owns):

```json
{
  "slug": "hello-world",
  "title": "Hello World",
  "themeKey": "cyberspace",
  "themeVersion": 1,
  "published": true
}
```

(`siteKey` is optional — omit for the default site.)

---

## 5. Build, pack, validate

From the project folder:

```bash
# 1. build Release — bin/ ends up with exactly ONE dll (host assemblies are excluded)
dotnet build -c Release

# 2. pack into a .idea  (--refs points at the Abstractions build output so the reflection-only
#    packer can resolve the SDK; --data ships the page.json seed)
dotnet run --project ../../src/MindAttic.Ideas.Sdk -- pack \
  --assembly bin/Release/net10.0/MindAttic.Ideas.Page.HelloWorld.dll \
  --out ./dist \
  --data ./data \
  --refs ../../src/MindAttic.Ideas.Abstractions/bin/Debug/net10.0

# 3. inspect — confirm identity, uses[], one bin dll, the seed
dotnet run --project ../../src/MindAttic.Ideas.Sdk -- inspect ./dist/MindAttic.Ideas.Page.HelloWorld.V1.idea
#   key          helloworld
#   category     Page
#   kind         code
#   uses         Plugin.tooltip@1, Control.textbox@1
#   bin/         1 file(s)
#   data/        1 file(s)

# 4. validate exactly as the host will (offline)
dotnet run --project ../../src/MindAttic.Ideas.Sdk -- install ./dist/MindAttic.Ideas.Page.HelloWorld.V1.idea
```

(Once `ma-idea` is installed as a global tool, the commands are just `ma-idea pack …`, `ma-idea inspect …`.)

---

## 6. Preview before you upload — the Test Harness

The harness renders your in-development page through the **real** host pipeline (PageHost → theme wrap →
CmsInclude → CmsHead → PageAssetCollector), resolving its theme + plugins/controls **by string id from a local
folder of dependency `.idea` files** — no database, no Vault/auth. What you see in preview is what
production renders; only the data source differs (an in-memory catalog + page row), never the renderer.

**Status:** the two prerequisites are in place. The resolver core,
[`LocalFolderPackageSource`](../src/MindAttic.Ideas.Core/Discovery/LocalFolderPackageSource.cs), is
implemented and tested — it reads a `./deps` folder of `.idea` files, validates each exactly as the host's
installer does, extracts them through the same `IPackageExtractor`, and emits production-identical
`Origin=Package` descriptors. And the render pipeline (`PageHost`/`CmsHead`) now lives in its own Razor
Class Library, [`MindAttic.Ideas.Rendering`](../src/MindAttic.Ideas.Rendering/) — so a second Blazor host
can render through the *exact same* components without the static-web-asset collision that blocks composing
two Blazor *web apps* by project reference. The one remaining step is the dev-host shell itself
(`ma-idea preview --project … --deps … --slug … --watch`) that references the RCL, wires the
`LocalFolderPackageSource` catalog, and serves a single slug.

Until then, the fastest faithful check is to **pack and install into a local dev CMS** (Section 7) — the
same install path, validated by `ma-idea install` first.

---

## 7. Upload

Upload the `.idea` in the CMS admin. The host validates it (same gate as `ma-idea install`), registers the
type, applies the `data/page.json` seed, and the page is live at its slug — wearing its theme, with its
plugins and controls resolved from their **separately installed** packages. Install `V2` later and the same page row
re-points to it; the old version coexists until nothing references it.

---

## Cheat sheet

| You want to… | Do this |
|---|---|
| Add a capability (tooltip, etc.) to a page | `<CmsInclude Ref="MindAttic.Ideas.Plugin.<Key>.V<n>" />` + `[Uses(ContentKind.Plugin, "<key>", n)]` |
| Place a UI control | `<CmsInclude Ref="MindAttic.Ideas.Control.<Key>.V<n>" attr="…" />` + `[Uses(ContentKind.Control, …)]` |
| Pick a theme | `themeKey` in `data/page.json` (don't place it with a tag) |
| Float to latest | omit the `.V{n}` in `Ref`, and use version `0` in `[Uses]` |
| Ship a new version | add a new `V{n+1}` class; never edit `V{n}` |
| Make the page routable | include `data/page.json` and pack with `--data ./data` |
