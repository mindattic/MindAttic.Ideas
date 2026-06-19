# MindAttic.Ideas project templates

Scaffold a new modular `.idea` citizen with `dotnet new`.

## Available templates

| Short name | What it scaffolds |
|---|---|
| `maidea-page` | A standalone Razor Class Library that ships as a `.idea` Page |

## Usage

```pwsh
# Install the template (once, from the repo root)
dotnet new install ./templates/maidea-page

# Scaffold a new Page (run from samples/ so the relative Abstractions path resolves)
cd samples
dotnet new maidea-page -n MyPage --slug my-page --theme cyberspace
# -> samples/MyPage/  with MindAttic.Ideas.Page.MyPage.csproj, namespace MindAttic.Ideas.Page.MyPage, class V1
```

## Template parameters

| Parameter | Default | Description |
|---|---|---|
| `-n` / `--name` | _(required)_ | Short name — becomes `MindAttic.Ideas.Page.<Name>` |
| `--slug` | `hello-world` | Route the page is served at after install |
| `--theme` | `cyberspace` | Theme key this page wears (referenced by string, not bundled) |

## What gets generated

The generated project mirrors [`samples/MindAttic.Ideas.Page.HelloWorld`](../samples/MindAttic.Ideas.Page.HelloWorld):
a Razor Class Library that:

- compiles against **only** `MindAttic.Ideas.Abstractions` (Private=false, ExcludeAssets=runtime so host assemblies stay out of `bin/`)
- references its theme and citizens by **string id** at runtime (never by assembly reference)
- includes a `data/page.json` seed file for the initial Page row

After scaffolding, follow [`docs/AUTHORING.md`](../docs/AUTHORING.md) to build, pack (`ma-idea pack`), and upload.

## Adding Widget / Theme templates

Plugin, Component, and Theme templates follow the same shape as `maidea-page`:
change the base type (`PluginBase` / `ComponentBase` / `ThemeBase`), the namespace `Kind` segment,
and drop `data/page.json` for non-Page kinds.
