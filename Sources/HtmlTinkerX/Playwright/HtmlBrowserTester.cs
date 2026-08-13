using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace HtmlTinkerX;

/// <summary>
/// Provides advanced browser testing capabilities with detailed network and console monitoring.
/// </summary>
public static class HtmlBrowserTester {
    /// <summary>
    /// Performs comprehensive browser testing on a URL.
    /// </summary>
    /// <param name="url">URL to test.</param>
    /// <param name="engine">Browser engine to use.</param>
    /// <param name="headless">Run browser in headless mode.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="proxy">Proxy server URL.</param>
    /// <param name="proxyUsername">Proxy username.</param>
    /// <param name="proxyPassword">Proxy password.</param>
    /// <param name="ignoreHttpsErrors">Ignore HTTPS certificate errors.</param>
    /// <returns>Detailed test results.</returns>
    public static async Task<HtmlBrowserTestResult> TestUrlAsync(
        string url,
        HtmlBrowserEngine engine = HtmlBrowserEngine.Chromium,
        bool headless = true,
        int timeout = 30000,
        string? proxy = null,
        string? proxyUsername = null,
        string? proxyPassword = null,
        bool ignoreHttpsErrors = false) {
        
        var result = new HtmlBrowserTestResult { Url = url };
        var startTime = DateTimeOffset.UtcNow;

        IPlaywright? playwright = null;
        try {
            // Ensure Playwright and browser are installed
            await HtmlBrowser.EnsureInstalledAsync(engine);

            // Create a browser session without navigating first
            playwright = await Playwright.CreateAsync();
            try {
                var browserType = engine switch {
                    HtmlBrowserEngine.Firefox => playwright.Firefox,
                    HtmlBrowserEngine.WebKit => playwright.Webkit,
                    _ => playwright.Chromium
                };

                var launchOptions = new BrowserTypeLaunchOptions { Headless = headless };
                if (!string.IsNullOrEmpty(proxy)) {
                    launchOptions.Proxy = new Proxy {
                        Server = proxy!,
                        Username = proxyUsername,
                        Password = proxyPassword
                    };
                }

                var browser = await browserType.LaunchAsync(launchOptions);
                try {
                    var contextOptions = new BrowserNewContextOptions { IgnoreHTTPSErrors = ignoreHttpsErrors };
                    var context = await browser.NewContextAsync(contextOptions);
                    try {
                        var page = await context.NewPageAsync();

                        // Set timeout
                        page.SetDefaultTimeout(timeout);

                        // Enhanced network and console monitoring - set up BEFORE navigation
                        var networkEntries = InitNetworkListeners(page, result);

                        // Navigate and measure
                        try {
                            await page.GotoAsync(url, new PageGotoOptions {
                                WaitUntil = WaitUntilState.NetworkIdle,
                                Timeout = timeout
                            });
                        } catch (Exception ex) {
                            result.ConsoleEntries.Add(new HtmlConsoleEntryDetailed {
                                Text = ex.Message,
                                Type = HtmlConsoleMessageType.Error,
                                Timestamp = DateTimeOffset.UtcNow
                            });
                        } finally {
                            result.PageLoadTime = DateTimeOffset.UtcNow - startTime;
                        }

                        // Wait a bit for any delayed console messages or network requests
                        await page.WaitForTimeoutAsync(1000);
                    } finally {
                        await context.CloseAsync();
                    }
                } finally {
                    await browser.CloseAsync();
                }
        } finally {
            if (playwright is not null) {
                await playwright.DisposeAsync();
            }
        }
    } catch (Exception ex) {
        result.ConsoleEntries.Add(new HtmlConsoleEntryDetailed {
            Text = ex.Message,
            Type = HtmlConsoleMessageType.Error,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    // Ensure PageLoadTime is always set
    if (!result.PageLoadTime.HasValue) {
        result.PageLoadTime = DateTimeOffset.UtcNow - startTime;
    }

    return result;
}
    
    /// <summary>
    /// Tests if a specific CSS resource is loaded.
    /// </summary>
    /// <param name="url">URL to test.</param>
    /// <param name="cssUrl">CSS resource URL fragment to find.</param>
    /// <param name="engine">Browser engine to use.</param>
    /// <param name="ignoreHttpsErrors">Ignore HTTPS certificate errors.</param>
    public static async Task<HtmlNetworkEntryDetailed?> TestCssResourceAsync(
        string url,
        string cssUrl,
        HtmlBrowserEngine engine = HtmlBrowserEngine.Chromium,
        bool ignoreHttpsErrors = false) {
        
        var result = await TestUrlAsync(url, engine, ignoreHttpsErrors: ignoreHttpsErrors);
        return result.CssResources.FirstOrDefault(r => r.Url.Contains(cssUrl));
    }
    
    /// <summary>
    /// Tests for console errors on a page.
    /// </summary>
    /// <param name="url">URL to test.</param>
    /// <param name="engine">Browser engine to use.</param>
    /// <param name="ignoreHttpsErrors">Ignore HTTPS certificate errors.</param>
    public static async Task<IList<HtmlConsoleEntryDetailed>> TestConsoleErrorsAsync(
        string url,
        HtmlBrowserEngine engine = HtmlBrowserEngine.Chromium,
        bool ignoreHttpsErrors = false) {
        
        var result = await TestUrlAsync(url, engine, ignoreHttpsErrors: ignoreHttpsErrors);
        return result.ConsoleErrors.ToList();
    }
    
    /// <summary>
    /// Tests page performance metrics.
    /// </summary>
    /// <param name="url">URL to test.</param>
    /// <param name="engine">Browser engine to use.</param>
    /// <param name="ignoreHttpsErrors">Ignore HTTPS certificate errors.</param>
    public static async Task<HtmlPerformanceMetrics> TestPerformanceAsync(
        string url,
        HtmlBrowserEngine engine = HtmlBrowserEngine.Chromium,
        bool ignoreHttpsErrors = false) {
        
        var result = await TestUrlAsync(url, engine, ignoreHttpsErrors: ignoreHttpsErrors);
        return result.GetPerformanceMetrics();
    }
    
    /// <summary>
    /// Tests a local HTML file from disk.
    /// </summary>
    /// <param name="filePath">Path to the HTML file.</param>
    /// <param name="engine">Browser engine to use.</param>
    /// <param name="headless">Run in headless mode.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="ignoreHttpsErrors">Ignore HTTPS certificate errors.</param>
    /// <returns>Detailed test results.</returns>
    public static async Task<HtmlBrowserTestResult> TestFileAsync(
        string filePath,
        HtmlBrowserEngine engine = HtmlBrowserEngine.Chromium,
        bool headless = true,
        int timeout = 30000,
        bool ignoreHttpsErrors = false) {
        
        Uri fileUri = HtmlBrowser.CreateLocalFileUri(filePath);
        string resolvedPath = fileUri.LocalPath;
        if (!System.IO.File.Exists(resolvedPath)) {
            throw new System.IO.FileNotFoundException($"HTML file not found: {resolvedPath}");
        }

        // Test the file URL
        return await TestUrlAsync(fileUri.AbsoluteUri, engine, headless, timeout, ignoreHttpsErrors: ignoreHttpsErrors);
    }

    /// <summary>
    /// Initializes network and console listeners for the given page.
    /// </summary>
    /// <param name="page">Playwright page instance.</param>
    /// <param name="result">Result object to collect events.</param>
    /// <returns>Dictionary tracking network entries.</returns>
    private static IDictionary<IRequest, HtmlNetworkEntryDetailed> InitNetworkListeners(
        IPage page,
        HtmlBrowserTestResult result) {
        var networkEntries = new ConcurrentDictionary<IRequest, HtmlNetworkEntryDetailed>();

        page.Request += (_, request) => {
            var entry = new HtmlNetworkEntryDetailed {
                Url = request.Url,
                Method = HtmlEnumParser.ParseHttpMethod(request.Method),
                RequestHeaders = new Dictionary<string, string>(request.Headers),
                Started = DateTimeOffset.UtcNow,
                ResourceType = DetermineResourceType(request),
                Initiator = request.Failure ?? string.Empty,
                PostData = request.PostData
            };

            if (request.Headers.TryGetValue("content-length", out var reqSize)) {
                if (long.TryParse(reqSize, out var size)) {
                    entry.RequestHeadersSize = size;
                }
            }

            networkEntries.GetOrAdd(request, entry);
        };

        page.Response += (_, response) => {
            if (networkEntries.TryGetValue(response.Request, out var entry)) {
                entry.Status = (System.Net.HttpStatusCode)response.Status;
                entry.ProtocolVersion = response.GetType()
                    .GetProperty("Protocol")?
                    .GetValue(response) as string;
                entry.ResponseHeaders = new Dictionary<string, string>(response.Headers);
                entry.ResponseReceived = DateTimeOffset.UtcNow;
                entry.ContentType = response.Headers.TryGetValue("content-type", out var ct) ? ct : null;
                entry.ContentEncoding = response.Headers.TryGetValue("content-encoding", out var ce) ? ce : null;
                entry.ServedFromCache = response.FromServiceWorker;

                if (response.Headers.TryGetValue("content-length", out var respSize)) {
                    if (long.TryParse(respSize, out var size)) {
                        entry.ResponseBodySize = size;
                    }
                }

                // Calculate header sizes
                entry.ResponseHeadersSize = response.Headers.Sum(h => h.Key.Length + h.Value.Length + 4); // +4 for ": " and "\r\n"
            }
        };

        page.RequestFinished += (_, request) => {
            if (networkEntries.TryGetValue(request, out var entry)) {
                if (entry.Finished is null) {
                    entry.Finished = DateTimeOffset.UtcNow;
                    result.NetworkEntries.Add(entry);
                }
            }
        };

        page.RequestFailed += (_, request) => {
            if (networkEntries.TryGetValue(request, out var entry)) {
                entry.ErrorType = ParseNetworkError(request.Failure);
                entry.ErrorMessage = request.Failure;
                if (entry.Finished is null) {
                    entry.Finished = DateTimeOffset.UtcNow;
                    result.NetworkEntries.Add(entry);
                }
            }
        };

        page.Console += async (_, msg) => {
            var entry = new HtmlConsoleEntryDetailed {
                Text = msg.Text,
                Type = HtmlEnumParser.ParseConsoleMessageType(msg.Type),
                Timestamp = DateTimeOffset.UtcNow,
                Location = msg.Location
            };

            if (!string.IsNullOrEmpty(msg.Location)) {
                var parts = msg.Location.Split(':');
                if (parts.Length >= 2) {
                    entry.SourceUrl = parts[0];
                    if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 2], out var line)) {
                        entry.LineNumber = line;
                        if (int.TryParse(parts[parts.Length - 1], out var col)) {
                            entry.ColumnNumber = col;
                        }
                    }
                }
            }

            if (entry.IsError || entry.IsWarning) {
                try {
                    var args = await Task.WhenAll(msg.Args.Select(async arg => {
                        try {
                            return await arg.JsonValueAsync<object>().ConfigureAwait(false);
                        } catch {
                            return null;
                        }
                    })).ConfigureAwait(false);
                    entry.Arguments = args.Where(a => a != null).ToList()!;
                } catch {
                    // Ignore serialization errors
                }
            }

            result.ConsoleEntries.Add(entry);
        };

        return networkEntries;
    }
    
