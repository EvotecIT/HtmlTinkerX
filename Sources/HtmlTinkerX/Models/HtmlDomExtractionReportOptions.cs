namespace HtmlTinkerX;

/// <summary>Optional expected item counts used to detect changes in a repeated dataset.</summary>
public sealed class HtmlDomExtractionReportOptions {
    /// <summary>Minimum acceptable item count; null imposes no lower bound.</summary>
    public int? MinimumItemCount { get; set; }
    /// <summary>Maximum acceptable item count; null imposes no upper bound.</summary>
    public int? MaximumItemCount { get; set; }
}