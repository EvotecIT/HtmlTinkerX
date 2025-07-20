using HtmlTinkerX;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HtmlTinkerX.Tests;

public class PreMailerClientRemoteCssAnalyticsTests {
    private static int GetFreePort() {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static HttpListener StartCssServer(string css, out string url) {
        int port = GetFreePort();
        string prefix = $"http://localhost:{port}/";
        url = $"{prefix}style.css";
        HttpListener listener = new();
        listener.Prefixes.Add(prefix);
        listener.Start();
        _ = Task.Run(async () => {
            var context = await listener.GetContextAsync();
            byte[] data = Encoding.UTF8.GetBytes(css);
            context.Response.ContentType = "text/css";
            context.Response.ContentLength64 = data.Length;
            await context.Response.OutputStream.WriteAsync(data, 0, data.Length);
            context.Response.Close();
        });
        return listener;
    }

    [Fact]
    public async Task MoveCssInline_InlinesRemoteAndLocalCss_AddsAnalyticsTags() {
        string localCssFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".css");
#if FRAMEWORK
        await WriteAllTextAsync(localCssFile, "h1{font-size:42px}");
#else
        await File.WriteAllTextAsync(localCssFile, "h1{font-size:42px}");
#endif
        string localCssPath = new Uri(localCssFile).AbsoluteUri;

        HttpListener server = StartCssServer("a{color:red}", out string remoteUrl);

        string html = $"<html><head><link rel='stylesheet' href='{localCssPath}'><link rel='stylesheet' href='{remoteUrl}'></head><body><h1>Header</h1><a href='https://example.com/page'>Link</a></body></html>";
        var options = new PreMailerOptions {
            DownloadRemoteCss = true,
            AddAnalyticsTags = true,
            AnalyticsSource = "newsletter",
            AnalyticsMedium = "email",
            AnalyticsCampaign = "campaign",
            AnalyticsContent = "content"
        };

        try {
            PreMailerResult result = await PreMailerClient.MoveCssInlineAsync(html, options, CancellationToken.None);
            // Check that styles are applied - be flexible about exact formatting
            Assert.Matches(@"style\s*=\s*[""'][^""']*font-size\s*:\s*42px", result.Html);
            Assert.Matches(@"style\s*=\s*[""'][^""']*color\s*:\s*red", result.Html);
            Assert.Contains("utm_source=newsletter", result.Html);
            Assert.Contains("utm_medium=email", result.Html);
            Assert.Contains("utm_campaign=campaign", result.Html);
            Assert.Contains("utm_content=content", result.Html);
        } finally {
            server.Stop();
            File.Delete(localCssFile);
        }
    }
}