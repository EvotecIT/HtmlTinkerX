using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed partial class HtmlBrowserPdfRendererLiveTests {
    [Fact]
    public async Task CaptureFailsWhenRequestedWebStorageExceedsTheOriginQuota() {
        await using LoopbackPopupServer server = new();
        await using HtmlBrowserPdfRenderer renderer = new(new HtmlBrowserPdfRendererOptions(
            maximumBrowserInstances: 1,
            networkPolicy: new HtmlBrowserNetworkPolicy(allowedHosts: new[] { "127.0.0.1" })));

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.CaptureAsync(
            new HtmlBrowserPdfRequest(
                HtmlBrowserPdfSource.FromUrl(server.HeaderUrl),
                localStorage: new Dictionary<string, string> {
                    ["oversized"] = new string('x', 20 * 1024 * 1024)
                })));

        Assert.Contains("web storage could not be initialized", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
