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
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public async Task InvalidInterceptionIdIsIgnoredOnlyForAnObservedCanceledRequest(bool canceled, bool terminalEventReceived, bool finished) {
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
        session.Verify(value => value.SendAsync("Network.enable", It.Is<Dictionary<string, object>>(arguments =>
            (int)arguments["maxTotalBufferSize"] == 0 && (int)arguments["maxResourceBufferSize"] == 0)), Times.Once);
        Event("Fetch.requestPaused").Raise(value => value.OnEvent += null, session.Object,
            ParseJson("{\"requestId\":\"stale\",\"networkId\":\"network-1\",\"resourceType\":\"XHR\",\"request\":{\"url\":\"https://example.test/image\",\"headers\":{}}}"));
        Assert.Same(continueSent.Task, await Task.WhenAny(continueSent.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        continueResponse.TrySetException(new PlaywrightException("Protocol error (Fetch.continueRequest): Invalid InterceptionId."));
        await Task.Delay(150);
        if (terminalEventReceived) {
            Event(finished ? "Network.loadingFinished" : "Network.loadingFailed").Raise(value => value.OnEvent += null, session.Object,
                ParseJson(finished
                    ? "{\"requestId\":\"network-1\"}"
                    : canceled
                        ? "{\"requestId\":\"network-1\",\"canceled\":true}"
                        : "{\"requestId\":\"network-1\",\"canceled\":false}"));
        }

        if (canceled) {
            await Task.Delay(150);
            interceptor.ThrowIfFaulted();
            session.Verify(value => value.SendAsync("Fetch.failRequest", It.IsAny<Dictionary<string, object>>()), Times.Never);
        } else if (terminalEventReceived) {
            Assert.Same(interceptionFailed.Task, await Task.WhenAny(interceptionFailed.Task, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.Contains("Invalid InterceptionId", Assert.Throws<InvalidOperationException>(interceptor.ThrowIfFaulted).InnerException?.Message);
        } else {
            Assert.False(interceptionFailed.Task.IsCompleted);
            await interceptor.DisposeAsync();
            Assert.False(interceptionFailed.Task.IsCompleted);
        }
    }

    [Fact]
    public async Task CanceledRedirectConfirmsEveryPauseSharingItsNetworkId() {
        Dictionary<string, Mock<ICDPSessionEvent>> events = new(StringComparer.Ordinal);
        Mock<ICDPSessionEvent> Event(string name) {
            if (!events.TryGetValue(name, out Mock<ICDPSessionEvent>? value)) {
                value = new Mock<ICDPSessionEvent>();
                events.Add(name, value);
            }
            return value;
        }

        Dictionary<string, TaskCompletionSource<JsonElement?>> responses = new(StringComparer.Ordinal);
        TaskCompletionSource<bool> bothSent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new Mock<ICDPSession>();
        session.Setup(value => value.Event(It.IsAny<string>())).Returns<string>(name => Event(name).Object);
        session.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, arguments) => {
                if (method == "Fetch.continueRequest") {
                    string id = (string)arguments!["requestId"];
                    TaskCompletionSource<JsonElement?> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    responses.Add(id, response);
                    if (responses.Count == 2) bothSent.TrySetResult(true);
                    return response.Task;
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
            new Dictionary<string, string> { ["X-Test"] = "value" }, CancellationToken.None);
        foreach (string id in new[] { "redirect-1", "redirect-2" }) {
            Event("Fetch.requestPaused").Raise(value => value.OnEvent += null, session.Object,
                ParseJson($"{{\"requestId\":\"{id}\",\"networkId\":\"network-1\",\"resourceType\":\"XHR\",\"request\":{{\"url\":\"https://example.test/image\",\"headers\":{{}}}}}}"));
        }
        Assert.Same(bothSent.Task, await Task.WhenAny(bothSent.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        foreach (TaskCompletionSource<JsonElement?> response in responses.Values) {
            response.TrySetException(new PlaywrightException("Protocol error (Fetch.continueRequest): Invalid InterceptionId."));
        }
        await Task.Delay(150);
        Event("Network.loadingFailed").Raise(value => value.OnEvent += null, session.Object,
            ParseJson("{\"requestId\":\"network-1\",\"canceled\":true}"));
        await Task.Delay(150);
        interceptor.ThrowIfFaulted();
        session.Verify(value => value.SendAsync("Fetch.failRequest", It.IsAny<Dictionary<string, object>>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorkerRequestUsesItsOwnNetworkOutcome(bool canceled) {
        Dictionary<string, Mock<ICDPSessionEvent>> events = new(StringComparer.Ordinal);
        Mock<ICDPSessionEvent> Event(string name) {
            if (!events.TryGetValue(name, out Mock<ICDPSessionEvent>? value)) {
                value = new Mock<ICDPSessionEvent>();
                events.Add(name, value);
            }
            return value;
        }
        var session = new Mock<ICDPSession>();
        void RaiseWorkerMessage(object message) => Event("Target.receivedMessageFromTarget").Raise(
            value => value.OnEvent += null, session.Object,
            ParseJson(JsonSerializer.Serialize(new { sessionId = "worker-1", message = JsonSerializer.Serialize(message) })));

        TaskCompletionSource<long> continueSent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> interceptionFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> workerNetworkTrackingEnabled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Setup(value => value.Event(It.IsAny<string>())).Returns<string>(name => Event(name).Object);
        session.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, arguments) => {
                if (method == "Target.sendMessageToTarget") {
                    using JsonDocument command = JsonDocument.Parse((string)arguments!["message"]);
                    JsonElement root = command.RootElement;
                    long id = root.GetProperty("id").GetInt64();
                    string? name = root.GetProperty("method").GetString();
                    if (name == "Network.enable") {
                        JsonElement options = root.GetProperty("params");
                        workerNetworkTrackingEnabled.TrySetResult(options.GetProperty("maxTotalBufferSize").GetInt32() == 0
                            && options.GetProperty("maxResourceBufferSize").GetInt32() == 0);
                    }
                    if (name == "Fetch.continueRequest") continueSent.TrySetResult(id);
                    else RaiseWorkerMessage(new { id, result = new { } });
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
        Event("Target.attachedToTarget").Raise(value => value.OnEvent += null, session.Object,
            ParseJson("{\"sessionId\":\"worker-1\",\"targetInfo\":{\"type\":\"worker\"}}"));
        Assert.Same(workerNetworkTrackingEnabled.Task,
            await Task.WhenAny(workerNetworkTrackingEnabled.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        Assert.True(await workerNetworkTrackingEnabled.Task);
        RaiseWorkerMessage(new {
            method = "Fetch.requestPaused",
            @params = new {
                requestId = "worker-pause", networkId = "worker-network", resourceType = "XHR",
                request = new { url = "https://example.test/image", headers = new { } }
            }
        });
        Assert.Same(continueSent.Task, await Task.WhenAny(continueSent.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        RaiseWorkerMessage(new { id = await continueSent.Task, error = new { message = "Invalid InterceptionId" } });
        await Task.Delay(150);
        Event("Network.loadingFailed").Raise(value => value.OnEvent += null, session.Object,
            ParseJson("{\"requestId\":\"worker-network\",\"canceled\":true}"));
        RaiseWorkerMessage(canceled
            ? new { method = "Network.loadingFailed", @params = new { requestId = "worker-network", canceled = true } }
            : new { method = "Network.loadingFailed", @params = new { requestId = "worker-network", canceled = false } });
        if (canceled) {
            await Task.Delay(150);
            interceptor.ThrowIfFaulted();
        } else {
            Assert.Same(interceptionFailed.Task, await Task.WhenAny(interceptionFailed.Task, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.Contains("Invalid InterceptionId", Assert.Throws<InvalidOperationException>(interceptor.ThrowIfFaulted).InnerException?.Message);
        }
    }

    [Fact]
    public async Task CancelingRequestPolicyDoesNotTurnIntoAnInterceptionFailureOnDispose() {
        Dictionary<string, Mock<ICDPSessionEvent>> events = new(StringComparer.Ordinal);
        Mock<ICDPSessionEvent> Event(string name) {
            if (!events.TryGetValue(name, out Mock<ICDPSessionEvent>? value)) {
                value = new Mock<ICDPSessionEvent>();
                events.Add(name, value);
            }
            return value;
        }
        TaskCompletionSource<bool> policyEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> policyResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> failedRequestSent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> interceptionFailed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new Mock<ICDPSession>();
        session.Setup(value => value.Event(It.IsAny<string>())).Returns<string>(name => Event(name).Object);
        session.Setup(value => value.SendAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Returns<string, Dictionary<string, object>?>((method, _) => {
                if (method == "Fetch.failRequest") failedRequestSent.TrySetResult(true);
                return Task.FromResult<JsonElement?>(method == "Page.getFrameTree"
                    ? ParseJson("{\"frameTree\":{\"frame\":{\"id\":\"main\"}}}")
                    : null);
            });
        session.Setup(value => value.DetachAsync()).Returns(Task.CompletedTask);
        var page = new Mock<IPage>();
        var context = new Mock<IBrowserContext>();
        context.Setup(value => value.NewCDPSessionAsync(page.Object)).ReturnsAsync(session.Object);
        using CancellationTokenSource cancellation = new();
        await using HtmlBrowserScopedHeaderInterceptor interceptor = await HtmlBrowserScopedHeaderInterceptor.CreateAsync(
            context.Object, page.Object, new Uri("https://example.test"),
            new Dictionary<string, string> { ["X-Test"] = "value" }, cancellation.Token,
            () => interceptionFailed.TrySetResult(true),
            requestAllowed: (_, _) => {
                policyEntered.TrySetResult(true);
                return policyResult.Task;
            });
        Event("Fetch.requestPaused").Raise(value => value.OnEvent += null, session.Object,
            ParseJson("{\"requestId\":\"pause-1\",\"networkId\":\"network-1\",\"resourceType\":\"XHR\",\"request\":{\"url\":\"https://example.test/data\",\"headers\":{}}}"));
        Assert.Same(policyEntered.Task, await Task.WhenAny(policyEntered.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        cancellation.Cancel();
        policyResult.TrySetCanceled(cancellation.Token);
        Assert.Same(failedRequestSent.Task, await Task.WhenAny(failedRequestSent.Task, Task.Delay(TimeSpan.FromSeconds(2))));
        interceptor.ThrowIfFaulted();
        Assert.False(interceptionFailed.Task.IsCompleted);
        await interceptor.DisposeAsync();
        interceptor.ThrowIfFaulted();
        Assert.False(interceptionFailed.Task.IsCompleted);
    }

    private static JsonElement ParseJson(string value) {
        using JsonDocument document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
