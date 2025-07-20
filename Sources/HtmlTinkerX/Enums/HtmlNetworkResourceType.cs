namespace HtmlTinkerX;

/// <summary>
/// Represents the type of network resource requested by the browser.
/// </summary>
public enum HtmlNetworkResourceType {
    /// <summary>HTML document.</summary>
    Document,
    /// <summary>CSS stylesheet.</summary>
    Stylesheet,
    /// <summary>Image resource.</summary>
    Image,
    /// <summary>Media resource (audio/video).</summary>
    Media,
    /// <summary>Font resource.</summary>
    Font,
    /// <summary>JavaScript file.</summary>
    Script,
    /// <summary>Text track (subtitles).</summary>
    TextTrack,
    /// <summary>XMLHttpRequest or fetch.</summary>
    XHR,
    /// <summary>Fetch API request.</summary>
    Fetch,
    /// <summary>Event source.</summary>
    EventSource,
    /// <summary>WebSocket connection.</summary>
    WebSocket,
    /// <summary>Web manifest.</summary>
    Manifest,
    /// <summary>Other resource type.</summary>
    Other
}