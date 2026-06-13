/* MindAttic.Ideas.Widget.Carousel — wires any .ma-carousel of .ma-slide children: arrows, dots,
 * Left/Right keys, optional data-autoplay="ms" (pauses on hover/focus). A MutationObserver wires
 * carousels rendered at any time. Safe to load more than once. No external requests. */
(function () {
  'use strict';
  if (window.__maCarousel) return;
  window.__maCarousel = true;

  function wire(root) {
    if (root.__maCarouselWired) return;
    root.__maCarouselWired = true;

    var slides = Array.prototype.filter.call(root.children, function (el) {
      return el.classList.contains('ma-slide');
    });
    if (slides.length < 2) { if (slides[0]) slides[0].classList.add('ma-slide-active'); return; }

    var index = 0, timer = null;
    var interval = parseInt(root.getAttribute('data-autoplay'), 10) || 0;

    var dots = document.createElement('div');
    dots.className = 'ma-carousel-dots';
    var dotEls = slides.map(function (_, i) {
      var d = document.createElement('button');
      d.type = 'button';
      d.className = 'ma-carousel-dot';
      d.setAttribute('aria-label', 'Slide ' + (i + 1));
      d.addEventListener('click', function () { show(i); });
      dots.appendChild(d);
      return d;
    });

    function arrow(cls, text, dir) {
      var b = document.createElement('button');
      b.type = 'button';
      b.className = 'ma-carousel-arrow ' + cls;
      b.textContent = text;
      b.setAttribute('aria-label', dir > 0 ? 'Next slide' : 'Previous slide');
      b.addEventListener('click', function () { show(index + dir); });
      return b;
    }

    function show(i) {
      index = (i + slides.length) % slides.length;
      slides.forEach(function (s, n) { s.classList.toggle('ma-slide-active', n === index); });
      dotEls.forEach(function (d, n) { d.setAttribute('aria-current', n === index ? 'true' : 'false'); });
    }

    root.appendChild(arrow('ma-carousel-prev', '‹', -1));
    root.appendChild(arrow('ma-carousel-next', '›', 1));
    root.appendChild(dots);
    root.tabIndex = 0;
    root.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowLeft') show(index - 1);
      else if (e.key === 'ArrowRight') show(index + 1);
    });

    if (interval > 0) {
      var play = function () { stop(); timer = setInterval(function () { show(index + 1); }, interval); };
      var stop = function () { if (timer) { clearInterval(timer); timer = null; } };
      root.addEventListener('mouseenter', stop);
      root.addEventListener('mouseleave', play);
      root.addEventListener('focusin', stop);
      root.addEventListener('focusout', function (e) {
        if (root.contains(e.relatedTarget)) return; // focus moved within the carousel
        play();
      });
      play();
    }
    show(0);
  }

  function wireAll(scope) {
    Array.prototype.forEach.call(scope.querySelectorAll('.ma-carousel'), wire);
  }

  function start() {
    wireAll(document);
    new MutationObserver(function (muts) {
      muts.forEach(function (m) {
        Array.prototype.forEach.call(m.addedNodes, function (n) {
          if (n.nodeType !== 1) return;
          if (n.classList && n.classList.contains('ma-carousel')) wire(n);
          else if (n.querySelectorAll) wireAll(n);
        });
      });
    }).observe(document.body, { childList: true, subtree: true });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
  else start();
})();
