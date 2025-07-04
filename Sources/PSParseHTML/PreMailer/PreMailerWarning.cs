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

    /// <summary>
    /// Initializes a new instance of the <see cref="PreMailerWarning"/> class.
    /// </summary>
    /// <param name="message">Warning message text.</param>
    /// <param name="severity">Warning severity or type.</param>
    public PreMailerWarning(string message, string severity = "Warning")
    {
        Message = message;
        Severity = severity;
    }
}
