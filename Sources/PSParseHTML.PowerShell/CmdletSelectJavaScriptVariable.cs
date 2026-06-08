using Acornima.Ast;
using HtmlTinkerX;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Finds variable declarations and assignment expressions in an Acornima JavaScript AST or JavaScript source.</summary>
/// <example>
///   <summary>Find a variable declaration by name</summary>
///   <code>ConvertFrom-JavaScriptAst -Content 'const token = "abc";' | Select-JavaScriptVariable -Name token</code>
/// </example>
/// <example>
///   <summary>Read a property from a loose JavaScript assignment</summary>
///   <code>
/// $script = @'
/// $Config = {
///     "fShowPersistentCookiesWarning": false,
///     "urlMsaLogout": "https://example.com/logout",
///     "sCtx": "expected-context"
/// }
/// '@
///
/// Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath sCtx
///   </code>
/// </example>
/// <example>
///   <summary>Match a member assignment and read a nested property path</summary>
///   <code>
/// $script = @'
/// window.$Config = {
///     auth: {
///         urls: {
///             logout: "https://example.com/logout"
///         }
///     }
/// }
/// '@
///
/// Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath auth.urls.logout
///   </code>
/// </example>
/// <example>
///   <summary>Return each matching assignment occurrence</summary>
///   <code>
/// $script = @'
/// $Config = { sCtx: "first" };
/// $Config = { sCtx: "second" };
/// '@
///
/// Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath sCtx
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Select, "JavaScriptVariable", DefaultParameterSetName = ParameterSetSource)]
[OutputType(typeof(PSObject))]
public sealed class CmdletSelectJavaScriptVariable : AsyncPSCmdlet {
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

    /// <summary>Variable names to return.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? Name { get; set; }

    /// <summary>Matches variable names that contain the provided Name values.</summary>
    [Parameter]
    public SwitchParameter Contains { get; set; }

    /// <summary>Matches variable names that start with the provided Name values.</summary>
    [Parameter]
    public SwitchParameter StartsWith { get; set; }

    /// <summary>Returns only variable declarations and skips loose assignment expressions.</summary>
    [Parameter]
    public SwitchParameter DeclarationOnly { get; set; }

    /// <summary>Returns a value from a dotted property path inside the matched JavaScript object or array literal.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? PropertyPath { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (Contains.IsPresent && StartsWith.IsPresent) {
            throw new PSArgumentException("Use either -Contains or -StartsWith, not both.");
        }

        Node root = ParameterSetName == ParameterSetAst
            ? Ast
            : new Acornima.Parser().ParseScript(Source, sourceFile: null, strict: false);

        foreach (VariableDeclaration declaration in HtmlJavaScriptAstUtilities.DescendantNodesAndSelf(root).OfType<VariableDeclaration>()) {
            ThrowIfStopped();
            foreach (VariableDeclarator declarator in declaration.Declarations) {
                ThrowIfStopped();
                if (declarator.Id is not Identifier identifier ||
                    string.IsNullOrEmpty(identifier.Name) ||
                    !IsMatch(identifier.Name, identifier.Name)) {
                    continue;
                }

                WriteVariable(identifier.Name, identifier.Name, declaration.Kind.ToString(), declarator.Init, declarator);
            }
        }

        if (!DeclarationOnly.IsPresent) {
            foreach (AssignmentExpression assignment in HtmlJavaScriptAstUtilities.DescendantNodesAndSelf(root).OfType<AssignmentExpression>()) {
                ThrowIfStopped();
                string? path = HtmlJavaScriptAstUtilities.GetMemberPath(assignment.Left);
                string? name = GetLastPathSegment(path);
                if (name is null || name.Length == 0 || !IsMatch(name, path)) {
                    continue;
                }

                WriteVariable(name, path ?? name, "Assignment", assignment.Right, assignment);
            }
        }

        return Task.CompletedTask;
    }

    private bool IsMatch(string variableName, string? variablePath) {
        if (Name == null || Name.Length == 0) {
            return true;
        }

        foreach (string name in Name) {
            ThrowIfStopped();
            if (Contains.IsPresent && (variableName.Contains(name) || (variablePath?.Contains(name) ?? false))) {
                return true;
            }

            if (StartsWith.IsPresent && (variableName.StartsWith(name) || (variablePath?.StartsWith(name) ?? false))) {
                return true;
            }

            if (!Contains.IsPresent && !StartsWith.IsPresent && (variableName == name || variablePath == name)) {
                return true;
            }
        }

        return false;
    }

    private void WriteVariable(string name, string path, string kind, Node? valueNode, Node node) {
        object? value = HtmlJavaScriptAstUtilities.EvaluateJavaScriptLiteral(valueNode);
        if (PropertyPath == null || PropertyPath.Length == 0) {
            WriteVariableObject(name, path, kind, null, value, valueNode, node);
            return;
        }

        foreach (string propertyPath in PropertyPath) {
            ThrowIfStopped();
            WriteVariableObject(name, path, kind, propertyPath, HtmlJavaScriptAstUtilities.GetPropertyPathValue(value, propertyPath), valueNode, node);
        }
    }

    private void WriteVariableObject(string name, string path, string kind, string? propertyPath, object? value, Node? valueNode, Node node) {
        PSObject result = new();
        result.Properties.Add(new PSNoteProperty("Name", name));
        result.Properties.Add(new PSNoteProperty("Path", path));
        result.Properties.Add(new PSNoteProperty("Kind", kind));
        if (propertyPath != null) {
            result.Properties.Add(new PSNoteProperty("PropertyPath", propertyPath));
        }

        result.Properties.Add(new PSNoteProperty("Value", value));
        result.Properties.Add(new PSNoteProperty("RawValue", GetRawValue(valueNode)));
        result.Properties.Add(new PSNoteProperty("Node", node));
        WriteObject(result);
    }

    private static string? GetLastPathSegment(string? path) {
        if (string.IsNullOrEmpty(path)) {
            return null;
        }

        int separator = path!.LastIndexOf('.');
        return separator >= 0 ? path.Substring(separator + 1) : path;
    }

    private static string? GetRawValue(Node? node) {
        return node is Literal literal ? literal.Raw : null;
    }
}
