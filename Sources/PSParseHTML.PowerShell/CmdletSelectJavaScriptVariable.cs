using Acornima.Ast;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Finds variable declarations in an Acornima JavaScript AST or JavaScript source.</summary>
/// <example>
///   <summary>Find a variable declaration by name</summary>
///   <code>ConvertFrom-JavaScriptAst -Content 'const token = "abc";' | Select-JavaScriptVariable -Name token</code>
/// </example>
[Cmdlet(VerbsCommon.Select, "JavaScriptVariable", DefaultParameterSetName = ParameterSetSource)]
[OutputType(typeof(PSObject))]
public sealed class CmdletSelectJavaScriptVariable : AsyncPSCmdlet {
    private const string ParameterSetSource = "Source";
    private const string ParameterSetAst = "Ast";

    /// <summary>JavaScript content to parse and inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetSource, ValueFromPipeline = true, Position = 0)]
    [Alias("Content")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Acornima AST node to inspect.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetAst, ValueFromPipeline = true, Position = 0)]
    public Node Ast { get; set; } = null!;

    /// <summary>Variable names to return.</summary>
    [Parameter]
    public string[]? Name { get; set; }

    /// <summary>Matches variable names that contain the provided Name values.</summary>
    [Parameter]
    public SwitchParameter Contains { get; set; }

    /// <summary>Matches variable names that start with the provided Name values.</summary>
    [Parameter]
    public SwitchParameter StartsWith { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (Contains.IsPresent && StartsWith.IsPresent) {
            throw new PSArgumentException("Use either -Contains or -StartsWith, not both.");
        }

        Node root = ParameterSetName == ParameterSetAst
            ? Ast
            : new Acornima.Parser().ParseScript(Source, sourceFile: null, strict: false);

        foreach (VariableDeclaration declaration in FindNodes<VariableDeclaration>(root)) {
            foreach (VariableDeclarator declarator in declaration.Declarations) {
                if (declarator.Id is not Identifier identifier || !IsMatch(identifier.Name)) {
                    continue;
                }

                PSObject result = new();
                result.Properties.Add(new PSNoteProperty("Name", identifier.Name));
                result.Properties.Add(new PSNoteProperty("Kind", declaration.Kind.ToString()));
                result.Properties.Add(new PSNoteProperty("Value", GetLiteralValue(declarator.Init)));
                result.Properties.Add(new PSNoteProperty("RawValue", GetRawValue(declarator.Init)));
                result.Properties.Add(new PSNoteProperty("Node", declarator));
                WriteObject(result);
            }
        }

        return Task.CompletedTask;
    }

    private bool IsMatch(string variableName) {
        if (Name == null || Name.Length == 0) {
            return true;
        }

        foreach (string name in Name) {
            if (Contains.IsPresent && variableName.Contains(name)) {
                return true;
            }

            if (StartsWith.IsPresent && variableName.StartsWith(name)) {
                return true;
            }

            if (!Contains.IsPresent && !StartsWith.IsPresent && variableName == name) {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<T> FindNodes<T>(Node root) where T : Node {
        Stack<Node> stack = new();
        stack.Push(root);
        while (stack.Count > 0) {
            Node node = stack.Pop();
            if (node is T matched) {
                yield return matched;
            }

            foreach (Node child in node.ChildNodes) {
                stack.Push(child);
            }
        }
    }

    private static object? GetLiteralValue(Node? node) {
        return node is Literal literal ? literal.Value : null;
    }

    private static string? GetRawValue(Node? node) {
        return node is Literal literal ? literal.Raw : null;
    }
}
