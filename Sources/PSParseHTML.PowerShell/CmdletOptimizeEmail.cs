using HtmlTinkerX;
using System;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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
public sealed class CmdletOptimizeEmail : AsyncPSCmdlet {
    private static readonly SemaphoreSlim LoggerLease = new(1, 1);

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

    /// <summary>HTTP client used to download linked stylesheets. The caller retains ownership of the client.</summary>
    [Parameter]
    public HttpClient? HttpClient { get; set; }

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
    private InternalLogger? _logger;
    private InternalLogger? _previousLogger;
    private InternalLoggerPowerShell? _loggerBridge;
    private int _loggerLeaseHeld;
    private int _processActive;

    /// <summary>
    /// Initializes logging and resolves ErrorActionPreference.
    /// </summary>
    protected override void BeginProcessing() {
        try {
            LoggerLease.Wait(CancelToken);
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw new PipelineStoppedException();
        }
        Volatile.Write(ref _loggerLeaseHeld, 1);
        try {
            var internalLogger = new InternalLogger();
            _loggerBridge = new InternalLoggerPowerShell(
                internalLogger,
                WriteVerbose,
                WriteWarning,
                WriteDebug,
                WriteError,
                WriteProgress,
                WriteInformation);
            _logger = internalLogger;
            _previousLogger = LoggingMessages.Logger;
            LoggingMessages.Logger = internalLogger;

            errorAction = (ActionPreference)SessionState.PSVariable.GetValue("ErrorActionPreference");
            if (MyInvocation.BoundParameters.ContainsKey("ErrorAction")) {
                string errorActionString = MyInvocation.BoundParameters["ErrorAction"]?.ToString() ?? string.Empty;
                if (Enum.TryParse(errorActionString, true, out ActionPreference actionPreference)) {
                    errorAction = actionPreference;
                }
            }
        } catch {
            DetachLogger();
            throw;
        }
    }

    /// <summary>
    /// Processes the input HTML or file and outputs optimized HTML.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        Interlocked.Increment(ref _processActive);
        try {
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
                HttpClient = HttpClient,
                AddAnalyticsTags = AddAnalyticsTags,
                AnalyticsSource = AnalyticsSource,
                AnalyticsMedium = AnalyticsMedium,
                AnalyticsCampaign = AnalyticsCampaign,
                AnalyticsContent = AnalyticsContent,
                AnalyticsDomain = AnalyticsDomain
            };

            PreMailerResult result = ParameterSetName == "File"
                ? await PreMailerClient
                    .MoveCssInlineFromFileAsync(Path, options, CancelToken)
                    .ConfigureAwait(false)
                : await PreMailerClient
                    .MoveCssInlineAsync(Body, options, CancelToken)
                    .ConfigureAwait(false);

            WriteObject(result.Html);

            foreach (var warning in result.Warnings) {
                LoggingMessages.Logger.WriteWarning(warning.Message);
            }
        } finally {
            if (Interlocked.Decrement(ref _processActive) == 0 &&
                CancelToken.IsCancellationRequested) {
                DetachLogger();
            }
        }
    }

    /// <inheritdoc />
    protected override void EndProcessing() {
        try {
            base.EndProcessing();
        } finally {
            DetachLogger();
        }
    }

    /// <inheritdoc />
    public override void Dispose() {
        base.Dispose();
        if (Volatile.Read(ref _processActive) == 0) {
            DetachLogger();
        }
    }

    private void DetachLogger() {
        if (Interlocked.Exchange(ref _loggerLeaseHeld, 0) == 0) {
            return;
        }

        try {
            _loggerBridge?.Dispose();
            _loggerBridge = null;
            var logger = Interlocked.Exchange(ref _logger, null);
            var previous = Interlocked.Exchange(ref _previousLogger, null);
            if (logger != null && previous != null) {
                _ = Interlocked.CompareExchange(ref LoggingMessages.Logger, previous, logger);
            }
        } finally {
            LoggerLease.Release();
        }
    }
}
