using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlFormHttpSubmissionTests {
    [Fact]
    public async Task SubmitAsync_OrderedValuesKeepRepeatedNamesAndEncoding() {
        string? body = null;
        using HttpClient client = new(new Handler(async request => {
            body = await request.Content!.ReadAsStringAsync();
        }));
        await HtmlFormSubmitter.SubmitAsync("https://example.org/save", FormMethod.Post, new[] {
            Pair("tag", "one two"), Pair("tag", "+three"), Pair("label", "Łódź")
        }, client);
        Assert.Equal("tag=one+two&tag=%2Bthree&label=%C5%81%C3%B3d%C5%BA", body);
    }

    [Fact]
    public async Task SubmitAsync_ParsedFormRetainsDefaultsAndReplacesAllValuesForAnOrdinalName() {
        Uri? destination = null;
        string? body = null;
        using HttpClient client = new(new Handler(async request => {
            destination = request.RequestUri;
            body = await request.Content!.ReadAsStringAsync();
        }));
        HtmlFormResult form = new() {
            Metadata = new() {
                Action = "save", ResolvedActionUri = new Uri("https://example.org/forms/save?mode=edit"), Method = FormMethod.Post
            },
            SuccessfulFields = new() {
                Pair("csrf", "original-token"), Pair("tag", "old-one"), Pair("middle", "unchanged"),
                Pair("tag", "old-two"), Pair("Tag", "case-sensitive")
            }
        };
        await HtmlFormSubmitter.SubmitAsync(form, new[] {
            Pair("tag", "new-one"), Pair("new", "first"), Pair("tag", "new-two"), Pair("new", "second")
        }, client);
        Assert.Equal("https://example.org/forms/save?mode=edit", destination!.AbsoluteUri);
        Assert.Equal("csrf=original-token&tag=new-one&tag=new-two&middle=unchanged&Tag=case-sensitive&new=first&new=second", body);
        Assert.Equal("old-one", form.SuccessfulFields[1].Value);
        Assert.Equal(5, form.SuccessfulFields.Count);
    }

    [Fact]
    public async Task SubmitAsync_ParsedGetFormReplacesTheActionQueryWithItsSuccessfulValues() {
        Uri? destination = null;
        using HttpClient client = new(new Handler(request => {
            destination = request.RequestUri;
            Assert.Null(request.Content);
            return Task.CompletedTask;
        }));
        HtmlFormResult form = new() {
            Metadata = new() { Action = "https://example.org/search?discard=old", Method = FormMethod.Get },
            SuccessfulFields = new() { Pair("q", "hello + world"), Pair("tag", "one"), Pair("tag", "two") }
        };
        await HtmlFormSubmitter.SubmitAsync(form, client: client);
        Assert.Equal("?q=hello+%2B+world&tag=one&tag=two", destination!.Query);
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("javascript:alert(1)")]
    public async Task SubmitAsync_UnresolvedOrNonHttpFormActionFailsBeforeSending(string action) {
        int requests = 0;
        using HttpClient client = new(new Handler(_ => {
            requests++;
            return Task.CompletedTask;
        }));
        HtmlFormResult form = new() { Metadata = new() { Action = action } };
        await Assert.ThrowsAsync<ArgumentException>(() => HtmlFormSubmitter.SubmitAsync(form, client: client));
        Assert.Equal(0, requests);
    }

    private static KeyValuePair<string, string> Pair(string name, string value) => new(name, value);

    private sealed class Handler(Func<HttpRequestMessage, Task> observe) : HttpMessageHandler {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            await observe(request);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        }
    }
}
