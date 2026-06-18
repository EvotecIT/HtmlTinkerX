using HtmlTinkerX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace PSParseHTML.PowerShell;

/// <summary>
/// Converts browser session cookies or HtmlCookie objects into a PowerShell WebRequestSession.
/// </summary>
/// <example>
///   <summary>Reuse an attended browser login with Invoke-WebRequest</summary>
///   <code>
/// $session = Start-HtmlBrowserSession -Url https://portal.contoso.example -Visible -ManualLogin
/// $webSession = ConvertTo-HtmlWebRequestSession -Session $session
/// Invoke-WebRequest -Uri https://portal.contoso.example/report -WebSession $webSession
///   </code>
/// </example>
[Cmdlet(VerbsData.ConvertTo, "HtmlWebRequestSession", DefaultParameterSetName = ParameterSetSession)]
[OutputType(typeof(PSObject))]
[Alias("ConvertTo-HtmlWebSession")]
public sealed class CmdletConvertToHtmlWebRequestSession : AsyncPSCmdlet {
    private const string ParameterSetSession = "Session";
    private const string ParameterSetCookie = "Cookie";
    private readonly List<HtmlCookie> pipelineCookies = new();

    /// <summary>Browser session whose cookies should be copied. When omitted, the default PSParseHTML session is used.</summary>
    [Parameter(Position = 0, ParameterSetName = ParameterSetSession, ValueFromPipeline = true)]
    public HtmlBrowserSession? Session { get; set; }

    /// <summary>Cookies to copy into a PowerShell WebRequestSession.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetCookie, ValueFromPipeline = true)]
    public HtmlCookie[]? Cookie { get; set; }

    /// <summary>Cookie domain filter used when reading cookies from a browser session.</summary>
    [Parameter(ParameterSetName = ParameterSetSession)]
    public string[]? Domain { get; set; }

    /// <summary>Optional User-Agent to set on the WebRequestSession.</summary>
    [Parameter]
    public string? UserAgent { get; set; }

    /// <summary>Optional headers to add to the WebRequestSession.</summary>
    [Parameter]
    public Hashtable? Header { get; set; }

    /// <summary>Include expired cookies instead of skipping them.</summary>
    [Parameter]
    public SwitchParameter IncludeExpired { get; set; }

    /// <summary>Suppress warnings about browser cookies that cannot be represented by System.Net.Cookie.</summary>
    [Parameter]
    public SwitchParameter Quiet { get; set; }

    /// <summary>Token used to cancel the operation.</summary>
    [Parameter]
    public CancellationToken CancellationToken { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (ParameterSetName == ParameterSetCookie) {
            if (Cookie != null) {
                pipelineCookies.AddRange(Cookie);
            }

            return;
        }

        HtmlBrowserSession session = Session ?? (HtmlBrowserSession?)GetVariableValue("PSParseHTML_DefaultSession")
            ?? throw new PSInvalidOperationException("No session provided and no default session found.");
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, CancellationToken);
        CancellationToken token = linkedCts.Token;
        List<HtmlCookie> cookies = await HtmlBrowser.GetCookiesAsync(session, Domain, token).ConfigureAwait(false);
        WriteObject(CreateWebRequestSession(cookies));
    }

    /// <inheritdoc />
    protected override Task EndProcessingAsync() {
        if (ParameterSetName == ParameterSetCookie) {
            WriteObject(CreateWebRequestSession(pipelineCookies));
        }

        return Task.CompletedTask;
    }

    private object CreateWebRequestSession(IEnumerable<HtmlCookie> cookies) {
        Type sessionType = ResolveWebRequestSessionType();
        object webSession = Activator.CreateInstance(sessionType)
            ?? throw new InvalidOperationException("Unable to create a PowerShell WebRequestSession instance.");
        if (!string.IsNullOrWhiteSpace(UserAgent)) {
            sessionType.GetProperty("UserAgent")?.SetValue(webSession, UserAgent);
        }

        if (Header != null) {
            Dictionary<string, string>? headers = sessionType.GetProperty("Headers")?.GetValue(webSession) as Dictionary<string, string>;
            if (headers == null) {
                throw new InvalidOperationException("PowerShell WebRequestSession did not expose a headers dictionary.");
            }

            foreach (DictionaryEntry entry in Header) {
                if (entry.Key == null || entry.Value == null) {
                    continue;
                }

                headers[entry.Key.ToString()!] = entry.Value.ToString() ?? string.Empty;
            }
        }

        CookieContainer cookieContainer = sessionType.GetProperty("Cookies")?.GetValue(webSession) as CookieContainer
            ?? throw new InvalidOperationException("PowerShell WebRequestSession did not expose a CookieContainer.");
        int skipped = 0;
        foreach (HtmlCookie cookie in cookies) {
            if (TryAddCookie(cookieContainer, cookie, out string? warning)) {
                continue;
            }

            skipped++;
            if (!Quiet.IsPresent && !string.IsNullOrWhiteSpace(warning)) {
                WriteWarning(warning!);
            }
        }

        if (skipped > 0 && !Quiet.IsPresent) {
            WriteWarning($"Skipped {skipped} browser cookie(s) that could not be copied to WebRequestSession.");
        }

        return webSession;
    }

    private static Type ResolveWebRequestSessionType() {
        const string typeName = "Microsoft.PowerShell.Commands.WebRequestSession";
        Type? sessionType = Type.GetType(typeName + ", Microsoft.PowerShell.Commands.Utility", throwOnError: false);
        if (sessionType != null) {
            return sessionType;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            sessionType = assembly.GetType(typeName, throwOnError: false);
            if (sessionType != null) {
                return sessionType;
            }
        }

        throw new InvalidOperationException("PowerShell WebRequestSession type is not available in this host.");
    }

    private bool TryAddCookie(CookieContainer container, HtmlCookie source, out string? warning) {
        warning = null;
        if (string.IsNullOrWhiteSpace(source.Name)) {
            warning = "Skipped a browser cookie without a name.";
            return false;
        }

        if (!IncludeExpired.IsPresent && source.Expires.HasValue) {
            DateTimeOffset expires = DateTimeOffset.FromUnixTimeSeconds(source.Expires.Value);
            if (expires <= DateTimeOffset.UtcNow) {
                warning = $"Skipped expired browser cookie '{source.Name}'.";
                return false;
            }
        }

        System.Net.Cookie cookie = new(source.Name, source.Value ?? string.Empty, string.IsNullOrWhiteSpace(source.Path) ? "/" : source.Path!) {
            HttpOnly = source.HttpOnly == true,
            Secure = source.Secure == true
        };

        if (source.Expires.HasValue) {
            cookie.Expires = DateTimeOffset.FromUnixTimeSeconds(source.Expires.Value).UtcDateTime;
        }

        try {
            if (!string.IsNullOrWhiteSpace(source.Url) && Uri.TryCreate(source.Url, UriKind.Absolute, out Uri? uri)) {
                container.Add(uri, cookie);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(source.Domain)) {
                cookie.Domain = source.Domain!;
                container.Add(cookie);
                return true;
            }
        } catch (CookieException ex) {
            warning = $"Skipped browser cookie '{source.Name}' because it is not compatible with WebRequestSession: {ex.Message}";
            return false;
        }

        warning = $"Skipped browser cookie '{source.Name}' because it has neither Url nor Domain.";
        return false;
    }
}
