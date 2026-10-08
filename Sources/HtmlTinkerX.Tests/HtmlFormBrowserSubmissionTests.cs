using HtmlTinkerX;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HtmlTinkerX.Tests;

[Collection("Playwright collection")]
public sealed class HtmlFormBrowserSubmissionTests {
    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task NamedControls_UseTheirTypesAndRespectValidationAndSubmitHandlers(HtmlBrowserEngine browser) {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank", browser: browser);
        int requests = 0;
        await session.Page.RouteAsync("https://forms.test/**", async route => {
            requests++;
            await route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html", Body = "<h1>Unexpected navigation</h1>" });
        });
        var cases = new[] {
            new ControlCase("<select name='color'><option value='red'>Red</option><option value='blue'>Blue</option></select>", "color", "blue", "color=blue"),
            new ControlCase("<input type='checkbox' name='accept' value='yes'>", "accept", "yes", "accept=yes"),
            new ControlCase("<input type='checkbox' name='accept' value='yes' checked>", "accept", "false", ""),
            new ControlCase("<input type='hidden' name='accept' value='false'><input type='checkbox' name='accept' value='true'>", "accept", "true", "accept=false|accept=true"),
            new ControlCase("<input type='radio' name='color' value='red' checked><input type='radio' name='color' value='blue'>", "color", "blue", "color=blue"),
            new ControlCase("<input name='q' value='old'>", "q", "new", "q=new"),
            new ControlCase("<input name='profile[&quot;name&quot;]' value='old'>", "profile[\"name\"]", "new", "profile[\"name\"]=new"),
            new ControlCase("<input name='submit' value='saved'><input name='requestSubmit' value='saved'><input name='elements' value='saved'><input name='q' value='old'>", "q", "new", "submit=saved|requestSubmit=saved|elements=saved|q=new"),
            new ControlCase("<input name='q' value='original'>", "outside", "new", "q=original|outside=new", "<input name='outside' form='f' value='old'>"),
            new ControlCase("<input type='hidden' name='token' value='old'>", "token", "new", "token=new"),
            new ControlCase("<input name='q' required>", null, null, null)
        };
        foreach (ControlCase item in cases) {
            await session.Page.SetContentAsync("<form id='f' action='https://forms.test/receive' method='post' onsubmit=\"event.preventDefault(); window.submitted = Array.from(new FormData(this)).map(([k,v]) => k+'='+v).join('|')\">"
                + item.Controls + "<button>Save</button></form>" + item.Outside + "<script>window.submitted=null</script>");
            var fields = new Dictionary<string, string>();
            if (item.Name != null) fields[item.Name] = item.Value!;
            await HtmlFormSubmitter.SubmitAsync(session.Page, "form#f", fields, 5000);
            Assert.Equal(item.Expected, await session.Page.EvaluateAsync<string?>("() => window.submitted"));
            Assert.Equal(0, requests);
        }
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task ParsedForm_UsesExactIdAndDocumentWideIndex(HtmlBrowserEngine browser) {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank", browser: browser);
        await session.Page.SetContentAsync("<section><form onsubmit='event.preventDefault()'><input name='q' value='first'></form></section>"
            + "<section><form id='sign:in&quot;form' onsubmit=\"event.preventDefault(); window.submitted=new FormData(this).get('q')\"><input name='q' value='second'></form></section>");
        var fields = new Dictionary<string, string> { ["q"] = "by index" };
        HtmlFormResult form = new() { Metadata = new() { FormIndex = 1 } };
        await HtmlFormSubmitter.SubmitAsync(session.Page, form, fields);
        Assert.Equal("by index", await session.Page.EvaluateAsync<string>("() => window.submitted"));
        form.Metadata.Id = "sign:in\"form";
        form.Metadata.FormIndex = 0;
        fields["q"] = "by id";
        await HtmlFormSubmitter.SubmitAsync(session.Page, form, fields);
        Assert.Equal("by id", await session.Page.EvaluateAsync<string>("() => window.submitted"));
        Assert.Equal("first", await session.Page.Locator("input").First.InputValueAsync());
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium, "get")]
    [InlineData(HtmlBrowserEngine.Chromium, "post")]
    [InlineData(HtmlBrowserEngine.Firefox, "get")]
    [InlineData(HtmlBrowserEngine.Firefox, "post")]
    [InlineData(HtmlBrowserEngine.WebKit, "get")]
    [InlineData(HtmlBrowserEngine.WebKit, "post")]
    public async Task NativeSubmission_AwaitsCurrentNavigationWithoutRequiringNetworkIdle(HtmlBrowserEngine browser, string method) {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank", browser: browser);
        string? submittedMethod = null;
        string? submittedValues = null;
        await session.Page.RouteAsync("https://forms.test/**", async route => {
            if (route.Request.Url.Contains("/receive")) {
                submittedMethod = route.Request.Method;
                submittedValues = method == "post" ? route.Request.PostData : new Uri(route.Request.Url).Query.TrimStart('?');
                await route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html", Body = "<h1>Received</h1><script>setInterval(() => fetch('https://forms.test/poll'), 50)</script>" });
            } else await route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/plain", Body = "polling" });
        });
        await session.Page.SetContentAsync($"<form action='https://forms.test/receive' method='{method}'><input name='token' value='keep'><input name='q' value='old'></form>");
        await HtmlFormSubmitter.SubmitAsync(session.Page, "form", new Dictionary<string, string> { ["q"] = "new value" }, 5000);
        Assert.Equal(method.ToUpperInvariant(), submittedMethod);
        Assert.Equal("token=keep&q=new+value", submittedValues);
        Assert.Equal("Received", await session.Page.Locator("h1").InnerTextAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Submission_PropagationStopsAndHistoryChangesDoNotCompleteNativeNavigation(bool stopsPropagation) {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        TaskCompletionSource<bool> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await session.Page.RouteAsync("https://forms.test/**", async route => {
            if (route.Request.Url.Contains("/receive")) {
                received.TrySetResult(true);
                await release.Task;
                await route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html", Body = "<h1>Received</h1>" });
            } else {
                string handler = stopsPropagation ? string.Empty : " onsubmit=\"history.replaceState({}, '', '#submitting')\"";
                string listener = stopsPropagation ? "<script>document.addEventListener('submit', event => event.stopPropagation(), true)</script>" : string.Empty;
                await route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html", Body = "<form action='/receive' method='post'" + handler + "><input name='q' value='old'></form>" + listener });
            }
        });
        await session.Page.GotoAsync("https://forms.test/start");
        Task submission = HtmlFormSubmitter.SubmitAsync(session.Page, "form", new Dictionary<string, string> { ["q"] = "new" }, 5000);
        try {
            Assert.Same(received.Task, await Task.WhenAny(received.Task, Task.Delay(5000)));
            Task guard = Task.Delay(150);
            Assert.Same(guard, await Task.WhenAny(submission, guard));
        } finally {
            release.TrySetResult(true);
            await submission;
            await session.Page.UnrouteAllAsync();
        }
        Assert.Equal("Received", await session.Page.Locator("h1").InnerTextAsync());
    }

    [Fact]
    public async Task Submission_CancellationAndTimeoutCoverTheWholeOperation() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        await session.Page.SetContentAsync("<form onsubmit='event.preventDefault()'><input name='q' value='old'></form>");
        var fields = new Dictionary<string, string> { ["q"] = "new" };
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HtmlFormSubmitter.SubmitAsync(session.Page, "form", fields, cancellationToken: canceled.Token));
        Assert.Equal("old", await session.Page.Locator("input").InputValueAsync());
        await Assert.ThrowsAsync<TimeoutException>(() => HtmlFormSubmitter.SubmitAsync(session.Page, "#missing", fields, 100));
        using CancellationTokenSource stopping = new();
        Task pending = HtmlFormSubmitter.SubmitAsync(session.Page, "#missing", fields, 1000, stopping.Token);
        stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await session.Page.SetContentAsync("<form onsubmit=\"event.preventDefault(); window.submitted=new FormData(this).get('q')\"></form>"
            + "<script>setTimeout(() => document.querySelector('form').innerHTML='<input name=\"q\" value=\"old\">', 200)</script>");
        await HtmlFormSubmitter.SubmitAsync(session.Page, "form", fields, 5000);
        Assert.Equal("new", await session.Page.EvaluateAsync<string>("() => window.submitted"));
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task NativeSubmission_CompletesFragmentNavigationWithoutReplacingTheDocument(HtmlBrowserEngine browser) {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank", browser: browser);
        int requests = 0;
        await session.Page.RouteAsync("https://forms.test/**", async route => {
            requests++;
            await route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html", Body = "<form action='#received' method='get'><input name='q' value='old'></form><script>window.documentProof='original'</script>" });
        });
        await session.Page.GotoAsync("https://forms.test/start?q=old");
        await HtmlFormSubmitter.SubmitAsync(session.Page, "form", new Dictionary<string, string>(), 2000);
        Assert.Equal("https://forms.test/start?q=old#received", session.Page.Url);
        Assert.Equal(1, requests);
        Assert.Equal("original", await session.Page.EvaluateAsync<string>("() => window.documentProof"));
    }

    [Fact]
    public async Task FragmentSubmission_UsesTheNativeEntryListAfterFormDataHandlers() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        int requests = 0;
        await session.Page.RouteAsync("https://forms.test/**", async route => {
            requests++;
            await route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html; charset=utf-8", Body = "<form action='#received'><input name='q' value='old'></form><script>window.formDataCalls=0; document.querySelector('form').addEventListener('formdata', event => { window.formDataCalls++; event.formData.set('q', 'new €'); })</script>" });
        });
        await session.Page.GotoAsync("https://forms.test/start?q=new+%E2%82%AC");
        await HtmlFormSubmitter.SubmitAsync(session.Page, "form", new Dictionary<string, string>(), 5000);
        Assert.Equal("https://forms.test/start?q=new+%E2%82%AC#received", session.Page.Url);
        Assert.Equal(1, requests);
        Assert.Equal(1, await session.Page.EvaluateAsync<int>("() => window.formDataCalls"));
    }

