using Microsoft.Playwright;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public sealed class HtmlBrowserResponseBodyTests {
    [Fact]
    public async Task SelectedResponseReturnsExactOriginalBytesWithoutTextConversion() {
        byte[] payload = { 0xff, 0, 0x80, 0x41 };
        Mock<IResponse> response = Response(payload);

        Assert.Same(payload, await HtmlBrowser.ReadResponseBodyAsync(response.Object, payload.Length));
        response.Verify(item => item.BodyAsync(), Times.Once);
    }

    [Fact]
    public async Task DeclaredOversizeIsRejectedBeforeBufferingAndUndeclaredOversizeIsNeverTruncated() {
        Mock<IResponse> declared = Response(new byte[5], new Dictionary<string, string> { ["Content-Length"] = "5" });
        await Assert.ThrowsAsync<InvalidDataException>(() => HtmlBrowser.ReadResponseBodyAsync(declared.Object, 4));
        declared.Verify(item => item.BodyAsync(), Times.Never);

        Mock<IResponse> undeclared = Response(new byte[5]);
        await Assert.ThrowsAsync<InvalidDataException>(() => HtmlBrowser.ReadResponseBodyAsync(undeclared.Object, 4));
        undeclared.Verify(item => item.BodyAsync(), Times.Once);
    }

    [Fact]
    public async Task CancellationStopsWaitingForAnUnavailableBody() {
        Mock<IResponse> response = Response(Array.Empty<byte>());
        TaskCompletionSource<byte[]> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        response.Setup(item => item.BodyAsync()).Returns(pending.Task);
        using CancellationTokenSource cancellation = new();
        Task<byte[]> capture = HtmlBrowser.ReadResponseBodyAsync(response.Object, 4, cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        pending.SetException(new IOException("late fixture failure"));
    }

    [Theory]
    [InlineData("HEAD", 200, 0)]
    [InlineData("HEAD", 200, 5)]
    [InlineData("GET", 304, 0)]
    [InlineData("GET", 304, 5)]
    public async Task RepresentationLengthDoesNotReplaceTheActualBodyLimit(string method, int status, int bodyLength) {
        Mock<IResponse> response = Response(new byte[bodyLength], new Dictionary<string, string> { ["Content-Length"] = "1000000" });
        Mock<IRequest> request = new();
        request.SetupGet(item => item.Method).Returns(method);
        response.SetupGet(item => item.Request).Returns(request.Object);
        response.SetupGet(item => item.Status).Returns(status);

        if (bodyLength == 0) Assert.Empty(await HtmlBrowser.ReadResponseBodyAsync(response.Object, 4));
        else await Assert.ThrowsAsync<InvalidDataException>(() => HtmlBrowser.ReadResponseBodyAsync(response.Object, 4));
        response.Verify(item => item.BodyAsync(), Times.Once);
    }

    private static Mock<IResponse> Response(byte[] body, Dictionary<string, string>? headers = null) {
        Mock<IResponse> response = new();
        response.SetupGet(item => item.Headers).Returns(headers ?? new Dictionary<string, string>());
        response.Setup(item => item.BodyAsync()).ReturnsAsync(body);
        return response;
    }
}
