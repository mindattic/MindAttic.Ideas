// Lazy-loads Monaco editor from CDN and provides a Blazor-callable façade (MaMonaco).
// init() is called from MonacoEditor.razor via IJSRuntime after first render.
(function () {
    'use strict';

    var _ready = false;
    var _queue = [];
    var _tokens = [];
    var _completionsRegistered = false;
    var _editors = new Map();
    var _nextId = 0;

    function _run(fn) {
        if (_ready) { fn(); return; }
        _queue.push(fn);
        if (_queue.length > 1) return; // CDN load already in flight

        var cfg = document.createElement('script');
        cfg.textContent = "var require={paths:{vs:'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.52.2/min/vs'}};";
        document.head.appendChild(cfg);

        var loader = document.createElement('script');
        loader.src = 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.52.2/min/vs/loader.js';
        loader.onload = function () {
            require(['vs/editor/editor.main'], function () {
                _ready = true;
                var pending = _queue.splice(0);
                for (var i = 0; i < pending.length; i++) pending[i]();
            });
        };
        document.head.appendChild(loader);
    }

    function _registerCompletions() {
        if (_completionsRegistered) return;
        _completionsRegistered = true;
        monaco.languages.registerCompletionItemProvider('html', {
            triggerCharacters: ['{'],
            provideCompletionItems: function (model, position) {
                var line = model.getLineContent(position.lineNumber);
                if (line.substring(0, position.column - 1).indexOf('{{') === -1) {
                    return { suggestions: [] };
                }
                return {
                    suggestions: _tokens.map(function (t) {
                        return {
                            label: t.label,
                            kind: monaco.languages.CompletionItemKind.Snippet,
                            insertText: t.token,
                            detail: t.kind + ' — ' + t.displayName,
                            range: {
                                startLineNumber: position.lineNumber,
                                endLineNumber: position.lineNumber,
                                startColumn: position.column,
                                endColumn: position.column
                            }
                        };
                    })
                };
            }
        });
    }

    window.MaMonaco = {
        init: function (el, dotnetRef, initialValue, tokenJson) {
            _tokens = tokenJson ? JSON.parse(tokenJson) : [];
            var id = 'mce' + (_nextId++);
            el.dataset.mceId = id;
            _run(function () {
                _registerCompletions();
                var editor = monaco.editor.create(el, {
                    value: initialValue || '',
                    language: 'html',
                    theme: 'vs-dark',
                    minimap: { enabled: false },
                    wordWrap: 'on',
                    lineNumbers: 'off',
                    scrollBeyondLastLine: false,
                    fontSize: 13,
                    automaticLayout: true
                });
                editor.onDidChangeModelContent(function () {
                    dotnetRef.invokeMethodAsync('OnValueChanged', editor.getValue());
                });
                _editors.set(id, editor);
            });
        },

        setValue: function (el, value) {
            var id = el && el.dataset && el.dataset.mceId;
            var ed = id && _editors.get(id);
            if (ed) {
                var v = value || '';
                if (ed.getValue() !== v) ed.setValue(v);
            }
        },

        dispose: function (el) {
            var id = el && el.dataset && el.dataset.mceId;
            var ed = id && _editors.get(id);
            if (ed) { ed.dispose(); _editors.delete(id); }
        }
    };
}());
