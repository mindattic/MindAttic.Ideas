/* MindAttic.Ideas.Widget.Footer — pin-when-short (the mindattic.com footer behavior).
 * While the document is shorter than the viewport the footer fixes to the bottom edge; as soon
 * as content grows past one screen it flows normally (the class comes off, so it can never
 * overlap content). Re-evaluated on resize and on DOM growth. Safe to load more than once. */
(function () {
  'use strict';
  if (window.__maFooter) return;
  window.__maFooter = true;

  function update() {
    var footer = document.querySelector('.ma-footer');
    if (!footer) return;
    // Measure with the pin off so a pinned footer doesn't hide its own height from the test.
    footer.classList.remove('ma-footer-pinned');
    var short = document.documentElement.scrollHeight <= window.innerHeight;
    if (short) footer.classList.add('ma-footer-pinned');
  }

  function start() {
    update();
    window.addEventListener('resize', update);
    new MutationObserver(update).observe(document.body, { childList: true, subtree: true });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
  else start();
})();
