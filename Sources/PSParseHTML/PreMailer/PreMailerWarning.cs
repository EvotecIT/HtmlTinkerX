namespace PSParseHTML;

/// <summary>
/// Represents a warning returned from PreMailer processing.
/// </summary>
public class PreMailerWarning
{
    /// <summary>Warning message.</summary>
    public string Message { get; }

    /// <summary>Warning severity or type.</summary>
    public string Severity { get; }

    public PreMailerWarning(string message, string severity = "Warning")
    {
        Message = message;
        Severity = severity;
    }
}
