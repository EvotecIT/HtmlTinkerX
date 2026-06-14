namespace HtmlTinkerX.Tests;

public class HtmlExtractionPlannerTests {
    [Fact]
    public void Analyze_RecommendsStaticForUsefulStaticContent() {
        string html = """
<html>
<head><title>Docs</title><meta property="og:title" content="Docs"></head>
<body>
<main>
<h1>Docs</h1>
<p>This page contains useful readable documentation content with enough words to parse statically.</p>
<form action="/search"><input name="q" value=""></form>
</main>
</body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html);

        Assert.Equal(HtmlExtractionPlanMode.Static, plan.RecommendedMode);
        Assert.Equal(1, plan.FormCount);
        Assert.True(plan.HasStructuredData);
        Assert.Equal("Select-HtmlData -Content $html", plan.SuggestedCommand);
    }

    [Fact]
    public void Analyze_RecommendsRenderedSnapshotForThinJavaScriptShell() {
        string html = """
<html>
<head><title>App</title><script src="/runtime.js"></script><script src="/app.js"></script></head>
<body><div id="root">Loading...</div></body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html, new Uri("https://example.org/app"));

        Assert.Equal(HtmlExtractionPlanMode.RenderedSnapshot, plan.RecommendedMode);
        Assert.True(plan.LooksLikeJavaScriptShell);
        Assert.Contains("Invoke-HtmlRendering", plan.SuggestedCommand);
        Assert.Contains("https://example.org/app", plan.SuggestedCommand);
        Assert.DoesNotContain("| Invoke-HtmlPageWorkbench", plan.SuggestedProfileCommand);
        Assert.Contains("-RenderedSnapshot $snapshot", plan.SuggestedProfileCommand);
    }

    [Fact]
    public void Analyze_RecommendsValidSessionCommandForLoginForms() {
        string html = """
<html><body><form action="/login"><input name="user"><input type="password" name="password"></form></body></html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html, new Uri("https://example.org/login"));

        Assert.Equal(HtmlExtractionPlanMode.AuthRequired, plan.RecommendedMode);
        Assert.Contains("Invoke-HtmlRendering", plan.SuggestedCommand);
        Assert.Contains("-Session", plan.SuggestedCommand);
        Assert.DoesNotContain("-Snapshot", plan.SuggestedCommand);
        Assert.Contains("-Session", plan.SuggestedProfileCommand);
        Assert.DoesNotContain("-Snapshot", plan.SuggestedProfileCommand);
    }

    [Fact]
    public void Analyze_DetectsHiddenAutoSubmitRelayCandidate() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="https://site.example/signinws">
<input type="hidden" name="wa" value="signin1.0">
<input type="hidden" name="wresult" value="redacted">
<input type="hidden" name="wctx" value="redacted">
</form>
<script>window.setTimeout('document.forms[0].submit()', 0);</script>
</body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html);

        Assert.Equal(HtmlExtractionPlanMode.BrowserlessRelayCandidate, plan.RecommendedMode);
        Assert.True(plan.HasAutoSubmitForm);
        Assert.Equal(3, plan.HiddenFieldCount);
        Assert.NotEmpty(plan.Warnings);
        Assert.Contains("Invoke-HtmlFormRelay", plan.SuggestedCommand);
    }
}
