namespace HtmlTinkerX;

/// <summary>
/// Represents types of network errors that can occur during resource loading.
/// </summary>
public enum HtmlNetworkErrorType {
    /// <summary>Request was aborted.</summary>
    Aborted,
    /// <summary>Access denied error.</summary>
    AccessDenied,
    /// <summary>Address unreachable.</summary>
    AddressUnreachable,
    /// <summary>Blocked by client.</summary>
    BlockedByClient,
    /// <summary>Blocked by response.</summary>
    BlockedByResponse,
    /// <summary>Connection aborted.</summary>
    ConnectionAborted,
    /// <summary>Connection closed.</summary>
    ConnectionClosed,
    /// <summary>Connection failed.</summary>
    ConnectionFailed,
    /// <summary>Connection refused.</summary>
    ConnectionRefused,
    /// <summary>Connection reset.</summary>
    ConnectionReset,
    /// <summary>Internet disconnected.</summary>
    InternetDisconnected,
    /// <summary>Name not resolved.</summary>
    NameNotResolved,
    /// <summary>Timed out.</summary>
    TimedOut,
    /// <summary>Request failed.</summary>
    Failed
}