    private static HtmlNetworkResourceType DetermineResourceType(IRequest request) {
        return HtmlEnumParser.ParseNetworkResourceType(request.ResourceType);
    }
    
    private static HtmlNetworkErrorType ParseNetworkError(string? error) {
        if (string.IsNullOrEmpty(error))
            return HtmlNetworkErrorType.Failed;
            
        return error!.ToLowerInvariant() switch {
            var e when e.Contains("abort") => HtmlNetworkErrorType.Aborted,
            var e when e.Contains("accessdenied") => HtmlNetworkErrorType.AccessDenied,
            var e when e.Contains("addressunreachable") => HtmlNetworkErrorType.AddressUnreachable,
            var e when e.Contains("blockedbyclient") => HtmlNetworkErrorType.BlockedByClient,
            var e when e.Contains("blockedbyresponse") => HtmlNetworkErrorType.BlockedByResponse,
            var e when e.Contains("connectionaborted") => HtmlNetworkErrorType.ConnectionAborted,
            var e when e.Contains("connectionclosed") => HtmlNetworkErrorType.ConnectionClosed,
            var e when e.Contains("connectionfailed") => HtmlNetworkErrorType.ConnectionFailed,
            var e when e.Contains("connectionrefused") => HtmlNetworkErrorType.ConnectionRefused,
            var e when e.Contains("connectionreset") => HtmlNetworkErrorType.ConnectionReset,
            var e when e.Contains("internetdisconnected") => HtmlNetworkErrorType.InternetDisconnected,
            var e when e.Contains("namenotresolved") => HtmlNetworkErrorType.NameNotResolved,
            var e when e.Contains("timedout") => HtmlNetworkErrorType.TimedOut,
            _ => HtmlNetworkErrorType.Failed
        };
    }
}
