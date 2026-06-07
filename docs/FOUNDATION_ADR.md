<!-- CODEX MIGRATION (2026-06-07): reformatted into the Codex L0 bible at docs/BIBLE.md. This frozen
     ADR is preserved as the historical deliberation; current truth = docs/BIBLE.md + docs/AMENDMENTS.md. -->

> ⚠️ **VOCABULARY SUPERSEDED.** This is the Legion deliberation that produced the foundation. Its
> *structure and reasoning* stand, but its *vocabulary* has been superseded by the implemented model in
> [`FOUNDATION_AMENDMENTS.md`](FOUNDATION_AMENDMENTS.md) (see "Taxonomy finalization A9–A14") and
> [`../README.md`](../README.md). In particular: there are **four kinds** (Page · Component · Theme ·
> Control) under a shared **`IdeaBase`**; the bases are `PageBase`/`ComponentBase`/`ThemeBase`/`ControlBase`
> (not `Cms`-prefixed); the include/compose tag is `<MindAttic.Ideas.{Kind}.{Name}.{Version} />` (version
> optional → latest), not `<ma-component>`; a **Component** is a capability activator and a **Control** is
> an atomic UI element. Read the amendments for the current truth.

# MindAttic.Ideas — Unified Foundation ADR (v1, ratify-then-build)

**Status:** Proposed for ratification. Once ratified, the build follows this document verbatim.
**Promise:** The FOUNDATION never changes â€” only enhances. Every contract below is append-only / forward-compatible / inheritance-root-stable for years. A Roslyn PublicAPI analyzer + CI tests enforce the never-change promise mechanically.

**Stack (verified):** .NET 10 (SDK 10.0.300), Blazor Web App global InteractiveServer, EF Core + SQL Server, Azure Blob, `IDbContextFactory`. Auth/seed/concurrency ported VERBATIM from MindAttic.Frontpage (do not reinvent). MindAttic.UiUx stays build-free, source-of-truth, consumed via pinned-tag jsDelivr.

---

## 1. Page model (reconciles FOUNDATION-001, FND-ABSTRACTIONS-SDK, FOUND-001-source-unification-alc, FND-001-RENDER-CSS-TRUST)

**Locked contract.** A Page is ONE durable EF row `Page`, discriminated by `PageKind { Data = 0, Code = 1 }`. There are NO zones, panes, slots, drop-targets, or grid (Directive A). Composition is **lexical**: the author writes free-form HTML/CSS/JS and drops `<ma-component key=\"ui.tooltip\" .../>` include tags wherever they want.

