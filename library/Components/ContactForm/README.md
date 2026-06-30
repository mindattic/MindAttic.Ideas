# ContactForm

A name/email/message contact form that POSTs to a configurable endpoint.

## Usage

**Token syntax:**
```
{{ Component.ContactForm action="https://formspree.io/f/abc" submittext="Send" }}
```

**HTML tag syntax:**
```html
<ContactForm Action="https://formspree.io/f/abc" SubmitText="Send" />
```

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `Action` | `string?` | *(site setting `contact.action`)* | POST endpoint URL. Overrides the `contact.action` site setting. Required for the form to render. |
| `SubmitText` | `string?` | `"Send message"` | Label text for the submit button. |
| `Class` | `string?` | — | Additional CSS class(es) applied to the root element. |
| `Style` | `string?` | — | Inline style applied to the root element. |
| `Padding` | `string?` | — | Padding applied to the root element. |
| `Margin` | `string?` | — | Margin applied to the root element. |
| `Align` | `string?` | — | Text/content alignment on the root element. |

## Examples

**Formspree relay (explicit action on the token):**
```
{{ Component.ContactForm action="https://formspree.io/f/xpwzgkra" submittext="Send message" }}
```

**Custom button label, action pulled from site setting `contact.action`:**
```
{{ Component.ContactForm submittext="Get in touch" }}
```

**With layout helpers:**
```
{{ Component.ContactForm action="https://formspree.io/f/xpwzgkra" margin="0 auto" style="max-width:600px" }}
```

## Notes

- **Action resolution:** If `Action` is blank, the component falls back to the `contact.action` site setting. If neither is set, the form does not render; instead a visible configuration hint is shown (`<div class="ma-contact ma-contact-unconfigured">`).
- **Unsafe URL guard:** URLs that fail an internal safety check are treated as absent, triggering the unconfigured hint. Use standard `https://` endpoints.
- **Honeypot spam protection:** A hidden `website` field is included. Bots that fill it are expected to be rejected server-side; your endpoint should check for and discard submissions where `website` is non-empty.
- **Root CSS class:** Both the configured form and the unconfigured hint carry the `ma-contact` class. The unconfigured branch also adds `ma-contact-unconfigured`.
- **Stylesheet:** The component injects `/_ideas/Component/contactform/1/contactform.css` via a `<link>` tag.
- **Fields:** Name (text, max 200), Email (email, max 320), Message (textarea, max 4000). All three are required by HTML validation.
