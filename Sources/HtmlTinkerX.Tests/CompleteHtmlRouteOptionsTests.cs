using System.Collections;
using System.Reflection;
using Microsoft.Playwright;
using Moq;
using PSParseHTML.PowerShell;
#if !NETFRAMEWORK
using System.Management.Automation;
#endif

namespace HtmlTinkerX.Tests;

#if !NETFRAMEWORK
public class CompleteHtmlRouteOptionsTests {
    [Fact]
    public void ApplyOptionsPreservesPowerShellObjectJsonPayload() {
        RouteFulfillOptions options = new();
        PSObject payload = new();
        payload.Properties.Add(new PSNoteProperty("message", "options-json-helper"));

        ApplyOptions(options, new Hashtable {
            ["Json"] = payload
        });

        Assert.Equal("{\"message\":\"options-json-helper\"}", options.Body);
        Assert.Equal("application/json", options.ContentType);
    }

    [Fact]
    public void ApplyOptionsMapsResponsePayload() {
        RouteFulfillOptions options = new();
        IAPIResponse response = new Mock<IAPIResponse>().Object;

        ApplyOptions(options, new Hashtable {
            ["Response"] = response
        });

        Assert.Same(response, options.Response);
    }

    private static void ApplyOptions(RouteFulfillOptions options, IDictionary values) {
        MethodInfo method = typeof(CmdletCompleteHtmlRoute).GetMethod(
            "ApplyOptions",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(CmdletCompleteHtmlRoute), "ApplyOptions");

        method.Invoke(null, new object[] { options, values });
    }
}
#endif
