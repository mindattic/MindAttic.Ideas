// Target glue (NOT canonical UiUx source): points console-bg.js at the circuitboard textures bundled
// in THIS Plugin's wwwroot, served under /_ideas/Plugin/cyberspace/1/. Must load BEFORE console-bg.js,
// which reads window.__cyberspaceCircuitboardSrcs at init (else it falls back to /api/media/* and 404s).
window.__cyberspaceCircuitboardSrcs = [
  '/_ideas/Plugin/cyberspace/1/assets/circuitboard.00.png',
  '/_ideas/Plugin/cyberspace/1/assets/circuitboard.01.png',
  '/_ideas/Plugin/cyberspace/1/assets/circuitboard.02.png'
];

// Instance settings (MAI-A45): the plugin renders <data data-ma-settings="plugin.cyberspace" value="{json}">.
// The engine calls this LIVE (every tick / frame), so a page's settings apply after in-circuit navigation too.
(function () {
  var cache = { raw: null, parsed: {} };
  window.__cyberspaceFx = function () {
    var el = document.querySelector('[data-ma-settings="plugin.cyberspace"]');
    var raw = el ? el.getAttribute('value') : null;
    if (raw !== cache.raw) {
      cache.raw = raw;
      try { cache.parsed = raw ? JSON.parse(raw) : {}; } catch (e) { cache.parsed = {}; }
    }
    return cache.parsed;
  };
})();
