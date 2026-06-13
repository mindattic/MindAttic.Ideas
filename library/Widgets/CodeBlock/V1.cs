using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.CodeBlock;

/// <summary>
/// MindAttic.Ideas.Widget.CodeBlock.V1 — presentable code blocks as an asset-only activator. Drop
/// the token once and EVERY <c>&lt;pre&gt;&lt;code&gt;</c> on the page gets the code chrome, a
/// one-click copy button, and an optional language badge from <c>data-lang</c>:
/// <code>&lt;pre data-lang="csharp"&gt;&lt;code&gt;var x = 1;&lt;/code&gt;&lt;/pre&gt;</code>
/// Styling + copy only — syntax highlighting is a V2 candidate (kept out so V1 ships zero parsing
/// risk and zero external dependencies).
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/codeblock/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/codeblock.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/codeblock.js" };
}
