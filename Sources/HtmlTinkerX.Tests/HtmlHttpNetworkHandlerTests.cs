#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[CollectionDefinition("Process default proxy", DisableParallelization = true)]
public sealed class ProcessDefaultProxyCollection { }

[Collection("Process default proxy")]
public sealed class HtmlHttpNetworkHandlerTests {
    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public async Task RoutedHostFailsInsteadOfGoingAroundAProxyThatApplies(string scheme) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using TcpListener proxy = new(IPAddress.Loopback, 0);
        proxy.Start();
        Task<TcpClient> contacted = proxy.AcceptTcpClientAsync(deadline.Token).AsTask();
        IWebProxy previous = HttpClient.DefaultProxy;
        try {
            HttpClient.DefaultProxy = new WebProxy(new Uri($"http://127.0.0.1:{((IPEndPoint)proxy.LocalEndpoint).Port}"));
            using HttpClient client = new(new HtmlHttpNetworkHandler(new SocketsHttpHandler { AllowAutoRedirect = false },
                new Dictionary<string, HtmlHttpNetworkPolicy> { ["publisher.example"] = new() { AddressFamily = HtmlHttpAddressFamily.IPv4Only } }));

            HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(
                () => client.GetAsync($"{scheme}://publisher.example/feed", deadline.Token));

            Exception? cause = error;
            while (cause is not null and not NotSupportedException) cause = cause.InnerException;
            Assert.IsType<NotSupportedException>(cause);
            Assert.False(contacted.IsCompleted);
        } finally {
            HttpClient.DefaultProxy = previous;
            deadline.Cancel();
            try { (await contacted).Dispose(); } catch (OperationCanceledException) { }
        }
    }
}
#endif
