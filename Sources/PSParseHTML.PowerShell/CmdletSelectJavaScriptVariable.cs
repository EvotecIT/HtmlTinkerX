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
///   <summary>Return only the final assignment when a value is overwritten</summary>
///   <code>
/// $script = @'
/// $Config = { sCtx: "first" };
/// $Config = { sCtx: "second" };
/// '@
///
/// Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath sCtx |
///     Select-Object -Last 1
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
///   <summary>Skip compound assignments whose value depends on previous state</summary>
///   <code>
/// $script = @'
/// $Config += suffix;
/// $Config = { sCtx: "final" };
/// '@
///
/// Select-JavaScriptVariable -Source $script -Name '$Config' -PropertyPath sCtx
///   </code>
/// </example>
/// <example>
///   <summary>Return unknown values when runtime expressions make a property path dynamic</summary>
///   <code>
/// $script = @'
/// const cfg = {
///     [key]: "dynamic",
///     token: "old",
///     ...override,
///     items: ["first", ...extra, "last"],
///     safe: "after"
/// };
///
/// const enabled = !window.disabled;
/// '@
///
/// Select-JavaScriptVariable -Source $script -Name cfg -PropertyPath key,token,items.0,items.2,safe
/// Select-JavaScriptVariable -Source $script -Name enabled
///   </code>
/// </example>
[Cmdlet(VerbsCommon.Select, "JavaScriptVariable", DefaultParameterSetName = ParameterSetSource)]
[Alias("sjsv")]
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
    [Alias("InputObject", "Node")]
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

    /// <summary>Enables Acornima tolerant parsing for JavaScript source input.</summary>
    [Parameter(ParameterSetName = ParameterSetSource)]
    public SwitchParameter Tolerant { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        if (Contains.IsPresent && StartsWith.IsPresent) {
            throw new PSArgumentException("Use either -Contains or -StartsWith, not both.");
        }

        IReadOnlyList<HtmlJavaScriptVariableMatch> matches = ParameterSetName == ParameterSetAst
            ? HtmlJavaScriptVariableSelector.SelectFromAst(Ast, Name, Contains.IsPresent, StartsWith.IsPresent, DeclarationOnly.IsPresent, PropertyPath)
            : HtmlJavaScriptVariableSelector.SelectJavaScript(Source, Name, Contains.IsPresent, StartsWith.IsPresent, DeclarationOnly.IsPresent, PropertyPath, Tolerant.IsPresent);

        foreach (HtmlJavaScriptVariableMatch match in matches) {
            ThrowIfStopped();
            WriteObject(ToPSObject(match));
        }

        return Task.CompletedTask;
    }

    internal static PSObject ToPSObject(HtmlJavaScriptVariableMatch match) {
        PSObject result = new();
        result.Properties.Add(new PSNoteProperty("Name", match.Name));
        result.Properties.Add(new PSNoteProperty("Path", match.Path));
        result.Properties.Add(new PSNoteProperty("Kind", match.Kind));
        if (match.PropertyPath != null) {
            result.Properties.Add(new PSNoteProperty("PropertyPath", match.PropertyPath));
        }

        if (match.ScriptIndex != null) {
            result.Properties.Add(new PSNoteProperty("ScriptIndex", match.ScriptIndex.Value));
            result.Properties.Add(new PSNoteProperty("ScriptType", match.ScriptType ?? string.Empty));
        }

        result.Properties.Add(new PSNoteProperty("Value", match.Value));
        result.Properties.Add(new PSNoteProperty("RawValue", match.RawValue));
        result.Properties.Add(new PSNoteProperty("Node", match.Node));
        return result;
    }
}
