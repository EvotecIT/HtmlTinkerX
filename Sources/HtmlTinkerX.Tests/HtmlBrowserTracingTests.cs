using HtmlTinkerX;
using Microsoft.Playwright;
using Moq;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlBrowserTracingTests {
    [Fact]
    public async Task StartAndStopTracingAsync_CreatesDirectoryAndCallsPlaywright() {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string file = Path.Combine(dir, "trace.zip");
        var tracing = new Mock<ITracing>();
        tracing.Setup(t => t.StartAsync(It.IsAny<TracingStartOptions>()))
               .Returns(Task.CompletedTask)
               .Verifiable();
        tracing.Setup(t => t.StopAsync(It.Is<TracingStopOptions>(o => o.Path == file)))
               .Returns(Task.CompletedTask)
               .Verifiable();
        var context = new Mock<IBrowserContext>();
        context.SetupGet(c => c.Tracing).Returns(tracing.Object).Verifiable();
        var session = (HtmlBrowserSession)RuntimeHelpers.GetUninitializedObject(typeof(HtmlBrowserSession));
        typeof(HtmlBrowserSession)
            .GetField("<Context>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(session, context.Object);

        await HtmlBrowser.StartTracingAsync(session);
        await HtmlBrowser.StopTracingAsync(session, file);

        Assert.True(Directory.Exists(dir));
        tracing.Verify();
        context.Verify();
        Directory.Delete(dir, true);
    }
}