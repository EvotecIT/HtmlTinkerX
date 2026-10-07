using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlBrowserScopedHeaderInterceptorCancellationTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidInterceptionIdIsIgnoredOnlyForAnObservedCanceledRequest(bool canceled) {
        Dictionary<string, Mock<ICDPSessionEvent>> events = new(StringComparer.Ordinal);
        Mock<ICDPSessionEvent> Event(string name) {
            if (!events.TryGetValue(name, out Mock<ICDPSessionEvent>? value)) {
                value = new Mock<ICDPSessionEvent>();
                events.Add(name, value);
            }
            return value;
        }

        TaskCompletionSource<JsonElement?> continueResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> continueSent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> interceptionFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new Mock<ICDPSession>();
        session.Setup(value => value.Event(It.IsAny<string>())).Returns<string>(name => Event(name).Object);
        session.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, _) => {
                if (method == "Fetch.continueRequest") {
                    continueSent.TrySetResult(true);
                    return continueResponse.Task;
                }
                return Task.FromResult<JsonElement?>(method == "Page.getFrameTree"
                    ? ParseJson("{\"frameTree\":{\"frame\":{\"id\":\"main\"}}}")
                    : null);
            });
        session.Setup(value => value.DetachAsync()).Returns(Task.CompletedTask);
        var page = new Mock<IPage>();
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewCDPSessionAsync(page.Object)).ReturnsAsync(session.Object);

        await using HtmlBrowserScopedHeaderInterceptor interceptor = await HtmlBrowserScopedHeaderInterceptor.CreateAsync(
            context.Object, page.Object, new Uri("https://example.test"),
            new Dictionary<string, string> { ["X-Test"] = "value" }, CancellationToken.None,
            () => interceptionFailed.TrySetResult(true));
        Event("Fetch.requestPaused").Raise(value => value.OnEvent += null, session.Object,
            ParseJson("{\"requestId\":\"stale\",\"networkId\":\"network-1\",\"resourceType\":\"XHR\",\"request\":{\"url\":\"https://example.test/image\",\"headers\":{}}}"));
        Assert.Same(continueSent.Task, await Task.WhenAny(continueSent.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        if (canceled) {
            Event("Network.loadingFailed").Raise(value => value.OnEvent += null, session.Object,
                ParseJson("{\"requestId\":\"network-1\",\"canceled\":true}"));
        }
        continueResponse.TrySetException(new PlaywrightException("Protocol error (Fetch.continueRequest): Invalid InterceptionId."));

        if (canceled) {
            await Task.Delay(150);
            interceptor.ThrowIfFaulted();
            session.Verify(value => value.SendAsync("Fetch.failRequest", It.IsAny<Dictionary<string, object>>()), Times.Never);
        } else {
            Assert.Same(interceptionFailed.Task, await Task.WhenAny(interceptionFailed.Task, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.Contains("Invalid InterceptionId", Assert.Throws<InvalidOperationException>(interceptor.ThrowIfFaulted).InnerException?.Message);
        }
    }

    private static JsonElement ParseJson(string value) {
        using JsonDocument document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
