/* MindAttic.Ideas.Widget.CodeBlock — dresses every pre>code: adds .ma-code, a copy button, and a
 * data-lang badge. MutationObserver wires blocks rendered at any time. Safe to load more than once. */
(function () {
  'use strict';
  if (window.__maCodeBlock) return;
  window.__maCodeBlock = true;

  function wire(pre) {
    if (pre.__maCodeWired || !pre.querySelector('code')) return;
    pre.__maCodeWired = true;
    pre.classList.add('ma-code');

    var lang = pre.getAttribute('data-lang');
    if (lang) {
      var badge = document.createElement('span');
      badge.className = 'ma-code-lang';
      badge.textContent = lang;
      pre.appendChild(badge);
    }

    var copy = document.createElement('button');
    copy.type = 'button';
    copy.className = 'ma-code-copy';
    copy.textContent = 'Copy';
    copy.addEventListener('click', function () {
      var text = pre.querySelector('code').textContent;
      if (!navigator.clipboard) return; // clipboard API needs a secure context
      navigator.clipboard.writeText(text).then(function () {
        copy.textContent = 'Copied';
        copy.classList.add('ma-code-copied');
        setTimeout(function () {
          copy.textContent = 'Copy';
          copy.classList.remove('ma-code-copied');
        }, 1600);
      }).catch(function () { /* permission denied — leave the button as "Copy" */ });
    });
    pre.appendChild(copy);
  }

  function wireAll(scope) {
    Array.prototype.forEach.call(scope.querySelectorAll('pre'), wire);
  }

  function start() {
    wireAll(document);
    new MutationObserver(function (muts) {
      muts.forEach(function (m) {
        Array.prototype.forEach.call(m.addedNodes, function (n) {
          if (n.nodeType !== 1) return;
          if (n.tagName === 'PRE') wire(n);
          else if (n.querySelectorAll) wireAll(n);
        });
      });
    }).observe(document.body, { childList: true, subtree: true });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
  else start();
})();
