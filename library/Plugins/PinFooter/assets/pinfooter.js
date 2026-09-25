// ════════════════════════════════════════════════════════════════════════
// Pin-when-short footer
// ════════════════════════════════════════════════════════════════════════
// If the document is shorter than the viewport, pin any element with
// class `.pin-when-short` to the bottom of the viewport so the page
// doesn't end with awkward dead space. As soon as content overflows we
// let the element flow normally — a pinned footer over scrollable content
// would cover the last line.
//
// Companion CSS lives alongside this file in pin-footer.css (`.pin-when-short.pinned`).
//
// Opt-in: `<footer class="pin-when-short">...</footer>`
// Multiple targets are supported; each toggles independently.

(function () {
    'use strict';

    // Idempotency: marker-block syncs can splice this script into a page that
    // already runs it. Bail out so we don't stack duplicate observers/listeners.
    if (window.__pinFooterInited) return;
    window.__pinFooterInited = true;

    // CMS adapter: instance setting (MAI-A45) "enabled" read LIVE from
    // <data data-ma-settings="plugin.pinfooter" value="{json}">; absent = pin (the verbatim behavior).
    var settingsCache = { raw: null, parsed: {} };
    function pinEnabled() {
        var el = document.querySelector('[data-ma-settings="plugin.pinfooter"]');
        var raw = el ? el.getAttribute('value') : null;
        if (raw !== settingsCache.raw) {
            settingsCache.raw = raw;
            try { settingsCache.parsed = raw ? JSON.parse(raw) : {}; } catch (e) { settingsCache.parsed = {}; }
        }
        return settingsCache.parsed.enabled !== false;
    }

    function pinAll() {
        var targets = document.querySelectorAll('.pin-when-short');
        if (!pinEnabled()) {
            for (var j = 0; j < targets.length; j++) targets[j].classList.remove('pinned');
            return;
        }
        var hasScrollbar = document.documentElement.scrollHeight > window.innerHeight;
        for (var i = 0; i < targets.length; i++) {
            targets[i].classList.toggle('pinned', !hasScrollbar);
        }
    }

    // Coalesce rapid triggers (mutation bursts, resize storms) into one rAF.
    var rafId = 0;
    function schedulePin() {
        if (rafId) return;
        rafId = requestAnimationFrame(function () {
            rafId = 0;
            pinAll();
        });
    }

    function start() {
        pinAll();
        window.addEventListener('resize', schedulePin);
        // Re-evaluate after fonts/images settle (late reflow common on mindattic.com).
        if (document.fonts && document.fonts.ready) {
            document.fonts.ready.then(pinAll).catch(function () {});
        }
        // Catch any DOM growth that pushes content past the viewport later.
        new MutationObserver(schedulePin).observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();


/* CMS adapter (the only non-verbatim block): the bundle re-pins on resize/load/fonts-ready, but a
 * Blazor host swaps the prerendered DOM after load — nudge its own resize listener when the body
 * mutates so a re-created .pin-when-short footer pins again. Debounced via rAF. */
(function () {
  'use strict';
  if (window.__maPinFooterAdapter) return;
  window.__maPinFooterAdapter = true;
  var scheduled = false;
  function nudge() {
    if (scheduled) return;
    scheduled = true;
    requestAnimationFrame(function () {
      scheduled = false;
      window.dispatchEvent(new Event('resize'));
    });
  }
  function start() { new MutationObserver(nudge).observe(document.body, { childList: true, subtree: true }); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
  else start();
})();
