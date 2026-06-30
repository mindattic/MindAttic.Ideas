# Card

A content card with an optional image, title, body text, footer, and link. When `href` is set the entire card becomes a clickable anchor element.

## Usage

**Token syntax** (inside a page or widget body):

```
{{ MindAttic.Ideas.Component.Card title="Getting Started" text="Read the guides." href="/docs" imageclass="img-docs" }}
```

**HTML tag syntax** (inside a Razor file):

```razor
<Card title="Getting Started" text="Read the guides." href="/docs" imageclass="img-docs" />
```

To lay out multiple cards side by side, wrap the tokens inside a `<div class="ma-cards">` — a responsive grid helper that `card.css` ships:

```html
<div class="ma-cards">
  {{ MindAttic.Ideas.Component.Card title="Alpha" text="First card." }}
  {{ MindAttic.Ideas.Component.Card title="Beta"  text="Second card." }}
  {{ MindAttic.Ideas.Component.Card title="Gamma" text="Third card." }}
</div>
```

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `title` | `string?` | — | Card heading rendered as an `<h3>`. |
| `text` | `string?` | — | Body text rendered as a `<p>`. Plain text only; for rich content write HTML around the card instead. |
| `imageclass` | `string?` | — | A CSS class defined in the page's stylesheet that supplies a `background-image` (the base64 image convention). Preferred over `image=`. |
| `image` | `string?` | — | Direct image URL or `data:` URI. Used only when `imageclass` is not set. |
| `href` | `string?` | — | Link target. When set the root element becomes an `<a>` and the whole card is clickable. |
| `footer` | `string?` | — | Small footer line (e.g. a date or category label) rendered at the bottom of the card body. |
| `class` | `string?` | — | Additional CSS classes appended to the root element. |
| `style` | `string?` | — | Inline CSS appended to the root element's `style` attribute. |
| `padding` | `string?` | — | CSS padding shorthand applied to the root element (e.g. `1rem`, `4px 8px`). |
| `margin` | `string?` | — | CSS margin shorthand applied to the root element. |
| `align` | `string?` | — | Sets `text-align` on the root element (e.g. `center`, `left`, `right`). |

## Examples

**Simple text card (no image, no link):**

```
{{ MindAttic.Ideas.Component.Card title="About" text="Learn what MindAttic does." }}
```

**Linked card with a base64 image class:**

```
{{ MindAttic.Ideas.Component.Card title="Documentation" text="Guides and API reference." href="/docs" imageclass="img-docs-hero" }}
```

The page CSS supplies the image via the `img-docs-hero` class:

```css
.img-docs-hero { background-image: url('data:image/png;base64,...'); }
```

**Card with footer and centered alignment:**

```
{{ MindAttic.Ideas.Component.Card title="Release Notes" text="See what changed in V3." footer="June 2026" href="/changelog" align="center" }}
```

## Notes

- **Root element switches on `href`.** When `href` is absent the root is `<article class="ma-card">`; when `href` is set it becomes `<a class="ma-card ma-card-link" href="...">`. There is no additional wrapper element.
- **Image precedence.** `imageclass` takes priority over `image`. If both are supplied only `imageclass` is rendered.
- **Base64 image convention.** Per the page-authoring convention, images should be embedded as inline base64 CSS classes in the page's `<style>` block and referenced via `imageclass=`. Using a plain URL with `image=` is supported but requires the URL to be reachable at render time.
- **`href` sanitisation.** The `href` value is passed through an internal `SafeUrl` helper that blocks `javascript:` and other unsafe schemes.
- **`text` is plain text.** The `text` parameter is rendered inside a `<p>` without HTML interpretation. For rich card bodies, place free-form HTML outside the component and wrap it around multiple cards.
- **`padding`, `margin`, and `align`** are convenience shorthands that emit inline styles on the root element. They are merged with any value supplied via the `style` parameter.
