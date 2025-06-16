using System;
using System.IO;
using System.Management.Automation;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Cmdlet that inlines CSS for email bodies using PreMailer.Net.
/// </summary>
/// <example>
/// <code>
/// Optimize-Email -Body $html -RemoveComments
/// </code>
/// </example>
[Cmdlet(VerbsCommon.Optimize, "Email", DefaultParameterSetName = "Body")]
[OutputType(typeof(string))]
public sealed class CmdletOptimizeEmail : PSCmdlet {
    /// <summary>
    /// HTML content to process.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Body", ValueFromPipeline = true)]
    [Alias("Content")]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Path to a HTML file to process.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "File")]
    [Alias("File")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Base URI for resolving relative URLs.</summary>
    [Parameter]
    public Uri? BaseUri { get; set; }

    /// <summary>Remove &lt;style&gt; elements after inlining.</summary>
    [Parameter]
    public SwitchParameter RemoveStyleElements { get; set; }

    /// <summary>CSS selector for elements to ignore.</summary>
    [Parameter]
    public string? IgnoreElements { get; set; }

    /// <summary>Additional CSS content to inline.</summary>
    [Parameter]
    public string? Css { get; set; }

    /// <summary>Path to a CSS file to include.</summary>
    [Parameter]
    public string? CssFilePath { get; set; }

    /// <summary>Strip id and class attributes from output.</summary>
    [Parameter]
    public SwitchParameter StripIdAndClassAttributes { get; set; }

    /// <summary>Remove comments from HTML and CSS.</summary>
    [Parameter]
    public SwitchParameter RemoveComments { get; set; }

    /// <summary>Preserve media queries from style nodes.</summary>
    [Parameter]
    public SwitchParameter PreserveMediaQueries { get; set; }

    /// <summary>Use the email formatter when generating HTML.</summary>
    [Parameter]
    public SwitchParameter UseEmailFormatter { get; set; }

    /// <summary>Download CSS from &lt;link&gt; elements.</summary>
    [Parameter]
    public SwitchParameter DownloadRemoteCss { get; set; }

    /// <summary>Add Google Analytics tags.</summary>
    [Parameter]
    public SwitchParameter AddAnalyticsTags { get; set; }

    /// <summary>Value for utm_source.</summary>
    [Parameter]
    public string? AnalyticsSource { get; set; }

    /// <summary>Value for utm_medium.</summary>
    [Parameter]
    public string? AnalyticsMedium { get; set; }

    /// <summary>Value for utm_campaign.</summary>
    [Parameter]
    public string? AnalyticsCampaign { get; set; }

    /// <summary>Value for utm_content.</summary>
    [Parameter]
    public string? AnalyticsContent { get; set; }

    /// <summary>Analytics domain.</summary>
    [Parameter]
    public string? AnalyticsDomain { get; set; }

    private ActionPreference errorAction;

    /// <summary>
    /// Initializes logging and resolves ErrorActionPreference.
    /// </summary>
    protected override void BeginProcessing() {
        var internalLogger = new InternalLogger();
        var internalLoggerPowerShell = new InternalLoggerPowerShell(
            internalLogger,
            WriteVerbose,
            WriteWarning,
            WriteDebug,
            WriteError,
            WriteProgress,
            WriteInformation);
        LoggingMessages.Logger = internalLogger;

        errorAction = (ActionPreference)SessionState.PSVariable.GetValue("ErrorActionPreference");
        if (MyInvocation.BoundParameters.ContainsKey("ErrorAction")) {
            string errorActionString = MyInvocation.BoundParameters["ErrorAction"]?.ToString() ?? string.Empty;
            if (Enum.TryParse(errorActionString, true, out ActionPreference actionPreference)) {
                errorAction = actionPreference;
            }
        }
    }

    /// <summary>
    /// Processes the input HTML or file and outputs optimized HTML.
    /// </summary>
    protected override void ProcessRecord() {
        PreMailerOptions options = new() {
            BaseUri = BaseUri,
            RemoveStyleElements = RemoveStyleElements,
            IgnoreElements = IgnoreElements,
            Css = Css,
            CssFilePath = CssFilePath,
            StripIdAndClassAttributes = StripIdAndClassAttributes,
            RemoveComments = RemoveComments,
            PreserveMediaQueries = PreserveMediaQueries,
            UseEmailFormatter = UseEmailFormatter,
            DownloadRemoteCss = DownloadRemoteCss,
            AddAnalyticsTags = AddAnalyticsTags,
            AnalyticsSource = AnalyticsSource,
            AnalyticsMedium = AnalyticsMedium,
            AnalyticsCampaign = AnalyticsCampaign,
            AnalyticsContent = AnalyticsContent,
            AnalyticsDomain = AnalyticsDomain
        };

        PreMailerResult result = ParameterSetName == "File"
            ? PreMailerClient.MoveCssInlineFromFile(Path, options)
            : PreMailerClient.MoveCssInline(Body, options);

        WriteObject(result.Html);

        foreach (var warning in result.Warnings) {
            LoggingMessages.Logger.WriteWarning(warning.Message);
        }
    }
}
