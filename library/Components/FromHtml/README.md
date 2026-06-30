# FromHtml

Embeds a local HTML file inline on a page — styles, scripts, and all — with no iframe and no theme wrapper.

## Usage

**Token syntax**

```
{{Component.FromHtml slot="main" showReturnLink="false"}}
```

**HTML tag syntax**

```html
<FromHtml Slot="main" ShowReturnLink="false" />
```

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Slot` | `string` | `"main"` | Instance discriminator. Use `"main"` when there is only one `FromHtml` per page; set a unique name when multiple instances coexist on the same page. Determines the metadata key used to store per-instance settings. |
| `ShowReturnLink` | `bool` | `false` | When `true`, renders a fixed return-link bar beneath the injected content so visitors can navigate back to the referring Ideas page. |
| `Class` | `string?` | — | CSS class(es) applied to the outer wrapper `<div>`. |
| `Style` | `string?` | — | Inline style string appended to the outer wrapper after any computed padding/margin/align values. |
| `Padding` | `string?` | — | Sets `padding` on the outer wrapper (e.g. `"2rem"`). |
| `Margin` | `string?` | — | Sets `margin` on the outer wrapper (e.g. `"0 auto"`). |
| `Align` | `string?` | — | Sets `text-align` on the outer wrapper (e.g. `"center"`). |

## Examples

**Basic usage — single document per page**

```
{{Component.FromHtml}}
```

Opens in edit mode with an empty state message. In edit mode, enter the full path to a local `.htm`/`.html` file and click "Update from Source" to snapshot the content.

**Multiple documents on the same page**

```
{{Component.FromHtml slot="intro"}}
{{Component.FromHtml slot="appendix"}}
```

Each instance stores its own source path and HTML snapshot independently.

**Centered document with a return link and padding**

```
{{Component.FromHtml slot="main" showReturnLink="true" padding="2rem" align="center"}}
```

Adds 2 rem of padding around the injected content, centers text, and shows a back-navigation link at the bottom.

## Notes

- **Inline injection, not an iframe.** The source file's `<style>` and `<script>` tags are emitted directly into the page DOM. The file's own CSS therefore runs at page scope and takes precedence over the host theme.
- **Snapshot model.** Content is read from the local filesystem at the time the author clicks "Update from Source" and stored as a metadata snapshot. The displayed HTML does not update automatically when the source file changes on disk; a manual re-sync is required.
- **Local filesystem access.** The source path is resolved on the server. The file must be accessible to the server process at the path entered in edit mode. Relative paths are not supported; use absolute paths (e.g. `D:/Projects/MyApp/index.htm`).
- **Edit mode only.** The source file path UI and the "Update from Source" button are only visible when the page is in edit mode. Visitors see only the stored HTML snapshot (or nothing if no snapshot has been saved yet).
- **`Slot` uniqueness.** If two `FromHtml` instances on the same page share the same `Slot` value they will read and write the same stored snapshot. Assign distinct slot names when composing more than one instance per page.
- **Requires plugin: `header` (v1).** The component declares `[Uses(ContentKind.Plugin, "header", 1)]`. The `header` plugin must be active on the page for the component to function correctly.
