# ModalPopup

A fixed-position overlay dialog with optional header, footer, backdrop dismiss, and keyboard navigation. Requires `InteractiveServer` render mode.

## Usage

**Token syntax** (inside a CMS page body):

```
{{Component.ModalPopup IsOpen="true" Title="Confirm Action" ConfirmText="OK" CancelText="Cancel"}}
  Your modal body content goes here.
{{/Component.ModalPopup}}
```

**Razor tag syntax** (inside another component or page):

```razor
<ModalPopup IsOpen="_showModal" Title="Confirm Action"
            ConfirmText="OK" CancelText="Cancel"
            OnConfirm="HandleConfirm" OnClose="HandleClose">
    Your modal body content goes here.
</ModalPopup>
```

## Parameters

| Name | Type | Default | Description |
|---|---|---|---|
| `IsOpen` | `bool` | `true` | Controls whether the modal is rendered and visible. |
| `Title` | `string?` | — | Text displayed in the modal header. Omit to suppress the title element. |
| `Size` | `string?` | `"md"` | Dialog width variant. One of `sm` (400 px), `md` (640 px), `lg` (900 px), `xl` (1200 px), `full` (100% − 2 rem). |
| `ShowClose` | `bool?` | `true` | Whether to show the ✕ close button in the header. |
| `ShowHeader` | `bool?` | auto | Force the header on or off. Defaults to `true` when a title or close button is present. |
| `ShowFooter` | `bool?` | auto | Force the footer on or off. Defaults to `true` when `ConfirmText` or `CancelText` is set. |
| `ConfirmText` | `string?` | — | Label for the primary action button. Omit to hide the button. |
| `CancelText` | `string?` | — | Label for the secondary/dismiss button. Omit to hide the button. |
| `WrapForm` | `bool?` | `false` | Wraps the dialog in a `<form>` element so the confirm button submits the form (type="submit"). |
| `Config` | `string?` | — | JSON blob with any of the above fields (camel- or pascal-case). Individual parameters override the JSON values. |
| `ChildContent` | `RenderFragment?` | — | Body content rendered inside the dialog. |
| `OnClose` | `EventCallback` | — | Fired when the close button or backdrop is clicked, or Escape is pressed. |
| `OnConfirm` | `EventCallback` | — | Fired when the confirm button is clicked (or the form is submitted when `WrapForm` is true). |
| `OnCancel` | `EventCallback` | — | Fired when the cancel button is clicked. |
| `Class` | `string?` | — | Extra CSS class(es) appended to the outermost backdrop element (`.ma-modal`). |
| `Style` | `string?` | — | Inline style string appended to the backdrop element. |
| `Padding` | `string?` | — | Shorthand for `padding:<value>` on the backdrop element. |
| `Margin` | `string?` | — | Shorthand for `margin:<value>` on the backdrop element. |
| `Align` | `string?` | — | Shorthand for `text-align:<value>` on the backdrop element. |

## Examples

**Simple confirmation dialog:**

```razor
<ModalPopup IsOpen="_confirm" Title="Delete item?"
            ConfirmText="Delete" CancelText="Keep"
            OnConfirm="DoDelete" OnCancel="CloseModal" OnClose="CloseModal">
    This action cannot be undone.
</ModalPopup>
```

**Large form modal with WrapForm:**

```razor
<ModalPopup IsOpen="_editOpen" Title="Edit Profile" Size="lg"
            ConfirmText="Save" CancelText="Discard" WrapForm="true"
            OnConfirm="SaveProfile" OnClose="CloseEdit">
    <InputText @bind-Value="_name" placeholder="Display name" />
    <InputText @bind-Value="_email" placeholder="Email" />
</ModalPopup>
```

**No header or footer — custom content only:**

```razor
<ModalPopup IsOpen="_infoOpen" ShowHeader="false" ShowFooter="false" OnClose="CloseInfo">
    <p>Welcome! Click anywhere outside this dialog to dismiss it.</p>
</ModalPopup>
```

## Notes

- **Render mode:** The component is decorated `[Idea(RenderMode = CmsRenderMode.InteractiveServer)]`. It must run on an interactive Blazor Server circuit; it will not function in static SSR.
- **Backdrop click and Escape:** Both invoke `OnClose`. Wire `OnClose` to set `IsOpen = false` to actually close the modal; the component does not close itself.
- **Auto-focus:** The dialog element receives focus automatically when it opens so assistive technologies announce it and Escape key events are captured without any extra setup.
- **`Config` JSON:** Useful when composing the modal from a CMS token where multiple parameters need to be passed as a single attribute. Example: `Config='{"title":"Alert","confirmText":"OK","size":"sm"}'`. Direct parameters (e.g. `Title="…"`) always win over the JSON values.
- **CSS custom properties:** Appearance is fully theme-able via `--ma-modal-bg`, `--ma-modal-line`, `--ma-modal-text`, `--ma-modal-muted`, `--ma-modal-accent`, `--ma-modal-radius`, and `--ma-modal-width`. Set these on any ancestor element.
- **Mobile:** Below 640 px the modal slides up from the bottom with squared top corners and fills 96 vh.
- **`WrapForm` + `ConfirmText`:** When `WrapForm` is true, the confirm button is `type="submit"` and form validation fires before `OnConfirm`. When false, the confirm button is `type="button"` and `OnConfirm` fires directly.
