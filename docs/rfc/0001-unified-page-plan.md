# Plan: one reference grammar, two backing forms (behavior vs. composition)

> Status: **PLAN — not yet implemented.** Supersedes the rigid `PageKind { Data, Code }` split and the
> "every citizen is a compiled dll" assumption. Legion-deliberated (see the deliberation in chat history).

## 1. The model

**One reference grammar** for everything an author places: a double-brace token

```
{{ <Kind>.<Name>[.V<n>|.Latest] [attr[=value] …] }}
```

- `<Kind>` ∈ `Theme` | `Plugin` | `Control` (a Page can't embed a Page). The `MindAttic.Ideas.` prefix is
  implied. Version is **optional** and pinned **at the reference** (`{{Control.Textbox.V1}}` pins;
  `{{Control.Textbox}}` floats to latest). Old versions coexist (never deleted), so a new upload can't break
  a page pinned to an old version.
- A **page** is free-form markup that hosts these tokens, **including its theme**:
  ```
  {{ Theme.Cyberspace }}
  <div>Hello World!</div>
  {{ Plugin.Tooltip }}
  {{ Control.Textbox label="Email" }}
  ```
  The `{{ Theme.X }}` token sets the page's wrap (host applies the theme, strips the token from output).

**Two backing forms, chosen by behavior vs. composition** — *not* by kind dogma:

- **Non-compiled (content / template / asset-manifest) — the DEFAULT** for things that are *composition or
  assets*: **Pages**, **Themes** (chrome template + a `{{Body}}` hole + css/js), and **asset-Plugins**
  (fonts, css/js bundles — the manifest just lists assets). Editable live, zero deploy, no ALC.
- **Compiled (`.razor` component) — the ESCAPE HATCH** for things that *do something*: **Controls**
  (interactive, typed params, binding, events) and **logic-Plugins** (e.g. TableOfContents querying the page
  tree, the Legion PersonaGallery). Typed, IDE-validated, but needs a build + an ALC + a version.

> **Rule:** don't compile what is content/assets (you'd lose live-editing for nothing); don't express
> interactive behavior as content (you'd reinvent components in hand-JS). The catalog resolves both
> uniformly by `{{tag}}`, so authors never see the seam.

| Kind | Default backing | Compiled when… |
|---|---|---|
| **Page** | non-compiled content (`page.razor` markup + tokens) | never |
| **Theme** | non-compiled template (`theme.razor`: chrome + `{{Body}}` + asset list) | it needs C# logic (rare) |
| **Plugin** | non-compiled asset-manifest (lists css/js) | it has render logic / params / queries |
| **Control** | — | **always** (typed, interactive) |

## 2. The `.idea` formats + the uniform manifest

Every `.idea` carries the **same tiny manifest**:

```json
{ "tagName": "{{Theme.Cyberspace}}", "friendlyName": "Cyberspace", "ContentType": "Theme", "version": 1 }
```

- `tagName` = the literal **version-less** token (braces included). `ContentType` ∈ Page|Theme|Plugin|Control.
  `version` = whole-number (coexisting). **No theme, slug, or hierarchy in the manifest** — theme is the body
  token; page placement is the CMS drag-drop tree.

Payload by backing form:

```
Page (non-compiled)      ├─ page.razor        (markup + {{…}} tokens)        └─ manifest.json [+ wwwroot/]
Theme (non-compiled)     ├─ theme.razor       (chrome + {{Body}} + tokens)   └─ manifest.json [+ wwwroot/]
Plugin asset (non-comp.) ├─ assets.json       (css[]/scripts[] it loads)     └─ manifest.json [+ wwwroot/]
Plugin logic / Control   ├─ bin/<dll>         (compiled .razor citizen)      └─ manifest.json [+ wwwroot/]
```

## 3. Install → catalog or page row

- **Page**: read `page.razor` → `Page.BodyHtml` (run `UpgradeLegacyTags` defensively); upsert a `Page` row
  by `(SiteId, Slug)` placed at top level (slug from the tag/name), then organized via the drag-drop
  hierarchy (#8). No ALC.
- **Theme / asset-Plugin (non-compiled)**: register a catalog citizen whose render is the template / the
  asset list — no ALC, no dll. Served + composed by `{{tag}}` like any citizen.
- **Control / logic-Plugin (compiled)**: extract `bin/`, register via the collectible ALC (today's path).
- All: persist `.idea` bytes (blob store) + extract `wwwroot/` to `/_ideas/{Kind}/{key}/{version}/…`.

## 4. Render + "missing → upload to fix"

- The expander resolves each `{{…}}` through the catalog (Missing/Disabled → placeholder + Admin-Inbox alert).
- The placeholder (`MissingContent`) becomes, **for an admin**, a **clickable error box** → opens
  `/admin/upload` prefilled with the missing reference (`?need={{Plugin.Tooltip.V1}}`); drop the `.idea`, the
  page re-renders. Neutral, non-interactive box for everyone else.

## 5. Typed attributes without compiling the page

`{{…}}` content stays editable text, but is **typed + validated against the real component contracts**:

- **Coerce at render:** when applying a token's attributes, reflect the resolved component's `[Parameter]`
  properties and convert the string to the real type (`{{Theme.Cyberspace effectFrequency=10}}` → typed
  `int`); unmatched attrs fall to the `CaptureUnmatchedValues` bag.
- **Validate at save/upload:** the same reflection flags unknown attribute, bad type, unknown/disabled ref,
  missing required param — surfaced in the editor. (No page compilation needed for "valid".)

## 6. Authoring: Monaco with catalog-driven IntelliSense + live validation

The admin page editor embeds **Monaco** (the VS Code engine) with a custom provider fed by the **live
catalog** — so `{{…}}` gets first-class, *catalog-aware* assistance (arguably better than IDE IntelliSense,
because it only ever offers what's actually installed):

- Type `{{` → autocomplete installed citizens (tag + friendlyName).
- After a tag → autocomplete its **parameters with types + descriptions**, **auto-derived by reflecting the
  component's `[Parameter]`s + their XML-doc `<summary>`** (ship the XML doc in the `.idea` so descriptions
  travel). No hand-written rules per component; a new plugin's params appear on install.
- Live **squiggles** from the §5 validator (unknown attr / bad type / unknown-disabled ref / missing required).
- A small endpoint exposes `{ tag, friendlyName, params:[{name,type,required,summary}] }` per citizen to feed
  the provider. *(Optional later: a VS Code extension/LSP for editing `.idea` content outside the CMS.)*

## 7. Migration steps (reviewed, incremental — after this plan is accepted)

1. **Manifest kernel**: `{ tagName, friendlyName, ContentType, version }`; `ma-idea pack` emits the right
   payload per backing form (page.razor / theme.razor / assets.json / dll).
2. **Install paths**: add non-compiled Page (→ Page row), Theme-template, and asset-Plugin registration;
   keep the compiled ALC path for Controls/logic-Plugins.
3. **PageHost**: collapse to one path — render the body through the expander wrapped in the `{{Theme.X}}`
   theme; drop the compiled-Page branch + `MissingPageHost`. Retire `PageKind.Code` (migrate `Code` rows →
   `Data`, null `ComponentTypeName`).
4. **Short grammar + theme token + typed coerce** in `IncludeExpander`/`IncludeReferenceParser`
   (`{{Kind.Name}}`, `{{Theme.X}}` directive, reflection coercion §5).
5. **Clickable placeholder** → upload-to-fix (§4).
6. **Monaco editor + catalog metadata endpoint + validator** (§6).
7. **Refactor the compiled pages**: Frontpage → an accordion **logic-Plugin** + thin content page;
   PersonaGallery → a gallery **Control/logic-Plugin** + thin content page; HelloWorld → content + tokens.
8. **Docs/amendment**: rewrite `AUTHORING.md`; add a `FOUNDATION_AMENDMENTS` entry for this model.
9. **Tests** for each new install path, the typed coercion, and the validator.

## 8. Open questions

- **Theme template shape:** how the `{{Body}}` hole + asset tiers are expressed in `theme.razor` (a reserved
  `{{Body}}` token + a leading asset block?). Default: `{{Body}}` reserved token + css/js via the manifest's
  asset list (same as asset-Plugins).
- **External-IDE authoring:** ship the VS Code LSP now or defer? Default: defer (CMS Monaco covers it).
- **`PageKind` enum:** leave append-only with `Code` unused vs. fully remove. Default: leave it, migrate rows.
