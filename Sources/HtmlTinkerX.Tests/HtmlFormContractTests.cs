using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlFormContractTests {
    [Theory]
    [InlineData("")]
    [InlineData("unrecognized")]
    public void InputInventoryUsesTheNativeTextFallbackForInvalidTypes(string type) {
        string html = "<form><input name='value' type='" + type + "' value='text'></form>";

        Assert.Equal(HtmlFormFieldType.Text, Assert.Single(Assert.Single(HtmlParser.ParseFormsWithAngleSharp(html)).Fields).Type);
        Assert.Equal(HtmlFormFieldType.Text, Assert.Single(HtmlFormFieldExtractor.ExtractFields(html)).Type);
    }

    [Fact]
    public void SuccessfulFieldsUseCurrentRadioSelectionAndActualFormOwner() {
        const string html = """
            <form id="first">
              <input type="radio" name="choice" value="a" checked>
              <input type="radio" name="choice" value="b" checked>
            </form>
            <form id="second"><input type="radio" name="choice" value="second" checked></form>
            <input type="radio" name="choice" value="outside" form="first" checked>
            """;

        var forms = HtmlParser.ParseFormsWithAngleSharp(html);

        Assert.Equal(new[] { "choice=outside" }, forms[0].SuccessfulFields.Select(field => field.Key + "=" + field.Value));
        Assert.Equal(new[] { "choice=second" }, forms[1].SuccessfulFields.Select(field => field.Key + "=" + field.Value));
        Assert.Equal(new[] { "", "", "outside" }, forms[0].Fields.Select(field => field.Value));
    }

    [Fact]
    public void FormInventoryUsesNativeOwnershipAndKeepsUnsuccessfulControls() {
        const string html = """
            <input form="first" name="before" value="outside">
            <form id="first">
              <input name="normal" value="one">
              <input name="disabled" disabled value="keep">
              <input name="image" type="image" value="keep">
              <input form="second" name="reassigned" value="two">
              <input form="" name="unowned" value="omit">
            </form>
            <form id="second"></form>
            <input form="first" name="after" value="outside">
            """;

        var forms = HtmlParser.ParseFormsWithAngleSharp(html);

        Assert.Equal(new[] { "before", "normal", "disabled", "image", "after" }, forms[0].Fields.Select(field => field.Name));
        Assert.Equal("reassigned", Assert.Single(forms[1].Fields).Name);
        Assert.Equal(HtmlFormFieldType.Text, forms[0].Fields.Single(field => field.Name == "normal").Type);
        Assert.Equal(new[] { "before", "normal", "after" }, forms[0].SuccessfulFields.Select(field => field.Key));
        Assert.DoesNotContain(HtmlFormFieldExtractor.ExtractFields(html), field => field.Name == "unowned");
        Assert.Contains(HtmlFormFieldExtractor.ExtractFields(html), field => field.Name == "before");
    }

    [Fact]
    public void SuccessfulFieldsRetainOrderedRepeatedValuesAndBrowserSelection() {
        const string html = """
            <form id="prefs">
              <input type="hidden" name="csrf" value="first">
              <input type="hidden" name="csrf" value="second">
              <input type="checkbox" name="enabled" checked>
              <input type="checkbox" name="unchecked" value="omit">
              <fieldset disabled>
                <legend><input name="legend" value="keep"></legend>
                <input name="disabled" value="omit">
              </fieldset>
              <input name="readOnly" readonly value="keep">
              <select name="default"><option disabled>omit</option><option value="yes">Yes</option></select>
              <select name="choice"><option selected value="old">Old</option><option selected value="new">New</option></select>
              <select name="tags" multiple>
                <option selected value="a">A</option>
                <optgroup disabled><option selected value="omit">Disabled</option></optgroup>
                <option selected disabled value="omit2">Disabled</option>
                <option selected value="b">B</option>
              </select>
              <button name="submit" value="omit">Submit</button>
            </form>
            """;

        var form = Assert.Single(HtmlParser.ParseFormsWithAngleSharp(html));

        Assert.Equal(new[] {
            "csrf=first", "csrf=second", "enabled=on", "legend=keep", "readOnly=keep",
            "default=yes", "choice=new", "tags=a", "tags=b"
        }, form.SuccessfulFields.Select(field => field.Key + "=" + field.Value));
        Assert.Equal("new", form.Fields.Single(field => field.Name == "choice").Value);
    }

    [Fact]
    public async Task UrlParsingPreservesRedirectProvenanceAndResolvesBaseAndEmptyActions() {
        using var client = new HttpClient(new RedirectedFormHandler());
        var source = new Uri("https://example.test/start");
        var final = new Uri("https://example.test/account/page?token=1");

        var forms = await HtmlParser.ParseUrlFormsWithAngleSharpAsync(source.AbsoluteUri, client);

        Assert.Equal(source, forms[0].Metadata.SourceUri);
        Assert.Equal(final, forms[0].Metadata.FinalUri);
        Assert.Equal(new Uri("https://example.test/submit/"), forms[0].Metadata.BaseUri);
        Assert.Equal("login", forms[0].Metadata.Action);
        Assert.Equal(new Uri("https://example.test/submit/login"), forms[0].Metadata.ResolvedActionUri);
        Assert.Equal(final, forms[1].Metadata.ResolvedActionUri);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///tmp/form")]
    public void NonHttpActionsRemainVisibleWithoutBecomingHttpSubmissionAddresses(string action) {
        var form = Assert.Single(HtmlParser.ParseFormsWithAngleSharp(
            "<form action='" + action + "'></form>", new Uri("https://example.test/page")));

        Assert.Equal(action, form.Metadata.Action);
        Assert.Null(form.Metadata.ResolvedActionUri);
    }

    [Fact]
    public void AbsoluteHtmlBaseResolvesRelativeActionsWithoutInventingSourceProvenance() {
        var forms = HtmlParser.ParseFormsWithAngleSharp(
            "<base href='https://example.test/forms/'><form action='save'></form><form></form>");

        Assert.Null(forms[0].Metadata.SourceUri);
        Assert.Null(forms[0].Metadata.FinalUri);
        Assert.Equal(new Uri("https://example.test/forms/"), forms[0].Metadata.BaseUri);
        Assert.Equal(new Uri("https://example.test/forms/save"), forms[0].Metadata.ResolvedActionUri);
        Assert.Null(forms[1].Metadata.ResolvedActionUri);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,ignored")]
    public void RejectedFirstHtmlBaseFallsBackToTheDocumentAddress(string href) {
        var source = new Uri("https://example.test/account/page");
        var form = Assert.Single(HtmlParser.ParseFormsWithAngleSharp(
            "<base href='" + href + "'><base href='https://other.test/'><form action='save'></form>", source));

        Assert.Equal(source, form.Metadata.BaseUri);
        Assert.Equal(new Uri("https://example.test/account/save"), form.Metadata.ResolvedActionUri);
    }

    [Theory]
    [InlineData("relative/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,ignored")]
    public void AnUnusableFirstBaseDoesNotInventAnUnknownDocumentAddress(string href) {
        var form = Assert.Single(HtmlParser.ParseFormsWithAngleSharp(
            "<base href='" + href + "'><base href='https://other.test/'><form action='save'></form>"));

        Assert.Null(form.Metadata.BaseUri);
        Assert.Null(form.Metadata.ResolvedActionUri);
    }

    private sealed class RedirectedFormHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            request.RequestUri = new Uri("https://example.test/account/page?token=1");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                RequestMessage = request,
                Content = new StringContent("<base href='/submit/'><form action='login'></form><form></form>")
            });
        }
    }
}
