namespace HtmlTinkerX;

/// <summary>
/// Viewport and user agent settings for a mobile device.
/// </summary>
public sealed class HtmlMobileDeviceInfo {
    /// <summary>User agent string.</summary>
    public string UserAgent { get; set; } = string.Empty;
    /// <summary>Viewport width in pixels.</summary>
    public int ViewportWidth { get; set; }
    /// <summary>Viewport height in pixels.</summary>
    public int ViewportHeight { get; set; }
}
