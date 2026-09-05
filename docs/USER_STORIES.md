---
codex: 1
project: MindAttic.Ideas
code: MAI
layer: stories
status: living
updated: 2026-06-16
---

# MindAttic.Ideas — User Stories

> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned · 🗑️ cut. Every ✅ cites the test that proves
> it. Derived from the [`README.md`](../README.md) living feature spec; test tokens name NUnit
> fixtures in `src/MindAttic.Ideas.Tests`. Build/test evidence: see [BIBLE §6](BIBLE.md#MAI-§6) —
> `dotnet test` reports **224 passed, 0 failed (2026-06-12)**, plus the [Explicit] SQL Server temporal proof.
>
> Personas: **Author** (an admin who writes pages), **Operator** (installs/manages `.idea` packages),
> **Visitor** (reads a rendered page), **Widget-Dev** (builds first-party content in MindAttic.Ideas.Library).

## Epic A — Authoring & rendering a page

- **MAI-US-A1 ✅** As an Author, I can drop a `{{Kind.Name[.Vn]}}` token into free-form markup and have
  it resolve to the right citizen, so I compose without zones. *Given a body with include tokens, When
  the reference parser runs, Then pinned/floating/`.Latest`/short-form/dotted-key tokens parse to the
  right `(Kind,Key,Version)` and attributes are ignored for resolution.* *(verified by `RenderGuardTests`,
  `IncludeReferenceParser` cases.)*
- **MAI-US-A2 ✅** As a Visitor, a code-page `<CmsInclude>` and a data-page include token render
  **identically** for Resolved/Missing/Disabled outcomes, so the authoring path doesn't change behavior.
  *(verified by `CmsIncludeParityTests.CmsInclude_MatchesDataPageInclude`.)*
- **MAI-US-A3 ✅** As an Author with the `Cms.AuthorRawMarkup` claim, my inline JS runs (raw passthrough);
  without it, my body is sanitized — set at **write time**. *Given a save, When the writer holds/doesn't
  hold the claim, Then `BodyTrust` is Author/Untrusted and the author Uid is captured (truncated to 64).*
  *(verified by `PageAuthoringTests.Stamp_WithClaim_IsAuthor_AndCapturesUid`,
  `Stamp_WithoutClaim_IsUntrusted`.)* See [MAI-LAW-5](BIBLE.md#MAI-§5).
- **MAI-US-A4 ✅** As a Visitor, untrusted markup is neutralized (script/style/inline-handler/`javascript:`
  stripped) while `{{tokens}}` survive so widgets still compose. *(verified by `RawContentGateTests`:
  `Untrusted_StripsScriptTag`, `Untrusted_NeutralizesJavascriptUri`,
  `Untrusted_PreservesIncludeTokens_SoWidgetsStillCompose`.)*
- **MAI-US-A5 ✅** As an Author, I can CRUD pages with soft-delete and publish/enable under the Admin
  policy. *(verified by `PageAdminServiceTests`, `AdminServiceContractTests`.)*
- **MAI-US-A6 ✅** As a Visitor on the running host, the seeded **Frontpage** — the mindattic.com
  recreation as a Data page ([A21](AMENDMENTS.md#MAI-A21)) — renders its widget capabilities (Tabs
  board, Gallery, pin-when-short Footer) through the Cyberspace theme end-to-end. *NUnit proves the
  mechanics (the real seeded body parses to exactly the three floating Widget tokens; the install →
  catalog → IncludeExpander pipeline resolves them to Component frames; the seed's
  create/migrate/never-clobber behavior holds), and an attended run proves the live render.*
  *(verified by `CmsIncludeParityTests`, `RawContentGateTests`, `SeededPageRenderTests`:
  `SeedBodyTokens_ParseToWidgetKind_FloatingVersion`, `FrontpageBody_AllSeedTokens_ParseFromTheRealSeededPage`,
  `Seed_MigratesStockCodeFrontpage_ToDataPage_ButNeverAnAdminPage`,
  `Seed_SoftDisablesStockHomePage_AndNeverAnEditedOne`,
  `SeedBody_InstalledTabsWidget_ExpandsToResolvedFrame`; live render observed 2026-06-09 — see
  [BIBLE §6](BIBLE.md#MAI-§6) live-render evidence: zero `ma-missing` placeholders. Interactive
  circuit behavior (clicking a tab tile) remains browser-only.)*
- **MAI-US-A7 ✅** As a Visitor, navigating to the application with **no route** forwards me to the
  Frontpage. *`PageHost` forwards the `""` slug to the slug named by the Host setting `page.frontpage`
  (default `frontpage`) instead of resolving it to a page; the retired stock home page is soft-disabled
  by the seed.* *(seed-side behavior verified by
  `SeededPageRenderTests.Seed_SoftDisablesStockHomePage_AndNeverAnEditedOne`; the forward observed live
  2026-06-09 — `GET /` → 302 → `/frontpage` ([BIBLE §6](BIBLE.md#MAI-§6)). See [A21](AMENDMENTS.md#MAI-A21).)*

## Epic B — Versioning, lifecycle & history

- **MAI-US-B1 ✅** As an Author, I pin a version (`.V3`) or float to latest (omit / `.Latest`), so I juggle
  versions only when I care. *(verified by `RenderGuardTests.Parse_FloatingAndLatest_HaveNullVersion`,
  `Parse_PinnedVersion`.)* See [MAI-A12](AMENDMENTS.md#MAI-A12).
- **MAI-US-B2 ✅** As an Operator, I cannot delete a version while any page pins it; a floating reference
  blocks only when deleting would orphan it. *(verified by `ContentLifecycleServiceTests`:
  `PinnedVersion_AlwaysBlocks_AndListsSlug`, `FloatingReference_BlocksOnlyWhenItWouldOrphan`,
  `DisabledOrUnpublishedPage_IsNotABlockingReference`; and
  `UsesDeclarationTests.DeleteGuard_BlocksDeletingAComponentACompiledPagePins`.)* See
  [MAI-A3](AMENDMENTS.md#MAI-A3), [HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2).
- **MAI-US-B3 ✅** As an Operator, disabling a content version reloads the catalog so the token then
  resolves as Disabled. *(verified by
  `ContentLifecycleServiceTests.SetEnabledFalse_ReloadsCatalog_SoResolveTagReportsDisabled`.)*
- **MAI-US-B4 ✅** As an Operator, the EF model guards reserved columns and a delete-guard projection, so
  integrity holds at the data layer. *(verified by `CmsModelGuardTests`.)*
- **MAI-US-B5 ✅** As an Operator, I can inspect and roll back to any prior page state via temporal
  history. *`IPageHistoryService` + `PageHistoryService` implemented; Admin "Page History" panel
  surfaces the temporal record inline in the page editor. `RestoreAsync` is unit-tested (4 tests);
  the live temporal query is proven against real SQL Server.*
  *(verified by `PageHistoryServiceTests`: `RestoreAsync_CopiesSnapshotContentFields_OntoCurrentPage`,
  `RestoreAsync_ReStampsTrust_FromRestoringUserClaims`, `RestoreAsync_NonAdminUser_StampsUntrusted`,
  `RestoreAsync_UnknownPage_ReturnsFalse`,
  `GetHistoryAsync_RequiresSqlServer_ThrowsOnInMemoryDb`; and the LIVE proof
  `PageHistorySqlServerTests.GetHistoryAsync_OnSqlServer_ReturnsOrderedTemporalVersions` —
  [Explicit], run 2026-06-09 against LocalDB: multiple ordered temporal versions of the frontpage
  row. See [A22](AMENDMENTS.md#MAI-A22).)*

## Epic C — Trust, degradation & the Admin Inbox

- **MAI-US-C1 ✅** As an Operator, a missing/disabled dependency raises a deduped Admin Inbox message that
  collapses recurrences and reopens after resolution. *(verified by `AdminInboxServiceTests`:
  `RaiseAsync_SameDedupKey_CollapsesToOneRow`, `RaiseAsync_AfterResolve_ReopensToNew`,
  `UnreadCount_CountsOnlyNew`.)* See [MAI-A5](AMENDMENTS.md#MAI-A5).
- **MAI-US-C2 ✅** As a Visitor, the render thread never throws on a bad reference — it degrades to a
  placeholder and fire-and-forgets the alert. *(verified by `RenderGuardTests`, `RawContentGateTests`,
  `RenderAlertSink` wiring.)* See [MAI-LAW-7](BIBLE.md#MAI-§5).

## Epic D — The `.idea` package & install

- **MAI-US-D1 ✅** As a Widget-Dev, the `.idea` manifest kernel reads and validates with explicit errors,
  rejecting host assemblies in `bin/` and enforcing the six-field kernel. *(verified by
  `ManifestReaderTests`, `ManifestValidatorTests`, `IdeaArchiveReaderTests`.)*
- **MAI-US-D2 ✅** As a Widget-Dev, packing is reflection-only and lossless/forward-compatible, with
  SHA-256 integrity and a zip-slip-guarded reader. *(verified by `PackerTests`, `ManifestAssetPackerTests`,
  `Sha256HasherTests`, `PackageExtractorTests`.)* See [HOUSE-LAW-5](../../MindAttic.HouseRules.md#HOUSE-LAW-5).
- **MAI-US-D3 ✅** As an Operator, the whole-number version/collision resolver picks the active version and
  refuses bad collisions. *(verified by `PackageVersionResolverTests`.)* See [MAI-A1](AMENDMENTS.md#MAI-A1).
- **MAI-US-D4 ✅** As an Operator, installing a `.idea` is idempotent: it registers the `InstalledPackage`
  row + a mirrored catalog row, retains prior versions on upgrade, soft-disables, and reloads the catalog.
  *(verified by `PackageInstallServiceTests`, `SeedOnInstallTests`.)*
- **MAI-US-D5 ✅** As an Operator, a package blob is kept verbatim in a blob store for re-share/rollback.
  *(verified by `LocalFilePackageBlobStoreTests`.)*
- **MAI-US-D6 ✅** As an Operator, a local folder source discovers packable `.idea` candidates.
  *(verified by `LocalFolderPackageSourceTests`.)*

## Epic E — Runtime load & asset cascade

- **MAI-US-E1 ✅** As an Operator, a `.idea` citizen loads through a per-package collectible ALC; host
  types unify by reference identity and others delegate to the default resolver. *(verified by
  `AlcAwareTypeResolverTests`, `CmsPackageLoadContextTests`.)* See [MAI-LAW-6](BIBLE.md#MAI-§5).
- **MAI-US-E2 ✅** As an Author, a page's citizen css/scripts are cascade-ordered, deduped, and hoisted
  into `<head>` (Global → Theme → Plugin → Component → Page → inline), fed by a no-schema manifest→`Extra`
  data path.
  *(verified by `PageAssetCollectorTests`, `AssetDataPathTests.Install_Then_Reload_SurfacesManifestCssScripts_OntoDescriptorExtra`,
  `UsesDeclarationTests.Collect_FromUses_HoistsReferencedCitizenAssets`.)* See [MAI-LAW-4](BIBLE.md#MAI-§5).
- **MAI-US-E3 ✅** As a Widget-Dev, a `[Uses]`/`uses[]` declaration parses (bare floats, pinned, case-
  insensitive kind, rejects malformed) and drives hoisting + the delete-guard. *(verified by
  `UsesDeclarationTests.TryParseUse_BareKey_FloatsToLatest`, `UsesDeclarationTests.TryParseUse_RejectsMalformed`.)*
- **MAI-US-E4 ✅** As an Operator, a corrupt manifest during reload doesn't abort the reload — it leaves
  that descriptor's `Extra` null. *(verified by `AssetDataPathTests.CorruptManifestJson_DoesNotAbortReload_LeavesThatExtraNull`.)*

## Epic F — Frontier (planned / partial)

- **MAI-US-F1 ✅** As an Operator, the `ma-idea` CLI can pack / inspect / list / install / verify. *(CLI in
  `src/MindAttic.Ideas.Sdk`; pack/validate paths covered by `PackerTests`/`ManifestValidatorTests`; an
  attended CLI-roundtrip e2e is not separately automated.)*
- **MAI-US-F2 ✅** As an Operator, the Admin can enable/disable/guarded-delete content definitions and
  triage the Admin Inbox under the Admin policy. *(verified by `AdminServiceContractTests`,
  `UsersAdminContractTests`, `IdeasClaimsAugmentorTests`.)*
- **MAI-US-F3 ✅** As an Author, a theme/component/plugin **assignment UI**, a file manager, and roles
  management. *Theme picker (catalog-driven `<select>` for key/version), component palette (catalog-driven
  token-insert), Assets panel (mounted CSS/scripts browser), and Packages panel (installed `.idea` blob
  browser with SHA-256 + admin-protected download) are all implemented in the admin shell. Roles
  management is already done at `/users`.*
  *(verified by `AdminAssignmentTests`: `WidgetToken_PinnedVersion_ParsesBack` (renamed to
  `ComponentToken_PinnedVersion_ParsesBack` in A26 refactor),
  `ThemeToken_PinnedVersion_ParsesBack`, `CatalogFilter_Theme_ReturnsOnlyThemes`,
  `CatalogFilter_Widget_ReturnsOnlyWidgets` (renamed to `CatalogFilter_Plugin_ReturnsOnlyPlugins` /
  `CatalogFilter_Component_ReturnsOnlyComponents` in A26 refactor);
  and `PackageRegistryServiceTests`: `ListAsync_ReturnsAllPackages_SortedByCategoryKeyVersionDesc`,
  `ListAsync_Empty_ReturnsEmptyList`, `ListAsync_MapsAllFields`.)*
- **MAI-US-F4 ✅** As an Operator, I sign in via **MindAttic.Authentication** (the package, not Ideas-owned).
  *`Program.cs` already wires `AddMindAtticAuthentication<CmsDbContext>`, `UseMindAtticAuthentication()`,
  and `MapMindAtticAuthEndpoints()`; claim augmentation is fully adopted.*
  *(verified by `IdeasClaimsAugmentorTests`; see [A16](AMENDMENTS.md#MAI-A16),
  [HOUSE-LAW-7](../../MindAttic.HouseRules.md#HOUSE-LAW-7).)*
- **MAI-US-F5 ✅** As a Visitor, a real packed `.idea` renders end-to-end through the **running** host.
  *NUnit verifies the pipeline (install → catalog reload → IncludeExpander produces a Resolved
  Component frame; unknown tokens correctly degrade), and an attended run proves the HTTP layer: all
  43 library `.idea`s installed at startup and the frontpage rendered their citizens with hoisted
  assets served at `/_ideas/...` mounts (200), zero placeholders.*
  *(verified by `RenderPipelineTests`: `Install_ThenReload_ThenExpand_ProducesResolvedFrame`,
  `Install_ThenExpand_UnknownToken_ProducesMissingFrame`; live HTTP render observed 2026-06-09 —
  [BIBLE §6](BIBLE.md#MAI-§6).)*
- **MAI-US-F6 ✅** As a Widget-Dev, compiled-citizen asset harvest (`Activator` on `PluginBase`/`ComponentBase`)
  hoists declared `StylesheetUrls`/`ScriptUrls` into `<head>` via `PageAssets.AllAssetsOf` — the same
  `PageAssetCollector` delegate used for package citizens, consistent with how `PageHost` harvests Theme
  assets. *(verified by `PageAssetsTests`: `CompiledWidget_AllAssetsOf_HarvestsViaActivator` (covers
  both Plugin and Component bases post-A26), `CompiledWidget_UnresolvableType_ReturnsEmpty`,
  `PackageWidget_AllAssetsOf_DelegatesToMountedManifestAssets`.)*
- **MAI-US-F7 ✅** As an Operator, official content lives in the first-party library and
  `MindAttic.Frontpage` / `MindAttic.Legion.Frontend` collapse into Pages. *(original spec said
  "official content lives in MindAttic.UiUx" — restated by [A22](AMENDMENTS.md#MAI-A22) per A19/A20:
  the single first-party home is **MindAttic.Ideas.Library**; UiUx remains upstream raw source.)*
  *Both frontends are collapsed: mindattic.com → the `frontpage` Data page
  ([A21](AMENDMENTS.md#MAI-A21)), Legion.Frontend → the seeded `personas` Data page whose body is one
  `{{ MindAttic.Ideas.Component.LegionPersonas }}` token.*
  *(verified by `SeededPageRenderTests.Seed_CreatesPersonasPage_CollapsingLegionFrontendIntoOneToken`
  and live renders 2026-06-09: `/personas` 200 with the full gallery and zero placeholders,
  `/frontpage` zero placeholders. See [A8](AMENDMENTS.md#MAI-A8), [A14](AMENDMENTS.md#MAI-A14),
  [A20](AMENDMENTS.md#MAI-A20), [A22](AMENDMENTS.md#MAI-A22).)*
- **MAI-US-F8 ✅** As an Author, I edit pages with **Monaco** catalog-driven IntelliSense, the unified
  `{{double-brace}}` grammar. *`MonacoEditor.razor` wraps Monaco (lazy-loaded from CDN) with a
  `{{ }}` completion provider fed by the live catalog; the BodyHtml textarea in the page editor is
  replaced by this component. RFC 0001 is now fully implemented ([A22](AMENDMENTS.md#MAI-A22)):
  **typed-attribute coercion** (token attributes bind to bool/int/double/enum `[Parameter]`s through
  the one shared `EmitInclude` path) and **clickable upload-to-fix placeholders** (`MissingContent`
  links to `/admin/upload?missing=<reference>`; the Upload panel shows what the page is waiting on).*
  *(verified by `MonacoEditorTokenTests`: `IntelliSenseToken_ParsesBackViaTagGrammar`,
  `IntelliSenseToken_InsertedInBody_ParsedByIncludeReferenceParser`;
  `IncludeAttributeCoercionTests` (9 tests incl. `Expand_TokenAttributes_BindTyped_AndLeaveUnmatchedRaw`);
  `RenderGuardTests.MissingPlaceholder_LinksToAdminUpload_WithTheMissingKey`; live Monaco interaction
  is browser-tested.)*

## Epic G — Page authoring enhancements (post-A22)

- **MAI-US-G1 ✅** As an Author, I can set a Theme for a page from a dropdown in the admin UI
  (catalog-driven, no token in the HTML body required), so theme assignment is a metadata operation
  not a markup change. *`ThemeKey`/`ThemeVersion` columns already existed; the Page Properties
  collapsible `<details>` panel and admin CSS (A24) make them accessible.* *(verified by the
  existing `AdminAssignmentTests`: `CatalogFilter_Theme_ReturnsOnlyThemes`,
  `ThemeToken_PinnedVersion_ParsesBack`; the panel UI is browser-confirmed.)*
- **MAI-US-G2 ✅** As an Author, I can set a custom SEO Title and SEO Description for a page,
  overriding the page title in the browser tab and providing a `<meta name="description">` tag.
  *`PageAdminService.SaveAsync` serializes `{title,description}` to `Page.SeoMetaJson`; `GetAsync`
  deserializes it; `PageHost.razor` reads `seo.title`/`seo.description` from the `IPageContext.Meta`
  dictionary.*
  *(verified by `PageAdminServiceTests`:
  `SeoMeta_Parse_ReturnsNull_ForNullOrEmpty`, `SeoMeta_Parse_ExtractsFields`,
  `SeoMeta_Parse_ReturnsNull_ForMalformedJson`, `SeoMeta_Serialize_ReturnsNull_WhenBothFieldsNull`,
  `SeoMeta_Serialize_ReturnsJson_WhenAnyFieldSet`, `Save_WithSeoFields_PersistsThroughGetAsync`,
  `Save_WithNullSeoFields_LeavesJsonNull`. See [A24](AMENDMENTS.md#MAI-A24).)*
- **MAI-US-G3 ✅** As a Widget-Dev, the first-party widget/theme library lives in the same git
  repo as the CMS engine (`library/` directory), so the project is maintained in one place without
  coupling the two build graphs. *`library/Directory.Build.props` carries a single intra-repo
  `Abstractions` reference; the CMS `src/` and `library/` each have their own `.slnx` and never
  cross-reference at build time. Abstractions types used by library widgets are exercised by
  `PackerTests` and `ManifestAssetPackerTests`; compose-graph independence is confirmed by
  `ma-idea verify` across all 37 `.idea`s.* *(see [A23](AMENDMENTS.md#MAI-A23).)*

## Epic H — Plugin/Component taxonomy (A26)

- **MAI-US-H1 ⬜** As an Operator, I can see a **Plugin checkbox list** in the Admin Page Properties
  panel (after the Theme dropdown, before SEO fields), scroll through all installed Plugins, and check
  those that should be active for the current page. *`Page.ActivePluginsJson` persists the selection as
  a JSON array of `"Plugin.key[@n]"` refs; `PageHost.razor` reads and emits each selected Plugin before
  the page body renders.* *(test: `PageAdminServiceTests.ActivePlugins_SaveAndLoad_RoundTrip`,
  `PageHost_ActivePlugins_EmittedBeforeBody`.)*
- **MAI-US-H2 ⬜** As an Author, I can inject `{{Plugin.tooltip}}` inline in a page body to activate a
  Plugin for that page without going through the Admin Plugin selection, so I have a one-off escape
  hatch. *`IncludeReferenceParser.TryParseTag` recognizes `ContentKind.Plugin` as a valid first
  segment; `IncludeExpander` emits the Plugin's assets into the page cascade.* *(test:
  `IncludeReferenceParser_ParsesPluginKind`, `IncludeExpander_InlinePlugin_EmitsAssets`.)*
- **MAI-US-H3 ⬜** As an Author, I can place `{{Theme.cyberspace}}` inline in a page body to override
  the page's Theme for asset injection on that page, so I can apply a non-default theme without
  changing the admin-panel Theme selection. *The tag emits no markup; it only swaps the theme asset
  cascade for that page render.* *(test: `IncludeExpander_InlineThemeOverride_SwapsAssetCascade`.)*
- **MAI-US-H4 ⬜** As a Widget-Dev, a Component can declare sub-Components via `[Uses]`/`uses[]` and
  the include expander nests them correctly, so a `TabControl` containing `TabButtonContainer`,
  `TabButton` instances, `TabPageContainer`, and `TabPage` instances (each of which may contain
  `Textbox`) renders the full composite tree. *`ContentKind.Component` is valid in `TryParseUse`
  and `TryParseTag`; `IncludeExpander` recurses through nested Component tokens.* *(test:
  `ComponentNestingTests.TabControl_RendersFullHierarchy_ViaNestedComponentTokens`.)*
- **MAI-US-H5 ⬜** As a Widget-Dev, the library's 43 `.idea`s are split into Themes (8), Plugins (12),
  and Components (23) with correct `ContentKind` on each, all packing clean and passing `ma-idea
  verify`. *(test: `LibraryKindClassificationTests.AllLibraryIdeas_HaveExpectedKind`; live: `ma-idea
  verify` reports compose-graph green with zero kind mismatches.)*

## Epic I — Media storage (A31)

- **MAI-US-I1 ✅** As an Operator, I can point the CMS at Azure Blob Storage by setting
  `Media:Provider=azure` (plus `Media:Azure:ConnectionString` **or** `BlobServiceUri`), and every page
  keeps working untouched, because `/_media/{uid}` is the contract and the backing store is an
  implementation detail. *`MediaProviderSetup.AddConfiguredMediaStore` replaces the local store
  registered by `AddIdeasCore`; an unknown provider or Azure without credentials throws at startup
  rather than silently falling back to disk.*
  *(test: `MediaProviderSetupTests.NoConfiguration_KeepsTheLocalDiskStore`,
  `ProviderAzure_ReplacesTheStoreAndRegistersASigner`,
  `ProviderAzure_CarriesSignedUrlLifetimeThroughToTheEndpointOptions`,
  `ProviderAzure_WithoutCredentials_FailsClosed`, `UnknownProvider_FailsClosed`. Live: the app boots
  clean on `Media:Provider=azure` and pre-existing inline rows still serve 200.)*
- **MAI-US-I2 ✅** As a Visitor, I can scrub through a video on a page, because `/_media/{uid}` 302s to a
  short-lived SAS URL and Azure serves the Range requests directly — the bytes never transit the app.
  *`IMediaUrlSigner` is the optional seam; `AzureBlobUrlSigner` mints a key-signed SAS, a
  user-delegation SAS, or a plain CDN URL under `PublicRead`.*
  *(test: `MediaEndpointTests.RedirectsToASignedUrlWhenASignerIsRegistered`,
  `FallsBackToStreamingWhenTheSignerDeclines`;
  `AzureBlobMediaStoreIntegrationTests.SignedUrlServesTheBytesAndHonoursRangeRequests`,
  `SignedUrlExpires`, `PublicReadModeHandsOutThePlainUrlRebasedOnTheCdnOrigin`. Live against Azurite
  through the running app: 302 → 41,943,040 bytes at the source SHA-256, and a seek to byte 20,000,000
  returning `206 · bytes 20000000-20000999/41943040 · video/mp4`.)*
- **MAI-US-I3 ✅** As an Operator, I can upload a file far larger than memory without the app buffering
  it, because both stores hash in flight over a single sequential pass and spill past the inline
  threshold. *`MediaStreams.CopyAndHashAsync` + `ThresholdSpillStream`; memory is bounded by
  `InlineThresholdBytes`, not by the payload.*
  *(test: `LocalDiskMediaStoreTests.Upload_OverThreshold_SpillsToDiskWithIntactBytesAndHash`,
  `Upload_AtExactlyThreshold_StaysInline`, `ThresholdSpillStreamTests.SpillsOnceAndPreservesEveryByteInOrder`,
  `CopyAndHashMatchesAOneShotHashOverTheSameBytes`,
  `AzureBlobMediaStoreIntegrationTests.LargePayloadStreamsUpAndBackWithItsHashIntact`.)*
- **MAI-US-I4 ✅** As a Visitor, a repeat request for an unchanged asset costs no bytes, and a
  non-inline asset downloads under its real filename. *The endpoint emits an ETag from the stored
  SHA-256, `Last-Modified`, `Cache-Control`, and `Accept-Ranges`; `video/*` and `audio/*` join
  `image/`, `text/` and PDF as inline dispositions.*
  *(test: `MediaEndpointTests.ServesInlinePayloadWithEtagAndRangeSupport`,
  `RepeatRequestWithMatchingEtagIsNotModified`, `ServesAByteRangeOutOfALargeSpilledPayload`,
  `NonInlineTypeIsServedAsAnAttachment`, `UnknownUidIs404`, `DeletedItemIs404`. Live: 200 + ETag,
  206 on Range, 304 on `If-None-Match`.)*
- **MAI-US-I5 ✅** As an Operator, I can get a video into the CMS from the command line rather than
  pushing it through the Admin panel's browser circuit. *`--upload-media <file…> [--folder site]
  [--media-type video] [--dry-run]` streams from disk into the configured store and prints the token
  to paste into a page.* *(test: `UploadMediaCliTests.UploadsAVideoWithTheRightContentTypeAndMediaType`,
  `UploadsEveryFileUpToTheNextFlag`, `DryRunUploadsNothing`, `MissingFileFailsBeforeUploadingAnything`,
  `NoFilesIsAnError`, `UnknownExtensionFallsBackToOctetStream`. Live: a 40 MB upload through the
  running app landed in blob storage with `Bytes` NULL and the source hash intact.)*

> **Where these tests live.** `MediaProviderSetupTests` and `UploadMediaCliTests` are in
> `src/MindAttic.Ideas.Tests`. The store/endpoint fixtures — `LocalDiskMediaStoreTests`,
> `MediaEndpointTests`, `ThresholdSpillStreamTests`, `AzureBlobNamingTests` and
> `AzureBlobMediaStoreIntegrationTests` — live in the sibling **MindAttic.Media** repo
> (`src/MindAttic.Media.Tests`), because that is where the code under test lives. The codex doctor
> only scans this repo's test tree, so it reports those citations as warnings; they are real tests.

- **MAI-US-I6 ⬜** As an Author, I can hand the CMS **pixels instead of a file path** — paste a base64
  image (or a clipboard capture) and have it become a stored asset with a `/_media/{uid}` URL, with
  the reference rewritten to point at the file. *Selecting real files means keeping throwaway files
  around; pasting is the natural motion for a screenshot. This is the inverse of
  [`--extract-media`](AMENDMENTS.md#MAI-A30), which lifts base64 out of a page body that already has
  it — this accepts base64 as an INPUT and never lets it reach a page.* **Lands in MindAttic.Media**
  (a dynamic→static asset conversion on the store), surfaced through the Ideas host CLI and the Admin
  Media panel. *Deferred by the owner 2026-09-04: "a good future feature … put it on the bottom of
  the list."*

## Epic J — Azure deployment (A32)

- **MAI-US-J1 ✅** As a Maintainer, CI can restore and publish this repo without my dev box, because
  every private MindAttic package is vendored into `lib/local-packages/` and `nuget.config` lists it
  first. *A GitHub runner has no `C:\LocalNuGet` and no `../local-feed`, and NuGet tolerates a
  missing local source silently — so the guard is a test, not a comment.*
  *(test: `DeploymentPackagingTests.EveryReferencedMindAtticPackageIsVendoredForCi`,
  `NugetConfigListsTheVendoredFeed`, `VendoredPackagesAreTrackedRatherThanGitIgnored`. Live: a
  Release restore **and** publish seeing only the vendored feed + nuget.org produced a complete 94 MB
  artifact carrying all 51 library `.idea`s.)*
- **MAI-US-J2 ✅** As an Operator, App Service can tell whether the site is alive, because `/_health`
  answers 200 without touching the database. *A health check that hits SQL turns a transient blip
  into a restart loop. Lives under `/_` with the other reserved routes so it cannot shadow a page
  slug.* *(test: `DeploymentPackagingTests.ProductionRequiresItsDataProtectionSettingsByName`
  pins the route and both required production settings; `DeployWorkflowPointsAtProjectsThatExist`
  pins the paths CI hands to dotnet. Live: `/_health` → `200 healthy`, `/frontpage` still 200.)*
- **MAI-US-J3 ✅** As a Maintainer, the engine ships with no known-vulnerable dependency.
  *`System.Security.Cryptography.Xml` 10.0.8 → 10.0.11 (five HIGH advisories), `AngleSharp` 0.17.1 →
  1.7.2 and `HtmlSanitizer` 9.0.892 → 9.2.1039 (GHSA-pgww-w46g-26qg). AngleSharp is load-bearing in
  the render path.* *(test: `DeploymentPackagingTests.SecurityPinnedPackagesAreNotDowngraded` holds
  a version floor per package, because a pinned version is easy to revert in a merge and nothing else
  in the build would notice. Live: the full suite stayed green across the bump, a sweep of 48 pages
  returned all 200 with zero `ma-missing`, and `dotnet list package --vulnerable --include-transitive`
  reports none.)*
- **MAI-US-J4 ✅ As an Operator, I can stand the whole estate up with one command**
  (`./infra/provision.ps1 -ResourceGroup rg-mindattic-ideas`), passwordless throughout: Entra-only
  SQL, no storage shared keys, managed-identity RBAC, and the auth Security bucket generated into
  Key Vault. *`infra/main.bicep` compiles and **validates against the live subscription**
  (`provisioningState: Succeeded`); what-if enumerates the 16 resources; both scripts parse under
  Windows PowerShell 5.1.* **Provisioned and live 2026-09-04** at
  https://mindattic-ideas.azurewebsites.net — 16 resources, 53 content definitions installed on first
  boot, reached over managed identity with no password anywhere
  ([A33](AMENDMENTS.md#MAI-A33)). *(test: `DeploymentPackagingTests` guards the packaging and
  configuration contract the estate depends on.)*
- **MAI-US-J5 🟡 As a Maintainer, a push to `master` builds, migrates and deploys**, with the deploy
  gated on green tests and on the migration having applied. *`.github/workflows/azure-deploy.yml`;
  the migrate stage opens and closes a single-run SQL firewall rule under an Entra token, and the
  running site holds `db_datareader`/`db_datawriter` only, so it cannot issue DDL even by mistake.*
  **🟡 because the workflow itself has never run** — the first deploy was driven by hand
  (`provision.ps1` → `migrate.ps1` → `az webapp deploy`), which proved every stage the workflow
  automates but not the workflow. It still needs `AZURE_WEBAPP_PUBLISH_PROFILE`, and the `ideas`
  entry in `MindAttic.Deploy/projects.json → apps[]` stays `disabled: true` until then
  ([HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2)).

## Epic K — Content portability (A34)

- **MAI-US-K1 ✅** As a Maintainer, I can move an authored site between environments, because
  `--export-content` writes pages, Host/Site settings, per-component metadata and media into one
  `.ideabundle` and `--import-content` applies it. *A `.idea` moves a citizen; this moves what an
  author built with citizens — the thing `--seed` regenerates the shape of but never the curation of
  ([A34](AMENDMENTS.md#MAI-A34)).*
  *(test: `ContentBundleTests.RoundTrip_PreservesTheAuthoredPage`, `SlugFilter_ExportsOnlyTheMatchingSubtree`,
  `PageTree_SurvivesOnParentUid`, `DryRunImport_WritesNothing`. Live: 55 pages / 86 metadata rows /
  7 settings / 12 media exported from the dev database as a 634 KB bundle, imported into a fresh
  LocalDB seeded exactly like production, then served — `/frontpage`, `/projects`, `/personas`,
  `/ideas`, `/chimesh` and the project pages all 200.)*
- **MAI-US-K2 ✅** As an Operator, importing into a database that was seeded independently **adopts**
  its pages instead of colliding with them, because reconciliation is `Uid` first and
  `(SiteId, Slug)` second. *Production already has a `frontpage` under a different uid; a uid-only
  match would hit the unique `(SiteId, Slug)` index rather than update the page I meant.*
  *(test: `ContentBundleTests.ImportAdoptsAnIndependentlySeededPage_BySlug_RatherThanDuplicatingIt`,
  `SecondImportUploadsNothingAndCreatesNothing`. Live: importing into a freshly seeded database
  reported 50 created, 5 updated — the baseline pages adopted, not duplicated; a second run reported
  0 created, 55 updated, 0 media uploaded.)*
- **MAI-US-K3 ✅** As an Author, every media reference still resolves after the move, because the
  store mints media uids and import rewrites `/_media/{uid}`, `<Component.MediaImage uid="…">` and
  uids inside component metadata through an old→new map. *Forcing the exported uid would work on the
  local disk store and corrupt the Azure one, where the blob is addressed by uid.*
  *(test: `ContentBundleTests.MediaUidsAreRemapped_SoEveryReferenceStillResolves`. Live: every
  `/_media/{uid}` on the imported front page returned 200, and a SQL sweep found zero page bodies
  referencing a uid with no matching media row.)*
- **MAI-US-K4 ✅** As an Operator, a bundle cannot silently grant itself raw-markup trust, because the
  import states how many pages carry `Author` trust and `--untrusted` downgrades them.
  *[MAI-LAW-5](BIBLE.md#MAI-LAW-5) stamps trust from the writer's claim; a CLI run against the server
  is strictly more privileged than an Admin, so the trust is honoured — but never quietly.*
  *(test: `ContentBundleTests.UntrustedFlag_DowngradesAuthorTrust`,
  `ABundleFromAFutureFormat_IsRefusedRatherThanPartiallyApplied`, `NotABundle_IsReportedRatherThanThrowing`.)*

## Epic L — Multi-domain (A35)

- **MAI-US-L1 ✅** As an Operator, one Ideas deployment can serve several domains, because
  `ISiteResolver` matches the request host against each site's `HostBindings` and `PageHost` resolves
  `(SiteId, Slug)` against the site it picks. *The column has been in the schema since migration #1
  and was read by nothing until [A35](AMENDMENTS.md#MAI-A35).*
  *(test: `SiteResolutionTests` — hostname/port/wildcard/catch-all matching, precedence, IPv6
  literals, stable tie-breaking. Live: with `mindattic.com` and `ryandebraal.com` bound on one
  instance, the same URL `/frontpage` served each site's own page; `/about` (rdb only) rendered on
  `ryandebraal.com` and 404'd on `mindattic.com`, and `/personas` did the reverse.)*
- **MAI-US-L2 ✅** As an Operator, an existing single-site deployment is unaffected, because a site
  with no bindings still answers every hostname it is the default for. *A regression here would 404
  every deployment that predates the amendment, so it is pinned rather than assumed.*
  *(test: `SiteResolutionTests.TheExistingSingleSiteInstallIsUnaffected`,
  `AnUnboundHostFallsBackToTheDefaultSite`. Live: `127.0.0.1`, bound to nothing, still served the
  default site's front page.)*
- **MAI-US-L3 ✅** As a visitor, the right site keeps answering **after** the page goes interactive,
  because the host is read from `NavigationManager.BaseUri` rather than `IHttpContextAccessor`.
  *`PageHost` is `InteractiveServer`, so `HttpContext` is null for every render after the circuit
  connects — the naive reading works on first paint and silently falls back to the default site on
  every click afterwards.*
  *(test: `SiteResolutionTests.PageHostReadsTheRequestHostFromNavigationManager_NotHttpContext` pins
  the source, because no unit test of the resolver could ever catch this — the guard was confirmed to
  FAIL when the two sources were swapped. Live, in a real browser: with the circuit connected,
  `Blazor.navigateTo('/about')` rendered `RYANDEBRAAL-ABOUT` on the bound host while the identical
  client-side navigation on the other host showed "Page not found". Zero page errors.)*
- **MAI-US-L4 ✅** As an Admin, I can add and bind a domain without touching SQL, because
  **Admin → Sites** manages sites, bindings and the default, and answers "which site would this
  hostname reach?" with the same rule the render path uses. *Deleting a site that still has pages is
  refused ([HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2)) — it would orphan them onto
  whatever site resolved next — and a binding another site already claims is refused, because the
  loser would be invisible with no error anywhere.*
  *(test: `SiteResolutionTests.CreatingASite_NormalizesItsBindings_AndDoesNotStealDefault`,
  `TwoSitesCannotClaimTheSameHostname`, `TheDefaultSiteCannotBeDeleted_AndNeitherCanOneThatStillHasPages`,
  `MakeDefault_LeavesExactlyOneDefault`, `TheResolverAndTheAdminProbeAgree`. Live, driving the real
  panel in a browser as a signed-in admin: creating a site normalized
  `" RyanDeBraal.com , https://www.ryandebraal.com/ "` to `ryandebraal.com, www.ryandebraal.com`;
  the probe answered `WWW.RyanDeBraal.com:5199 → Ryan DeBraal (rdb)` and an unbound host → the default
  site; a duplicate binding was refused with `"ryandebraal.com" is already bound to site "rdb"` and
  created nothing; the default site offered no Delete. Zero page errors.)*
- **MAI-US-L5 ✅** As a Maintainer, a content bundle carries exactly one site, because multi-site made
  the old behaviour wrong: an import that fell back to the default site would republish one domain's
  pages under another. *`--export-content --site <key>` scopes pages and Site-scope settings;
  `--import-content` matches by key and creates the site when absent (`--into-site` overrides).*
  *(test: `ContentBundleTests.ExportCarriesOneSite_AndImportCreatesThatSiteRatherThanDumpingOntoTheDefault`,
  `ExportTakesOnlyTheNamedSitesPages`, `ExportWithAnUnknownSiteKey_FailsRatherThanExportingTheWrongSite`.)*

## Epic M — Showroom mode (A36)

- **MAI-US-M1 ✅** As an Operator, a package a sandbox visitor installs is invisible to every other
  site, because the catalog resolves site-first-then-shared and a site-less lookup means shared-only.
  *Letting a stranger install a `.idea` is the headline claim being demonstrated, so it has to be
  real — and every caller holding no site is a back door into the live catalog.*
  *(test: `SiteScopedCatalogTests` — isolation both ways, site-own-wins precedence, version ordering,
  pinned versions never promoted, Disabled vs Missing preserved, and a catalog implementing only the
  frozen members still working through the appended default methods.)*
- **MAI-US-M2 ✅** As the owner, **the main site is never reset**, because the reset gate refuses the
  default site first and independently of the sandbox flag, and the admin service refuses to create
  the dangerous state from either direction. *Showroom mode contains a routine that deletes content on
  a timer; the safety is structural and deliberately redundant, so neither a row hand-edited in SQL
  nor a future caller that skips the admin service can wipe production.*
  *(test: `SandboxGuardTests.TheDefaultSiteIsNeverResettable_EvenIfItIsSomehowFlaggedAsASandbox`,
  `DueForReset_NeverIncludesTheDefaultSite`, `TheDefaultSiteCannotBePutIntoShowroomMode`,
  `AShowroomSiteCannotBePromotedToDefault`.)*
- **MAI-US-M3 ✅** As a visitor, the showroom is not wiped while I am using it, because liveness comes
  from unrevoked, unexpired `AuthSession.LastSeenUtc` and a per-site grace period. *"The moment they
  leave" would fire between page loads and read as a crash.*
  *(test: `SandboxGuardTests.ALiveSessionHoldsOffTheReset`, `OnceTheGracePeriodPasses_TheShowroomIsDue`,
  `ARevokedSessionDoesNotHoldOffTheReset`.)*
- **MAI-US-M4 ✅** As a visitor, I can upload a `.idea` into the showroom and watch it render, with the
  install landing in my site only, because the install takes an OWNER and every lookup it makes is scoped
  the same way — rows, planning, dependencies, bytes, extraction, assets and the ALC
  ([A37](AMENDMENTS.md#MAI-A37)). *Letting a stranger install a package is the headline claim being
  demonstrated; an install that leaks to the shared scope is a stranger changing what production renders,
  and one that collides on bytes or on the shadow computation is one site serving another site's package.*
  *(test: `SiteScopedInstallTests` — owner stamped on both rows with a sibling asset mount and a site
  blob path, the shared install unchanged, two sites holding one identity without colliding, both copies
  staying live through a PER-SITE shadow computation, disable not crossing the boundary, planning against
  own versions only, no override needed to beat a compiled citizen while a shared install still needs one,
  `requires[]` never satisfied by another site, a seeded page landing in the owning site whatever its
  manifest names, separate extraction roots and asset resolution, `InstallScope.OwnerFor`, and a
  site-visible registry listing.)*
- **MAI-US-M5 ✅** As a visitor, the showroom returns to Day Zero once I log off, restored from the
  baseline `.ideabundle` ([A34](AMENDMENTS.md#MAI-A34)) by a background sweep — through the same
  importer an operator's `--import-content` runs ([A38](AMENDMENTS.md#MAI-A38)). *The sweep decides
  nothing: the executor re-asks the gate immediately before the first delete, so the main site is
  refused from every direction, including a button that should never have offered it.*
  *(test: `SandboxResetTests` — the default site refused with nothing touched even when hand-flagged as
  a sandbox, a policy-less sandbox refused, only the site's own pages/settings/packages dropped while
  the shared library and the real site stand, hard-delete so the baseline can reclaim its slugs,
  `ComponentMetadata` following the pages it keys on, Day Zero restored from a real exported bundle, the
  sandbox's key/name/bindings/sandbox flags surviving that restore, a restore never stealing another
  site's page of the same uid, an unreadable baseline reported rather than swallowed, `LastResetUtc`
  stamped, and the sweep resetting an idle showroom while leaving a live one alone.)*
- **MAI-US-M6 ⬜** As a visitor, the showroom fires up on first navigation, provisioned from the
  baseline rather than kept warm.
- **MAI-US-M7 ⬜** As a visitor, the admin area explains itself — a guided tour with coach marks on
  each panel. *Built raw as a Plugin reusing `Plugin.Tooltip`'s edge-aware positioning rather than
  taking an intro.js dependency, so the tour is CONTENT the showroom activates rather than markup
  baked into admin.*
- **MAI-US-M8 ⬜** As a visitor, I can download sample `.idea` files to upload, so the demo has
  something to demonstrate with.
- **MAI-US-M9 ⬜** As a reader, `/ideas` is a Data page composed of discrete components rather than one
  compiled `Component.IdeasBrochure`. *A brochure that cannot be edited without a redeploy argues
  against the very claim it is making.*

## Priority backlog

**Entries from Epic H** — A26 taxonomy refactor (2026-06-16, [A26](AMENDMENTS.md#MAI-A26)). The headline goal is met:
standalone frontends collapse into Pages with zero-deploy upload (`frontpage` = mindattic.com,
`personas` = Legion.Frontend), RFC 0001 is fully implemented, and the foundation-era definition of
done holds (224 NUnit green + the explicit SQL Server temporal proof + live render checks). New work
enters as new stories.

Shipping record: F6/F8 2026-06-08 · F4/F3 2026-06-08 · F5/A6/A7 2026-06-09 (A21) ·
B5/F7 + RFC 0001 completion 2026-06-09 (A22) · G1/G2/G3 (library mono-repo + Page Properties + SEO) 2026-06-12 (A23/A24).

### Audit log

No story has been *changed* from its original README spec; this file is the first derivation. The README
marks the foundation features ✅ and the frontier 🔨/📋; this file initially downgraded two README items
where the proof was mechanics-only rather than a live e2e, in keeping with
[HOUSE-LAW-8](../../MindAttic.HouseRules.md#HOUSE-LAW-8) (verified, not asserted). Both were subsequently
promoted to ✅ when attended live renders confirmed the HTTP layer on 2026-06-09:

- **MAI-US-A6** — initially 🟡 (the live render through the running host was not yet captured by an
  automated test; only the constituent mechanics were). **Promoted to ✅** when the attended run on
  2026-06-09 confirmed zero `ma-missing` placeholders with all 43 library `.idea`s installed
  ([A21](AMENDMENTS.md#MAI-A21), [BIBLE §6](BIBLE.md#MAI-§6)).
- **MAI-US-F5** — initially ⬜ (preserved from the README's own caveat: "end-to-end render of a real
  packed `.idea` through the running host is not yet verified"). **Promoted to ✅** when the attended run on
  2026-06-09 confirmed `/_ideas/...` asset mounts serving 200 with zero placeholders
  ([A22](AMENDMENTS.md#MAI-A22), [BIBLE §6](BIBLE.md#MAI-§6)).
