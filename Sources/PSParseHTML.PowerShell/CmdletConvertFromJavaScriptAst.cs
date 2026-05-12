using Acornima;
using Acornima.Ast;
using HtmlTinkerX;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>Parses JavaScript into an Acornima abstract syntax tree.</summary>
/// <example>
///   <summary>Parse JavaScript content</summary>
///   <code>ConvertFrom-JavaScriptAst -Content 'const value = 42;'</code>
/// </example>
[Cmdlet(VerbsData.ConvertFrom, "JavaScriptAst", DefaultParameterSetName = ParameterSetContent)]
[Alias("ConvertFrom-JSAst")]
[OutputType(typeof(Node))]
public sealed class CmdletConvertFromJavaScriptAst : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetFile = "File";

    /// <summary>JavaScript content to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("FileContent")]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a JavaScript file to parse.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetFile)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Parses the input as an ECMAScript module.</summary>
    [Parameter]
    public SwitchParameter Module { get; set; }

    /// <summary>Enables Acornima tolerant parsing.</summary>
    [Parameter]
    public SwitchParameter Tolerant { get; set; }

    /// <summary>Preserves parenthesized expression nodes.</summary>
    [Parameter]
    public SwitchParameter PreserveParens { get; set; }

    /// <summary>Allows hashbang comments at the start of scripts.</summary>
    [Parameter]
    public SwitchParameter AllowHashBang { get; set; }

    /// <inheritdoc />
    protected override Task ProcessRecordAsync() {
        string source = ParameterSetName == ParameterSetFile
            ? File.ReadAllText(Path.ToFullPath())
            : Content;
        ParserOptions options = new() {
            Tolerant = Tolerant.IsPresent,
            PreserveParens = PreserveParens.IsPresent,
            AllowHashBang = AllowHashBang.IsPresent ? true : null
        };
        Parser parser = new(options);
        Node ast = Module.IsPresent
            ? parser.ParseModule(source, ParameterSetName == ParameterSetFile ? Path : null)
            : parser.ParseScript(source, ParameterSetName == ParameterSetFile ? Path : null, strict: false);

        WriteObject(ast);
        return Task.CompletedTask;
    }
}