    [Theory]
    [InlineData(HtmlBrowserEngine.Chromium)]
    [InlineData(HtmlBrowserEngine.Firefox)]
    [InlineData(HtmlBrowserEngine.WebKit)]
    public async Task FragmentSubmission_CompletesWithLegacyEncodedValues(HtmlBrowserEngine browser) {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank", browser: browser);
        int requests = 0;
        await session.Page.RouteAsync("https://forms.test/**", async route => {
            requests++;
            await route.FulfillAsync(new RouteFulfillOptions {
                ContentType = "text/html; charset=windows-1252",
                Body = "<form action='#received' accept-charset='windows-1252'><input name='q' value='&#233;'></form><script>window.documentProof='original'</script>"
            });
        });
        await session.Page.GotoAsync("https://forms.test/start?q=%E9");
        await HtmlFormSubmitter.SubmitAsync(session.Page, "form", new Dictionary<string, string>(), 5000);
        Assert.Equal("https://forms.test/start?q=%E9#received", session.Page.Url);
        Assert.Equal(1, requests);
        Assert.Equal("original", await session.Page.EvaluateAsync<string>("() => window.documentProof"));
    }

    [Fact]
    public async Task Submission_CancellationWhileNavigatingPreservesCallerTokenAndPage() {
        await using HtmlBrowserSession session = await HtmlBrowser.OpenSessionAsync("about:blank");
        TaskCompletionSource<bool> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await session.Page.RouteAsync("https://forms.test/receive", async route => {
            received.TrySetResult(true);
            await release.Task;
            await route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html", Body = "<h1>Received</h1>" });
        });
        await session.Page.SetContentAsync("<form action='https://forms.test/receive' method='post'><input name='q' value='old'></form>");
        using CancellationTokenSource stopping = new();
        Task pending = HtmlFormSubmitter.SubmitAsync(session.Page, "form", new Dictionary<string, string> { ["q"] = "new" }, 5000, stopping.Token);
        try {
            Assert.Same(received.Task, await Task.WhenAny(received.Task, Task.Delay(5000)));
            stopping.Cancel();
            OperationCanceledException error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(stopping.Token, error.CancellationToken);
            Assert.False(session.Page.IsClosed);
        } finally {
            release.TrySetResult(true);
            await session.Page.Locator("h1").WaitForAsync();
            await session.Page.UnrouteAllAsync();
        }
        await session.Page.SetContentAsync("<form onsubmit=\"event.preventDefault(); window.submitted=new FormData(this).get('q')\"><input name='q'></form>");
        await HtmlFormSubmitter.SubmitAsync(session.Page, "form", new Dictionary<string, string> { ["q"] = "reused" }, 5000);
        Assert.Equal("reused", await session.Page.EvaluateAsync<string>("() => window.submitted"));
    }

    private sealed class ControlCase {
        internal ControlCase(string controls, string? name, string? value, string? expected, string outside = "") {
            Controls = controls; Name = name; Value = value; Expected = expected; Outside = outside;
        }
        internal string Controls { get; }
        internal string? Name { get; }
        internal string? Value { get; }
        internal string? Expected { get; }
        internal string Outside { get; }
    }
}
