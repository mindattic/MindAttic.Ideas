# Nav Menu

A responsive site navigation bar with a brand link and configurable menu entries; collapses to a hamburger toggle below 720 px.

## Usage

**Token syntax**

```
{{Plugin.NavMenu}}
{{Plugin.NavMenu links="Home=/;About=/about;Blog=/blog"}}
{{Plugin.NavMenu brand="Acme" brandhref="/" links="Products=/products;Contact=/contact"}}
```

**HTML tag syntax**

```html
<NavMenu />
<NavMenu Links="Home=/;About=/about;Blog=/blog" />
<NavMenu Brand="Acme" BrandHref="/" Links="Products=/products;Contact=/contact" />
```

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Links` | `string?` | site setting `nav.links` | Semicolon-separated menu entries in `Text=/href` format (e.g. `"Home=/;About=/about"`). Overrides the site setting when provided. |
| `Brand` | `string?` | site setting `nav.brand`, then site key, then `"Home"` | Brand text displayed at the left of the bar. |
| `BrandHref` | `string?` | site setting `nav.brandhref`, then `"/"` | Href for the brand link. |
| `Class` | `string?` | — | Additional CSS class(es) appended to the `<nav>` element. |
| `Style` | `string?` | — | Inline style string appended to the `<nav>` element's `style` attribute. |
| `Padding` | `string?` | — | CSS padding shorthand applied to the `<nav>` element (e.g. `"0.5rem 1rem"`). |
| `Margin` | `string?` | — | CSS margin shorthand applied to the `<nav>` element. |
| `Align` | `string?` | — | Sets `text-align` on the `<nav>` element (e.g. `"center"`). |

## Examples

**Site-wide default — links and brand from site settings**

```
{{Plugin.NavMenu}}
```

No parameters needed. The plugin reads `nav.brand`, `nav.brandhref`, and `nav.links` from the Host → Site → Page setting cascade. Every site in a multi-site install supplies its own settings, so one theme token serves all sites automatically.

**Fully explicit nav bar**

```
{{Plugin.NavMenu brand="MindAttic" brandhref="/" links="Home=/;Docs=/docs;Blog=/blog;Contact=/contact"}}
```

Renders a bar with "MindAttic" as the brand and four navigation links. The link matching the current page's slug receives `aria-current="page"`.

**Minimal two-link bar with a custom brand target**

```html
<NavMenu Brand="Attic" BrandHref="/home" Links="Features=/features;Pricing=/pricing" />
```

## Notes

- **Link format:** each entry is `Text=/href` separated by `;`. Malformed entries (no `=`, empty text, or empty href) are silently skipped. Hrefs containing `javascript:`, `data:`, or `vbscript:` are blocked by the internal `IsUnsafeUrl` check.
- **Priority chain:** an explicit parameter always wins over the site setting. If neither is present, `Brand` falls back to the site key, then `"Home"`.
- **Active page:** the link whose href (slash-trimmed) matches the current page's slug (case-insensitive) receives `aria-current="page"`. Only an exact slug match qualifies; prefix matching is not performed.
- **Responsive collapse:** the component self-loads `navmenu.css` and `navmenu.js`. Below 720 px the list collapses and the `.ma-nav-toggle` button becomes visible. No manual asset import is needed.
- **CSS classes:** `ma-nav`, `ma-nav-brand`, `ma-nav-toggle`, `ma-nav-list`, `ma-nav-item`, `ma-nav-link`. These can be targeted by theme overrides.
- **Multi-site seam:** a single `{{Plugin.NavMenu}}` token in a shared theme works across every site because `nav.links` is resolved per-site at render time.
- **`Padding`, `Margin`, `Align`, and `Style`** are merged into a single `style` attribute on the `<nav>` element in that order; `Style` is always last so it can override the others.
