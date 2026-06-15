namespace HtmlTinkerX;

/// <summary>
/// Reason browserless form relay processing stopped.
/// </summary>
public enum HtmlFormRelayStopReason {
    /// <summary>No further relay form was found.</summary>
    NoRelayForm,
    /// <summary>The configured maximum relay count was reached.</summary>
    MaxRelayCountReached,
    /// <summary>The next relay action crossed hosts and cross-host relay was not allowed.</summary>
    CrossHostBlocked
}
