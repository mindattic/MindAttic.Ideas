import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);

/**
 * Renders Mermaid source to a standalone SVG.
 *
 * WHY RENDER HERE INSTEAD OF IN THE BROWSER
 * -----------------------------------------
 * mermaid.min.js is 3.5 MB. Shipping it to every brochure page to draw one flowchart is a poor
 * trade, and it would put a hard JS dependency on pages that otherwise need none (see MAI-A30, where
 * the home page was deliberately made JS-free). Rendering once, at author time, gives a few hundred
 * bytes of SVG that needs no script, scales without blurring, and is a reviewable artifact — the
 * same trade already accepted for screenshots.
 *
 * The cost is that a diagram is a build output: change the source, re-run the tool. That is the
 * intended workflow, and the source stays in the repo as the thing under review.
 */

/** Locates the vendored mermaid UMD bundle (a devDependency — never committed). */
export function mermaidBundlePath() {
  try {
    return require.resolve('mermaid/dist/mermaid.min.js');
  } catch {
    return null;
  }
}

/** Palette per theme, tuned to read on the CMS's dark themes as well as on white. */
const THEMES = {
  dark: {
    background: 'transparent',
    variables: {
      darkMode: 'true',
      fontFamily: 'ui-sans-serif, system-ui, Segoe UI, Roboto, sans-serif',
      fontSize: '15px',
      primaryColor: '#1b2434',
      primaryTextColor: '#e6edf6',
      primaryBorderColor: '#3d5379',
      lineColor: '#7f9bc7',
      secondaryColor: '#182034',
      tertiaryColor: '#131a29',
      mainBkg: '#1b2434',
      nodeBorder: '#3d5379',
      clusterBkg: '#121926',
      clusterBorder: '#2b3850',
      titleColor: '#e6edf6',
      edgeLabelBackground: '#121926',
      textColor: '#c9d6e8',
    },
  },
  light: {
    background: 'transparent',
    variables: {
      darkMode: 'false',
      fontFamily: 'ui-sans-serif, system-ui, Segoe UI, Roboto, sans-serif',
      fontSize: '15px',
      primaryColor: '#eef3fb',
      primaryTextColor: '#16202e',
      primaryBorderColor: '#a8bdd8',
      lineColor: '#54708f',
      mainBkg: '#eef3fb',
      clusterBkg: '#f6f9fd',
      textColor: '#16202e',
    },
  },
};

/**
 * Renders `source` and returns the SVG markup.
 * Runs inside the page so mermaid's own layout engine (dagre) does the work.
 */
export async function renderMermaid(browser, source, { theme = 'dark', scale = 1 } = {}) {
  const bundle = mermaidBundlePath();
  if (!bundle) throw new Error('mermaid is not installed — run `npm install` in tools/shoot');

  const ctx = await browser.newContext({ viewport: { width: 1600, height: 1200 }, deviceScaleFactor: scale });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e)));

  try {
    await page.setContent('<!doctype html><html><body><div id="host"></div></body></html>');
    await page.addScriptTag({ path: bundle });

    const conf = THEMES[theme] ?? THEMES.dark;
    const svg = await page.evaluate(async ({ src, themeVariables, bg }) => {
      // eslint-disable-next-line no-undef
      const m = window.mermaid;
      m.initialize({
        startOnLoad: false,
        theme: 'base',
        themeVariables,
        securityLevel: 'strict',      // no click handlers / html labels from diagram source
        flowchart: { curve: 'basis', htmlLabels: false, padding: 14 },
        sequence: { useMaxWidth: true },
      });
      const { svg } = await m.render('d' + Math.random().toString(36).slice(2), src);
      return svg.replace('<svg ', `<svg style="background:${bg}" `);
    }, { src: source, themeVariables: conf.variables, bg: conf.background });

    if (errors.length) throw new Error(errors.join(' | '));
    return svg;
  } finally {
    await ctx.close();
  }
}

/**
 * Strips author comments before mermaid sees them.
 *
 * Mermaid's own comment handling cannot cope with a BARE `%%` separator line: it removes the comment
 * text but leaves the markers, so two adjacent comment lines collapse into `%%%%flowchart LR` and the
 * diagram dies with "Parse error on line 1" pointing at a line the author never wrote. Since a `.mmd`
 * file is meant to carry the reasoning behind the diagram, comments have to be free — so they are
 * removed here instead. `%%{init: ...}%%` directives are real syntax and are kept.
 */
function stripComments(src) {
  return src
    .split(/\r?\n/)
    .filter((line) => {
      const t = line.trim();
      return !(t.startsWith('%%') && !t.startsWith('%%{'));
    })
    .join('\n')
    .trim();
}

/** Reads a shot's diagram source: inline `mermaid`, or a `.mmd` file relative to tools/shoot. */
export function sourceFor(shot, toolRoot) {
  if (shot.mermaid) return stripComments(shot.mermaid);
  if (shot.mermaidFile) {
    const p = path.join(toolRoot, shot.mermaidFile);
    if (!fs.existsSync(p)) throw new Error(`no diagram source at ${p}`);
    return stripComments(fs.readFileSync(p, 'utf8'));
  }
  throw new Error('diagram shot needs "mermaid" or "mermaidFile"');
}
