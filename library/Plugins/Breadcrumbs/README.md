# Breadcrumbs

A breadcrumb trail derived lexically from the current page's slug segments, requiring no site-tree feature and working on any page the moment it is added.

## Usage

**Token syntax**

```
{{Plugin.Breadcrumbs}}
{{Plugin.Breadcrumbs home="Start"}}
```

**HTML tag syntax**

```html
<Breadcrumbs />
<Breadcrumbs Home="Start" />
```

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Home` | `string?` | `"Home"` | Label for the leading root crumb that links to `/`. |
| `Class` | `string?` | — | Additional CSS class(es) applied to the outermost `<nav>` element. |
| `Style` | `string?` | — | Inline style applied to the outermost `<nav>` element. |
| `Padding` | `string?` | — | Shorthand padding applied as an inline style. |
| `Margin` | `string?` | — | Shorthand margin applied as an inline style. |
| `Align` | `string?` | — | Text/flex alignment applied as an inline style. |

## Examples

**Default — page at slug `docs/widgets/intro`**

```
{{Plugin.Breadcrumbs}}
```

Renders: Home / Docs / Widgets / *Intro* (last crumb uses the page's real `Title`).

**Custom root label**

```
{{Plugin.Breadcrumbs home="Library"}}
```

Renders: Library / Docs / Widgets / *Intro*.

**Scoped to a section with extra spacing**

```html
<Breadcrumbs Home="Knowledge Base" Margin="0 0 1rem 0" />
```

Renders: Knowledge Base / ... with a bottom margin separating it from page content.

## Notes

- The component renders **nothing** on the home page (zero slug segments) or when the slug is `home`, so it is safe to include in a site-wide layout.
- Intermediate crumb labels are prettified from slug segments (`my-page` becomes `My Page`); the final crumb always uses the page's real `Title` when one is set.
- Intermediate crumbs link to the cumulative slug prefix (e.g. the "Docs" crumb links to `/docs`). Those intermediate pages may or may not exist in the CMS.
- The component self-loads its stylesheet (`breadcrumbs.css`) via a scoped `<link>` tag; no manual stylesheet import is needed.
- CSS classes follow the `ma-crumbs-*` prefix convention (`ma-crumbs`, `ma-crumbs-list`, `ma-crumbs-item`, `ma-crumbs-link`, `ma-crumbs-current`) and can be targeted by theme overrides.
- The current (last) crumb is rendered as a `<span>` with `aria-current="page"` rather than a link.
