using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that executes JavaScript against HTML using AngleSharp.Js.
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "HTMLDomScript", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(object))]
public sealed class CmdletInvokeHtmlDomScript : AsyncPSCmdlet {
    private const string ParameterSetContent = "Content";
    private const string ParameterSetPath = "Path";

    /// <summary>HTML content to process.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetContent, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    public string Content { get; set; } = string.Empty;

    /// <summary>Path to a HTML file.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ParameterSetPath)]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>JavaScript code to run.</summary>
    [Parameter(Mandatory = true)]
    public string Script { get; set; } = string.Empty;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string html = ParameterSetName == ParameterSetPath
            ? await HtmlUtilities.ReadFileCheckedAsync(Path).ConfigureAwait(false)
            : Content;

        object? result = await HtmlScriptRunner.RunAsync<object>(html, Script).ConfigureAwait(false);
        WriteObject(result);
    }
}
