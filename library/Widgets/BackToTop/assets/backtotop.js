/* MindAttic.Ideas.Widget.BackToTop — creates the floating button and shows it after one screen of
 * scroll; click smooth-scrolls to the top. Safe to load more than once. */
(function () {
  'use strict';
  if (window.__maBackToTop) return;
  window.__maBackToTop = true;

  function start() {
    var b = document.createElement('button');
    b.type = 'button';
    b.className = 'ma-backtotop';
    b.setAttribute('aria-label', 'Back to top');
    b.addEventListener('click', function () {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
    document.body.appendChild(b);

    function update() {
      b.classList.toggle('ma-backtotop-show', window.scrollY > window.innerHeight);
    }
    window.addEventListener('scroll', update, { passive: true });
    update();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
  else start();
})();
