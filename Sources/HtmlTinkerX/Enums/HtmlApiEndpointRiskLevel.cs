namespace HtmlTinkerX;

/// <summary>
/// Replay and sensitivity risk for a discovered API or form endpoint.
/// </summary>
public enum HtmlApiEndpointRiskLevel {
    /// <summary>Endpoint looks safe to inspect, usually same-origin GET without sensitive query names.</summary>
    Low,

    /// <summary>Endpoint needs operator review, usually external, unknown method, or auth-adjacent.</summary>
    Medium,

    /// <summary>Endpoint should not be replayed casually, usually state-changing or sensitive.</summary>
    High
}
