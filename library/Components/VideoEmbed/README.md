# Video Embed

Responsive video player that accepts YouTube, Vimeo, or direct video URLs and renders a privacy-friendly embed.

## Usage

**Token syntax**

```
{{ MindAttic.Ideas.Component.VideoEmbed url="https://youtu.be/xyz" title="Launch demo" }}
```

**HTML tag syntax**

```html
<VideoEmbed Url="https://youtu.be/xyz" Title="Launch demo" />
```

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Url` | `string?` | — | The video URL. Accepts YouTube page/short/embed URLs, Vimeo page URLs, direct file URLs, or `data:` URIs. |
| `Title` | `string?` | `"Video"` | Accessible title attribute for the `<iframe>` or `<video>` element. |
| `Class` | `string?` | — | Additional CSS class(es) applied to the outer element. |
| `Style` | `string?` | — | Inline style applied to the outer element. |
| `Padding` | `string?` | — | CSS padding shorthand applied to the outer element. |
| `Margin` | `string?` | — | CSS margin shorthand applied to the outer element. |
| `Align` | `string?` | — | Alignment hint (`left`, `center`, `right`) applied to the outer element. |

## Examples

**YouTube video**

```
{{ MindAttic.Ideas.Component.VideoEmbed url="https://www.youtube.com/watch?v=dQw4w9WgXcQ" title="Product walkthrough" }}
```

**YouTube Shorts**

```
{{ MindAttic.Ideas.Component.VideoEmbed url="https://youtu.be/shorts/abc123XYZ01" title="Quick tip" }}
```

**Vimeo video**

```
{{ MindAttic.Ideas.Component.VideoEmbed url="https://vimeo.com/123456789" title="Behind the scenes" }}
```

**Direct video file**

```
{{ MindAttic.Ideas.Component.VideoEmbed url="https://cdn.example.com/intro.mp4" title="Intro video" }}
```

## Notes

- **YouTube** URLs (`youtube.com/watch`, `youtu.be`, `youtube.com/shorts`, existing embed URLs) are rewritten to `https://www.youtube-nocookie.com/embed/{id}` to avoid third-party tracking cookies.
- **Vimeo** page URLs are rewritten to `https://player.vimeo.com/video/{id}?dnt=1` (Do Not Track enabled).
- Any other absolute URL — including `data:` URIs — renders a native `<video controls>` element. This follows the inline base64 asset convention used throughout the library.
- If `Url` is empty or not a valid absolute URI, the component renders nothing.
- The YouTube video ID must be 1–20 characters of `[A-Za-z0-9_-]`; malformed IDs are rejected and the component renders nothing.
- The outer wrapper is `<div class="ma-video">` with a responsive aspect-ratio CSS rule provided by `videoembed.css`.
- `Title` defaults to `"Video"` when not provided; supply a meaningful value for accessibility.
