---
codex: 1
project: MindAttic.Ideas
code: MAI
layer: stories
status: living
updated: 2026-10-03
---

# MindAttic.Ideas — User Stories

> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test that proves it. Test
> tokens name NUnit fixtures in `src/MindAttic.Ideas.Tests` unless noted. Build/test evidence: see
> [BIBLE §6](BIBLE.md#MAI-§6).
>
> Personas: **Author** (an admin who writes pages), **Operator** (installs/manages `.idea` packages and
> sites), **Visitor** (reads a rendered page), **Citizen-Dev** (builds first-party content in `library/`),
> **Maintainer** (builds and deploys the engine).

## Epic A — Authoring & rendering a page

- **MAI-US-A1 ✅** As an Author, I can place a `<Kind.Key />` tag (optionally `data-version="n"`) in
  free-form markup and have it resolve to the right citizen, so I compose without zones. *Given a body
  with citizen tags, When the reference parser runs, Then dotted, short-form and kind-attribute tags
  parse to the right `(Kind,Key,Version)`; an unknown kind never becomes a Page reference; any stored
  brace token is migrated to a tag.* *(verified by
  `RenderGuardTests.TryParseTag_ShortForm_WithVersionAndDottedKey`,
  `RenderGuardTests.Parse_DottedTag_CollectedWithCorrectKind`,
  `RenderGuardTests.Parse_PascalTag_WithUnrecognisedKind_StillDefaultsToComponent`,
  `LegacyTokenMigrationTests`.)* See [BIBLE §4.4](BIBLE.md#MAI-§4.4).
- **MAI-US-A2 ✅** As a Visitor, a code-page `<CmsInclude>` and a data-page tag render **identically** for
  Resolved/Missing/Disabled outcomes, so the authoring path doesn't change behavior.
  *(verified by `CmsIncludeParityTests.CmsInclude_MatchesDataPageInclude`.)*
- **MAI-US-A3 ✅** As an Author with the `Cms.AuthorRawMarkup` claim, my page is stamped `Author` trust at
  **write time** (without it, `Untrusted`), and my deliberate JavaScript runs from the Page JS field.
  *Given a save, When the writer holds/doesn't hold the claim, Then `BodyTrust` is Author/Untrusted and
  the author Uid is captured (truncated to 64).* *(verified by
  `PageAuthoringTests.Stamp_WithClaim_IsAuthor_AndCapturesUid`, `Stamp_WithoutClaim_IsUntrusted`,
  `Stamp_TruncatesUidTo64`.)* See [MAI-LAW-5](BIBLE.md#MAI-LAW-5).
- **MAI-US-A4 ✅** As a Visitor, every page body is sanitized before it renders: no script, handler,
  executable URL, frame, form or style element survives at any trust level; Author pages keep their
  citizen tags and settings, Untrusted pages keep none. *(verified by `RawContentGateTests`:
  `Author_ScriptStyleHandlersAndFrames_AreStripped`,
  `Author_KeepsCitizenTagsAndTheirSettings_ButNotHandlersOrScriptUrls`,
  `Author_KeepsSanitizedInlineStyleAndCustomProperties`, `Untrusted_DropsCitizenTags`,
  `Untrusted_StripsScriptTag`, `Untrusted_NeutralizesJavascriptUri`;
  `RawTextElementFrameTests.FreeFormPage_EmitsStyleAndScriptAsSingleMarkupFrames`.)* See
  [BIBLE §4.6](BIBLE.md#MAI-§4.6).
- **MAI-US-A5 ✅** As an Author, I can CRUD pages with soft-delete and publish/enable under the Admin
  policy. *(verified by `PageAdminServiceTests`, `AdminServiceContractTests`.)*
- **MAI-US-A6 ✅** As a Visitor, the seeded **Frontpage** Data page renders its library components through
  its theme; the seed creates it, migrates a stock compiled copy to the Data page, and never overwrites an
  admin-authored one. *(verified by `SeededPageRenderTests`:
  `SeedBodyTags_ParseToCorrectKind_FloatingVersion`, `FrontpageBody_AllSeedTokens_ParseFromTheRealSeededPage`,
  `Seed_MigratesStockCodeFrontpage_ToDataPage_ButNeverAnAdminPage`,
  `SeedBody_InstalledTabsComponent_ExpandsToResolvedFrame`.)*
- **MAI-US-A7 ✅** As a Visitor, navigating to the application with **no route** forwards me to the
  site's front page: the Site-scope `page.frontpage` setting, else the Host-scope one (default
  `frontpage`); the stock home page is soft-disabled by the seed. *(verified by
  `SeededPageRenderTests.Seed_SoftDisablesStockHomePage_AndNeverAnEditedOne`; the forward observed live —
  `GET /` → 302 → `/frontpage`.)*

## Epic B — Versioning, lifecycle & history

- **MAI-US-B1 ✅** As an Author, I pin a version (`data-version="3"`) or float to the highest enabled
  version (omit it), so I juggle versions only when I care. *(verified by
  `ContentCatalogTests.FindLatest_StillPicksTheHighestVersion`,
  `PageAssetCollectorTests.PinnedVersion_ResolvesViaFind_FloatingResolvesToHighest`.)*
- **MAI-US-B2 ✅** As an Operator, I cannot delete a version while any page pins it (body tag, `uses[]`,
  theme pin or `ActivePluginsJson`); a floating reference blocks only when deleting would orphan it.
  *(verified by `ContentLifecycleServiceTests`: `PinnedVersion_AlwaysBlocks_AndListsSlug`,
  `FloatingReference_BlocksOnlyWhenItWouldOrphan`, `DisabledOrUnpublishedPage_IsNotABlockingReference`,
  `ActivePluginsJson_VersionedPin_BlocksDeletion`, `ThemePin_Blocks_AndThemeFloatFollowsOrphanRule`; and
  `UsesDeclarationTests.DeleteGuard_BlocksDeletingAComponentACompiledPagePins`.)* See
  [HOUSE-LAW-2](../../MindAttic.HouseRules.md#HOUSE-LAW-2).
- **MAI-US-B3 ✅** As an Operator, disabling a content version reloads the catalog so the tag then
  resolves as Disabled. *(verified by
  `ContentLifecycleServiceTests.SetEnabledFalse_ReloadsCatalog_SoResolveTagReportsDisabled`.)*
- **MAI-US-B4 ✅** As an Operator, the EF model guards reserved columns and a delete-guard projection, so
  integrity holds at the data layer. *(verified by `CmsModelGuardTests`.)*
- **MAI-US-B5 ✅** As an Operator, I can inspect and roll back to any prior page state via temporal
  history in the Admin Page History panel. *(verified by `PageHistoryServiceTests`:
  `RestoreAsync_CopiesSnapshotContentFields_OntoCurrentPage`, `RestoreAsync_ReStampsTrust_FromRestoringUserClaims`,
  `RestoreAsync_NonAdminUser_StampsUntrusted`, `RestoreAsync_UnknownPage_ReturnsFalse`; and the live
  `PageHistorySqlServerTests.GetHistoryAsync_OnSqlServer_ReturnsOrderedTemporalVersions` ([Explicit],
  LocalDB).)*

## Epic C — Degradation & the Admin Inbox

- **MAI-US-C1 ✅** As an Operator, a missing/disabled dependency raises a deduped Admin Inbox message that
  collapses recurrences and reopens after resolution. *(verified by `AdminInboxServiceTests`:
  `RaiseAsync_SameDedupKey_CollapsesToOneRow`, `RaiseAsync_AfterResolve_ReopensToNew`,
  `UnreadCount_CountsOnlyNew`.)*
- **MAI-US-C2 ✅** As a Visitor, the render thread never throws on a bad reference — it degrades to a
  placeholder that links to `/admin/upload?missing=<reference>` and fire-and-forgets the alert.
  *(verified by `RenderGuardTests`: `MissingInclude_RendersPlaceholder_AndRaisesOneMissingAlert`,
  `DisabledInclude_RendersPlaceholder_AndRaisesOneDisabledAlert`,
  `MissingPlaceholder_LinksToAdminUpload_WithTheMissingKey`.)* See [MAI-LAW-7](BIBLE.md#MAI-LAW-7).

## Epic D — The `.idea` package & install

- **MAI-US-D1 ✅** As a Citizen-Dev, the `.idea` manifest kernel reads and validates with explicit errors,
  rejecting host assemblies in `bin/` and retired categories. *(verified by `ManifestReaderTests`,
  `ManifestValidatorTests`, `IdeaArchiveReaderTests`.)*
- **MAI-US-D2 ✅** As a Citizen-Dev, packing is reflection-only and lossless/forward-compatible, with
  SHA-256 integrity and a zip-slip-guarded reader. *(verified by `PackerTests`, `ManifestAssetPackerTests`,
  `Sha256HasherTests`, `PackageExtractorTests`.)* See [HOUSE-LAW-5](../../MindAttic.HouseRules.md#HOUSE-LAW-5).
- **MAI-US-D3 ✅** As an Operator, the whole-number version/collision resolver picks the active version,
  refuses downgrades, and refuses a compiled-citizen collision without confirmation. *(verified by
  `PackageVersionResolverTests`.)*
- **MAI-US-D4 ✅** As an Operator, installing a `.idea` is idempotent: it registers the `InstalledPackage`
  row + a mirrored catalog row, retains prior versions on upgrade, soft-disables, and reloads the catalog.
  *(verified by `PackageInstallServiceTests`, `SeedOnInstallTests`.)*
- **MAI-US-D5 ✅** As an Operator, a package blob is kept verbatim in a blob store for re-share/rollback.
  *(verified by `LocalFilePackageBlobStoreTests`.)*
- **MAI-US-D6 ✅** As an Operator, a local folder source discovers packable `.idea` candidates.
  *(verified by `LocalFolderPackageSourceTests`.)*
- **MAI-US-D7 ✅** As an Operator, only a `.idea` signed by the host-trusted certificate installs, whatever
  path its bytes took. *(verified by `PackageSignerTests`; `PackageInstallServiceTests`:
  `Install_UnsignedPackage_ThrowsPackageSignatureException_NoRowsWritten`,
  `Install_TamperedPackage_ThrowsPackageSignatureException_NoRowsWritten`,
  `Install_UntrustedSigner_ThrowsPackageSignatureException`; `VaultPackageSigningTrustTests`.)* See
  [MAI-LAW-10](BIBLE.md#MAI-LAW-10).
- **MAI-US-D8 ✅** As an Operator, re-publishing the same version with different content is a conflict I
  am told about, not a silent no-op, while a re-sign of unchanged content is a no-op. *(verified by
  `PackageInstallServiceTests.Install_HashConflict_ThrowsInstallException_AndRaisesAdminInboxAlert`,
  `PackageInstallServiceTests.Install_SameVersion_SameContent_ReSignedDifferently_IsStillANoOp`,
  `PackageVersionResolverTests.SameVersion_DifferentHash_IsAConflict_NotANoOp`.)*
- **MAI-US-D9 ✅** As an Operator, a package whose `requires[]` is unmet, or whose `minHostVersion` exceeds
  the host, is refused with nothing written. *(verified by
  `PackageInstallServiceTests.Requires_Missing_ThrowsInstallException_NoRowsWritten`,
  `PackageInstallServiceTests.Requires_AllPresent_InstallSucceeds`, `ManifestValidatorTests`.)*
- **MAI-US-D10 ✅** As an Operator, an `.idealist` entry not present locally is fetched from a NuGet feed
  (`MindAttic.Ideas.{Category}.{Key}` at `{n}.0.0`) and cached. *(verified by
  `NuGetIdeaListPackageResolverTests` against a fake fetcher; the live GitHub Packages fetch is not
  automated.)*

## Epic E — Runtime load & asset cascade

- **MAI-US-E1 ✅** As an Operator, a `.idea` citizen loads through a per-package collectible ALC; host
  types unify by reference identity and others delegate to the default resolver. *(verified by
  `AlcAwareTypeResolverTests`, `CmsPackageLoadContextTests`.)* See [MAI-LAW-6](BIBLE.md#MAI-LAW-6).
- **MAI-US-E2 ✅** As an Author, a page's citizen css/scripts are ordered, deduped, and hoisted into
  `<head>`, fed by the manifest→`Extra` data path. *(verified by `PageAssetCollectorTests`,
  `AssetDataPathTests.Install_Then_Reload_SurfacesManifestCssScripts_OntoDescriptorExtra`,
  `UsesDeclarationTests.Collect_FromUses_HoistsReferencedCitizenAssets`.)*
- **MAI-US-E3 ✅** As a Citizen-Dev, a `[Uses]`/`uses[]` declaration parses (bare floats, pinned,
  case-insensitive kind, rejects malformed) and drives hoisting + the delete-guard. *(verified by
  `UsesDeclarationTests.TryParseUse_BareKey_FloatsToLatest`, `UsesDeclarationTests.TryParseUse_RejectsMalformed`.)*
- **MAI-US-E4 ✅** As an Operator, a corrupt manifest during reload doesn't abort the reload — it leaves
  that descriptor's `Extra` null. *(verified by `AssetDataPathTests.CorruptManifestJson_DoesNotAbortReload_LeavesThatExtraNull`.)*
- **MAI-US-E5 ✅** As an Author, tiers win by cascade layer — Global → Theme → Page → Component — so I never
  need `!important` to beat a lower tier, and a Component keeps its own look on any page. *(verified by
  `CmsHeadCssLayerTests.LayerOrderStatement_IsEmittedOnceAndFirst_EvenWhenAllTiersAreEmpty`,
  `CmsHeadCssLayerTests.ComponentLayer_IsDeclaredAfterPageLayer_SoComponentWinsPrecedence`,
  `FreeFormPageCssTests`.)* See [MAI-LAW-4](BIBLE.md#MAI-LAW-4).
- **MAI-US-E6 ✅** As an Author, saving Untrusted page CSS collapses same-selector duplicate blocks into one
  without touching anything else, and I am warned about a shorthand that overrides some of its longhands.
  *(verified by `CssConflictMergerTests`, `CssShorthandLinterTests`.)*

## Epic F — Admin, CLI and authoring tools

- **MAI-US-F1 ✅** As an Operator, the `ma-idea` CLI can pack / inspect / list / install / verify / sign /
  nupkg. *(CLI in `src/MindAttic.Ideas.Sdk`; pack/validate/sign paths covered by `PackerTests`,
  `ManifestValidatorTests`, `PackageSignerTests`, `ArgParserTests`.)*
- **MAI-US-F2 ✅** As an Operator, the Admin can enable/disable/guarded-delete content definitions and
  triage the Admin Inbox under the Admin policy. *(verified by `AdminServiceContractTests`,
  `UsersAdminContractTests`, `IdeasClaimsAugmentorTests`.)*
- **MAI-US-F3 ✅** As an Author, I have a theme picker, a component palette, an Assets panel and a
  Packages panel (installed blobs with content hash and admin-protected download); roles are managed at
  `/users`. *(verified by `AdminAssignmentTests`: `PluginToken_PinnedVersion_ParsesBack`,
  `ThemeToken_PinnedVersion_ParsesBack`, `CatalogFilter_Theme_ReturnsOnlyThemes`,
  `CatalogFilter_Plugin_ReturnsOnlyPlugins`; `PackageRegistryServiceTests`.)*
- **MAI-US-F4 ✅** As an Operator, I sign in via **MindAttic.Authentication**; the Admin role carries the
  `Cms.AuthorRawMarkup` claim. *(verified by `IdeasClaimsAugmentorTests`, `CmsDbContextAuthModelTests`;
  see [HOUSE-LAW-7](../../MindAttic.HouseRules.md#HOUSE-LAW-7).)*
- **MAI-US-F5 ✅** As a Visitor, a real packed `.idea` renders end-to-end through the running host.
  *(verified by `RenderPipelineTests`: `Install_ThenReload_ThenExpand_ProducesResolvedFrame`,
  `Install_ThenExpand_UnknownToken_ProducesMissingFrame`; live renders serve `/_ideas/...` mounts with
  zero placeholders.)*
- **MAI-US-F6 ✅** As a Citizen-Dev, a compiled citizen's declared `StylesheetUrls`/`ScriptUrls` are
  harvested into `<head>` through the same `PageAssetCollector` path as package citizens. *(verified by
  `PageAssetsTests`: `CompiledPlugin_AllAssetsOf_HarvestsViaActivator`,
  `CompiledPlugin_UnresolvableType_ReturnsEmpty`, `PackagePlugin_AllAssetsOf_DelegatesToMountedManifestAssets`.)*
- **MAI-US-F7 ✅** As an Operator, official content lives in the first-party library and standalone
  frontends collapse into Pages: mindattic.com is the `frontpage` Data page and Legion.Frontend is the
  `personas` page, whose body is one `<Component.LegionPersonas />` tag. *(verified by
  `SeededPageRenderTests.Seed_CreatesPersonasPage_CollapsingLegionFrontendIntoOneToken`; `/personas` and
  `/frontpage` render live with zero placeholders.)*
- **MAI-US-F8 ✅** As an Author, I edit pages with **Monaco** and catalog-driven completion of citizen
  tags; tag attributes bind to typed `[Parameter]`s. *(verified by `MonacoEditorTokenTests`:
  `IntelliSenseTag_ParsesBackViaIncludeReferenceParser`,
  `IntelliSenseTag_InsertedInBody_ParsedByIncludeReferenceParser`; `IncludeAttributeCoercionTests`
  (incl. `Expand_TagAttributes_BindTyped_AndLeaveUnmatchedRaw`); live Monaco interaction is
  browser-tested.)*
- **MAI-US-F9 ✅** As an Author, saving a page tells me about unresolved tags, undeclared attributes,
  badly typed values and anything the sanitizer would strip; a library citizen with unsafe JS/CSS or
  malformed settings cannot be packed; CI checks every shipped package and seed page. *(verified by
  `PageMarkupValidatorTests`, `CitizenValidatorTests`,
  `ShippedContentValidationTests.EveryShippedPackage_PassesCitizenValidation`,
  `ShippedContentValidationTests.EverySeedPage_ValidatesAgainstTheShippedPackages`.)*
- **MAI-US-F11 ✅** As a Citizen-Dev, a package that hard-codes its own asset URLs (Cyberspace's
  circuitboard textures) uses the mount the host serves it under, `/_ideas/{Category}/{key}/{version}`,
  and every file it names is in its assets; a stale category such as the retired `Widget` fails CI.
  *(verified by `ShippedContentValidationTests.EveryShippedPackage_SelfReferencingAssetUrls_MatchItsMountAndExist`,
  `EveryLibrarySource_SelfReferencingAssetUrls_MatchItsMountAndExist`,
  `SelfReferencingAssetUrlCheck_FlagsAWrongCategory`.)*
- **MAI-US-F10 ✅** As a user who forgot my password, I follow "Forgot password?" on `/login`, get an
  emailed link to this site's `/account/reset` page (absolute, from `MindAttic:Auth:Reset:PublicBaseUrl`:
  `https://localhost:7207` in Development; the company site and the demo each set their own in
  `infra/main.bicep`), and setting a new password there replaces the old one. Mail is sent only when the
  Vault `Notifications` SMTP settings are configured. *(verified by
  `PasswordResetFlowTests.RequestReset_EmailsAnAbsoluteLinkToTheResetPage_WhichResetsThePassword`,
  `PasswordResetFlowTests.TheResetAndForgotPages_AreAnonymous`,
  `PasswordResetFlowTests.EachAzureSite_SetsItsOwnPublicBaseUrl`.)*

## Epic G — Page authoring enhancements

- **MAI-US-G1 ✅** As an Author, I set a page's Theme from a dropdown in the collapsible Page Properties
  panel, so theme assignment is metadata, not markup. *(verified by
  `AdminAssignmentTests.CatalogFilter_Theme_ReturnsOnlyThemes`,
  `AdminAssignmentTests.ThemeToken_PinnedVersion_ParsesBack`; the panel UI is browser-confirmed.)*
- **MAI-US-G2 ✅** As an Author, I can set a custom SEO Title and Description for a page.
  *(verified by `PageAdminServiceTests`: `Save_WithSeoFields_PersistsThroughGetAsync`,
  `Save_WithNullSeoFields_ReturnsNullOnLoad`.)*
- **MAI-US-G3 ✅** As a Citizen-Dev, the first-party library lives in the same git repo as the engine
  (`library/`) without coupling the two build graphs. *(Abstractions types used by library citizens are
  exercised by `PackerTests` and `ManifestAssetPackerTests`; `ma-idea verify` is green across all 53
  `.idea`s.)*
- **MAI-US-G4 ✅** As an Author, I configure each citizen instance on a page — a component's tag
  attributes, or the theme/plugin/code-page slots — from a generated editor, and share configuration by
  Copy/Paste between instances of the same citizen. *(verified by `InstanceSettingsTests`,
  `BodyTagIndexTests`, `InstanceClipboardTests`,
  `IdeaListTests.RoundTrip_CarriesThemeAndPluginInstanceSettings`.)* See [BIBLE §4.5](BIBLE.md#MAI-§4.5).
- **MAI-US-G5 ✅** As an Author, I can move a page through a named workflow whose transitions are
  role-gated, and only the `Published` state publishes it. *(verified by `WorkflowServiceTests`.)*
- **MAI-US-G6 ✅** As a Visitor, an old or vanity slug answers 301 to the page's current slug.
  *(verified by `SlugRedirectServiceTests`.)*
- **MAI-US-G7 ✅** As an Author, tagless instance settings keep a version history and roll back.
  *(verified by `WidgetInstanceSettingsServiceTests`.)*

## Epic H — Plugin/Component taxonomy

- **MAI-US-H1 ✅** As an Operator, I pick the Plugins active on a page from a checkbox list in Page
  Properties, or let the page inherit the site's `plugins.default`; an explicitly empty selection means
  none. *(verified by `EffectivePluginsTests`: `NoSelection_InheritsTheSiteDefaults`,
  `ExplicitEmptySelection_MeansNoPlugins`, `OwnSelection_Wins`;
  `ContentLifecycleServiceTests.ActivePluginsJson_VersionedPin_BlocksDeletion`.)*
- **MAI-US-H2 ✅** As an Author, I can place `<Plugin.Tooltip />` inline in a page body to activate a
  Plugin on that page without going through the Admin selection. *(verified by
  `RenderGuardTests.Parse_DottedTag_CollectedWithCorrectKind`,
  `PageAssetsTests.CompiledPlugin_AllAssetsOf_HarvestsViaActivator`.)*
- **MAI-US-H3 ⬜** As an Author, I can place `<Theme.Cyberspace />` inline in a page body to override the
  page's Theme for that render without changing the Page Properties selection. *(not implemented:
  `PageHost` takes the theme from the page or site only.)*
- **MAI-US-H4 ✅** As a Citizen-Dev, Components nest: a paired tag passes its inner tags to the outer
  component as `ChildContent`. *(verified by
  `RenderGuardTests.Expander_NestedPascalTags_OuterReceivesInnerAsChildContent`.)*
- **MAI-US-H5 ✅** As a Citizen-Dev, the library's 53 `.idea`s are Themes (7), Plugins (15) and
  Components (31), each packing clean, and a retired category is a hard validation error. *(verified by
  `ShippedContentValidationTests.EveryShippedPackage_PassesCitizenValidation`,
  `ManifestValidatorTests.RetiredCategory_IsHardError`.)*
- **MAI-US-H6 ✅** As a Citizen-Dev, a Plugin declares whether it renders before or after the body, and
  author order is kept within each slot. *(verified by `PluginSlotTests`.)*
- **MAI-US-H7 ✅** As a Citizen-Dev, a component can list another page's children (scoped to the site)
  and read their metadata in one query. *(verified by `PageTreeFeatureTests`:
  `ChildrenOf_PopulatesPageId_SoMetadataCanBeJoined`, `ChildrenOfSlug_ScopedToSite_ReturnsOnlyThatSitesChildren`,
  `ChildrenOfSlug_UnknownSite_FallsBackToTheUnscopedLookup`,
  `IPageTree_DefaultOverload_DelegatesToTheSlugOnlyForm`; `ComponentMetadataServiceTests`.)*

## Epic I — Media storage

> The store/endpoint fixtures — `LocalDiskMediaStoreTests`, `MediaEndpointTests`,
> `ThresholdSpillStreamTests` and `AzureBlobMediaStoreIntegrationTests` — live in the sibling
> **MindAttic.Media** repo (`src/MindAttic.Media.Tests`), where the code under test lives; the codex
> doctor scans only this repo's test tree and reports them as warnings.

- **MAI-US-I1 ✅** As an Operator, I can point the CMS at Azure Blob Storage by setting
  `Media:Provider=azure` (plus `Media:Azure:ConnectionString` **or** `BlobServiceUri`), and every page
  keeps working untouched, because `/_media/{uid}` is the contract. An unknown provider or Azure without
  credentials fails at startup. *(verified by `MediaProviderSetupTests.NoConfiguration_KeepsTheLocalDiskStore`,
  `ProviderAzure_ReplacesTheStoreAndRegistersASigner`,
  `ProviderAzure_CarriesSignedUrlLifetimeThroughToTheEndpointOptions`,
  `ProviderAzure_WithoutCredentials_FailsClosed`, `UnknownProvider_FailsClosed`.)*
- **MAI-US-I2 ✅** As a Visitor, I can scrub through a video on a page, because `/_media/{uid}` 302s to a
  short-lived SAS URL and Azure serves the Range requests directly. *(verified by
  `MediaEndpointTests.RedirectsToASignedUrlWhenASignerIsRegistered`,
  `FallsBackToStreamingWhenTheSignerDeclines`;
  `AzureBlobMediaStoreIntegrationTests.SignedUrlServesTheBytesAndHonoursRangeRequests`, `SignedUrlExpires`,
  `PublicReadModeHandsOutThePlainUrlRebasedOnTheCdnOrigin`.)*
- **MAI-US-I3 ✅** As an Operator, I can upload a file far larger than memory without the app buffering
  it. *(verified by `LocalDiskMediaStoreTests.Upload_OverThreshold_SpillsToDiskWithIntactBytesAndHash`,
  `Upload_AtExactlyThreshold_StaysInline`, `ThresholdSpillStreamTests.SpillsOnceAndPreservesEveryByteInOrder`,
  `CopyAndHashMatchesAOneShotHashOverTheSameBytes`,
  `AzureBlobMediaStoreIntegrationTests.LargePayloadStreamsUpAndBackWithItsHashIntact`.)*
- **MAI-US-I4 ✅** As a Visitor, a repeat request for an unchanged asset costs no bytes, and a non-inline
  asset downloads under its real filename. *(verified by
  `MediaEndpointTests.ServesInlinePayloadWithEtagAndRangeSupport`,
  `RepeatRequestWithMatchingEtagIsNotModified`, `ServesAByteRangeOutOfALargeSpilledPayload`,
  `NonInlineTypeIsServedAsAnAttachment`, `UnknownUidIs404`, `DeletedItemIs404`.)*
- **MAI-US-I5 ✅** As an Operator, I can get a video into the CMS from the command line with
  `--upload-media <file…> [--folder site] [--media-type video] [--dry-run]`. *(verified by
  `UploadMediaCliTests.UploadsAVideoWithTheRightContentTypeAndMediaType`, `UploadsEveryFileUpToTheNextFlag`,
  `DryRunUploadsNothing`, `MissingFileFailsBeforeUploadingAnything`, `NoFilesIsAnError`,
  `UnknownExtensionFallsBackToOctetStream`.)*
- **MAI-US-I6 ⬜** As an Author, I can hand the CMS **pixels instead of a file path** — paste a base64
  image (or a clipboard capture) and have it become a stored asset with a `/_media/{uid}` URL, so base64
  never reaches a page. Lands in MindAttic.Media, surfaced through the host CLI and the Admin Media panel.
  Lowest priority.
- **MAI-US-I7 ✅** As an Operator, `--extract-media` lifts inline base64 images out of page bodies and
  stylesheets into managed assets, deduplicated by content, leaving undecodable data inline and reported.
  *(verified by `ExtractMediaCliTests`.)*

## Epic J — Azure deployment

- **MAI-US-J1 ✅** As a Maintainer, CI can restore and publish this repo without my dev box, because every
  private MindAttic package is vendored into `lib/local-packages/` and `nuget.config` lists it first.
  *(verified by `DeploymentPackagingTests.EveryReferencedMindAtticPackageIsVendoredForCi`,
  `NugetConfigListsTheVendoredFeed`, `VendoredPackagesAreTrackedRatherThanGitIgnored`.)*
- **MAI-US-J2 ✅** As an Operator, App Service can tell whether the site is alive, because `/_health`
  answers 200 without touching the database. *(verified by
  `DeploymentPackagingTests.ProductionRequiresItsDataProtectionSettingsByName`,
  `DeployWorkflowPointsAtProjectsThatExist`.)*
- **MAI-US-J3 ✅** As a Maintainer, the engine ships with no known-vulnerable dependency, and the security
  floors cannot be silently reverted. *(verified by
  `DeploymentPackagingTests.SecurityPinnedPackagesAreNotDowngraded`.)*
- **MAI-US-J4 ✅** As an Operator, I can stand the whole estate up with one command
  (`./infra/provision.ps1 -ResourceGroup rg-mindattic-ideas`), passwordless throughout. The company site
  and the demo run on it. *(verified by `DeploymentPackagingTests`, which guards the packaging and
  configuration contract the estate depends on; live at https://mindattic.azurewebsites.net.)*
- **MAI-US-J5 ✅** As a Maintainer, a push to `master` builds, tests, migrates and deploys both sites over
  GitHub OIDC, with deploy gated on green tests and on the migration having applied. *(verified by
  `DeploymentPackagingTests.DeployWorkflowPointsAtProjectsThatExist`; the workflow's latest run on
  `master` succeeded 2026-10-03.)*
- **MAI-US-J6 🟡** As a visitor evaluating Ideas, I can sign in to a public demo whose login is revealed
  behind Turnstile and which is wiped and re-provisioned every hour. *(verified by `DemoRevealTests`,
  `AdminBootstrapTests`, `SeedServiceTests.WithAnIdealist_SeedsOnlyTheStructuralMinimum`. 🟡 because the
  hourly `demo-reset.yml` run is currently failing.)*

## Epic K — Content portability

- **MAI-US-K1 ✅** As a Maintainer, I can move an authored site between environments, because
  `--export-idealist` writes pages, settings, per-component metadata and media into one `.idealist` and
  `--import-idealist` applies it. *(verified by `IdeaListTests.RoundTrip_PreservesTheAuthoredPage`,
  `SlugFilter_ExportsOnlyTheMatchingSubtree`, `PageTree_SurvivesOnParentUid`, `DryRunImport_WritesNothing`.)*
- **MAI-US-K2 ✅** As an Operator, importing into an independently seeded database **adopts** its pages
  instead of colliding with them, and importing into another site copies rather than moves.
  *(verified by `IdeaListTests.ImportAdoptsAnIndependentlySeededPage_BySlug_RatherThanDuplicatingIt`,
  `SecondImportUploadsNothingAndCreatesNothing`,
  `IntoSite_CopiesThePagesRatherThanMovingThemOffTheSiteThatHasThem`.)*
- **MAI-US-K3 ✅** As an Author, every media reference still resolves after the move, because import
  rewrites media uids through an old→new map. *(verified by
  `IdeaListTests.MediaUidsAreRemapped_SoEveryReferenceStillResolves`.)*
- **MAI-US-K4 ✅** As an Operator, an idealist cannot silently grant itself raw-markup trust: the import
  states how many pages carry `Author` trust and `--untrusted` downgrades them. *(verified by
  `IdeaListTests.UntrustedFlag_DowngradesAuthorTrust`,
  `AnIdeaListFromAFutureFormat_IsRefusedRatherThanPartiallyApplied`, `NotAnIdeaList_IsReportedRatherThanThrowing`.)*
- **MAI-US-K5 ✅** As an Operator, an idealist's Packages install in order before any content, and an
  unmet page `Uses[]` fails the whole apply with nothing written. *(verified by
  `IdeaListTests.Packages_InstallInListedOrder_LaterEntryCanRequireAnEarlierOne`,
  `PackagesFailure_AbortsBeforeAnyPageIsWritten`, `PageUsesUnmet_FailsLoudly_NoPartialApply`,
  `PackageResolver_SearchesMultipleDirectoriesInOrder_FirstMatchWins`,
  `ComposeIdealist_PackagesOnly_ProducesAnEmptySiteContentFreeArtifact`.)*
- **MAI-US-K6 ✅** As an Operator, a deployment with no idealist configured installs the whole library,
  and one with an idealist applies it and refuses to start if it fails. *(verified by
  `BootProvisioningTests`.)*

## Epic L — Multi-domain

- **MAI-US-L1 ✅** As an Operator, one Ideas deployment can serve several domains, because
  `ISiteResolver` matches the request host against each site's `HostBindings`. *(verified by
  `SiteResolutionTests` — hostname/port/wildcard/catch-all matching, precedence, IPv6 literals, stable
  tie-breaking.)*
- **MAI-US-L2 ✅** As an Operator, an existing single-site deployment is unaffected, because a site with no
  bindings still answers every hostname it is the default for. *(verified by
  `SiteResolutionTests.TheExistingSingleSiteInstallIsUnaffected`, `AnUnboundHostFallsBackToTheDefaultSite`.)*
- **MAI-US-L3 ✅** As a Visitor, the right site keeps answering **after** the page goes interactive,
  because the host is read from `NavigationManager.BaseUri`. *(verified by
  `SiteResolutionTests.PageHostReadsTheRequestHostFromNavigationManager_NotHttpContext`.)*
- **MAI-US-L4 ✅** As an Admin, I can add and bind a domain without touching SQL in **Admin → Sites**.
  *(verified by `SiteResolutionTests.CreatingASite_NormalizesItsBindings_AndDoesNotStealDefault`,
  `TwoSitesCannotClaimTheSameHostname`, `TheDefaultSiteCannotBeDeleted_AndNeitherCanOneThatStillHasPages`,
  `MakeDefault_LeavesExactlyOneDefault`, `TheResolverAndTheAdminProbeAgree`.)*
- **MAI-US-L5 ✅** As a Maintainer, an idealist carries exactly one site and import creates that site by
  key rather than dumping onto the default. *(verified by
  `IdeaListTests.ExportCarriesOneSite_AndImportCreatesThatSiteRatherThanDumpingOntoTheDefault`,
  `ExportTakesOnlyTheNamedSitesPages`, `ExportWithAnUnknownSiteKey_FailsRatherThanExportingTheWrongSite`.)*

## Epic M — Marketing pages

- **MAI-US-M9 ⬜** As a reader, `/ideas` is a Data page composed of discrete components rather than one
  compiled `Component.IdeasBrochure`, so the brochure can be edited without a redeploy.

## Epic N — Themes

- **MAI-US-N1 🟡** As a Visitor, every theme has an AA-compliant light and dark palette; the site's default
  mode applies on first visit, my toggle choice persists, and neither flashes the wrong mode. *(no
  automated test: contrast was checked by hand with a WCAG relative-luminance script against every theme ×
  mode × text/muted/link/button pair.)*

## Priority backlog

1. **MAI-US-J6** — fix the failing hourly `demo-reset.yml` run.
2. **MAI-US-N1** — an automated contrast test over every theme × mode.
3. **MAI-US-M9** — recompose `/ideas` from components.
4. **MAI-US-H3** — inline `<Theme.X />` override.
5. **MAI-US-I6** — paste-to-asset media input.
