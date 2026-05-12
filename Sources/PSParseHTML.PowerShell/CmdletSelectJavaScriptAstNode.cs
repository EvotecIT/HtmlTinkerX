using Acornima;
using Acornima.Ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Returns descendant nodes from an Acornima JavaScript AST or JavaScript source.</summary>
/// <example>
///   <summary>Select variable declaration nodes</summary>
///   <code>ConvertFrom-JavaScriptAst -Content 'const value = 42;' | Select-JavaScriptAstNode -Type VariableDeclaration</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "JavaScriptAstNode", DefaultParameterSetName = ParameterSetSource)]
[OutputType(typeof(Node))]
public sealed class CmdletSelectJavaScriptAstNode : AsyncPSCmdlet {
    private const string ParameterSetSource = "Source";
    private const string ParameterSetAst = "Ast";

    /// <summary>JavaScript content to parse and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSource, ValueFromPipeline = true, Position = 0)]
    [Alias("Content")]
    [ValidateNotNullOrEmpty]
    public string Source { get; set; } = string.Empty;

    /// <summary>Acornima AST node to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetAst, ValueFromPipeline = true, Position = 0)]
    [ValidateNotNull]
    public Node Ast { get; set; } = null!;

    /// <summary>Node type names to return, such as VariableDeclaration, ObjectExpression, or ClassBody.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? Type { get; set; }

    /// <summary>Includes the root node in the traversal output.</summary>
    [Parameter]
    public SwitchParameter IncludeRoot { get; set; }

    /// <summary>Parses source input as an ECMAScript module.</summary>
    [Parameter(ParameterSetName = ParameterSetSource)]
    public SwitchParameter Module { get; set; }

    /// <summary>Enables Acornima tolerant parsing for source input.</summary>
    [Parameter(ParameterSetName = ParameterSetSource)]
    public SwitchParameter Tolerant { get; set; }

    /// <summary>Preserves parenthesized expression nodes for source input.</summary>
    [Parameter(ParameterSetName = ParameterSetSource)]
    public SwitchParameter PreserveParens { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        Node root = ParameterSetName == ParameterSetAst ? Ast : ParseSource(Source);
        foreach (Node node in EnumerateNodes(root, IncludeRoot.IsPresent)) {
            ThrowIfStopped();
            if (MatchesType(node)) {
                WriteObject(node);
            }
        }

        return Task.CompletedTask;
    }

    private Node ParseSource(string source) {
        ParserOptions options = new() {
            Tolerant = Tolerant.IsPresent,
            PreserveParens = PreserveParens.IsPresent
        };
        Parser parser = new(options);
        return Module.IsPresent
            ? parser.ParseModule(source, sourceFile: null)
            : parser.ParseScript(source, sourceFile: null, strict: false);
    }

    private bool MatchesType(Node node) {
        if (Type == null || Type.Length == 0) {
            return true;
        }

        string runtimeType = node.GetType().Name;
        string fullName = node.GetType().FullName ?? runtimeType;
        string typeText = node.TypeText;
        string enumName = node.Type.ToString();
        return Type.Any(filter => {
            ThrowIfStopped();
            return string.Equals(filter, runtimeType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, fullName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, typeText, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filter, enumName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private IEnumerable<Node> EnumerateNodes(Node root, bool includeRoot) {
        Stack<Node> stack = new();
        if (includeRoot) {
            stack.Push(root);
        } else {
            foreach (Node child in root.ChildNodes.Reverse()) {
                stack.Push(child);
            }
        }

        while (stack.Count > 0) {
            ThrowIfStopped();
            Node node = stack.Pop();
            yield return node;

            foreach (Node child in node.ChildNodes.Reverse()) {
                stack.Push(child);
            }
        }
    }
}
