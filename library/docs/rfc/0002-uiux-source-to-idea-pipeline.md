---
codex: 1
project: MindAttic.Ideas.Library
code: MAIL
layer: rfc
status: planned
updated: 2026-06-07
---

# RFC 0002 — UiUx raw-source → `.idea` build pipeline

## Problem
Components are authored as **raw js/css/html** in **MindAttic.UiUx** (the source-of-truth for
markup/styles). The shippable **`.idea`** projects live in **MindAttic.Ideas.Library**. Today the
Library's `.idea` projects are **hand-maintained** — effectively a second copy of what UiUx already
holds. The duplicate `.idea` projects that had crept into UiUx (`Ideas/MindAttic.Ideas.*`) were
**deleted** (the Library is the single home), but that leaves the raw→`.idea` step manual.

## Decision — DEFERRED (do later)
Per direction: getting widgets working in Ideas came first; the dedup pipeline is a follow-up.
Captured here so the intent isn't lost.

## Sketch (not yet built)
A small generator (its **own repo/tool**, e.g. `MindAttic.Ideas.Forge`) that:
1. Reads a UiUx component bundle — its assets (`*.css`/`*.js`/`*.html`) + a `deps.json`/descriptor
   (kind = `Widget`|`Theme`, key, version, `uses[]`, asset list).
2. Emits the corresponding `MindAttic.Ideas.<Kind>.<Key>` project (or packs the `.idea` directly)
   into `MindAttic.Ideas.Library` — namespace/`@inherits`/mount by convention, assets copied to
   `assets/`, so the output matches what's there now by hand.
3. Re-runs `ma-idea pack` + `verify` so `dist/` stays the single source of installable packages.

Net effect: UiUx stays raw source; the Library's `.idea`s become **generated, not duplicated**.

## What NOT to do
- Don't reintroduce `.idea` *projects* into UiUx — it stays raw source.
- Don't hand-edit generated Library projects once the pipeline exists (regenerate instead).

## Graduates into
A real tool + a Library `docs/` note on "regenerate, don't hand-edit." Until then, Library `.idea`
projects are authored/maintained by hand (current state).
