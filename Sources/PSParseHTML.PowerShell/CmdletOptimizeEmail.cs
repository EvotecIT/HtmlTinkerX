namespace PSParseHTML.PowerShell;

[Cmdlet(VerbsCommon.Optimize, "Email", DefaultParameterSetName = "Body", SupportsShouldProcess = true)]
[CmdletBinding()]
public sealed class CmdletOptimizeEmail : PSCmdlet {
    [Parameter(Mandatory = true, ParameterSetName = "Body")]
    [Alias("Content")]
    public string Body { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Body")]
    public SwitchParameter RemoveComments { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Body")]
    public SwitchParameter RemoveStyleElements { get; set; }

    private ActionPreference errorAction;

    protected override void BeginProcessing() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        var internalLogger = new InternalLogger();
        var internalLoggerPowerShell = new InternalLoggerPowerShell(internalLogger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        LoggingMessages.Logger = internalLogger;

        // Get the error action preference as user requested
        // It first sets the error action to the default error action preference
        // If the user has specified the error action, it will set the error action to the user specified error action
        errorAction = (ActionPreference)this.SessionState.PSVariable.GetValue("ErrorActionPreference");
        if (this.MyInvocation.BoundParameters.ContainsKey("ErrorAction")) {
            string errorActionString = this.MyInvocation.BoundParameters["ErrorAction"].ToString();
            if (Enum.TryParse(errorActionString, true, out ActionPreference actionPreference)) {
                errorAction = actionPreference;
            }
        }
    }

    protected override void ProcessRecord() {
        var result = PreMailer.Net.PreMailer.MoveCssInline(Body, removeComments: RemoveComments, removeStyleElements: RemoveStyleElements);
        WriteObject(result.Html);

        // Log the information message
        foreach (var warning in result.Warnings) {
            LoggingMessages.Logger.WriteWarning(warning);
        }
    }
}