- **Data page** (zero deploy): stores free-form author `BodyHtml`/`PageCss`/`PageJs` in DB columns; rendered by the built-in `FreeFormPage : CmsPageBase` via `RenderStrategy.RawMarkup`. The `ComponentMarkupExpander` (AngleSharp tokenization, never regex) rewrites each `<ma-component>` into `DynamicComponent` against the catalog; unknown key â†’ `CmsMissingContent`; literal markup â†’ `IRawContentGate.Emit(html, BodyTrust)`.
- **Code page** (full compiled C#/interactivity): names a `[CmsPage(key)]` `CmsPageBase` subclass; resolved late by `(Kind,Key)`â†’Type and rendered via `RenderStrategy.ClrType` â†’ `DynamicComponent(Type, {[\"Context\"]=ctx})`. Deploys once per TYPE, never per page.

Both are first-class rows in one Page table on one render path. A page can **graduate Dataâ†”Code as a row edit**, never a schema change. Resolution is by `(SiteId, Slug)` data lookup, NEVER per-page routing â€” so a runtime-loaded .idea type renders with zero router changes.

**Rejected:** DNN zones/panes/IdeaInstance (Directive A); two Page tables/routers (forking hazard); `ComponentReferencesJson` sidecar placement (drift hazard â€” the `<ma-component>` tag is the SOLE placement channel).

**Under-specified risk called out:** the `<ma-component>` include GRAMMAR is frozen public API the day an admin authors against it. Lock it now as an HTML-valid custom element (attributes-only, with reserved `ChildContent` for inner content via `IPageContext`), ship a parser test, and never change the grammar.

---

## 2. Abstractions SDK surface â€” frozen v1 (reconciles FND-ABSTRACTIONS-SDK, FOUNDATION-001, FOUND-001-source-unification-alc)

`MindAttic.Ideas.Abstractions` (net10.0) references ONLY `Microsoft.AspNetCore.Components` + `System.Text.Json`. MAJOR pinned at 1 forever; additive-only.

**Inheritance roots (RESOLVED naming):** `CmsPageBase`, `CmsComponentBase` (nullable context so the unchanged UiUx Tooltip wrapper registers as-is), `CmsThemeBase` (single `RenderFragment Body`, NO zones). The `Cms` prefix preserves Directive D's concept-mapping while avoiding a public type literally named `ComponentBase` clashing with Razor's. The word **Idea** never names a type â€” only the `.idea` file and `/_ideas` route.

**Discovery attributes:** `[CmsPage(key)]`, `[CmsComponent(key)]`, `[CmsTheme(key)]` â€” stable string `Key` is the forever-identity, never the CLR type name.

**Render context (RESOLVED to an INTERFACE):** `IRenderContext` delivered as a `[CascadingParameter]` (not a sealed-class `[Parameter]`), carrying `IPageContext`/`ISiteContext`/`IInlineMarkup`, `IServiceProvider Services`, `ContentMode Mode`, `CmsRenderMode RenderMode`, `RawSettingsJson`+`GetSettings<T>()`, and a default `TryGetFeature<T>` escape hatch. Grows append-only via interface DEFAULT methods â€” the only shape that is binary-additive forever AND unifies across the Phase-5 collectible-ALC boundary.

**Enums (explicit ascending integers, append-only):** `ContentKind{Page=0,Component=1,Theme=2}`, `PageKind{Data=0,Code=1}`, `CmsRenderMode{Static=0,InteractiveServer=1}` (WASM EXCLUDED â€” a runtime .idea assembly cannot reach the browser WASM runtime; appending later is additive if Blazor ever allows it), `ContentMode{View=0,Edit=1,Preview=2}`, `ContentOrigin{Compiled=0,Package=1}`, `RenderStrategy{ClrType=0,RawMarkup=1}`, `ContentTrust{Untrusted=0,Author=1}`.

**Seams:** `ICmsContentSource{ Origin; Priority; IEnumerable<ContentDescriptor> Discover(); }` (sync), `ITypeResolver{ Type? Resolve(ContentDescriptor); }` (the ONE class that absorbs all ALC/framework churn â€” type stored as `ClrTypeName`+`AssemblyName`, resolved late, never an on-descriptor `Func<Type>`), `IPackageAssetSource` (reserved), `IRawContentGate` (the sole MarkupString chokepoint). `ContentDescriptor` = D5 superset (`Kind,Key,DisplayName,Origin,Priority,Strategy,RenderMode,Version,AllowOverride,ClrTypeName,AssemblyName,RawBundle,AssetMount`); deferred fields (RegionNames/SettingsModelTypeName) arrive additively. `[assembly:IdeaSdkVersion(1)]`+`Sdk.Version`. `SharedContracts.DeferToDefaultPrefixes`.

**Settings:** v1 carries only `RawSettingsJson`+`GetSettings<T>()` + optional Type hints on attributes. NO settingsSchema engine / auto-form / dep-resolver in the foundation (additive later). Microscopic surface = strongest never-change guarantee.

---

## 3. .idea package format (reconciles FND-001-IDEA-PACKAGE-FORMAT as owner, mapping D1/D2/D5/D6 onto it)

A `.idea` is a plain ZIP whose only mandatory member is `idea.json` (Directive E â€” a zip of files, not a heavyweight descriptor).

**Frozen six-field kernel (never moves/renames/repurposes):** `manifestVersion` (int, schema of THIS file, host-gated), `category` (`Page|Theme|Component` â€” WHAT it is), `kind` (`data|code` â€” HOW it renders), `key`, `version` (whole-number content version + asset-URL segment), `displayName`.

**Well-known optional (safe defaults, append-only):** `sdk` (integer floor vs Abstractions; REQUIRED if code, OMIT if data), `entryType` (AQN; required if code), `renderMode`, ordered `css[]`/`scripts[]` (mapped to the locked cascade â€” NOT auto-discovery), `assets`, `dependsOn[]`, `uiux[]` (pinned jsDelivr bundles), category sub-blocks. `[JsonExtensionData] Extra` + `UnmappedMemberHandling.Skip` mandatory from day one (forward-compat). **Loaders MUST ignore unknown fields/folders.**

**ZIP layout:** `idea.json` (root) Â· `wwwroot/` (served at `/_ideas/{key}/{version}/...`) Â· `bin/` (kind=code only) Â· `data/` (optional seed) Â· icon/README/LICENSE. **FORBIDDEN in bin/:** `MindAttic.Ideas.Abstractions`, `MindAttic.Ideas.Core`, `Microsoft.*`, `System.*` (the ALC unification rule â€” install validator rejects by prefix).

**Two version axes, never conflated:** `manifestVersion` (file format, integer host gate, `HOST_MAX_MANIFEST_VERSION=1`) vs `sdk` (runtime contract floor). A too-new manifest is rejected with \"upgrade MindAttic.Ideas\", never mis-rendered.

---

## 4. EF Core data model (reconciles FOUNDATION-EFCORE-DATAMODEL as owner)

Every content entity implements the reserved-column contract from migration #1 (matches the Frontend donor): `int Id` IDENTITY clustered PK, `Guid Uid` unique, `string Key` (UPSERT authority for seed/.idea import), `byte[] RowVersion`, `IsDeleted`+`DeletedUtc` (soft delete), full audit, `Extra` JSON bag, `int? SourcePackageId`.

- **`Page`** â€” `PageKind Kind`; Data columns `BodyHtml`/`PageCss`/`PageJs`/`BodyTrust`/`AuthoredByUserId`/`AuthorTrustVersion`; Code columns `ComponentTypeName`/`AssemblyName`/`SettingsJson`; shared `SiteId`(nullable)/`ParentId`/`Slug`/`Title`/`ThemeKey`/`IsPublished`/`SortOrder`/`SeoMetaJson`. (Single `PageCss` column â€” D6's PageCss/PageInlineCss split is dropped; inline `style=\"\"` is tier 4 by DOM, no column needed.)
- **`Theme`, `Component`** â€” sibling content tables (Key, asset/markup, type-name).
- **`CmsContentDefinition`** â€” persisted catalog: `UNIQUE(Kind,Key,Origin)`, `Priority`, `IsShadowed`, `IsActive`, `RawBundleJson`, `AssetMount`. Upserted from sources at boot; stale â†’ IsActive=false â†’ placeholder; never deleted on discovery.
- **`InstalledPackage`** â€” `{Category,Kind,Key,Version,DisplayName,ManifestJson(verbatim),BlobPath,Sha256,ManifestVersion,Enabled,IsActiveVersion,InstalledUtc}`, `UNIQUE(Category,Key,Version)`. Reserved/unwritten until Phase 5.
- **`Asset`** â€” Blob-backed (BlobUri + optional small inline Bytes); content-addressed by Sha256.
- **`SettingEntry`** â€” `UNIQUE(Scope,ScopeId,Key)`; Hostâ†’Siteâ†’Page override chain; **global CSS lives at `Scope=Host, Key='css.global'`**.
- **Auth (ported verbatim):** `User`(string Id, BCrypt PasswordHash, SecurityStamp, MustChangePassword), `Role`, `UserRole`, `PagePermission`, `ComponentPermission`.

**Key indexes:** `UNIQUE(SiteId,Slug)`; `UNIQUE(Key)` per content table; `Uid` alternate key; filtered published index; `ParentId` self-FK `OnDelete(NoAction)`; `ThemeKey` FK `OnDelete(SetNull)`; soft-delete query filters. ONE initial migration; append-only thereafter. `SiteId` nullable from day 1 â†’ multi-tenant is a resolver, not a schema break.

---

## 5. Source unification & ALC (reconciles FOUND-001-source-unification-alc as owner)

ONE catalog (`CmsContentDefinition`) fed by ordered `ICmsContentSource` providers yielding uniform `ContentDescriptor`s keyed by `(Kind,Key)`. Phase 1 ships exactly `CompiledContentSource` (Origin=Compiled, Priority=100; reflects referenced assemblies). Phase 5 adds `PackageContentSource` (Origin=Package, Priority=50, collectible ALC) + real `PackageAssetSource` â€” purely additive, NO renderer/catalog/route/schema change.

**Precedence:** Compiled wins a `(Kind,Key)` collision; loser kept VISIBLE as `IsShadowed` (never silently dropped). A package shadows a compiled key only with manifest `allowOverride:true` PLUS admin confirmation.

**The ALC linchpin:** the per-.idea collectible `AssemblyLoadContext` MUST return null (defer-to-default) for `SharedContracts.DeferToDefaultPrefixes` (Abstractions, Core, Microsoft.*, System.*, netstandard, mscorlib) so the package's `CmsPageBase` unifies by reference identity with the host's; otherwise casts fail at the `DynamicComponent` boundary. A Phase-1 cast-identity unit test guards this before Phase 5 exists. Type resolution lives ONLY in `ITypeResolver` (Phase-1 default-ALC impl; Phase-5 ALC-aware impl behind the same interface). Live hot-swap is NOT promised â€” load-on-boot; upgrade = stage Blob + recycle.

---

## 6. Render path, CSS cascade & trust (reconciles FND-001-RENDER-CSS-TRUST as owner)

**ONE catch-all host** `PageHost.razor` `@page \"/{*slug}\"` â€” the single permanent render entry point. `/admin` (RequireAuthorization Admin), `/_*`, and `/_ideas/{key}/{version}/{**path}` are reserved and mapped BEFORE the fallback; a defense-in-depth reserved-prefix 404 guard sits inside PageHost; the save validator rejects slugs starting `admin`/`_`.

Render: resolve Page by `(SiteId,Slug)` (null/unpublished â†’ CMS 404 Page; 403 if no view perm) â†’ resolve Theme (page override â†’ site default â†’ bootstrap fallback) â†’ emit `CmsHead` in the FROZEN cascade â†’ wrap `DynamicComponent(theme.LayoutType)` around the body via the render fork: `Strategy=RawMarkup` â†’ `FreeFormPage`; `Strategy=ClrType` â†’ `DynamicComponent(resolvedType,{Context=ctx})`; unresolved/stale â†’ `CmsMissingContent` (never crash).

**Fixed CSS cascade (LOCKED, code-enforced, one place):** ordinal **0 GLOBAL** (`SettingEntry` Host/`css.global`) â†’ **100 THEME** (`CmsThemeBase.GlobalCssUrls` then `ThemeCssUrls`, mirrored from UiUx deps.json) â†’ **200 PAGE** (`Page.PageCss` in `<style>`) â†’ **300+DOM INLINE** (`style=\"\"` in BodyHtml). Reserved gaps (e.g. 250) allow a future component-scoped tier additively; never reorder. CI test asserts the order.

**Trust line (LOCKED at AUTHOR IDENTITY, WRITE TIME â€” Directive F):** on save, `Page.BodyTrust = Author` iff the writer holds the `Cms.AuthorRawMarkup` claim (Admin role), else `Untrusted`; `AuthoredByUserId`+`AuthorTrustVersion` recorded. At render, the SINGLE `IRawContentGate.Emit(html,trust)` is the ONLY place a `MarkupString` is born (analyzer-enforced): Author â†’ raw passthrough (intentional admin JS honored); Untrusted â†’ Ganss.Xss. CSP: author-trusted responses carry a per-response nonce (author `<script>` + pinned jsDelivr); all others `script-src 'self'` no-unsafe-inline. Demotion is a deliberate epoch bump, never a silent re-render.

---

## 7. UiUx citizenship (no UiUx build, zero duplication)

Verified ground truth: UiUx has no .csproj/build; ships raw files + thin stateless wrappers; distributes via pinned-tag jsDelivr. The CMS consumes it WITHOUT a UiUx build:

- `[CmsTheme(\"cyberspace\")] CyberspaceTheme` â€” asset URL lists **mirror, and are CI-verified against, `Themes/Cyberspace/deps.json` at pinned tag `V4`** (css[]: outfit-font, attic-font, back-home-m, frontpage.css; scripts[]: loader, tv-static, home-bg, sacred-geometry, console-bg; reproduce the `window.__cyberspaceCircuitboardSrcs` PNG inline-setter + body-prelude). **Note:** D1's hand-typed list was wrong (it included `theme.css` and omitted tv-static/home-bg/sacred-geometry); deps.json is authoritative and a CI test fails on drift.
- `[CmsComponent(\"ui.tooltip\")] TooltipComponent` â€” body delegates to a synced copy of the UNCHANGED Tooltip.razor (`<span>` + data-* attributes, no interop); StylesheetUrls/ScriptUrls = pinned jsDelivr tooltip.css/tooltip.js.
- `Abstractions.dll` is HOST-OWNED and NEVER shipped in a .idea.

---

## 8. MindAttic.Frontpage collapse path

Port Frontend's auth/seed/concurrency verbatim into Core. Collapse exercises BOTH render halves: `[CmsPage(\"frontend.root\")] FrontendRootPage : CmsPageBase` (Code page, interactive accordion/hub) at `Slug=\"\"`; leaf landing pages (portfolio/software/books, from the existing projects.json/books.json seed) as Data pages with author HTML + `Trust=Author`, seeded by the SAME idempotent upsert-by-slug seeder that never clobbers admin edits. (See Open Question #1 for the type-safety-vs-zero-deploy-purity call.)

---

## 9. Cross-cutting invariants (never change)

Naming lock; (Kind,Key) identity lock; context/enum/attribute/descriptor append-only lock (PublicAPI analyzer + snapshot test, MAJOR=1 forever); ALC defer-to-default lock; CSS cascade lock; trust-at-write-time lock; two-version-axes lock; `/_ideas/{key}/{version}/{**path}` asset-route lock; one-render-primitive (DynamicComponent / FreeFormPage, no zones, no routing) lock; port-don't-reinvent lock. (Full text in the Crosscutting-Invariants section.)

## 10. Build order

P0.1 Abstractions â†’ P0.2 Core entities+migration â†’ P0.3 ported auth/seed + CompiledContentSource + catalog + gate â†’ P0.4 FreeFormPage + ma-component expander + placeholders â†’ P1.1 Web host (ported Program.cs) â†’ P1.2 PageHost + CmsHead + render fork + CI tests â†’ P1.3 Cyberspace+Tooltip citizens (deps.json-verified) â†’ P1.4 prove end-to-end Data page â†’ P1.5 Frontend collapse (both halves) â†’ P2+ Admin, then Phase-5 PackageContentSource/ALC/installer/SDK packer drop in additively with zero changes to catalog/descriptor/renderer/route/schema. (Full text in the Build-Order section.)


---

# Appendix A — Crosscutting Invariants (full text)

- NAMING LOCK: the three inheritance roots are `MindAttic.Ideas.Abstractions.CmsPageBase`, `CmsComponentBase`, `CmsThemeBase`; discovery attributes are `[CmsPage(key)]`, `[CmsComponent(key)]`, `[CmsTheme(key)]`; the three content kinds are the enum `ContentKind{Page=0,Component=1,Theme=2}`. The word 'Idea' NEVER names a content type, a base class, an attribute, a context, or a catalog row â€” it appears ONLY as the `.idea` zip file extension and the `/_ideas/...` asset route. A CI naming test fails the build if any public type in Abstractions matches `^Idea` or is literally `ComponentBase`/`PageBase`/`ThemeBase` (unprefixed).

- IDENTITY LOCK: every citizen's forever-identity is the tuple `(ContentKind Kind, string Key)` â€” a stable lowercase dotted string (`frontend.root`, `cyberspace`, `ui.tooltip`) â€” NEVER the CLR type name and NEVER the int Id. Renaming/moving a CLR type is a non-event (catalog resolves Key->Type late and degrades to a placeholder on miss). For .idea import/seed reconciliation, the string `Key` is the UPSERT authority; `Guid Uid` is the stable secondary; int `Id` is environment-local and NEVER crosses a boundary.

- CONTEXT-GROWTH LOCK: `IRenderContext` (and `IPageContext`/`ISiteContext`/`IInlineMarkup`) grow ONLY by adding interface members WITH default implementations; the three base classes grow ONLY by adding non-abstract members; the discovery attributes and `ContentDescriptor` grow ONLY by appending init-only properties; all enums grow ONLY by appending members with explicit ascending integer values, never reordering/renumbering. A Roslyn PublicAPI analyzer (PublicAPI.Shipped.txt) + a public-API snapshot test fail CI on ANY removal, rename, reorder, retype, or accessibility change in Abstractions. MAJOR version of Abstractions is pinned at 1 forever; the only escape hatch for a truly unforeseen break is a side-by-side additive `IRenderContext2`/Abstractions-v2 the catalog routes by manifest sdk-major.

- ALC UNIFICATION LOCK (the linchpin for Phase 5): `MindAttic.Ideas.Abstractions.dll`, `MindAttic.Ideas.Core.dll`, and all `Microsoft.AspNetCore.*`/`Microsoft.Extensions.*`/`Microsoft.EntityFrameworkCore.*`/`Microsoft.JSInterop.*`/`System.*`/`netstandard`/`mscorlib` assemblies are HOST-OWNED and resolved to the default ALC; the collectible per-.idea AssemblyLoadContext MUST return null for these (defer-to-default) so the package's CmsPageBase unifies by reference identity with the host's. The .idea packer is FORBIDDEN from shipping any of these in `bin/`/`lib/` (install-time validator rejects them by prefix). A Phase-1 unit test asserts a type loaded through the source pipeline binds as CmsPageBase by reference identity, guarding the rule before Phase 5 lights up the loader.

- CSS CASCADE LOCK: the emit order is GLOBAL(ordinal 0, from SettingEntry Host/css.global) -> THEME(100, from CmsThemeBase GlobalCssUrls+ThemeCssUrls mirroring deps.json) -> PAGE(200, from Page.PageCss) -> INLINE(300+DOM, from style="" inside BodyHtml). This order lives in exactly ONE component (`CmsHead`), is enforced by a CI test, and may only be EXTENDED at reserved integer gaps (e.g. a component-scoped tier at 250) â€” never reordered. This is a frozen public contract because every published page's appearance depends on it.

- TRUST-LINE LOCK: the trust boundary is AUTHOR IDENTITY at WRITE TIME, never content shape and never the current viewer. On save, `Page.BodyTrust` is stamped `Author` iff the writer holds the `Cms.AuthorRawMarkup` claim (granted to the Admin role), else `Untrusted`; `AuthoredByUserId`+`AuthorTrustVersion` are recorded. At render, the SINGLE `IRawContentGate.Emit(html,trust)` is the ONLY place a `MarkupString` is constructed (enforced by analyzer/convention): Author => raw passthrough (intentional admin JS honored, Directive F); Untrusted => Ganss.Xss sanitized. CSP: author-trusted responses get a per-response nonce allowing the author <script>+pinned jsDelivr; all other responses get strict `script-src 'self'` no-unsafe-inline. Demoting an author is a deliberate policy action (bump AuthorTrustVersion epoch), never a silent re-render.

- VERSIONING LOCK: TWO independent version axes, never conflated. (1) `manifestVersion` â€” integer schema version of idea.json, host-gated by `HOST_MAX_MANIFEST_VERSION` (Phase-1=1); a higher value is rejected with 'upgrade MindAttic.Ideas', never mis-rendered. (2) `sdk` â€” an integer FLOOR the package declares against `[assembly:IdeaSdkVersion]`/`Sdk.Version` of Abstractions; REQUIRED for kind=code, OMITTED for kind=data (data pages bind no compiled contract and keep working when Abstractions moves). Package `version` is the content's own whole number and the asset-URL segment. Abstractions stays strictly additive so the sdk floor only ever needs the lower bound checked.

- ASSET-ROUTE & SOURCE-OF-TRUTH LOCK: uploaded package assets serve forever from `/_ideas/{key}/{version}/{**path}` (immutable cache, versioned for side-by-side upgrade) via a CompositeFileProvider; compiled-RCL assets serve from build-time `_content/{Assembly}/`; UiUx assets serve from pinned-tag jsDelivr URLs mirrored from deps.json. `AssetMount` on the descriptor abstracts which. Azure Blob is the source of truth for package bytes (App Service disk is ephemeral); package bytes are content-addressed by Sha256. `/_ideas` and `/_*` and `/admin` are reserved route prefixes mapped BEFORE the `@page "/{*slug}"` catch-all, with a defense-in-depth reserved-prefix 404 guard inside the host and a save-time validator rejecting slugs starting `admin` or `_`.

- RENDER-PRIMITIVE LOCK: every citizen renders through exactly ONE primitive â€” `DynamicComponent(Type, {["Context"]=ctx})` for Strategy=ClrType, or the built-in `FreeFormPage`/`RawMarkupRenderer` for Strategy=RawMarkup â€” wrapped in the selected theme's single `@Body` hole. There are NO zones, panes, slots, drop-targets, or grid; composition is lexical position in free-form markup plus `<ma-component>` includes. Page resolution is by (SiteId, Slug) data lookup, NEVER per-page routing, so a runtime-loaded .idea type renders with zero router changes. Unresolved type or unknown component key degrades to a visible `CmsMissingContent` placeholder, never a render crash or exception.

- PORT-DON'T-REINVENT LOCK: auth, seed, and concurrency are ported VERBATIM from MindAttic.Frontpage, not redesigned â€” cookie auth + `OnValidatePrincipal` SecurityStamp revalidation every request + BCrypt + string `User.Id` + rate-limited login + forwarded-headers + `MapStaticAssets`; idempotent upsert-by-natural-key SeedService that NEVER clobbers admin-edited body content (the projects.json/books.json pattern); `byte[] RowVersion` optimistic concurrency; `IDbContextFactory<CmsDbContext>`. Multi-site is a nullable `SiteId` column with `UNIQUE(SiteId,Slug)` from migration #1 so going multi-tenant is a host->SiteId resolver, not a schema break.


# Appendix B — Build Order (full text)

- P0.1 â€” Create `src/MindAttic.Ideas.Abstractions` (net10.0, PackageReferences ONLY Microsoft.AspNetCore.Components + System.Text.Json; NO EF/host/SQL/UiUx/sanitizer). Define exactly: enums `ContentKind{Page,Component,Theme}`, `PageKind{Data,Code}`, `CmsRenderMode{Static,InteractiveServer}`, `ContentMode{View,Edit,Preview}`, `ContentOrigin{Compiled,Package}`, `RenderStrategy{ClrType,RawMarkup}`, `ContentTrust{Untrusted,Author}` â€” all with explicit ascending integer values. Interfaces `IRenderContext`(+default `TryGetFeature<T>`), `IPageContext`, `ISiteContext`, `IInlineMarkup`. Base classes `CmsPageBase`, `CmsComponentBase`(nullable context so UiUx Tooltip works), `CmsThemeBase`(single `RenderFragment Body`, NO zones). Attributes `[CmsPage]`/`[CmsComponent]`/`[CmsTheme]` with string key + optional DisplayName/SettingsModel/EditComponent. `[assembly:IdeaSdkVersion("1.0.0")]`+`Sdk.Version`. Records `ContentDescriptor`(D5 superset minus deferred fields)+`RawContentBundle`. Interfaces `ICmsContentSource`, `ITypeResolver`, `IPackageAssetSource`, `IRawContentGate`. `SharedContracts.DeferToDefaultPrefixes`. Add Roslyn PublicApiAnalyzers + PublicAPI.Shipped.txt seeded from this surface; add naming test (no `^Idea`/unprefixed base names) and enum-integer-stability test.

- P0.2 â€” Create `src/MindAttic.Ideas.Core` (net10.0): EF entities on the IContentEntity reserved-column contract (int Id IDENTITY PK, Guid Uid unique, string Key upsert-authority, byte[] RowVersion, IsDeleted+DeletedUtc, audit, Extra JSON, SourcePackageId) â€” `Site`, `Page`(PageKind discriminator; Data columns BodyHtml/PageCss/PageJs/BodyTrust/AuthoredByUserId/AuthorTrustVersion; Code columns ComponentTypeName/AssemblyName/SettingsJson; shared ThemeKey/Slug/ParentId/IsPublished/SortOrder/SeoMetaJson), `Theme`, `Component`, `CmsContentDefinition`(catalog: UNIQUE(Kind,Key,Origin),Priority,IsShadowed,IsActive,RawBundleJson,AssetMount), `InstalledPackage`(reserved/unwritten; UNIQUE(Category,Key,Version)), `Asset`(Blob-backed), `SettingEntry`(Host/Site/Page override chain; global CSS at Host/css.global), and PORTED auth `User`/`Role`/`UserRole`/`PagePermission`/`ComponentPermission`. `CmsDbContext` with all locked indexes/FKs/filters (UNIQUE(SiteId,Slug), NoAction self-FK on ParentId, SetNull on ThemeKey FK, soft-delete query filters). ONE initial migration with every column present (append-only thereafter).

- P0.3 â€” Port VERBATIM from MindAttic.Frontpage into Core: AuthService (BCrypt, SecurityStamp, SeedUser, MustChangePassword), UserRepository, the idempotent upsert-by-natural-key SeedService pattern (never clobber admin-edited BodyHtml), IDbContextFactory usage. Implement `CompiledContentSource : ICmsContentSource`(Origin=Compiled,Priority=100; reflect referenced assemblies for [CmsPage]/[CmsComponent]/[CmsTheme]; emit ContentDescriptor with ClrTypeName+AssemblyName), `DefaultTypeResolver : ITypeResolver`(Type.GetType over default ALC; null->placeholder), the discovery-upsert service (max-Priority winner, IsShadowed losers, IsActive=false on stale, never delete), and `IContentCatalog` (Find by (Kind,Key); (Kind,Key) uniqueness of the winner). Implement `IRawContentGate`(Ganss.Xss for Untrusted, raw for Author) as the sole MarkupString chokepoint + analyzer banning raw MarkupString elsewhere.

- P0.4 â€” Implement the built-in `FreeFormPage : CmsPageBase` (RawMarkup renderer for Data pages) + `ComponentMarkupExpander` (AngleSharp tokenization, NOT regex: rewrite `<ma-component key=... attrs>` with inner content->ChildContent into DynamicComponent against the catalog; unknown key->CmsMissingContent; literal markup -> gate.Emit honoring BodyTrust). Build `MissingPageHost`/`CmsMissingContent` placeholders.

- P1.1 â€” Build `src/MindAttic.Ideas.Web` (Blazor Web App, global InteractiveServer). Wire Program.cs by PORTING Frontend's spine: cookie auth + OnValidatePrincipal SecurityStamp revalidation + AddPolicy('Admin',RequireRole) + AddPolicy('AuthorRawMarkup',RequireClaim) + rate limiter + forwarded headers + HSTS + MapStaticAssets + AddInteractiveServerRenderMode().AddAdditionalAssemblies(citizen RCLs). On startup: db.Migrate(); run discovery upsert; idempotent seed of one Site + one bootstrap fallback Theme + admin user.

- P1.2 â€” Build `CmsHead` (emit CSS strictly by ordinal: Global 0 / Theme 100 / Page 200 / inline 300, with reserved gaps) and the single render path: catch-all `PageHost.razor @page "/{*slug}"` mapped AFTER reserved `/admin`(RequireAuthorization Admin) and `/_*` (incl. the `/_ideas/{key}/{version}/{**path}` CompositeFileProvider route with a NULL runtime arm in Phase 1) and the defense-in-depth reserved-prefix 404 guard. Render: resolve Page by (SiteId,Slug) -> resolve Theme -> emit CmsHead in cascade order -> wrap DynamicComponent(theme.LayoutType) around the body via the render fork (Strategy=RawMarkup => FreeFormPage; Strategy=ClrType => DynamicComponent(resolvedType); unresolved => CmsMissingContent). Add CI tests: head cascade order; malformed `<ma-component>` degrades to placeholder; ALC cast-identity (CmsPageBase by reference).

- P1.3 â€” Author the Cyberspace citizens whose asset URLs MIRROR (and are CI-verified against) UiUx `Themes/Cyberspace/deps.json` at pinned tag V4: `[CmsTheme("cyberspace")] CyberspaceTheme`(GlobalCssUrls=outfit-font+attic-font, ThemeCssUrls=back-home-m+frontpage.css per deps.json css[], ScriptUrls=loader/tv-static/home-bg/sacred-geometry/console-bg per deps.json scripts[], reproduce the circuitboard PNG inline-setter + body-prelude) and a tiny CyberspaceLayout.razor (chrome + ONE @Body, no zones). `[CmsComponent("ui.tooltip")] TooltipComponent` whose body delegates to a synced copy of the UNCHANGED UiUx Tooltip.razor wrapper, StylesheetUrls/ScriptUrls = jsDelivr tooltip.css/tooltip.js. Plus the bootstrap fallback Theme so the host renders before any package.

- P1.4 â€” Prove end-to-end: seed one Data home Page (BodyHtml containing an `<ma-component key="ui.tooltip">`, Trust=Author, ThemeKey=cyberspace) and render it through PageHost validating GLOBAL->THEME->PAGE cascade, the ma-component expansion, and author-trusted raw emit. This is the zero-deploy path the Frontend collapse leaf pages use.

- P1.5 (Frontend collapse, pending the open-question answer) â€” Code path: `[CmsPage("frontend.root")] FrontendRootPage : CmsPageBase` in a referenced RCL porting the interactive accordion/hub (full C#/EF/InteractiveServer), seeded as Page{Slug="",PageKind=Code,ComponentTypeName=...,ThemeKey=cyberspace}. Data path: leaf landing pages (portfolio/software/books) ported from the Frontend SeedService's projects.json/books.json into Page{PageKind=Data,BodyHtml=<author html>,Trust=Author,ThemeKey=cyberspace} via the SAME idempotent upsert-by-slug seeder (never clobbers admin edits). Validates BOTH render halves in one migration. (DEFER all interactivity to pure Data pages only if the user chooses zero-deploy purity over type-safety.)

- P2+ (designed-for, deferred, additive only) â€” Admin UI (page CRUD on the existing render path in Edit mode); then Phase 5 `PackageContentSource`(collectible ALC + AssemblyDependencyResolver honoring DeferToDefaultPrefixes) + real `PackageAssetSource`(fills the /_ideas runtime arm) + the .idea install/upgrade/uninstall pipeline writing InstalledPackage + the SDK packer (excludes host-provided assemblies). Catalog, descriptor, renderer, route table, and EF schema are written ONCE in P0/P1 and NEVER touched when Phase 5 drops in as just another ICmsContentSource.


# Appendix C — Open Questions (for ratification)

- FRONTEND COLLAPSE PATH (the single most consequential product decision; all six decisions independently surface this): for the MindAttic.Frontpage migration, ship the interactive accordion/hub root as ONE compiled Code page (CmsPageBase subclass â€” type-safe, debuggable, full C#/EF interactivity, but its CODE changes require a redeploy) with the leaf landing pages (portfolio/software/books) as zero-deploy Data pages â€” RECOMMENDED and exercises both render halves â€” OR reconstruct the ENTIRE frontend as pure Data pages with <ma-component> includes so nothing about the collapse ever needs a build (absolute zero-deploy purity, at the cost of re-implementing the accordion/card/diagram rendering as data templates and losing compiled interactivity)? The foundation supports both and a page can graduate Data<->Code as a row edit, so this is purely which path the flagship migration takes first.

- MULTI-TENANCY GRAIN: should each project frontend (MindAttic.Frontpage, MindAttic.Legion.Frontend) become its own Site resolved by host header (true multi-tenant), or a Page-subtree within ONE default Site (the original plan's lean toward subtree, Sites reserved for genuinely separate domains)? The schema (nullable SiteId, UNIQUE(SiteId,Slug), host->SiteId resolver) supports both unchanged, but it changes how Phase 1 seeds and routes the first Site, so confirm before seeding.

- .IDEA UPLOAD COLLISION DEFAULT: when an admin later uploads an .idea whose (Kind,Key) collides with a compiled citizen, should the upload be (A) hard-refused with a logged conflict â€” compiled always authoritative, simplest/safest â€” or (B) accepted-but-shadowed and promoted to live winner only after explicit admin confirmation via the manifest's allowOverride flag (enables hotfix-by-upload)? The IsShadowed/AllowOverride/Priority fields are already locked to support both; this is only which default ships. (A Phase-5 concern but the default informs the install UX.)

- AUTHOR-DEMOTION POLICY: when an admin who authored a raw (Trust=Author) Data page is later demoted / loses the Cms.AuthorRawMarkup claim, should their already-published raw HTML/JS keep rendering as-is (trust stamped at write time â€” the locked default, no surprise change to live pages) or be re-gated as Untrusted/sanitized on next render (safer, but can silently break live pages)? The AuthorTrustVersion epoch already supports a bulk re-gate without a schema change; confirm the default or choose epoch-bump-on-demotion.


# Appendix D — Conflicts found & how they were resolved

**Conflict 1:** NAMING OF INHERITANCE ROOTS â€” three incompatible name sets across the six decisions. D1/D3/D4 use `PageBase`/`ComponentBase`(or `CmsComponentBase`)/`ThemeBase`; D2 mandates `PageBase`/`ComponentBase`/`ThemeBase` verbatim (including a class literally named `ComponentBase` that namespace-shadows Microsoft's); D5 uses `CmsPageBase`/`CmsComponentBase`/`CmsThemeBase`; D6 uses `PageBase`/`ThemeBase`/`CmsComponentBase`. The user (Directive D) named them PageBase/ComponentBase/ThemeBase but a public type literally called `ComponentBase` in a Razor assembly is a footgun. All six cannot ship; one set must win for the SDK every package author compiles against.

**Resolved:** INHERITANCE ROOTS â€” RESOLVED to namespaced `CmsPageBase` / `CmsComponentBase` / `CmsThemeBase` in namespace `MindAttic.Ideas.Abstractions` (D5's set wins). Rationale honoring Directive D: the user's intent is 'a page inherits a page base, a component a component base, a theme a theme base' â€” the `Cms` prefix preserves that 1:1 semantic exactly while eliminating D2's genuine footgun of a public type literally named `ComponentBase` colliding with Microsoft.AspNetCore.Components.ComponentBase (which would force every author to write `using` aliases forever). The user's directive is about the CONCEPT mapping, not the literal token; `CmsPageBase` satisfies it without a permanent ergonomic tax. This is the one decision where I override two decisions' literal naming for a non-negotiable longevity reason.

**Conflict 2:** RENDER-CONTEXT TYPE â€” irreconcilable shapes. D1 ships a SEALED CLASS `CmsContext` (init-only, append-only) passed as a `[Parameter] Cms`. D2 ships an INTERFACE `IRenderContext` (+ `IPageContext`/`ISiteContext`/`IInlineMarkup`) delivered as a `[CascadingParameter]`, argued as the ONLY ALC-safe shape. D5 ships a sealed class `CmsRenderContext` as `[Parameter] Context`. D6 ships a sealed class `PageRenderContext` as `[Parameter] Ctx`. Class-vs-interface and Parameter-vs-CascadingParameter are mutually exclusive on the wire; every package binds to exactly one.

**Resolved:** RENDER CONTEXT â€” RESOLVED to D2's INTERFACE shape `IRenderContext` delivered as a `[CascadingParameter]`, NOT a sealed class as a `[Parameter]`. Decisive reason: this is the single most-load-bearing forward-compat choice in the whole foundation. A sealed class passed by `[Parameter]` cannot grow members safely across a collectible-ALC boundary in Phase 5 (the package compiled against v1 of the class and the host running v2 unify by reference identity, but added members on a class are a recompile risk and `[Parameter]` widening invites breakage); an interface grows append-only via default methods and unifies cleanly when deferred to the default ALC. CascadingParameter also lets the context-free UiUx Tooltip wrapper ignore it entirely (nullable on CmsComponentBase). We collapse D1's flat `CmsContext`, D5's `CmsRenderContext`, D6's `PageRenderContext` into `IRenderContext` carrying nested `IPageContext`/`ISiteContext`/`IInlineMarkup`. Trust line lives on `IInlineMarkup.Trusted` (D2).

**Conflict 3:** RENDERKIND/PAGEKIND ENUM NAME & VALUES â€” D1 `RenderKind{Data=0,Code=1}`; D4/D6 `PageKind{Data=0,Code=1}`; D2 dissolves the discriminator entirely (data vs code are both just PageDescriptors, no enum); D3 uses a manifest string `kind:data|code` (lowercase) plus a separate `category`. Same concept, four spellings â€” the EF discriminator column can have only one name/shape.

**Resolved:** DISCRIMINATOR â€” RESOLVED to a single EF enum `PageKind{Data=0,Code=1}` (D4/D6 name wins over D1's `RenderKind` because 'PageKind' reads as a property of a Page row, and `RenderKind`/`RenderMode` are too easily confused). D2's 'no discriminator' is rejected: an explicit stored discriminator makes the renderer branch, admin badge, and validator self-documenting (D3's exact argument for explicit `kind`). The manifest uses the same lowercase `kind:"data"|"code"` string (D3). One concept, one name everywhere: enum `PageKind` in C#, string `kind` on the wire.

**Conflict 4:** PAGE BODY COLUMN NAMES â€” the Data-page free-form HTML/CSS/JS columns are named differently in every data-model decision. D1: `BodyHtml`/`PageCss`/`PageJs`. D4: `BodyMarkup`/`PageCss`/`PageScript` (+ `ComponentReferencesJson`). D6: `BodyHtml`/`PageCss`/`PageInlineCss`/`PageScript` (splits page CSS into TWO columns at ordinals 200 and 300). A single Page table can have only one column set; D6's split-CSS vs D1/D4's single PageCss is a real schema fork.

**Resolved:** PAGE BODY COLUMNS â€” RESOLVED to D1/D4's SINGLE page-CSS column. Columns frozen as: `BodyHtml` (free-form author HTML, may contain `<ma-component>` includes), `PageCss` (the entire page-level stylesheet, cascade tier 3), `PageJs` (intentional author JS). D6's split into PageCss(200)+PageInlineCss(300) is rejected as an over-distinction: inline `style=""` attributes inside BodyHtml already win by CSS nature (they are tier 4 by DOM position, needing no column), so a separate PageInlineCss column buys nothing and forks the schema. We KEEP D6's reserved-ordinal-gap idea for the EMIT order (so a future component-scoped tier can slot at 250 without renumbering) but that is renderer logic, not columns.

**Conflict 5:** TRUST MODEL â€” three different mechanisms for the same Directive-F trust line. D1: `AuthorTrust` enum stored per page, set 'by writer's role'. D4: a `bool TrustedAuthorContent` column. D6: `ContentTrust` enum stamped at WRITE time from a `Cms.AuthorRawMarkup` CLAIM, PLUS `AuthoredByUserId`+`AuthorTrustVersion` epoch columns, PLUS per-response CSP nonce. D2: a `bool Trusted` on `IInlineMarkup` (data, host-enforced). D5: `RawContentTrust{Author,Untrusted}` on the bundle. The 'who/when' semantics (role-at-render vs claim-at-write-time-with-epoch) materially differ and only one can be the stored contract.

**Resolved:** TRUST MODEL â€” RESOLVED to D6's claim-at-write-time-with-epoch as the SEMANTICS, stored via D1's enum NAME. Frozen: a `ContentTrust{Untrusted=0,Author=1}` enum column on Page (`BodyTrust`), STAMPED at save time from whether the writer holds the `Cms.AuthorRawMarkup` claim (Admin role), PLUS `AuthoredByUserId` and `AuthorTrustVersion` columns (D6's epoch, so demoting an author is a policy decision not a silent re-render). D1's 'set by writer's role at render' is rejected (re-evaluating trust at render time means demoting an admin silently changes published output â€” a stability hazard). D4's bare bool is rejected (loses the who/when audit and the epoch escape hatch). D2's `IInlineMarkup.Trusted` is the RENDER-TIME projection of this stored `BodyTrust`. The single sanitize chokepoint is D6's `IRawContentGate` (Ganss.Xss for Untrusted, raw passthrough for Author).

**Conflict 6:** COMPOSITION / COMPONENT-PLACEMENT MECHANISM â€” D1 mandates a lexical `<ma-component key=...>` custom-element include tag expanded by an AngleSharp tokenizer in BodyHtml (NO placement rows). D4 stores an ordered `ComponentReferencesJson` list `[{key,sortOrder,settingsJson}]` on the Page (a parallel placement channel). D6 mentions 'ordered Component references' but renders via DynamicComponent. These are two DIFFERENT placement models (inline tag in markup vs a JSON sidecar list); shipping both invites drift over which is authoritative.

**Resolved:** COMPOSITION â€” RESOLVED to D1's lexical `<ma-component key=...>` include tag as the SOLE placement mechanism; D4's parallel `ComponentReferencesJson` sidecar column is DROPPED. Rationale: the free-form directive (B) means the author places the component WHERE they want in their markup â€” a sidecar JSON list reintroduces a separate ordering/placement channel that can drift from the markup (the exact zone-drift failure class Directive A rejects). One authoritative source: the `<ma-component>` tag inside BodyHtml, expanded by an AngleSharp tokenizer (D1, never regex) into `DynamicComponent`. Settings ride as tag attributes (and reserved `ChildContent` for inner content). If relational querying of component usage is ever needed, it is an ADDITIVE derived index built from parsing BodyHtml, never an authoritative second channel.

**Conflict 7:** DISCOVERY-SOURCE INTERFACE â€” name and async-ness differ. D1: `IContentSource.Discover()` (sync) yielding `ContentDescriptor`. D2: `IContentSource{Name; Discover()}` (sync) + a separate `IContentCatalog`. D5: `ICmsContentSource{Origin;Priority;DiscoverAsync()}` (ASYNC `IAsyncEnumerable`) + `ITypeResolver` + `IPackageAssetSource`. D3: `IIdeaSource.Discover()`. Sync vs async and the presence/absence of the `ITypeResolver` seam are a real contract difference for the one provider seam Phase 5 plugs into.

**Resolved:** DISCOVERY SOURCE â€” RESOLVED to D5's `ICmsContentSource` with `Origin`, `Priority`, and SYNC `Discover()` (D1/D2 sync wins; Phase-1 reflection is synchronous and async buys nothing, while `IAsyncEnumerable` complicates the seam), PLUS D5's separate `ITypeResolver` seam (the explicit rejection of an on-descriptor `Func<Type>` is correct â€” it keeps catalog rows rehydratable and isolates all ALC churn in one class). The catalog is D5's persisted `CmsContentDefinition` with `UNIQUE(Kind,Key,Origin)`, `Priority`, `IsShadowed`, `IsActive`. Compiled wins (Priority=100) over Package (50); admin-confirmed `AllowOverride` is the only shadow-promotion path. D5's `IPackageAssetSource` seam is reserved (Phase-1 stub returns empty).

**Conflict 8:** CONTENTDESCRIPTOR SHAPE & TYPE-RESOLUTION STRATEGY â€” D1 descriptor carries `ClrTypeName`+`AssemblyName`+`AssetUrls`. D2 adds `Category`,`RenderMode`,`Scope`,`SettingsModelTypeName`,`EditComponentTypeName`,`RegionNames`,`Extra`. D5 adds `Origin`,`Priority`,`Strategy{ClrType|RawMarkup}`,`AllowOverride`,`RawContentBundle`,`AssetMount` and resolves type LATE via a separate `ITypeResolver` (explicitly rejecting an on-descriptor `Func<Type>`). The descriptor is the uniform record every source yields; the fields and the resolution seam must be one shape.

**Resolved:** CONTENTDESCRIPTOR â€” RESOLVED to D5's record as the superset (it alone carries `Origin`+`Priority`+`Strategy{ClrType|RawMarkup}`+`AllowOverride`+`RawContentBundle`+`AssetMount`, which the persisted-catalog and precedence model require), MINUS D2's speculative `RegionNames`/`SettingsModelTypeName`/`EditComponentTypeName` which are deferred (additive via the reserved `Extra` bag or new init props later, per D3's YAGNI). `RenderStrategy` resolves the data-vs-code-vs-rawfiles fork cleanly: ClrType => DynamicComponent (covers Code pages AND compiled Components AND Themes), RawMarkup => the built-in `FreeFormPage`/`RawMarkupRenderer` (covers Data pages AND pure-CDN UiUx components). Type is resolved LATE via `ITypeResolver`, never stored as identity.

**Conflict 9:** CATALOG EF ENTITY â€” D1 `ContentRegistration` (Kind/Key/ClrTypeName/Source/Enabled). D4 has NO catalog row (compiled types reflected live; only `Component`/`Theme`/`Page` content tables). D5 `CmsContentDefinition` with `UNIQUE(Kind,Key,Origin)`, `Priority`, `IsShadowed`, `IsActive`, `RawBundleJson`, `AssetMount`. D2 keeps the catalog purely in-memory (`IContentCatalog`). Whether the catalog is persisted, and with what collision/precedence columns, is unresolved across decisions.

**Resolved:** .IDEA MANIFEST â€” RESOLVED to D3's two-axis kernel as the frozen wire format, because D3 is the decision that OWNS the package format and its reasoning (category=WHAT, kind=HOW; two independent version axes manifestVersion vs sdk; ignore-unknown via JsonExtensionData) is the most rigorously forward-compatible. Frozen six-field kernel `{manifestVersion, category, kind, key, version, displayName}` + well-known optional `{sdk, entryType, renderMode, css[], scripts[], assets, dependsOn[], uiux[]}` + category sub-blocks. We MAP the other decisions onto it: D1's `clrType`->`entryType`, D1/D5's `assets`/`assetsCss`/`assetsJs` -> ordered `css[]`/`scripts[]` (D3, honoring the cascade), D5's `trust`/`allowOverride` -> retained optional fields. Lowercase `kind:data|code` (D3) is the canonical discriminator string. `JsonExtensionData Extra` + `UnmappedMemberHandling.Skip` are mandatory from day one.

**Conflict 10:** .IDEA MANIFEST SCHEMA â€” three field sets. D1: `{schema, kind, key, displayName, clrType, assets[]}`. D2: `{key, kind, displayName, version, sdk, entryType, renderMode, assets[], regionNames[], scope}`. D3: a SIX-FIELD kernel `{manifestVersion, category, kind, key, version, displayName}` with `category(Page|Theme|Component)` AND `kind(data|code)` as TWO orthogonal discriminators, plus `sdk`,`entryType`,`css[]`,`scripts[]`,`assets`,`dependsOn`,`uiux[]`, category sub-blocks, `[JsonExtensionData]`. D5: `{kind, key, displayName, version, sdk, entryType, renderMode, trust, allowOverride, assetsCss[], assetsJs[]}`. D6: `{kind, key, version, renderMode}`. The ZIP's manifest is a frozen wire format; the field names, the one-discriminator-vs-two (D3's category+kind) question, and the css ordering arrays must be reconciled to ONE schema.

**Resolved:** INSTALLED-PACKAGE REGISTRY â€” RESOLVED to one entity `InstalledPackage` carrying D3's richer column set (it correctly separates `Category` from `Kind` and adds `ManifestVersion`+`IsActiveVersion` for side-by-side upgrade): `{Id, Uid, Category, Kind, Key, Version, DisplayName, ManifestJson(verbatim), BlobPath, Sha256, ManifestVersion, Enabled, IsActiveVersion, InstalledUtc, RowVersion}` with `UNIQUE(Category,Key,Version)`. ManifestJson stored VERBATIM so unknown `Extra` fields round-trip losslessly. Reserved-but-unwritten until Phase 5 (D1/D5).

**Conflict 11:** INSTALLED-PACKAGE REGISTRY ENTITY â€” D1 `InstalledPackage{Key,Kind,Version,BlobPath,ManifestJson,Sha256}`. D3 `InstalledIdea{Category,Kind,Key,Version,DisplayName,ManifestJson,BlobPath,Sha256,ManifestVersion,IsActiveVersion}` with `UNIQUE(Category,Key,Version)`. D4 `InstalledPackage{PackageKey,Version,Kind,Sha256,BlobPath,ManifestJson}`. D5 `InstalledPackage{Kind,Key,Version,BlobPath,ManifestJson,Sha256,Enabled}`. Same table, four names/keysets â€” needs one canonical entity + unique index.

**Resolved:** ASSET ROUTE â€” RESOLVED to ONE frozen runtime route `/_ideas/{key}/{version}/{**path}` (D3's shape wins: it omits the `{kind}` segment D5 included, since (key,version) is already unique across all kinds and the extra segment is dead weight). Backed by a `CompositeFileProvider` (D5) whose runtime arm is a `PhysicalFileProvider` over extracted package wwwroot (D3); Phase-1 stub arm returns nothing. Cache-Control immutable. Descriptor `AssetMount` exposes this base so markup references assets identically regardless of origin. Compiled-RCL assets continue to use build-time `_content/{Assembly}/` via MapStaticAssets. D1's 'opaque hrefs may be CDN or host-relative' is PRESERVED as the asset-URL semantics layered on top â€” UiUx assets stay absolute jsDelivr URLs; only true uploaded package assets use `/_ideas/...`.

**Conflict 12:** ASSET URL / DELIVERY MODEL â€” D1 says AssetUrls are opaque hrefs that may be jsDelivr CDN OR host-relative (`_content`/`_packages`). D3 LOCKS a stable runtime route `/_ideas/{key}/{version}/{*path}` (immutable cache). D5 LOCKS `/_pkg/{kind}/{key}/{version}/{**path}` via a CompositeFileProvider. Two different reserved runtime asset route shapes, both claimed frozen-forever â€” they collide.

**Resolved:** CSS CASCADE â€” RESOLVED to four code-enforced emit tiers by integer ordinal with reserved gaps (D6's ordinal mechanism), sourcing each tier as: tier 0 GLOBAL = `SettingEntry(Scope=Host,Key='css.global')` (D4 â€” global CSS is a host setting, editable without deploy); tier 100 THEME = `CmsThemeBase.GlobalCssUrls` then `ThemeCssUrls` MIRRORED from deps.json; tier 200 PAGE = `Page.PageCss` in `<style>`; tier 300+DOM = inline `style=""` in BodyHtml (last by CSS nature, no column). Order lives in EXACTLY ONE place (the `CmsHead` component) with a CI test asserting the ordering. Reserved gaps (e.g. 250) allow a future component-scoped tier additively. This unifies D1's 4 steps, D4's storage locations, and D6's ordinals.

**Conflict 13:** CSS CASCADE TIER COUNT & SOURCE OF GLOBAL CSS â€” Directive C fixes Global->Theme->Page->inline. D1: 4 emit steps, global = 'host-owned global app css'. D4: global CSS lives in a `SettingEntry(Host,null,'css.global')` row; theme CSS from `Theme.Css`/`DepsJson`. D6: integer ordinals Global=0/Theme=100/Page=200/inline=300 with reserved gaps and SPLITS page into PageCss(200)+PageInlineCss(300). D2 calls inline the page's `IInlineMarkup` (tier 3) with no explicit global tier owner. Where global CSS is stored and whether page-CSS is one tier or two is unresolved.

**Resolved:** RENDER-MODE ENUM â€” RESOLVED to `CmsRenderMode{Static=0, InteractiveServer=1}` with WASM EXCLUDED, but with EXPLICIT integer values and the documented rule that new modes append at the end (a softening of D5/D6's 'never' that keeps D2's forward-compat discipline WITHOUT a misleading reserved WASM slot). Rationale: a runtime-loaded .idea assembly genuinely cannot reach the browser WASM runtime (hard .NET boundary), so reserving an `InteractiveWebAssembly` value (D2) would let the SDK contract advertise something the package loader can never honor â€” that is worse than omitting it. If Blazor ever makes WASM runtime-loadable, appending a new enum member is purely additive. So: D5/D6's omission wins on substance; D2's append-only discipline wins on form.

**Conflict 14:** RENDER-MODE ENUM â€” D2 `ContentRenderMode{Static=0,InteractiveServer=1,(2 reserved),InteractiveWebAssembly=3}` (explicitly RESERVES a WASM value). D5/D6 `RenderMode{Static,InteractiveServer}` with WASM EXCLUDED FOREVER from the enum as a deliberate forward-compat guarantee. D1 doesn't expose a render-mode enum at all. A reserved-WASM-slot vs no-WASM-ever is a direct contradiction about a frozen enum.

**Resolved:** SETTINGS MACHINERY â€” RESOLVED to D3's YAGNI: the frozen v1 surface carries ONLY `RawSettingsJson`+`GetSettings<T>()` on IRenderContext and an optional `SettingsModel`/`EditComponent` Type hint on the discovery attributes (kept because they cost nothing and D2 wants them), but NO settingsSchema engine, NO auto-form generator, NO dependency-version-range resolver in the foundation. Rich settings editors and JSON-schema admin forms are additive later via the reserved `Extra` bag and new EF columns. This keeps Abstractions microscopic (the strongest never-change guarantee).

**Conflict 15:** SETTINGS-TASTE OF THE FOUNDATION â€” D2 ships rich attribute fields (`SettingsModel`,`EditComponent`,`Category`,`Scope`) and `PageBase<TSettings>` generic bases; D3 explicitly REJECTS `settingsSchema` for v1 (YAGNI, additive later via JsonExtensionData); D1 keeps attributes minimal (`key`,`DisplayName` only). How much settings machinery is in the frozen v1 surface is contested.

**Resolved:** JSDELIVR ASSETS â€” RESOLVED: the CyberspaceTheme and TooltipComponent citizen asset lists MUST be generated FROM (or mechanically verified AGAINST) the actual UiUx `Themes/Cyberspace/deps.json` at the pinned tag, never hand-typed. Frozen rule: deps.json is the source of truth; the CMS theme's `GlobalCssUrls`/`ThemeCssUrls`/`ScriptUrls` mirror its css[]/scripts[] arrays verbatim, and the circuitboard PNG inline-setter pattern (window.__cyberspaceCircuitboardSrcs) is reproduced. Pin a SPECIFIC tag (`V4` per UiUx CLAUDE.md — whole-number scheme), never @latest. A Phase-1 test fetches deps.json at the pinned tag and asserts the CMS theme's URL lists match â€” so a UiUx tag bump that changes the bundle fails CI until the CMS theme is re-synced. This converts D1's hand-typed (and already-wrong) list into a verified mirror.

**Conflict 16:** JSDELIVR PINNED TAG MISMATCH (ground-truth) â€” D1's CyberspaceTheme/TooltipComponent hardcode `@v1.0.1` AND list `Themes/Cyberspace/theme.css` + `Components/Cyberspace/frontpage.css` in ThemeCssUrls, but the actual UiUx `Themes/Cyberspace/deps.json` css[] array does NOT include `theme.css` (it lists outfit-font, attic-font, back-home-m, frontpage.css) and uses different script names (tv-static.js, home-bg.js, sacred-geometry.js) than D1's list (loader.js, console-bg.js). The CMS theme asset list must MIRROR deps.json, not a hand-typed approximation, or the Cyberspace look will silently differ.


# Appendix E — Per-decision LOCKED contracts (the exact frozen code)


## FOUNDATION-001: Free-form Page model: one Page catalog, two bodies (Data | Code), rendered by DynamicComponent-by-Type; lexical composition, no zones

**Decision:** A Page is ONE durable EF entity, `PageRecord`, discriminated by `RenderKind { Data, Code }`, and EVERY page renders through the single primitive `DynamicComponent(Type, parameters)` wrapped in the selected Theme's one `@Body` hole. A Data page (no deploy) stores free-form author HTML/CSS/JS in DB columns and renders through one built-in compiled `DataPageHost : PageBase`; a Code page (full interactive C#) names a `[CmsPage]`-attributed `PageBase` subclass resolved by type-name from the catalog. There are NO zones, panes, or slots: composition is lexical â€” the author writes free-form markup and drops `<ma-component key="ui.tooltip" .../>` include tags wherever they want, which a single markup expander rewrites into `DynamicComponent` calls against the Component registry. The MindAttic.Frontpage collapse uses the Code path for its interactive accordion root, and Data pages for its leaf landing content, exercising both halves in one migration.

```csharp
// ====== MindAttic.Ideas.Abstractions (net10.0; ONLY dep: Microsoft.AspNetCore.Components) ======
// FROZEN public surface. Evolve ONLY by adding init-only members to CmsContext / the manifest (schema-versioned).
namespace MindAttic.Ideas.Abstractions;

public enum ContentKind  { Page, Theme, Component }      // the three content kinds (NEVER "Idea")
public enum RenderKind   { Data = 0, Code = 1 }          // append-only; future e.g. RazorString = 2
public enum AuthorTrust  { Untrusted = 0, TrustedAdmin = 1 }   // the trust line, stored per page, set by writer's role

// The SINGLE evolving object handed to every citizen at render time. Add init-only members; never remove/rename/reorder.
public sealed class CmsContext {
    public required Guid    PageId   { get; init; }
    public required string  Slug     { get; init; }
    public required string  ThemeKey { get; init; }
    public required RenderKind  RenderKind { get; init; }
    public required AuthorTrust Trust      { get; init; }
    public required IServiceProvider Services { get; init; }      // scoped DI for the circuit
    public bool EditMode { get; init; }
    public IReadOnlyDictionary<string,string?> Attributes { get; init; } = Empty;  // tag attrs for a placed component
    public RenderFragment? ChildContent { get; init; }            // RESERVED: <ma-component>...inner...</ma-component>
    public IReadOnlyDictionary<string,object?> Extensions { get; init; } = EmptyObj; // RESERVED escape hatch
    private static readonly IReadOnlyDictionary<string,string?> Empty = new Dictionary<string,string?>();
    private static readonly IReadOnlyDictionary<string,object?> EmptyObj = new Dictionary<string,object?>();
}

// The three INHERITANCE ROOTS. Near-empty by design; no required ctor args; everything arrives via CmsContext.
public abstract class PageBase : ComponentBase {                  // Code pages inherit this
    [Parameter] public CmsContext Cms { get; set; } = default!;
    protected bool EditMode => Cms.EditMode;
}
public abstract class CmsComponentBase : ComponentBase {          // Components inherit this (name avoids Razor ComponentBase clash)
    [Parameter] public CmsContext Cms { get; set; } = default!;
    [Parameter(CaptureUnmatchedValues = true)] public IDictionary<string,object>? Attributes { get; set; }
    public virtual IReadOnlyList<string> StylesheetUrls => Array.Empty<string>();  // e.g. tooltip.css (jsDelivr/_content)
    public virtual IReadOnlyList<string> ScriptUrls     => Array.Empty<string>();  // e.g. tooltip.js
}
public abstract class ThemeBase {                                 // Themes inherit this (NOT a component)
    public abstract string Key { get; }
    public abstract Type   LayoutComponentType { get; }           // tiny .razor: chrome + ONE @Body hole, NO zones
    public virtual IReadOnlyList<string> GlobalCssUrls => Array.Empty<string>();   // cascade tier 1 source (host-owned global may precede)
    public virtual IReadOnlyList<string> ThemeCssUrls  => Array.Empty<string>();   // cascade tier 2
    public virtual IReadOnlyList<string> ScriptUrls    => Array.Empty<string>();
    public virtual string? BodyPreludeHtml => null;               // e.g. Cyberspace body-prelude.html
}

// Discovery: stable string KEY is the forever-id (survives type renames/moves).
[AttributeUsage(AttributeTargets.Class)] public sealed class CmsPageAttribute(string key)      : Attribute { public string Key => key; public string DisplayName { get; init; } = key; }
[AttributeUsage(AttributeTargets.Class)] public sealed class CmsComponentAttribute(string key) : Attribute { public string Key => key; public string DisplayName { get; init; } = key; }
[AttributeUsage(AttributeTargets.Class)] public sealed class CmsThemeAttribute(string key)     : Attribute { public string Key => key; public string DisplayName { get; init; } = key; }

// The ONE discovery seam. Phase-1 CompiledSource and Phase-5 PackageSource (ALC) both implement this -> same catalog, same renderer.
public interface IContentSource { IEnumerable<ContentDescriptor> Discover(); }
public sealed record ContentDescriptor(
    ContentKind Kind, string Key, string DisplayName,
    string? ClrTypeName, string AssemblyName,                     // null/empty TypeName => pure-data citizen
    IReadOnlyList<string> AssetUrls);                             // jsDelivr OR host-relative (_content//_packages) â€” opaque hrefs

// ====== MindAttic.Ideas.Core â€” EF entity: ONE Page table, both render kinds. NO Zone/Pane/Instance tables. ======
public sealed class PageRecord {
    public int    Id { get; set; }
    public int?   SiteId { get; set; }                 // multi-site is a nullable column now; unique index (SiteId,Slug)
    public int?   ParentId { get; set; }               // tree -> nav menu
    public string Slug { get; set; } = "";             // "" = home
    public string Title { get; set; } = "";
    public string? ThemeKey { get; set; }              // page override; else site/host default
    public RenderKind RenderKind { get; set; }         // Data | Code  <- the discriminator
    // --- Data page body (null for Code pages): the free-form author content ---
    public string? BodyHtml { get; set; }              // author HTML, may contain <ma-component .../> includes
    public string? PageCss  { get; set; }              // cascade tier 3 (per-page <style>)
    public string? PageJs   { get; set; }              // intentional author JS (emitted only when Trust=TrustedAdmin)
    public AuthorTrust Trust { get; set; }             // set at save by the role-gated editor
    // --- Code page body (null for Data pages) ---
    public string? ComponentTypeName { get; set; }     // resolved to Type via catalog
    public string? AssemblyName { get; set; }
    public string? SettingsJson { get; set; }
    public bool IsPublished { get; set; }
    public int  SortOrder { get; set; }
    public string? SeoMetaJson { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public byte[]? RowVersion { get; set; }            // optimistic concurrency (port Frontend pattern)
}
public sealed class ContentRegistration {              // upserted from IContentSource at boot; stale key -> placeholder, never crash
    public int Id { get; set; } public ContentKind Kind { get; set; } public string Key { get; set; } = "";
    public string DisplayName { get; set; } = ""; public string? ClrTypeName { get; set; } public string AssemblyName { get; set; } = "";
    public string AssetUrlsJson { get; set; } = "[]"; public string Source { get; set; } = "compiled"; public bool Enabled { get; set; } = true;
}
public sealed class InstalledPackage {                 // Phase-5 ALC registry; present + reserved, unused until then
    public int Id { get; set; } public string Key { get; set; } = ""; public ContentKind Kind { get; set; }
    public string Version { get; set; } = ""; public string BlobPath { get; set; } = ""; public string ManifestJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true; public DateTime InstalledUtc { get; set; } public string Sha256 { get; set; } = "";
}
// + PORT VERBATIM from MindAttic.Frontpage: User/Role (cookie + SecurityStamp OnValidatePrincipal + BCrypt),
//   idempotent upsert-by-natural-key SeedService, MediaAsset->Asset, SiteSetting->SettingEntry.

// ====== Render path (single, for BOTH kinds; NO zones). CmsPageHost.razor  @page "/{*slug}"  (reserve /admin and /_* FIRST) ======
//  1. Resolve PageRecord by (SiteId, Slug); null -> CMS 404 page (itself a PageRecord).
//  2. Resolve ThemeBase by ThemeKey (page override -> site default -> bootstrap fallback theme).
//  3. Emit <head> in FROZEN cascade order: (1) GLOBAL app css -> (2) theme.GlobalCssUrls then theme.ThemeCssUrls
//     -> (3) <style>{page.PageCss}</style> -> (4) inline style="" in BodyHtml (last by CSS nature). Order lives ONLY here.
//  4. bodyType = RenderKind switch { Data => typeof(DataPageHost), Code => Catalog.ResolveType(ComponentTypeName, AssemblyName) ?? typeof(MissingPageHost), _ => typeof(MissingPageHost) };
//  5. <DynamicComponent Type=theme.LayoutComponentType> wraps <DynamicComponent Type=bodyType Parameters=@(new(){["Cms"]=ctx})/>.
//  DataPageHost.OnParametersSet: html = Cms.Trust==TrustedAdmin ? page.BodyHtml : Sanitizer.Sanitize(page.BodyHtml /*Ganss.Xss*/);
//     then ComponentMarkupExpander.Expand(html, ctx): AngleSharp-tokenize, rewrite each <ma-component key=...> to
//     <DynamicComponent Type=Catalog[key] Parameters=tagAttrs(+ChildContent)>, unknown key -> <CmsMissingComponent/> (never throws),
//     remaining literal -> (MarkupString) verbatim-if-TrustedAdmin-else-sanitized.

// ====== UiUx citizenship WITHOUT a UiUx build (ZERO duplication) ======
[CmsComponent("ui.tooltip", DisplayName="Tooltip")]
public sealed class TooltipComponent : CmsComponentBase {        // body delegates to the UNCHANGED thin Tooltip.razor wrapper
    const string CDN = "https://cdn.jsdelivr.net/gh/mindattic/MindAttic.UiUx@V4/";
    public override IReadOnlyList<string> StylesheetUrls => new[]{ CDN+"Components/Tooltip/tooltip.css" };
    public override IReadOnlyList<string> ScriptUrls     => new[]{ CDN+"Components/Tooltip/tooltip.js" };
}
[CmsTheme("cyberspace", DisplayName="Cyberspace")]
public sealed class CyberspaceTheme : ThemeBase {               // URLs MIRROR the existing deps.json; UiUx stays source of truth
    public override string Key => "cyberspace";
    public override Type LayoutComponentType => typeof(CyberspaceLayout);   // tiny .razor: <div class="page">@Body</div> + prelude
    const string CDN = "https://cdn.jsdelivr.net/gh/mindattic/MindAttic.UiUx@V4/";
    public override IReadOnlyList<string> GlobalCssUrls => new[]{ CDN+"Components/OutfitFont/outfit-font.css", CDN+"Components/AtticFont/attic-font.css" };
    public override IReadOnlyList<string> ThemeCssUrls  => new[]{ CDN+"Components/Cyberspace/frontpage.css", CDN+"Themes/Cyberspace/theme.css" };
    public override IReadOnlyList<string> ScriptUrls    => new[]{ CDN+"Components/Cyberspace/loader.js", CDN+"Components/Cyberspace/console-bg.js" };
}

// ====== .idea package = JUST A ZIP ======
// tooltip.idea
//  â”” manifest.json  { "schema":1, "kind":"Component", "key":"ui.tooltip", "displayName":"Tooltip",
//                     "clrType":"", "assets":["<jsDelivr url>tooltip.css","<jsDelivr url>tooltip.js"] }
//  (Theme manifest adds "globalCss":[],"themeCss":[]; a code citizen adds "clrType" + lib/*.dll; data/code pages add "renderKind".)
//  Pure-CDN citizens carry NO dll and NO wwwroot. Loaders MUST ignore unknown manifest fields (forward-compat). NEVER ship Abstractions.dll.

// ====== MindAttic.Frontpage collapse (exact path) ======
[CmsPage("frontend.root", DisplayName="MindAttic Frontend")]
public sealed class FrontendRootPage : PageBase { /* port the existing interactive accordion/hub root; @inject its EF services */ }
//  -> PageRecord { Slug="", RenderKind=Code, ComponentTypeName="...FrontendRootPage", ThemeKey="cyberspace" }
//  Leaf landing pages (portfolio/software/books) -> PageRecord { RenderKind=Data, BodyHtml=<author html>, Trust=TrustedAdmin, ThemeKey="cyberspace" },
//  seeded by the SAME idempotent upsert-by-slug SeedService ported from Frontend (never clobber admin-edited BodyHtml).
```


## FND-ABSTRACTIONS-SDK: MindAttic.Ideas.Abstractions: the frozen v1 SDK surface â€” attribute-first discovery, one cascading context interface, string-key identity, additive-forever

**Decision:** Lock MindAttic.Ideas.Abstractions as a microscopic net10.0 assembly that references ONLY Microsoft.AspNetCore.Components and System.Text.Json â€” zero EF, zero web host, zero SQL, zero UiUx. Discovery is ATTRIBUTE-FIRST ([Page]/[Component]/[Theme] on any class), with PageBase/ComponentBase/ThemeBase shipped as OPTIONAL convenience bases (sugar over context + typed settings), so the existing UiUx wrappers register unchanged. Every render-time dependency arrives through ONE opaque interface, IRenderContext, delivered as a [CascadingParameter] (never ctor-injected), whose growth is additive via interface default methods. Identity is a stable string Key on every attribute and descriptor (never the CLR type name); the catalog resolves Key->Type at render and degrades to a placeholder on miss. A DATA page (free-form HTML/CSS/JS in the DB, rendered by the host's built-in FreeFormPage) and a CODE page (a compiled [Page] subclass) are BOTH PageDescriptors on the same DynamicComponent path; the MindAttic.Frontpage collapse uses DataPages for content plus exactly one compiled ProjectHub CodePage. MAJOR is pinned at 1 forever, additive-only, enforced by a CI public-API analyzer; a manifest sdk:N integer floor gated against an [assembly: IdeaSdkVersion] stamp governs cross-version package loading.

```csharp
// ============ MindAttic.Ideas.Abstractions (net10.0) â€” v1, MAJOR FROZEN FOREVER ============
// refs ONLY: Microsoft.AspNetCore.Components, System.Text.Json. NO EF/host/SQL/UiUx/sanitizer.
// APPEND-ONLY: new types, new init-only props, new interface DEFAULT methods, new enum members
// appended at the end with explicit ascending integers. NEVER remove/rename/reorder/retype/reaccess.
namespace MindAttic.Ideas.Abstractions;

[assembly: IdeaSdkVersion(1)] // host reads this to gate package loads
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class IdeaSdkVersionAttribute(int version) : Attribute { public int Version { get; } = version; }
public static class Sdk { public const int Version = 1; } // whole-number only, additive-forever

// ---- Discovery attributes: stable string Key IS the identity (never the type name) ----
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PageAttribute(string key) : Attribute {
    public string Key { get; } = key;                  // e.g. "frontend.projecthub"
    public string DisplayName { get; init; } = key;
    public string Category { get; init; } = "General";
    public Type? SettingsModel { get; init; }
    public Type? EditComponent { get; init; }
    public ContentRenderMode RenderMode { get; init; } = ContentRenderMode.InteractiveServer;
}
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ComponentAttribute(string key) : Attribute {
    public string Key { get; } = key;                  // e.g. "ui.tooltip"
    public string DisplayName { get; init; } = key;
    public string Category { get; init; } = "General";
    public ComponentScope Scope { get; init; } = ComponentScope.Placeable;
    public Type? SettingsModel { get; init; }
    public Type? EditComponent { get; init; }
}
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ThemeAttribute(string key) : Attribute {
    public string Key { get; } = key;                  // e.g. "cyberspace"
    public string DisplayName { get; init; } = key;
}

// ---- Enums: EXPLICIT integer values, append-only, never reorder/renumber ----
public enum ContentRenderMode { Static = 0, InteractiveServer = 1, /*2 reserved*/ InteractiveWebAssembly = 3 }
public enum ContentMode       { View = 0, Edit = 1, Preview = 2 }
public enum ComponentScope    { Placeable = 0, Global = 1 }
public enum ContentKind       { Page = 0, Component = 1, Theme = 2 }

// ---- The ONE opaque render context. Grows via interface DEFAULT members only. ----
public interface IRenderContext {
    Guid             InstanceId { get; }
    ContentMode      Mode       { get; }
    ContentRenderMode RenderMode { get; }
    IPageContext     Page       { get; }
    ISiteContext     Site       { get; }
    IServiceProvider Services   { get; }            // scoped DI for this circuit/request
    string?          RawSettingsJson { get; }
    T GetSettings<T>() where T : class, new();      // host owns the serializer (Core)
    // additive-forever escape hatch â€” never requires an interface change to add behavior:
    bool TryGetFeature<T>(out T feature) where T : class { feature = Services.GetService(typeof(T)) as T; return feature is not null; }
}
public interface IPageContext {
    Guid PageId { get; } string Slug { get; } string Title { get; } string? ThemeKey { get; }
    IInlineMarkup Inline { get; }                   // free-form author HTML/CSS/JS for THIS page
    IReadOnlyDictionary<string,string?> Meta { get; }
}
public interface ISiteContext {
    Guid SiteId { get; } string Key { get; } string Host { get; } string DefaultThemeKey { get; }
    string? GetSetting(string key);                 // override-chain read
}
public interface IInlineMarkup {                    // cascade tier 3 (after GLOBAL -> THEME)
    string? Html { get; } string? Css { get; } string? Js { get; }
    bool Trusted { get; }                           // THE trust line: true => host emits raw; false => sanitized
}

// ---- The three OPTIONAL convenience bases (inheriting is NEVER required) ----
public abstract class PageBase : Microsoft.AspNetCore.Components.ComponentBase {
    [Microsoft.AspNetCore.Components.CascadingParameter] protected IRenderContext Context { get; set; } = default!;
    protected bool EditMode => Context.Mode == ContentMode.Edit;
}
public abstract class PageBase<TSettings> : PageBase where TSettings : class, new() {
    protected TSettings Settings { get; private set; } = new();
    protected override void OnParametersSet() => Settings = Context.GetSettings<TSettings>();
}
public abstract class ComponentBase : Microsoft.AspNetCore.Components.ComponentBase {  // resolved by namespace
    [Microsoft.AspNetCore.Components.CascadingParameter] protected IRenderContext? Context { get; set; } // nullable: context-free components (Tooltip) work
}
public abstract class ComponentBase<TSettings> : ComponentBase where TSettings : class, new() {
    protected TSettings Settings { get; private set; } = new();
    protected override void OnParametersSet() => Settings = Context?.GetSettings<TSettings>() ?? new();
}
public abstract class ThemeBase : Microsoft.AspNetCore.Components.ComponentBase {
    [Microsoft.AspNetCore.Components.CascadingParameter] protected IRenderContext Context { get; set; } = default!;
    [Microsoft.AspNetCore.Components.Parameter] public Microsoft.AspNetCore.Components.RenderFragment? Body { get; set; } // page free-form markup renders here; NO zones/panes
    public virtual IReadOnlyList<string> RegionNames => Array.Empty<string>(); // free-form: a theme MAY declare optional regions, default none
}

// ---- Source / catalog seam: PackageContentSource (.idea) drops in later as just another source ----
public sealed record ContentDescriptor {
    public required string Key { get; init; }
    public required ContentKind Kind { get; init; }
    public required string DisplayName { get; init; }
    public required string TypeName { get; init; }       // resolvable CLR type name (Key->Type lives in catalog)
    public required string AssemblyName { get; init; }
    public string Category { get; init; } = "General";
    public ContentRenderMode RenderMode { get; init; } = ContentRenderMode.InteractiveServer;
    public ComponentScope Scope { get; init; } = ComponentScope.Placeable;
    public string? SettingsModelTypeName { get; init; }
    public string? EditComponentTypeName { get; init; }
    public IReadOnlyList<string>? RegionNames { get; init; }            // themes only
    public IReadOnlyDictionary<string,string>? Extra { get; init; }    // reserved extensibility bag
}
public interface IContentSource { string Name { get; } IEnumerable<ContentDescriptor> Discover(); }
public interface IContentCatalog {
    IReadOnlyCollection<ContentDescriptor> All { get; }
    ContentDescriptor? Find(ContentKind kind, string key);
    Type? ResolveType(ContentDescriptor descriptor);     // null => host renders MissingContent placeholder
}

// ===== idea.json manifest (lives in the zip; validated/enforced by Core, NOT by Abstractions) =====
// { "key":"frontend.projecthub", "kind":"Page|Component|Theme", "displayName":"Project Hub",
//   "version":1,                  // package's OWN whole-number version
//   "sdk":1,                      // FLOOR: host refuses if hostSdk < floor; accepts any floor <= hostSdk
//   "entryType":"Ns.ProjectHub", "renderMode":"InteractiveServer",
//   "assets":["projecthub.css"], "regionNames":[], "scope":"Placeable" }
//
// WHAT LIVES IN CORE (never Abstractions): all EF entities (Site, Page, Theme, ContentDefinition,
// ContentInstance, InstalledPackage, Asset, SettingEntry, User/Role/Permission), CmsDbContext,
// CompiledContentSource, the future PackageContentSource + collectible ALC, IContentCatalog impl,
// FreeFormPage (the built-in DATA-page renderer), the HTML sanitizer, JsonSerializerOptions, and the
// sdk-floor gate. Abstractions only EXPOSES Sdk.Version + IdeaSdkVersionAttribute.
```


## FND-001-IDEA-PACKAGE-FORMAT: .idea Package Format: a plain ZIP with a six-field manifest kernel, explicit category+kind discriminators, and two independent version axes

**Decision:** A .idea file is a plain ZIP whose only mandatory member is idea.json. The manifest has exactly six required kernel fields â€” manifestVersion, category, kind, key, version, displayName â€” and everything else is optional with documented defaults. category (Page|Theme|Component) is WHAT the content is; kind (data|code) is HOW it renders. A data package ships html/css/js/assets with NO bin/ and installs with zero build and zero recycle; a code package is the identical ZIP plus a bin/ folder loaded into a collectible AssemblyLoadContext. Both register as the same content row and render through the same DynamicComponent path, so a data Page and a code Page are interchangeable at the call site. Forward-compatibility is bought with two independent version axes and ignore-unknown deserialization, host-provided assemblies are forbidden in bin/, and assets serve forever from the immutable /_ideas/{key}/{version}/{*path} URL.

```csharp
// ====================== idea.json â€” FROZEN WIRE FORMAT (manifestVersion 1) ======================
// Plain JSON inside a plain ZIP. The six kernel fields NEVER move or change meaning across versions.
{
  // ---- REQUIRED KERNEL (frozen forever; an old host can always identify+route any package) ----
  "manifestVersion": 1,            // int, increment-only. Schema of THIS file. Host-gated. NOT content version.
  "category": "Page",              // "Page" | "Theme" | "Component". Unknown future value => ignore-with-warning.
  "kind": "data",                  // "data" (no DLL, dynamic render) | "code" (compiled, ALC). Unknown => reject.
  "key": "frontend.about",         // stable id, ^[a-z0-9][a-z0-9._-]{0,119}$. Immutable identity across versions.
  "version": 1,                    // whole-number CONTENT version. (key,version) is install identity + asset URL segment.
  "displayName": "About MindAttic",// human label. (kept required: every Manager row needs one)

  // ---- WELL-KNOWN OPTIONAL (safe defaults; absence is meaningful; append-only forever) ----
  "description": "",
  "sdk": 1,                        // integer FLOOR vs host Abstractions contract. REQUIRED if kind==code; OMIT if data.
  "entryType": null,               // REQUIRED if kind==code: AQN of PageBase|ThemeBase|ComponentBase subclass in bin/.
                                   //   "MindAttic.Pkg.About.AboutPage, MindAttic.Pkg.About". MUST be null/absent if data.
  "renderMode": "InteractiveServer",// "Static" | "InteractiveServer". code only. WASM is compile-time-only (never a pkg).
  "css":     [],                   // ordered paths under wwwroot/. Emitted in this content kind's cascade slot. (dir. C)
  "scripts": [],                   // ordered paths under wwwroot/, emitted as <script>. Page kind => trusted author JS.
  "assets":  null,                 // null => serve ALL of wwwroot/. non-null => explicit allow-list of paths.
  "dependsOn": [],                 // flat list { category, key } of packages that must already be installed. NO ranges v1.
  "uiux": [],                      // UiUx CDN bundles: { key, tag } pinned jsDelivr (consume UiUx with zero duplication).

  // ---- CATEGORY SUB-BLOCK: host reads ONLY the one matching `category`; all optional ----
  "page":      { "slug": "/about", "themeKey": "cyberspace", "html": "page.html" }, // data Page body source
  "theme":     { "preludeHtml": "body-prelude.html", "deps": "deps.json" },          // mirrors UiUx theme shape
  "component": { "scope": "Placeable" }                                             // "Global" | "Placeable"

  // ---- ANY OTHER FIELD: tolerated, round-tripped via JsonExtensionData, ignored by an old host. ----
}
// Minimum legal data Page (hand-written, 6 lines + page.html):
//   { "manifestVersion":1, "category":"Page", "kind":"data", "key":"about", "version":1, "displayName":"About" }

// ====================== ZIP LAYOUT (frozen; new top-level folders are additive & ignored by old hosts) ======================
//   <key>.idea  (a plain ZIP)
//    â”œâ”€ idea.json              REQUIRED (root)
//    â”œâ”€ wwwroot/               OPTIONAL. css/js/images/fonts -> served at /_ideas/{key}/{version}/...
//    â”‚   â”œâ”€ page.html page.css page.js   (data Page: author inline markup â€” TRUSTED, served verbatim)
//    â”‚   â””â”€ <assets...>
//    â”œâ”€ bin/                   OPTIONAL. kind==code ONLY. Entry assembly + ONLY non-host deps.
//    â”‚                         FORBIDDEN: MindAttic.Ideas.Abstractions.dll, Microsoft.AspNetCore.*,
//    â”‚                         Microsoft.Extensions.*, Microsoft.EntityFrameworkCore.*, Microsoft.JSInterop.*, System.*
//    â”œâ”€ data/                  OPTIONAL. idempotent seed.sql / migration for the package's own tables.
//    â””â”€ icon.png  README.md  LICENSE   OPTIONAL, never parsed by host.

// ====================== STABLE RUNTIME ASSET URL (LOCKED FOREVER) ======================
//   GET /_ideas/{key}/{version}/{*path}   ->  PhysicalFileProvider over extracted wwwroot/
//   Cache-Control: public,max-age=31536000,immutable   (versioned => two versions coexist during upgrade)

// ====================== HOST-SIDE C# CONTRACTS ======================
public enum IdeaCategory { Page, Theme, Component }   // behavior closed; parser tolerates unknown strings (skip+warn).
public enum IdeaKind     { Data, Code }               // unknown kind => hard reject (cannot place safely).

public sealed class IdeaManifest
{
    public required int    ManifestVersion { get; init; }   // host floor-checks vs HOST_MAX_MANIFEST_VERSION
    public required string Category        { get; init; }   // parsed to IdeaCategory
    public required string Kind            { get; init; }   // parsed to IdeaKind
    public required string Key             { get; init; }
    public required int    Version         { get; init; }   // whole number
    public required string DisplayName     { get; init; }
    public string?   Description { get; init; }
    public int?      Sdk         { get; init; }             // integer floor; null for data
    public string?   EntryType   { get; init; }             // AQN; null for data
    public string    RenderMode  { get; init; } = "Static";
    public string[]  Css         { get; init; } = [];
    public string[]  Scripts     { get; init; } = [];
    public string[]? Assets      { get; init; }             // null => all of wwwroot/
    public ManifestDependency[] DependsOn { get; init; } = [];
    public UiUxBundleRef[]      Uiux      { get; init; } = [];
    public PageBlock?      Page      { get; init; }
    public ThemeBlock?     Theme     { get; init; }
    public ComponentBlock? Component { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; } // v2+ fields preserved losslessly
}
public sealed record ManifestDependency(string Category, string Key);   // NO version range in v1 (append-only later)
public sealed record UiUxBundleRef(string Key, string Tag);
public sealed record PageBlock(string? Slug, string? ThemeKey, string? Html);
public sealed record ThemeBlock(string? PreludeHtml, string? Deps);
public sealed record ComponentBlock(string Scope);

// Forward-compat deserialization policy (LOCKED â€” enforce in code day one):
static readonly JsonSerializerOptions ManifestOptions = new() {
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip   // belt; JsonExtensionData = suspenders
};

// EF registry â€” ONE table, all categories+kinds. (key,version) immutable & content-addressed by Sha256.
public sealed class InstalledIdea
{
    public int Id { get; set; }
    public required string Category { get; set; }       // unique index: (Category, Key, Version)
    public required string Kind     { get; set; }
    public required string Key      { get; set; }
    public required string Version  { get; set; }
    public required string DisplayName { get; set; }
    public required string ManifestJson { get; set; }   // VERBATIM idea.json â€” re-derive typed cols on load; never lose Extra
    public required string BlobPath  { get; set; }      // Azure Blob = source of truth
    public required string Sha256    { get; set; }      // host-computed at install
    public int  ManifestVersion { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsActiveVersion { get; set; }           // upgrade flips this; old version served until recycle
    public DateTime InstalledUtc { get; set; }
}

public interface IPackageInstaller {
    Task<InstallResult>   InstallAsync(Stream ideaZip, CancellationToken ct);
    Task<InstallResult>   UpgradeAsync(string key, Stream newerZip, CancellationToken ct);   // side-by-side, flip active
    Task<UninstallResult> UninstallAsync(string key, string version, bool force, CancellationToken ct); // block if referenced
    IReadOnlyList<InstalledIdea> List();
}
public sealed record InstallResult(bool Ok, string Key, string Version, string[] Errors, string[] Warnings);
public sealed record UninstallResult(bool Ok, string[] BlockedByReferences);

public static class PackageValidation {
    public const int HOST_MAX_MANIFEST_VERSION = 1;     // bump ONLY for additive, kernel-compatible changes
    public static readonly string[] ForbiddenBinPrefixes =
        ["MindAttic.Ideas.Abstractions", "Microsoft.AspNetCore.", "Microsoft.Extensions.",
         "Microsoft.EntityFrameworkCore.", "Microsoft.JSInterop.", "System."];
    // Reject: manifestVersion > HOST_MAX (clear "upgrade MindAttic.Ideas"); unknown kind; forbidden bin/ assemblies;
    //         kind==code without entryType/bin; kind==data with bin/entryType; sdk range unsatisfied; sha256 mismatch.
    // Tolerate: unknown category (skip+warn); unknown manifest fields (Extra); unknown top-level folders.
}
```


## FOUNDATION-EFCORE-DATAMODEL: Free-form Page EF Core model: one Page table (Data|Code), int PK + portable string/GUID natural keys, no Zone tables

**Decision:** Lock a free-form content model with NO Zone/Pane/IdeaInstance tables. Content is three sibling tables â€” Page, Theme, Component â€” each carrying a reserved-column contract (int Id IDENTITY clustered PK, Guid Uid unique, string Key/Slug natural key, RowVersion, IsDeleted+DeletedUtc, full audit, Extra JSON bag, SourcePackageId) from migration #1. A Page is one self-contained row with a PageKind discriminator (Data|Code): a Data page renders stored BodyMarkup/PageCss/PageScript and an ordered ComponentReferencesJson list (no join table); a Code page renders a compiled type via ComponentTypeName/AssemblyName through DynamicComponent, degrading to a placeholder when the type is stale. The MindAttic.Frontpage collapse uses Code pages; net-new content uses Data pages. The fixed CSS cascade (Globalâ†’Themeâ†’Pageâ†’inline) is enforced by renderer emit order over four storage locations, never a CSS table or data-driven ordering.

```csharp
// ===== Reserved contract â€” EVERY content entity, from migration #1 (matches Frontend donor habits) =====
public interface IContentEntity {
  int      Id { get; set; }            // IDENTITY(1,1) clustered PK â€” donor pattern, lean FKs
  Guid     Uid { get; set; }           // UNIQUE; stable cross-env identity (Guid.NewGuid())
  string   Key { get; set; }           // human natural key; .idea import + seed UPSERT authority
  byte[]   RowVersion { get; set; }    // [Timestamp] concurrency (proven on LandingPage)
  bool     IsDeleted { get; set; }     // soft delete (global query filter)
  DateTime? DeletedUtc { get; set; }
  DateTime CreatedUtc { get; set; }  string? CreatedBy { get; set; }
  DateTime ModifiedUtc { get; set; } string? ModifiedBy { get; set; }
  string?  Extra { get; set; }         // nvarchar(max) JSON bag â€” never-migrate escape hatch
  int?     SourcePackageId { get; set; } // FK InstalledPackage.Id; null = authored in-app
}
public enum PageKind : byte { Data = 0, Code = 1 }
public enum ContentKind : byte { Page = 0, Theme = 1, Component = 2 }
public enum SettingScope : byte { Host = 0, Site = 1, Page = 2, Component = 3 }   // append-only

public class Site : IContentEntity {                 // Key e.g. "mindattic","legion"
  public int Id; public Guid Uid; public string Key=""; public string Name="";
  public string HostBindingsJson="[]"; public int? DefaultThemeId; public int? HomePageId;
  public string SettingsJson="{}"; /* + reserved */ }

public class Page : IContentEntity {
  public int Id; public Guid Uid; public string Key="";          // Key = stable, slug-independent
  public int SiteId; public int? ParentId;                       // self-FK -> nav tree
  public string Slug=""; public string Title=""; public int SortOrder;
  public PageKind Kind = PageKind.Data;
  // DATA payload (free-form, no deploy):
  public string? BodyMarkup; public string? PageCss; public string? PageScript;
  public bool TrustedAuthorContent;                              // true=emit raw, false=sanitize
  public string ComponentReferencesJson="[]";                    // [{key,sortOrder,settingsJson}] ordered, NOT a join
  // CODE payload (compiled, one deploy/RCL):
  public string? ComponentTypeName; public string? AssemblyName; // stale -> CmsMissingPage placeholder
  // shared:
  public int? ThemeId; public bool IsPublished; public DateTime? PublishedUtc;
  public string SeoMetaJson="{}"; /* + reserved */ }

public class Theme : IContentEntity {                // Key e.g. "cyberspace"
  public int Id; public Guid Uid; public string Key=""; public string Name="";
  public string? Css; public string? BodyPrelude;               // body-prelude.html
  public string DepsJson="{}";                                   // Cyberspace deps.json (css[]/scripts[]/assets[]) -> jsDelivr
  public string? LayoutComponentTypeName; public string? AssemblyName; /* + reserved */ }

public class Component : IContentEntity {            // Key e.g. "ui.tooltip"
  public int Id; public Guid Uid; public string Key=""; public string Name="";
  public string? ComponentTypeName; public string? Markup;       // compiled wrapper OR data snippet
  public string AssetsJson="[]"; public string SettingsSchemaJson="{}"; public bool IsGlobal;
  public string? AssemblyName; /* + reserved */ }

public class InstalledPackage {                      // the .idea registry â€” "a zip is just files"
  public int Id; public Guid Uid; public string PackageKey="";   // == manifest key
  public string Version=""; public ContentKind Kind; public string Sha256="";
  public string BlobPath=""; public string ManifestJson="{}"; public bool Enabled=true;
  public DateTime InstalledUtc; public byte[] RowVersion=default!; }

public class Asset : IContentEntity {
  public int Id; public Guid Uid; public string Key=""; public int? SiteId;
  public string Folder="/"; public string FileName=""; public string ContentType="application/octet-stream";
  public string? BlobUri; public byte[]? Bytes; public long SizeBytes; public string Sha256="";
  /* + reserved */ }

public class SettingEntry {                          // Host>Site>Page override chain + global CSS home
  public int Id; public SettingScope Scope; public int? ScopeId; // null for Host
  public string Key=""; public string Value="";                  // "css.global" lives at Scope=Host
  public byte[] RowVersion=default!; public DateTime ModifiedUtc; }

// AUTH â€” PORT verbatim from Frontend donor (string Id, BCrypt PasswordHash, SecurityStamp):
public class User { public string Id=""; public string Username=""; public string DisplayName="";
  public string? Email; public string PasswordHash=""; public string Role=""; public string SecurityStamp="";
  public bool MustChangePassword; public DateTime? LastLoginUtc; public DateTime CreatedUtc; public bool IsActive; }
public class Role { public int Id; public string Key=""; public string Name=""; }
public class UserRole { public string UserId=""; public int RoleId; }
public class PagePermission { public int Id; public int PageId; public int RoleId; public bool CanView; public bool CanEdit; }
public class ComponentPermission { public int Id; public int ComponentId; public int RoleId; public bool CanUse; }

// ===== OnModelCreating (LOCKED) =====
// Page:  HasIndex(x=>new{x.SiteId,x.Slug}).IsUnique();
//        HasIndex(x=>x.Key).IsUnique();  HasAlternateKey(x=>x.Uid);
//        HasIndex(x=>new{x.SiteId,x.ParentId,x.SortOrder}).HasFilter("[IsPublished]=1 AND [IsDeleted]=0");
//        HasOne<Site>().WithMany().HasForeignKey(x=>x.SiteId).OnDelete(Restrict);
//        HasOne<Page>().WithMany().HasForeignKey(x=>x.ParentId).OnDelete(NoAction);   // no cascade subtree
//        HasOne<Theme>().WithMany().HasForeignKey(x=>x.ThemeId).OnDelete(SetNull);
//        Property(x=>x.RowVersion).IsRowVersion(); Property(x=>x.Slug).HasMaxLength(160);
//        HasQueryFilter(x=>!x.IsDeleted);
// Theme/Component/Site/Asset: HasIndex(Key).IsUnique(); HasAlternateKey(Uid); soft-delete filter.
// InstalledPackage: HasIndex(new{PackageKey,Version}).IsUnique();
// SettingEntry: HasIndex(new{Scope,ScopeId,Key}).IsUnique();
// User: HasIndex(Username).IsUnique(); Property(Id).HasMaxLength(40).  UserRole/PagePermission composite keys.
// Every IContentEntity: Property(Extra) default '{}'; Uid is the stable secondary, Key is the import authority.

// ===== CSS cascade (LOCKED emit order, no table): =====
// 1 GlobalCss  = SettingEntry(Host,null,"css.global")
// 2 ThemeCss   = Theme.Css and/or Theme.DepsJson -> jsDelivr <link>/<script>
// 3 PageCss    = Page.PageCss  in <style id="ma-page">
// 4 inline      = inside Page.BodyMarkup (rendered last)

// ===== .idea manifest (kept tiny â€” directive E): =====
// { "key":"cyberspace", "kind":"Theme", "version":1,
//   "css":["theme.css"], "scripts":[], "assets":["assets/*.png"],
//   "componentType":null, "assembly":null, "extra":{} }
// Install: unzip -> files to Blob/wwwroot -> manifest to InstalledPackage row
//          -> UPSERT Page/Theme/Component row BY Key (never by Id/Uid across environments).
```


## FOUND-001-source-unification-alc: One Catalog, Many Sources: ICmsContentSource + ContentDescriptor + DynamicComponent-by-Type, with a defer-shared-to-Default ALC rule

**Decision:** Lock a single catalog (one EF entity, CmsContentDefinition) fed by an ordered set of ICmsContentSource providers, each yielding uniform ContentDescriptor records keyed by the (Kind, Key) string tuple. Phase 1 ships exactly one provider â€” CompiledContentSource (reflection over referenced RCLs for [Page]/[Theme]/[Component]) â€” plus stubbed-but-real seams (ITypeResolver, IPackageAssetSource returning empty, the /_pkg/{kind}/{key}/{version}/{**path} route mapped to a CompositeFileProvider with an empty runtime arm, and the InstalledPackage table migrated but unwritten). Phase 5 is purely additive: register PackageContentSource (a collectible AssemblyLoadContext per .idea) and a real PackageAssetSource â€” the catalog entity, discovery upsert, renderer, and route table are written once and never touched again. Rendering is always DynamicComponent-by-Type for code content and a single built-in RawMarkupRenderer for data content (pure html/css/js zips and free-form inline Pages â€” no assembly load at all).

```csharp
// ===== MindAttic.Ideas.Abstractions (tiny: NO EF, NO web host) â€” LOCK FOREVER, append-only =====
public enum ContentKind { Page, Theme, Component }            // Directive D: the only three kinds. ".idea" is the file format, never a content noun.
public enum ContentOrigin { Compiled, Package, Data }          // provenance only; renderer NEVER branches on it. Append-only, never renumber.
public enum RenderStrategy { ClrType, RawMarkup }              // ClrType => DynamicComponent; RawMarkup => built-in RawMarkupRenderer (no assembly)
public enum RenderMode { Static, InteractiveServer }           // WASM is compiled-only forever (assembly must reach browser at build time)
public enum RawContentTrust { Author, Untrusted }              // Directive F trust line: who saved it, never what the content claims

// The three inheritance roots. Namespaced to avoid the Microsoft ComponentBase clash. THESE NAMES CANNOT CHANGE.
public abstract class CmsPageBase      : ComponentBase          { [Parameter] public CmsRenderContext Context { get; set; } = default!; }
public abstract class CmsComponentBase : ComponentBase         { [Parameter] public CmsRenderContext Context { get; set; } = default!; }
public abstract class CmsThemeBase     : LayoutComponentBase    { [Parameter] public CmsRenderContext Context { get; set; } = default!; } // Body inherited; NO zones/panes (Directive A)
public sealed class CmsRenderContext { public required string Key {get;init;} public required ContentKind Kind {get;init;} public required string? Slug {get;init;} public required IServiceProvider Services {get;init;} public string? RawSettingsJson {get;init;} public T GetSettings<T>() where T:class,new() => RawSettingsJson is {Length:>0} j ? System.Text.Json.JsonSerializer.Deserialize<T>(j) ?? new() : new(); }

[AttributeUsage(AttributeTargets.Class)] public sealed class PageAttribute(string key)      : Attribute { public string Key => key; public string DisplayName {get;init;}=key; public RenderMode RenderMode {get;init;}=RenderMode.InteractiveServer; public Type? SettingsModel {get;init;} public Type? EditComponent {get;init;} }
[AttributeUsage(AttributeTargets.Class)] public sealed class ThemeAttribute(string key)     : Attribute { public string Key => key; public string DisplayName {get;init;}=key; }
[AttributeUsage(AttributeTargets.Class)] public sealed class ComponentAttribute(string key) : Attribute { public string Key => key; public string DisplayName {get;init;}=key; }

// The ONE descriptor every source yields. Identical shape for all three tiers. Record with init props => append-only.
public sealed record ContentDescriptor {
    public required ContentKind   Kind        { get; init; }
    public required string        Key         { get; init; }   // stable id e.g. "cyberspace","ui.tooltip","frontend.hub". (Kind,Key) is THE identity.
    public required string        DisplayName { get; init; }
    public required ContentOrigin Origin      { get; init; }
    public required int           Priority    { get; init; }   // higher wins a (Kind,Key) collision. Compiled=100, Package=50, Data=50.
    public RenderStrategy Strategy   { get; init; } = RenderStrategy.ClrType;
    public RenderMode     RenderMode { get; init; } = RenderMode.InteractiveServer;
    public int Version { get; init; } = 1;
    public bool   AllowOverride { get; init; }                  // package opt-in to shadow a compiled key (still needs admin confirm)
    // ClrType strategy â€” LATE-BOUND resolution hint, NOT identity (resolved by ITypeResolver at render):
    public string? ClrTypeName { get; init; }                   // "Ns.Type"
    public string? AssemblyName { get; init; }                  // resolution scope; null for RawMarkup
    public string? EditComponentTypeName { get; init; }
    public string? SettingsModelTypeName { get; init; }
    // RawMarkup strategy â€” pure files, ZERO assembly (covers UiUx raw Components/Themes + free-form data Pages):
    public RawContentBundle? RawBundle { get; init; }
    public string? AssetMount { get; init; }                    // virtual base e.g. "/_pkg/component/ui.tooltip/1/"
}
public sealed record RawContentBundle(string? Html, string? Css, string? Js, RawContentTrust Trust,
    IReadOnlyList<string> CssUrls, IReadOnlyList<string> ScriptUrls);  // CssUrls in CASCADE order: GLOBAL -> THEME -> PAGE (Directive C)

// THE forward-compatible seam. Phase 5 = register one more implementor in DI. Nothing downstream changes.
public interface ICmsContentSource { ContentOrigin Origin {get;} int Priority {get;} IAsyncEnumerable<ContentDescriptor> DiscoverAsync(CancellationToken ct = default); }
// Late-bind a descriptor's Type from the correct load context. Phase 1 impl = Default ALC only. Phase 5 swaps in ALC-aware impl. null => placeholder, never throw.
public interface ITypeResolver { Type? Resolve(ContentDescriptor d); }
// Asset seam: RCL _content (compile-stitched) vs runtime wwwroot. Phase 1 stub returns null/empty.
public interface IPackageAssetSource { IFileProvider? GetFileProvider(string key, int version); string? GetAssetMount(string key, int version); }

// The HARD ALC unification rule â€” declared in Abstractions, enforced by the loader. THIS is the linchpin.
public static class SharedContracts {
    public static readonly string[] DeferToDefaultPrefixes = {
        "MindAttic.Ideas.Abstractions", "MindAttic.Ideas.Core",
        "Microsoft.AspNetCore", "Microsoft.Extensions", "Microsoft.EntityFrameworkCore", "Microsoft.JSInterop",
        "System.", "netstandard", "mscorlib" };
}

// ===== MindAttic.Ideas.Core (EF) â€” the catalog row. Columns ADD-ONLY forever. =====
public sealed class CmsContentDefinition {
    public int Id { get; set; }
    public string Kind { get; set; } = "";            // ContentKind as string (open enum on the wire â€” a future 4th kind is additive)
    public string Key  { get; set; } = "";            // UNIQUE(Kind,Key,Origin); render-time winner = MAX(Priority) per (Kind,Key)
    public string Origin { get; set; } = "";
    public int    Priority { get; set; }
    public string Strategy { get; set; } = "ClrType";
    public string RenderMode { get; set; } = "InteractiveServer";
    public string DisplayName { get; set; } = "";
    public int Version { get; set; } = 1;
    public string? ClrTypeName { get; set; }
    public string? AssemblyName { get; set; }
    public string? EditComponentTypeName { get; set; }
    public string? SettingsModelTypeName { get; set; }
    public string? RawBundleJson { get; set; }        // serialized RawContentBundle for RawMarkup
    public string? AssetMount { get; set; }
    public bool    IsShadowed { get; set; }            // lost a (Kind,Key) collision; kept VISIBLE for admin, never silently dropped
    public bool    IsActive { get; set; } = true;      // type unresolved/absent => false => placeholder
    public DateTime LastSeenUtc { get; set; }
}
// Phase-5 registry â€” table migrated in Phase 1, UNWRITTEN until Phase 5:
// InstalledPackage(Id, Kind, Key, Version, BlobPath, ManifestJson, Sha256, Enabled, InstalledUtc)

// ===== Discovery upsert (Core) â€” identical for ALL sources, written ONCE =====
// foreach source ORDER BY Priority DESC:  foreach d in source.DiscoverAsync():
//   winner = max-Priority seen for (Kind,Key); on equal Priority, incumbent stays UNLESS d.AllowOverride && adminConfirmed
//   upsert row by (Kind,Key,Origin); set IsShadowed = (this row is not the winner). NEVER delete on discovery; absence => IsActive=false (Stale) => placeholder.

// ===== The render fork (Web) â€” ONE decision, Phase 5 changes NOTHING here =====
// var d = catalog.Find(kind,key);  var t = d.Strategy==RawMarkup ? null : typeResolver.Resolve(d);
// if (d.Strategy==RawMarkup)        <RawMarkupRenderer Bundle="d.RawBundle" Context="ctx" />   // tier 3, no assembly
// else if (t is null || !d.IsActive)<CmsMissingContent Definition="d" />                        // stale, never crash
// else                              <DynamicComponent Type="t" Parameters="ctx.AsParams()" />   // tiers 1 & 2 identical

// ===== Phase 5 ALC loader (DESIGN now, body deferred) â€” the cast-safety rule =====
// public sealed class CmsPackageLoadContext : AssemblyLoadContext {   // isCollectible:true
//   AssemblyDependencyResolver _r;  ctor(entryDllPath) => _r = new(entryDllPath);
//   protected override Assembly? Load(AssemblyName n) {
//     if (SharedContracts.DeferToDefaultPrefixes.Any(p => n.Name!.StartsWith(p, OrdinalIgnoreCase))
//         || Default.Assemblies.Any(a => a.GetName().Name == n.Name)) return null;  // => Default ALC: ONE identity, casts succeed
//     var path = _r.ResolveAssemblyToPath(n);  return path is null ? null : LoadFromAssemblyPath(path); } }  // private dep ONLY
// PackageContentSource.DiscoverAsync(): per enabled InstalledPackage extracted from Blob ->
//   lib/<entry>.dll present => new CmsPackageLoadContext(entry).LoadFromAssemblyPath -> reflect CmsPageBase/CmsComponentBase/CmsThemeBase
//                              -> ContentDescriptor{Origin=Package,Strategy=ClrType,ClrTypeName,AssemblyName,Priority=50,AssetMount="/_pkg/..."}
//   lib/ ABSENT (pure html/css/js)=> ContentDescriptor{Origin=Package,Strategy=RawMarkup,RawBundle=...,Priority=50}  // NO ALC at all

// ===== idea.json (zip manifest, kept zip-simple per Directive E â€” a zip of files, not a plugin descriptor) =====
// { "kind":"Page|Theme|Component", "key":"ui.tooltip", "displayName":"Tooltip", "version":1,
//   "sdk":1,                         // floor enforced at install; mismatch rejected before load
//   "entryType": null|"Ns.Type",     // null => RawMarkup tier (no lib/, no assembly)
//   "renderMode":"Static", "trust":"Author", "allowOverride":false,
//   "assetsCss":["tooltip.css"], "assetsJs":["tooltip.js"] }
// Layout: idea.json + (optional) lib/<entry>.dll+private deps + wwwroot/<assets> + (Page) page.html/css/js
```


## FND-001-RENDER-CSS-TRUST: Free-form PageHost rendering, fixed CSS cascade, and authorship-based inline-JS trust

**Decision:** Lock ONE catch-all host component PageHost (@page "/{*slug}") as the single, permanent render entry point, with /admin and /_* reserved before the fallback. A Page is one EF row rendered in two kinds behind that one host: a DataPage (free-form HTML/CSS/JS stored in DB columns, rendered via the IRawContentGate and emitted scripts â€” zero deploy, this is the MindAttic.Frontpage collapse path) and a CodePage (a compiled PageBase subclass resolved by type name and rendered via DynamicComponent â€” full C#/interactivity). There are no zones; a Theme is thin chrome wrapping a single free-form Body. CSS is emitted by one CmsHead component in a FIXED, ordinal-numbered cascade (Global=0, Theme=100, Page=200, page-inline=300). The trust boundary is drawn at AUTHOR IDENTITY, not content shape: content written by a principal holding the Cms.AuthorRawMarkup claim (Admin role) is stamped trusted on the row at write time and rendered RAW (intentional admin JS = full in-process/browser trust, accepted by directive F); everything originating from any non-admin request is Untrusted and MUST pass the single IRawContentGate sanitizer. Render mode is global InteractiveServer; interactive WebAssembly is EXCLUDED FOREVER from the package contract.

```csharp
// ===== Tiny append-only abstractions (no EF/web deps) =====
public enum PageKind { Data = 0, Code = 1 }                      // append-only; never renumber
public enum CmsRenderMode { Static = 0, InteractiveServer = 1 }  // NO Wasm member â€” ever
public enum ContentTrust { Untrusted = 0, Author = 1 }           // append-only (future HtmlOnly=2)

// Inheritance roots (user directive D names). Keep minimal forever.
public abstract class PageBase : ComponentBase {                 // Microsoft ComponentBase; CodePages inherit
    [Parameter] public PageRenderContext Ctx { get; set; } = default!;
    protected SiteContext Site => Ctx.Site;  protected PageDescriptor Page => Ctx.Page;
}
public abstract class ThemeBase : ComponentBase {                // theme = thin chrome, NO zones
    public abstract IReadOnlyList<CssAsset> StyleSheets { get; } // emitted at ordinal 100
    public abstract IReadOnlyList<ScriptAsset> Scripts { get; }
    [Parameter] public RenderFragment Body { get; set; } = default!;   // page body slots here
}
public abstract class CmsComponentBase : ComponentBase { }       // CMS/UiUx component root (distinct namespace)

public readonly record struct CssAsset(string Href, int Ordinal);          // 0 Global,100 Theme,200 Page,300 inline
public readonly record struct ScriptAsset(string Src, bool Defer, ContentTrust Trust);
public sealed class PageRenderContext {
    public required SiteContext Site { get; init; }
    public required PageDescriptor Page { get; init; }
    public required ThemeDescriptor Theme { get; init; }
    public required IServiceProvider Services { get; init; }
}

// THE single sanitize chokepoint â€” the ONLY place a MarkupString is born.
public interface IRawContentGate {
    MarkupString Emit(string? html, ContentTrust trust);  // Author=>raw passthrough; Untrusted=>Ganss.Xss
    string SanitizeAttribute(string? value);              // any value interpolated into author markup
}

// ===== EF entity: ONE table, both kinds (extends the plan's Page; columns append-only) =====
public sealed class Page {
    public int Id; public int SiteId; public int? ParentId;     // tree -> nav
    public required string Slug;                                 // unique per (SiteId,Slug)
    public required string Title;
    public PageKind Kind;                                        // Data | Code
    public string? ThemeOverrideKey;                             // else Site.DefaultThemeKey
    public CmsRenderMode RenderMode;                             // Data->Static default; Code->InteractiveServer default
    // Data payload (null for Code):
    public string? BodyHtml;          // free-form HTML, ordinal body
    public string? PageCss;           // ordinal 200
    public string? PageInlineCss;     // ordinal 300 (<style>)
    public string? PageScript;        // author inline JS, emitted raw iff BodyTrust==Author
    // Code payload (null for Data):
    public string? ComponentTypeName; // resolved to a PageBase subclass for DynamicComponent
    public string? AssemblyName;
    // Trust stamp = property of the DATA, set at write time:
    public ContentTrust BodyTrust;    // Author iff writer held Cms.AuthorRawMarkup
    public string? AuthoredByUserId; public int AuthorTrustVersion;
    public bool IsPublished; public int SortOrder; public string? SeoMetaJson;
    public DateTime CreatedUtc, ModifiedUtc; public byte[]? RowVersion;
}
// Unique index (SiteId, Slug). Reserved-prefix check at save: reject slug starting "admin" or "_".

// ===== Render pipeline (Program.cs ordering, after porting Frontend auth/seed verbatim) =====
// app.MapStaticAssets();
// app.MapGroup("/admin").RequireAuthorization("Admin");          // reserved BEFORE fallback
// (system endpoints under /_* mapped before fallback)
// app.MapRazorComponents<App>().AddInteractiveServerRenderMode()
//    .AddAdditionalAssemblies(/* Page/Theme/Component RCLs + loaded .idea asms */);
// App.razor Router route component = PageHost.

@page "/{*slug}"   // PageHost.razor â€” THE one foundation render entry point
// OnParametersSetAsync:
//   slug = (Slug ?? "").TrimStart('/');
//   if (slug.StartsWith("admin") || slug.StartsWith("_")) -> 404 reserved;   // defense-in-depth
//   site  = SiteContext.FromHost(HttpContext.Request.Host);
//   page  = await Pages.ResolveAsync(site.Id, slug);   // null/unpublished -> CMS 404 Page; 403 if no view perm
//   theme = Themes.Resolve(page.ThemeOverrideKey ?? site.DefaultThemeKey);
// Render: <CmsHead Site Theme Page/> then theme chrome wrapping Body:
//   <DynamicComponent Type=theme.LayoutType Parameters=@(["Body"]=RenderBody)/>
// RenderBody dispatch by Kind:
//   Data: @Gate.Emit(page.BodyHtml, page.BodyTrust); page.PageScript emitted as <script defer> iff Author
//   Code: <DynamicComponent Type=resolvedPageType Parameters=@(["Ctx"]=ctx)/>  // missing type -> CmsMissing, never crash

// ===== CmsHead: FIXED cascade by ordinal, append-only =====
// StyleSheets.OrderBy(Ordinal): Global(0) -> Theme(100) -> PageCss(200) -> PageInlineCss(300); element style= last by DOM.
// New layers insert at reserved gaps (e.g. component-scoped at 250) WITHOUT renumbering existing layers.

// ===== Trust on write (admin editor / save endpoint) =====
// page.BodyTrust = user.HasClaim("Cms.AuthorRawMarkup") ? ContentTrust.Author : ContentTrust.Untrusted;
// page.AuthoredByUserId = user.Id; page.AuthorTrustVersion = CurrentTrustEpoch;
// Authorization: options.AddPolicy("AuthorRawMarkup", p => p.RequireClaim("Cms.AuthorRawMarkup")); // Admin role granted it

// ===== .idea = JUST A ZIP =====
// /manifest.json (only required file): { "kind":"Page|Theme|Component", "key":"...", "version":"...",
//   "renderMode":"Static|InteractiveServer" }  // NO "wasm" value
//   Page kind:      + body.html, [styles.css], [script.js]              (data page; no assembly)
//   Theme kind:     + theme.css OR cssHref, [layout type name]
//   Component kind: + lib/<assembly.dll> (NEVER ship Abstractions.dll), [wwwroot/*]
// Install = unzip into running instance + upsert the matching row. Admin-only + Sha256/signature gated.

// ===== CSP posture (per response) =====
// Author-trusted page: Content-Security-Policy: script-src 'self' 'nonce-{n}' <pinned jsDelivr>; (author <script> carries nonce)
// All other responses:  script-src 'self'; object-src 'none'; base-uri 'self';  (no unsafe-inline)
```


