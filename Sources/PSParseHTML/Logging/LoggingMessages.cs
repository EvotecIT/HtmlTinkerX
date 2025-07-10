namespace PSParseHTML;

/// <summary>
/// Convenience wrapper exposing a shared <see cref="InternalLogger"/> instance.
/// </summary>
public class LoggingMessages {
    /// <summary>Shared logger instance.</summary>
    public static InternalLogger Logger = new InternalLogger();

    /// <summary>Enable or disable error messages.</summary>
    public static bool Error {
        get => Logger.IsError;
        set => Logger.IsError = value;
    }

    /// <summary>Enable or disable verbose output.</summary>
    public static bool Verbose {
        get => Logger.IsVerbose;
        set => Logger.IsVerbose = value;
    }

    /// <summary>Enable or disable warning messages.</summary>
    public static bool Warning {
        get => Logger.IsWarning;
        set => Logger.IsWarning = value;
    }

    /// <summary>Enable or disable progress reporting.</summary>
    public static bool Progress {
        get => Logger.IsProgress;
        set => Logger.IsProgress = value;
    }

    /// <summary>Enable or disable debug output.</summary>
    public static bool Debug {
        get => Logger.IsDebug;
        set => Logger.IsDebug = value;
    }

}
