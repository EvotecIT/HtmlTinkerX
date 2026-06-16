using System;
using System.Linq;

namespace HtmlTinkerX.Tests;

public class HtmlExtractionProfilesTests {
    [Fact]
    public void Defaults_ExposeProductLevelWorkflowProfiles() {
        Assert.Contains(HtmlExtractionProfiles.Defaults, profile => profile.Name == "docs-content" && profile.CrawlProfileName == "docs-content");
        Assert.Contains(HtmlExtractionProfiles.Defaults, profile => profile.Name == "api-docs-content" && profile.CrawlProfileName == "api-docs-content");
        Assert.Contains(HtmlExtractionProfiles.Defaults, profile => profile.Name == "app-shell" && profile.RenderProfile == HtmlRenderProfile.AppShell);
        Assert.Contains(HtmlExtractionProfiles.Defaults, profile => profile.Name == "interactive-page" && profile.RenderProfile == HtmlRenderProfile.InteractivePage);
        Assert.Contains(HtmlExtractionProfiles.Defaults, profile => profile.Name == "lazy-loaded-content" && profile.RenderProfile == HtmlRenderProfile.LazyLoadedContent);
        Assert.Contains(HtmlExtractionProfiles.Defaults, profile => profile.Name == "network-capture" && profile.RenderProfile == HtmlRenderProfile.NetworkCapture);
        Assert.Contains(HtmlExtractionProfiles.Defaults, profile => profile.Name == "low-bandwidth" && profile.RenderProfile == HtmlRenderProfile.LowBandwidth);
        Assert.Contains(HtmlExtractionProfiles.Defaults, profile => profile.Name == "login-protected-page" && profile.RenderProfile == HtmlRenderProfile.LoginProtected);
        Assert.Contains(HtmlExtractionProfiles.Defaults, profile => profile.Name == "auth-relay-page" && profile.RecommendedMode == HtmlExtractionPlanMode.BrowserlessRelayCandidate);
        Assert.Contains(HtmlExtractionProfiles.Names, name => name == "dataset-page");
    }

    [Fact]
    public void Recommend_UsesApiDocsProfileForApiReferenceUrls() {
        HtmlExtractionPlan plan = new() {
            RecommendedMode = HtmlExtractionPlanMode.Crawl,
            Title = "REST API Reference",
            WordCount = 120,
            LinkCount = 30
        };

        HtmlExtractionProfile profile = HtmlExtractionProfiles.Recommend(plan, new Uri("https://docs.example.org/api/reference"));

        Assert.Equal("api-docs-content", profile.Name);
        Assert.Equal("api-docs-content", profile.CrawlProfileName);
        Assert.True(profile.DatasetReady);
    }

    [Fact]
    public void Analyze_AddsProfileGuidanceToJavaScriptShellPlans() {
        string html = """
<html>
<head><title>App</title><script src="/runtime.js"></script><script src="/app.js"></script></head>
<body><div id="root">Loading...</div></body>
</html>
""";

        HtmlExtractionPlan plan = HtmlExtractionPlanner.Analyze(html, new Uri("https://example.org/app"));

        Assert.Equal(HtmlExtractionPlanMode.RenderedSnapshot, plan.RecommendedMode);
        Assert.Equal("app-shell", plan.SuggestedProfileName);
        Assert.Contains("AppShell", plan.SuggestedProfileCommand, StringComparison.Ordinal);
        Assert.Contains("Thin JavaScript shells", plan.SuggestedProfileReason, StringComparison.Ordinal);
    }
}
