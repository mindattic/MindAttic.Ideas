/* MindAttic.Ideas.Widget.Accordion — exclusive-open behavior for .ma-accordion[data-exclusive]
 * (open/close itself is native <details>; this only closes the siblings). Event delegation, so
 * accordions rendered at any time work. Safe to load more than once. */
(function () {
  'use strict';
  if (window.__maAccordion) return;
  window.__maAccordion = true;

  document.addEventListener('toggle', function (e) {
    var details = e.target;
    if (!(details instanceof HTMLElement) || !details.open) return;
    var group = details.closest('.ma-accordion[data-exclusive]');
    if (!group) return;
    Array.prototype.forEach.call(group.querySelectorAll(':scope > details[open]'), function (d) {
      if (d !== details) d.open = false;
    });
  }, true);  // toggle doesn't bubble; capture it.
})();
