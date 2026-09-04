# ma-shoot — photograph every MindAttic project

Captures a screenshot of each project for its Ideas brochure page, then those images go into
MindAttic.Media and are referenced from a page as `<Component.MediaImage uid="…" />`.

```pwsh
cd tools/shoot
npm install
node shoot.mjs --list          # what would run
node shoot.mjs                 # everything enabled
node shoot.mjs --only hyperspace
```

Output lands in `out/` (gitignored) with an `index.json` describing the run. To ingest:

```pwsh
dotnet run --project src/MindAttic.Ideas.Blazor -- --upload-media <abs paths…> --folder screenshots
```

## Why a manifest instead of detection

A survey of the 34 repos found static sites, Vite apps, ASP.NET hosts, WPF apps, console tools and
pure libraries — and heuristics get it wrong in ways worse than useless. A stray `index.html` in a
docs folder outranks the real application; Prose's project scan turns up eight `.csproj` files,
several inside `.claude/worktrees` and not source at all. Worse, one repo often has **several**
visual surfaces (Prose has a CLI *and* an observer UI), so "the" screenshot for a repo is not even a
well-formed idea.

So every shot is declared in [`shots.json`](shots.json): reviewable, re-runnable, and corrected by
editing one entry rather than fighting a guess.

## Drivers

| Driver | For | How |
|---|---|---|
| `web-static` | a repo with a root `index.htm(l)` | serves the directory over http, then shoots it |
| `web-cmd` | a build step or a dev server | runs `build` then serves `serveDir`, or runs `run` and waits for `readyUrl` |
| `console` | a CLI | runs the command, captures stdout, composes it as a terminal card |

**Why http and not `file://`** — modules, `fetch()`, workers and textures all trip CORS or an opaque
origin under `file://`, so a 3D gallery that works when double-clicked renders an empty canvas under
automation. Serving over http keeps the capture honest.

**Why the console driver renders rather than screenshots a window** — a real console screenshot needs
the window visible, sized, unobscured and on a machine with a desktop session, none of which holds on
a build agent, and it differs every run. Capturing stdout and composing it is deterministic, works
headless, keeps ANSI colour, and looks better than OS console chrome.

## Shot options

`viewport` `[w,h]` · `scale` (device pixel ratio, default 2) · `settleMs` (a 3D scene needs *frames*,
not a load event — `networkidle` fires long before the first render) · `waitFor` (selectors) ·
`actions` (`click` / `press` / `evaluate` / `waitMs`, for dismissing a splash or entering a gallery) ·
`clip` (screenshot one element) · `enabled: false` to park a shot.
