using HtmlTinkerX;
using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that executes JavaScript against HTML using AngleSharp.Js.
/// </summary>
/// <example>
/// <code>
/// Invoke-HtmlBrowserDomScript -Content '&lt;p id="value"&gt;Hello&lt;/p&gt;' -Script 'document.getElementById("value").textContent' -Timeout 3000 -RegexTimeout 250 -SkipPageScripts
/// </code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HtmlBrowserDomScript", DefaultParameterSetName = ParameterSetContent)]
[OutputType(typeof(object))]
[Alias("Invoke-HtmlDomScript")]
public sealed class CmdletInvokeHtmlBrowserDomScript : AsyncPSCmdlet {
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

    /// <summary>Whole DOM execution timeout in milliseconds, including inline page scripts. Defaults to 10000.</summary>
    [Parameter]
    [PSDefaultValue(Value = 10000)]
    [ValidateRange(1, int.MaxValue)]
    public int Timeout { get; set; } = 10000;

    /// <summary>Maximum native regex match duration in milliseconds. Defaults to 1000 and bounds cancellation latency during a match.</summary>
    [Parameter]
    [PSDefaultValue(Value = HtmlScriptRunOptions.DefaultRegexTimeoutMilliseconds)]
    [ValidateRange(1, int.MaxValue - 1)]
    public int RegexTimeout { get; set; } = HtmlScriptRunOptions.DefaultRegexTimeoutMilliseconds;

    /// <summary>Maximum statements per script execution. Defaults to 1000000.</summary>
    [Parameter]
    [PSDefaultValue(Value = HtmlScriptRunOptions.DefaultMaximumStatements)]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumStatements { get; set; } = HtmlScriptRunOptions.DefaultMaximumStatements;

    /// <summary>Maximum managed bytes allocated by each script execution. Defaults to 67108864 bytes. This does not bound the DOM or process memory.</summary>
    [Parameter]
    [PSDefaultValue(Value = HtmlScriptRunOptions.DefaultMaximumMemoryBytes)]
    [ValidateRange(1L, long.MaxValue)]
    public long MaximumMemoryBytes { get; set; } = HtmlScriptRunOptions.DefaultMaximumMemoryBytes;

    /// <summary>Maximum decoded HTML input length, including file input. Defaults to 16777216 characters.</summary>
    [Parameter]
    [PSDefaultValue(Value = HtmlScriptRunOptions.DefaultMaximumHtmlCharacters)]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumHtmlCharacters { get; set; } = HtmlScriptRunOptions.DefaultMaximumHtmlCharacters;

    /// <summary>Maximum requested script length. Defaults to 1048576 characters.</summary>
    [Parameter]
    [PSDefaultValue(Value = HtmlScriptRunOptions.DefaultMaximumScriptCharacters)]
    [ValidateRange(1, int.MaxValue)]
    public int MaximumScriptCharacters { get; set; } = HtmlScriptRunOptions.DefaultMaximumScriptCharacters;

    /// <summary>Skip scripts and event handlers in the HTML and evaluate only the requested script.</summary>
    [Parameter]
    public SwitchParameter SkipPageScripts { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string html = ParameterSetName == ParameterSetPath
            ? await HtmlUtilities.ReadFileCheckedAsync(Path, MaximumHtmlCharacters, CancelToken).ConfigureAwait(false)
            : Content;

        object? result = await HtmlScriptRunner.RunAsync<object>(html, Script, new HtmlScriptRunOptions {
            ExecutionTimeout = TimeSpan.FromMilliseconds(Timeout), RegexTimeout = TimeSpan.FromMilliseconds(RegexTimeout),
            MaximumStatements = MaximumStatements,
            MaximumMemoryBytes = MaximumMemoryBytes, MaximumHtmlCharacters = MaximumHtmlCharacters,
            MaximumScriptCharacters = MaximumScriptCharacters, ExecutePageScripts = !SkipPageScripts.IsPresent
        }, CancelToken).ConfigureAwait(false);
        WriteObject(result);
    }
}
