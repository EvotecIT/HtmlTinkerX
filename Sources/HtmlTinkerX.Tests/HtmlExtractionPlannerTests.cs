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
    public void Analyze_DoesNotTreatStructuredDataScriptsAsJavaScriptShell() {
        string html = """
<html>
<head>
<script type="application/ld+json">{"@context":"https://schema.org","@type":"Article","headline":"A"}</script>
<script type="application/ld+json">{"@context":"https://schema.org","@type":"BreadcrumbList"}</script>
</head>
<body></body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html);

        Assert.Equal(HtmlExtractionPlanMode.Static, plan.RecommendedMode);
        Assert.False(plan.LooksLikeJavaScriptShell);
        Assert.True(plan.HasStructuredData);
        Assert.DoesNotContain("Invoke-HtmlRendering", plan.SuggestedCommand);
    }

    [Fact]
    public void Analyze_DoesNotTreatExternalStructuredDataScriptsAsJavaScriptShell() {
        string html = """
<html>
<head>
<script type="application/ld+json" src="/schema.json"></script>
</head>
<body><main></main></body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html);

        Assert.Equal(HtmlExtractionPlanMode.Static, plan.RecommendedMode);
        Assert.False(plan.LooksLikeJavaScriptShell);
        Assert.Equal(0, plan.ExternalScriptCount);
        Assert.DoesNotContain("Invoke-HtmlRendering", plan.SuggestedCommand);
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

    [Fact]
    public void Analyze_DoesNotRecommendRelayWhenGenericSubmitDoesNotTargetForm() {
        string html = """
<html>
<body>
<form method="POST" name="a" action="/continue">
<input type="hidden" name="csrf" value="redacted">
</form>
<script>const data = { submit: function() {} }; data.submit(); const marker = "hiddenform";</script>
</body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html);

        Assert.NotEqual(HtmlExtractionPlanMode.BrowserlessRelayCandidate, plan.RecommendedMode);
        Assert.False(plan.HasAutoSubmitForm);
        Assert.DoesNotContain("Invoke-HtmlFormRelay", plan.SuggestedCommand);
    }

    [Fact]
    public void Analyze_DoesNotRecommendRelayFromNonExecutableScriptMarker() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="/continue">
<input type="hidden" name="SAMLResponse" value="redacted">
<input type="hidden" name="RelayState" value="state">
</form>
<script type="application/json">{"x":"document.forms[0].submit()"}</script>
</body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html);

        Assert.NotEqual(HtmlExtractionPlanMode.BrowserlessRelayCandidate, plan.RecommendedMode);
        Assert.False(plan.HasAutoSubmitForm);
        Assert.DoesNotContain("Invoke-HtmlFormRelay", plan.SuggestedCommand);
    }

    [Fact]
    public void Analyze_DoesNotRecommendRelayForNonHttpAction() {
        string html = """
<html>
<body>
<form method="POST" name="hiddenform" action="mailto:security@example.org">
<input type="hidden" name="SAMLResponse" value="redacted">
</form>
<script>document.forms[0].submit()</script>
</body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html);

        Assert.NotEqual(HtmlExtractionPlanMode.BrowserlessRelayCandidate, plan.RecommendedMode);
        Assert.False(plan.HasAutoSubmitForm);
        Assert.DoesNotContain("Invoke-HtmlFormRelay", plan.SuggestedCommand);
    }

    [Fact]
    public void Analyze_DoesNotRecommendApiProfileForApiLettersInsideNormalWords() {
        string html = """
<html>
<head><title>Capital scraping rapid examples</title></head>
<body>
<main>
<p>This guide covers capital city examples and scraping workflows with rapid iteration for ordinary page extraction.</p>
<p>It has enough readable content to be useful as a static dataset without endpoint catalog signals.</p>
</main>
</body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html, new Uri("https://example.org/capital-scraping-rapid"));

        Assert.NotEqual("api-docs-content", plan.SuggestedProfileName);
        Assert.DoesNotContain("api-docs-content", plan.SuggestedProfileCommand);
    }

    [Fact]
    public void Analyze_RecommendsApiProfileForApiTokenMarkers() {
        string html = """
<html>
<head><title>API reference</title></head>
<body>
<main>
<p>This API reference lists endpoint details, response schemas, and request formats for service integrations.</p>
</main>
</body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html, new Uri("https://example.org/docs/api/reference"));

        Assert.Equal("api-docs-content", plan.SuggestedProfileName);
        Assert.Contains("api-docs-content", plan.SuggestedProfileCommand);
    }

    [Fact]
    public void Analyze_RedactsSensitiveUrlValuesInSuggestedCommands() {
        string html = """
<html>
<body>
<main>
<p>This page contains useful readable content that can be extracted statically while keeping URL secrets out of suggested commands.</p>
</main>
</body>
</html>
""";
        Uri url = new("https://user:pass@example.org/page?access_token=abc123&safe=ok#id_token=frag456");

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html, url);

        Assert.DoesNotContain("user:pass", plan.SuggestedCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", plan.SuggestedCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("frag456", plan.SuggestedCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("user:pass", plan.SuggestedProfileCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", plan.SuggestedProfileCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("frag456", plan.SuggestedProfileCommand, StringComparison.Ordinal);
        Assert.Contains("access_token=<redacted>", plan.SuggestedCommand);
        Assert.Contains("id_token=<redacted>", plan.SuggestedProfileCommand);
    }
}
