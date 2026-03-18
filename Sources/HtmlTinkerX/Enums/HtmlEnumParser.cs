using System;

namespace HtmlTinkerX;

/// <summary>
/// Helper methods for parsing enum values from strings.
/// </summary>
public static class HtmlEnumParser {
    /// <summary>
    /// Converts a console message type string to <see cref="HtmlConsoleMessageType"/>.
    /// </summary>
    public static HtmlConsoleMessageType ParseConsoleMessageType(string? type) => type?.ToLowerInvariant() switch {
        "log" => HtmlConsoleMessageType.Log,
        "debug" => HtmlConsoleMessageType.Debug,
        "info" => HtmlConsoleMessageType.Info,
        "error" => HtmlConsoleMessageType.Error,
        "warning" => HtmlConsoleMessageType.Warning,
        "dir" => HtmlConsoleMessageType.Dir,
        "dirxml" => HtmlConsoleMessageType.DirXml,
        "table" => HtmlConsoleMessageType.Table,
        "trace" => HtmlConsoleMessageType.Trace,
        "clear" => HtmlConsoleMessageType.Clear,
        "startgroup" => HtmlConsoleMessageType.StartGroup,
        "startgroupcollapsed" => HtmlConsoleMessageType.StartGroupCollapsed,
        "endgroup" => HtmlConsoleMessageType.EndGroup,
        "assert" => HtmlConsoleMessageType.Assert,
        "profile" => HtmlConsoleMessageType.Profile,
        "profileend" => HtmlConsoleMessageType.ProfileEnd,
        "count" => HtmlConsoleMessageType.Count,
        "timeend" => HtmlConsoleMessageType.TimeEnd,
        _ => HtmlConsoleMessageType.Unknown
    };

    /// <summary>
    /// Converts an HTTP method string to <see cref="HtmlHttpMethod"/>.
    /// </summary>
    public static HtmlHttpMethod ParseHttpMethod(string? method) => method?.ToUpperInvariant() switch {
        "GET" => HtmlHttpMethod.Get,
        "HEAD" => HtmlHttpMethod.Head,
        "POST" => HtmlHttpMethod.Post,
        "PUT" => HtmlHttpMethod.Put,
        "DELETE" => HtmlHttpMethod.Delete,
        "CONNECT" => HtmlHttpMethod.Connect,
        "OPTIONS" => HtmlHttpMethod.Options,
        "TRACE" => HtmlHttpMethod.Trace,
        "PATCH" => HtmlHttpMethod.Patch,
        _ => HtmlHttpMethod.Other
    };

    /// <summary>
    /// Converts a browser resource type string to <see cref="HtmlNetworkResourceType"/>.
    /// </summary>
    public static HtmlNetworkResourceType ParseNetworkResourceType(string? resourceType) => resourceType?.ToLowerInvariant() switch {
        "document" => HtmlNetworkResourceType.Document,
        "stylesheet" => HtmlNetworkResourceType.Stylesheet,
        "image" => HtmlNetworkResourceType.Image,
        "media" => HtmlNetworkResourceType.Media,
        "font" => HtmlNetworkResourceType.Font,
        "script" => HtmlNetworkResourceType.Script,
        "texttrack" => HtmlNetworkResourceType.TextTrack,
        "xhr" => HtmlNetworkResourceType.XHR,
        "fetch" => HtmlNetworkResourceType.Fetch,
        "eventsource" => HtmlNetworkResourceType.EventSource,
        "websocket" => HtmlNetworkResourceType.WebSocket,
        "manifest" => HtmlNetworkResourceType.Manifest,
        _ => HtmlNetworkResourceType.Other
    };
}
