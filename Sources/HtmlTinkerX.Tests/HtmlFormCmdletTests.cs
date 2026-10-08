#if !NETFRAMEWORK
using System;
using System.Linq;
using System.Collections;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using HtmlTinkerX;
using PSParseHTML.PowerShell;

namespace HtmlTinkerX.Tests;

public class HtmlFormCmdletTests {
    [Fact]
    public void ContentCommandPreservesOwnedValuesAndResolvedActionMetadata() {
        using Runspace runspace = CreateRunspace();
        using PowerShell command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("ConvertFrom-HtmlForm")
            .AddParameter("Content", """
                <input form="settings" name="before" value="one">
                <form id="settings" action="save" class="account">
                  <input type="hidden" name="tag" value="first">
                  <input type="hidden" name="tag" value="second">
                  <input name="disabled" value="omit" disabled>
                </form>
                """)
            .AddParameter("BaseUri", new Uri("https://example.test/account/"))
            .AddParameter("IncludeMetadata");

        PSObject form = Assert.Single(command.Invoke());

        Assert.Empty(command.Streams.Error);
        Assert.Equal("save", form.Properties["Action"].Value);
        Assert.Equal("https://example.test/account/save", form.Properties["ResolvedAction"].Value);
        Assert.Equal("https://example.test/account/", form.Properties["BaseUrl"].Value);
        Assert.Equal("settings", form.Properties["FormId"].Value);
        Assert.Equal("account", form.Properties["FormClasses"].Value);
        Assert.Equal(new[] { "before", "tag", "tag", "disabled" },
            Assert.IsType<PSObject[]>(form.Properties["Fields"].Value)
                .Select(field => field.Properties["Name"].Value));
        Assert.Equal(new[] { "before=one", "tag=first", "tag=second" },
            Assert.IsType<System.Collections.Generic.KeyValuePair<string, string>[]>(form.Properties["SuccessfulFields"].Value)
                .Select(field => field.Key + "=" + field.Value));
    }

    [Fact]
    public void ContentCommandKeepsMultipleFormsAsOneOrderedResult() {
        using Runspace runspace = CreateRunspace();
        using PowerShell command = PowerShell.Create();
        command.Runspace = runspace;
        command.AddCommand("ConvertFrom-HtmlForm")
            .AddParameter("Content", "<form id='first'><input name='one' value='1'></form><form id='second'><input name='two' value='2'></form>");

        PSObject[] forms = Assert.IsType<PSObject[]>(Assert.Single(command.Invoke()).BaseObject);

        Assert.Empty(command.Streams.Error);
        Assert.Single(command.Streams.Warning);
        Assert.Equal(new[] { "one", "two" }, forms.Select(form =>
            Assert.Single(Assert.IsType<PSObject[]>(form.Properties["Fields"].Value)).Properties["Name"].Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadedFormCommandRetainsDefaultsAndReusesTheCallerClient(bool repeatedOverride) {
        using Runspace runspace = CreateRunspace();
        using FormHandler handler = new();
        using HttpClient client = new(handler) { Timeout = TimeSpan.FromSeconds(37) };
        client.DefaultRequestHeaders.Add("X-Session", "caller");
        using PowerShell download = PowerShell.Create();
        download.Runspace = runspace;
        download.AddCommand("ConvertFrom-HtmlForm")
            .AddParameter("Url", new Uri("https://example.test/account/"))
            .AddParameter("HttpClient", client);
        PSObject form = Assert.Single(download.Invoke());
        Assert.Empty(download.Streams.Error);

        using PowerShell submit = PowerShell.Create();
        submit.Runspace = runspace;
        submit.AddCommand("Submit-HtmlBrowserForm")
            .AddParameter("Form", form)
            .AddParameter("HttpClient", client)
            .AddParameter("FieldValue", new Hashtable {
                ["tag"] = repeatedOverride ? new[] { "new one", "+two" } : "new one"
            });
        Assert.Equal("saved", Assert.Single(submit.Invoke()).BaseObject);
        Assert.Empty(submit.Streams.Error);
        Assert.Equal("https://example.test/account/save", handler.Destination);
        Assert.Equal(repeatedOverride ? "csrf=token&tag=new+one&tag=%2Btwo" : "csrf=token&tag=new+one", handler.Body);
        Assert.Equal(TimeSpan.FromSeconds(37), client.Timeout);
        Assert.Equal("caller", Assert.Single(client.DefaultRequestHeaders.GetValues("X-Session")));
        using HttpResponseMessage response = await client.GetAsync("https://example.test/account/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.Requests);
    }

    private sealed class FormHandler : HttpMessageHandler {
        public string? Destination { get; private set; }
        public string? Body { get; private set; }
        public int Requests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Requests++;
            Assert.Equal("caller", Assert.Single(request.Headers.GetValues("X-Session")));
            if (request.Method == HttpMethod.Post) {
                Destination = request.RequestUri!.AbsoluteUri;
                Body = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("saved") };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("<form action='save' method='post'><input name='csrf' value='token'><input name='tag' value='old one'><input name='tag' value='old two'></form>")
            };
        }
    }

    private static Runspace CreateRunspace() {
        InitialSessionState state = InitialSessionState.Create();
        state.Commands.Add(new SessionStateCmdletEntry("ConvertFrom-HtmlForm", typeof(CmdletConvertFromHtmlForm), null));
        state.Commands.Add(new SessionStateCmdletEntry("Submit-HtmlBrowserForm", typeof(CmdletSubmitHtmlBrowserForm), null));
        Runspace runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        return runspace;
    }
}
#endif
