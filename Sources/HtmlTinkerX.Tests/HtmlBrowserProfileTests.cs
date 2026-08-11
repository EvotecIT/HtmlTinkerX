using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserProfileTests {
    [Fact]
    public async Task BrowserProfile_RoundTripsJsonAndAppliesDefaults() {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string profilePath = Path.Combine(dir, "profile.json");
        HtmlBrowserProfile profile = new() {
            Name = "work-chrome",
            Browser = HtmlBrowserEngine.Chromium,
            UserDataDirectory = Path.Combine(dir, "user-data"),
            BrowserChannel = "chrome",
            Locale = "en-US",
            Timezone = "America/New_York",
            ViewportWidth = 1365,
            ViewportHeight = 768,
            IgnoreHttpsErrors = true,
            PreventSsoAutoSubmit = true
        };
        profile.BrowserArguments.Add("--disable-dev-shm-usage");
        profile.Permissions.Add("geolocation");

        try {
            await profile.SaveAsync(profilePath);
            HtmlBrowserProfile loaded = await HtmlBrowserProfile.LoadAsync(profilePath);

            HtmlBrowserLaunchOptions options = new();
            options.ApplyProfile(loaded);

            Assert.Equal("work-chrome", loaded.Name);
            Assert.Equal("chrome", options.BrowserChannel);
            Assert.Equal("en-US", options.Locale);
            Assert.Equal("America/New_York", options.Timezone);
            Assert.Equal(1365, options.ViewportWidth);
            Assert.Equal(768, options.ViewportHeight);
            Assert.True(options.IgnoreHTTPSErrors);
            Assert.True(options.PreventSsoAutoSubmit);
            Assert.Contains("--disable-dev-shm-usage", options.BrowserArguments);
            Assert.Contains("geolocation", options.Permissions);
        } finally {
            if (Directory.Exists(dir)) {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}

[Collection("Playwright collection")]
public class HtmlBrowserPersistentProfileTests {
    [Fact]
    public async Task OpenSessionAsync_WithUserDataDirectory_PreservesLocalStorageBetweenSessions() {
        string userDataDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using HttpListener listener = StartLocalPageServer(out string url);
        HtmlBrowserLaunchOptions options = new() {
            UserDataDirectory = userDataDirectory,
            Headless = true,
            LoadState = HtmlBrowserLoadState.Load,
            Timeout = 30000
        };

        try {
            await using (HtmlBrowserSession first = await HtmlBrowser.OpenSessionAsync(url, options)) {
                await first.Page.EvaluateAsync("() => localStorage.setItem('persist', 'sweet')");
                Assert.True(first.IsPersistent);
                Assert.Equal(Path.GetFullPath(userDataDirectory), first.UserDataDirectory);
            }

            await using HtmlBrowserSession second = await HtmlBrowser.OpenSessionAsync(url, options);
            string value = await second.Page.EvaluateAsync<string>("() => localStorage.getItem('persist') || ''");

            Assert.Equal("sweet", value);
        } finally {
            await DeleteDirectoryWithRetryAsync(userDataDirectory);
        }
    }

    [Fact]
    public async Task OpenSessionAsync_WithUserDataDirectory_AppliesHttpCredentials() {
        string userDataDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using HttpListener listener = StartBasicAuthPageServer(out string url, "auditor", "proof-secret");
        HtmlBrowserLaunchOptions options = new() {
            UserDataDirectory = userDataDirectory,
            Username = "auditor",
            Password = "proof-secret",
            Headless = true,
            LoadState = HtmlBrowserLoadState.DomContentLoaded,
            Timeout = 30000
        };

        try {
            await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync(url, options);
            string body = await session.Page.Locator("main").TextContentAsync() ?? string.Empty;

            Assert.Equal("basic auth ready", body);
        } finally {
            await DeleteDirectoryWithRetryAsync(userDataDirectory);
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string path) {
        for (int attempt = 0; attempt < 10; attempt++) {
            if (!Directory.Exists(path)) {
                return;
            }

            try {
                Directory.Delete(path, recursive: true);
                return;
            } catch (IOException) when (attempt < 9) {
                await Task.Delay(250).ConfigureAwait(false);
            } catch (UnauthorizedAccessException) when (attempt < 9) {
                await Task.Delay(250).ConfigureAwait(false);
            }
        }

        if (Directory.Exists(path)) {
            Directory.Delete(path, recursive: true);
        }
    }

    private static HttpListener StartLocalPageServer(out string url) {
        int port = GetFreePort();
        url = $"http://127.0.0.1:{port}/";
        HttpListener listener = new();
        listener.Prefixes.Add(url);
        listener.Start();

        _ = Task.Run(async () => {
            while (listener.IsListening) {
                try {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    byte[] body = Encoding.UTF8.GetBytes("<!doctype html><html><body><main>profile ready</main></body></html>");
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = body.Length;
                    await context.Response.OutputStream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                    context.Response.Close();
                } catch (HttpListenerException) {
                    break;
                } catch (ObjectDisposedException) {
                    break;
                }
            }
        });

        return listener;
    }

    private static HttpListener StartBasicAuthPageServer(out string url, string username, string password) {
        int port = GetFreePort();
        url = $"http://127.0.0.1:{port}/";
        string expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        HttpListener listener = new();
        listener.Prefixes.Add(url);
        listener.Start();

        _ = Task.Run(async () => {
            while (listener.IsListening) {
                try {
                    HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                    if (!string.Equals(context.Request.Headers["Authorization"], expected, StringComparison.Ordinal)) {
                        context.Response.StatusCode = 401;
                        context.Response.AddHeader("WWW-Authenticate", "Basic realm=\"HtmlTinkerX\"");
                        context.Response.Close();
                        continue;
                    }

                    byte[] body = Encoding.UTF8.GetBytes("<!doctype html><html><body><main>basic auth ready</main></body></html>");
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = body.Length;
                    await context.Response.OutputStream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                    context.Response.Close();
                } catch (HttpListenerException) {
                    break;
                } catch (ObjectDisposedException) {
                    break;
                }
            }
        });

        return listener;
    }

    private static int GetFreePort() {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
