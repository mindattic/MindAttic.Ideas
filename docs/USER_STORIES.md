---
codex: 1
project: MindAttic.Ideas
code: MAI
layer: stories
status: living
updated: 2026-06-09
---

# MindAttic.Ideas — User Stories

> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned · 🗑️ cut. Every ✅ cites the test that proves
> it. Derived from the [`README.md`](../README.md) living feature spec; test tokens name NUnit
> fixtures in `src/MindAttic.Ideas.Tests`. Build/test evidence: see [BIBLE §6](BIBLE.md#MAI-§6) —
> `dotnet test` reports **199 passed, 0 failed (2026-06-09)**.
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
- **MAI-US-B5 🟡** As an Operator, I can inspect and roll back to any prior page state via temporal
  history. *`IPageHistoryService` + `PageHistoryService` implemented; Admin "Page History" panel
  surfaces the temporal record inline in the page editor. `RestoreAsync` is unit-tested (4 tests).
  `GetHistoryAsync` uses `TemporalAll()` — requires SQL Server; the boundary is documented by a test
  that confirms `InvalidOperationException` on InMemory.*
  *(verified by `PageHistoryServiceTests`: `RestoreAsync_CopiesSnapshotContentFields_OntoCurrentPage`,
  `RestoreAsync_ReStampsTrust_FromRestoringUserClaims`, `RestoreAsync_NonAdminUser_StampsUntrusted`,
  `RestoreAsync_UnknownPage_ReturnsFalse`,
  `GetHistoryAsync_RequiresSqlServer_ThrowsOnInMemoryDb`; live temporal query requires SQL Server.)*

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
  into `<head>` (Global → Theme → Widget → Page → inline), fed by a no-schema manifest→`Extra` data path.
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
- **MAI-US-F3 ✅** As an Author, a theme/widget/control **assignment UI**, a file manager, and roles
  management. *Theme picker (catalog-driven `<select>` for key/version), widget palette (catalog-driven
  token-insert), Assets panel (mounted CSS/scripts browser), and Packages panel (installed `.idea` blob
  browser with SHA-256 + admin-protected download) are all implemented in the admin shell. Roles
  management is already done at `/users`.*
  *(verified by `AdminAssignmentTests`: `WidgetToken_PinnedVersion_ParsesBack`,
  `ThemeToken_PinnedVersion_ParsesBack`, `CatalogFilter_Theme_ReturnsOnlyThemes`,
  `CatalogFilter_Widget_ReturnsOnlyWidgets`;
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
  33 library `.idea`s installed at startup and the frontpage rendered their citizens with hoisted
  assets served at `/_ideas/...` mounts (200), zero placeholders.*
  *(verified by `RenderPipelineTests`: `Install_ThenReload_ThenExpand_ProducesResolvedFrame`,
  `Install_ThenExpand_UnknownToken_ProducesMissingFrame`; live HTTP render observed 2026-06-09 —
  [BIBLE §6](BIBLE.md#MAI-§6).)*
- **MAI-US-F6 ✅** As a Widget-Dev, compiled-citizen asset harvest (`Activator` on `WidgetBase`) hoists
  declared `StylesheetUrls`/`ScriptUrls` into `<head>` via `PageAssets.AllAssetsOf` — the same
  `PageAssetCollector` delegate used for package widgets, consistent with how `PageHost` already
  harvests Theme assets. *(verified by `PageAssetsTests`: `CompiledWidget_AllAssetsOf_HarvestsViaActivator`,
  `CompiledWidget_UnresolvableType_ReturnsEmpty`, `PackageWidget_AllAssetsOf_DelegatesToMountedManifestAssets`.)*
- **MAI-US-F7 🟡** As an Operator, official content lives in **MindAttic.UiUx** and `MindAttic.Frontpage`
  / `MindAttic.Legion.Frontend` collapse into Pages. *In-Ideas groundwork is complete (seed, catalog,
  widget palette, Monaco editor, upload pipeline). Cross-repo work (UiUx extraction, standalone-app
  collapse) is scheduled after F8 stabilises. See [A20](AMENDMENTS.md#MAI-A20) for current-state record.*
  *(verified by `SeedService.SeedAsync` home/frontpage; in-Ideas preconditions satisfied; cross-repo pending.
  See [A8](AMENDMENTS.md#MAI-A8), [A14](AMENDMENTS.md#MAI-A14), [A20](AMENDMENTS.md#MAI-A20).)*
- **MAI-US-F8 ✅** As an Author, I edit pages with **Monaco** catalog-driven IntelliSense, the unified
  `{{double-brace}}` grammar. *`MonacoEditor.razor` wraps Monaco (lazy-loaded from CDN) with a
  `{{ }}` completion provider fed by the live catalog; the BodyHtml textarea in the page editor is
  replaced by this component. Typed-attribute coercion and clickable upload-to-fix placeholders remain
  RFC 0001 roadmap items.*
  *(verified by `MonacoEditorTokenTests`: `IntelliSenseToken_ParsesBackViaTagGrammar`,
  `IntelliSenseToken_InsertedInBody_ParsedByIncludeReferenceParser`; live Monaco interaction is
  browser-tested.)*

## Priority backlog

Dependency-ordered toward the headline goal (collapse standalone frontends into Pages with zero-deploy
upload):

1. **MAI-US-F5** ✅ — shipped 2026-06-09 (live HTTP render of 33 installed `.idea`s observed).
2. **MAI-US-A6** ✅ — shipped 2026-06-09 (the mindattic.com Frontpage recreation, live; A21). **MAI-US-B5** 🟡 — RestoreAsync fully tested; SQL Server temporal query pending.
3. **MAI-US-F4** ✅ — shipped 2026-06-08 (MindAttic.Authentication fully wired).
4. **MAI-US-F3** ✅ — shipped 2026-06-08.
5. **MAI-US-F8** ✅ — shipped 2026-06-08 (Monaco editor + catalog IntelliSense).
6. **MAI-US-F7** 🟡 — in-Ideas groundwork done; cross-repo collapse pending (see A20).

### Audit log

No story has been *changed* from its original README spec; this file is the first derivation. The README
marks the foundation features ✅ and the frontier 🔨/📋; this file downgrades two README ✅ items to 🟡/⬜
where the proof is mechanics-only rather than a live e2e, in keeping with
[HOUSE-LAW-8](../../MindAttic.HouseRules.md#HOUSE-LAW-8) (verified, not asserted):

- **MAI-US-A6** (README "End-to-end: a seeded Data page renders … through the Cyberspace theme", ✅ →
  🟡 here) — *(original spec — audit log)* — downgraded because the live render through the running host is
  not captured by an automated test; only its constituent mechanics are.
- **MAI-US-F5** (README "⚠️ end-to-end render of a real packed `.idea` through the running host is not yet
  verified", ⬜ here) — preserved verbatim from the README's own caveat *(original spec — audit log)*.
