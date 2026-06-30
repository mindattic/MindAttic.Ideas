# Markdown Doc

Renders a Markdown file from the local filesystem as HTML inside a page. In edit mode the component exposes a file-path input and an "Update from Source" button that reads the file, converts it with [Markdig](https://github.com/xoofx/markdig) (advanced extensions enabled), and persists the rendered output to the page's metadata store.

---

## Usage

**Token syntax** (inside a page's body):

```
{{Component.FromMd slot="main"}}
```

**Razor / HTML tag syntax**:

```razor
<FromMd Slot="main" Padding="2rem" />
```

---

## Parameters

| Name | Type | Default | Description |
|------|------|---------|-------------|
| `Slot` | `string` | `"main"` | Instance discriminator. Use `"main"` when only one `FromMd` appears on the page; use a unique name (e.g. `"api-docs"`) when multiple instances coexist on the same page. |
| `Class` | `string?` | `null` | Additional CSS class(es) appended to the root `<div class="ma-frommd">` element. |
| `Style` | `string?` | `null` | Inline style string appended verbatim to the root element's `style` attribute. |
| `Padding` | `string?` | `null` | Sets `padding` on the root element (accepts any valid CSS value, e.g. `"2rem"` or `"16px 24px"`). |
| `Margin` | `string?` | `null` | Sets `margin` on the root element (accepts any valid CSS value). |
| `Align` | `string?` | `null` | Sets `text-align` on the root element (e.g. `"left"`, `"center"`, `"justify"`). |

`Padding`, `Margin`, `Align`, and `Style` are combined into a single `style` attribute in that order; `Style` is always appended last so it can override the others.

---

## Examples

**Basic — single doc on a page:**

```
{{Component.FromMd}}
```

No `slot` needed when only one instance exists; `"main"` is used automatically.

**Two docs on the same page, side-by-side:**

```
{{Component.FromMd slot="intro" style="width:50%;display:inline-block"}}
{{Component.FromMd slot="api-ref" style="width:50%;display:inline-block"}}
```

**Centered doc with breathing room:**

```
{{Component.FromMd slot="main" padding="2rem 3rem" margin="0 auto" align="left"}}
```

---

## Notes

- **Content is persisted, not live.** The Markdown source file is read once when you click "Update from Source" in edit mode. The rendered HTML is stored in the page's metadata store. Edits to the source file on disk do not automatically propagate — you must click "Update from Source" again.
- **Source file path is server-local.** The path entered in edit mode must be accessible from the server process (e.g. `D:/Projects/MyDocs/README.md`). It is not exposed to site visitors.
- **Empty state.** If no content has been loaded yet, the component renders a placeholder message inside `<div class="ma-frommd ma-frommd-empty">`. This is only visible until the first "Update from Source" is performed.
- **Markdig advanced extensions** are enabled, which includes tables, task lists, auto-links, footnotes, definition lists, and more.
- **Stylesheet.** The component self-loads `/_ideas/Component/frommd/1/frommd.css` for prose styling. Add the `Class` parameter to attach additional classes for local overrides.
- **Multiple slots.** Each unique `Slot` value is stored independently. Reusing the same slot name on two different component instances on the same page will cause them to share content.
