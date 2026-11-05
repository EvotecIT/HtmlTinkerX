using AngleSharp.Dom;
using System.IO;
using System.Linq;

namespace HtmlTinkerX.Tests;

public class HtmlSelectorRelocatorTests {
    private const string SavedSelector = "div.release-entry";

    internal static string Fixture(string name) => File.ReadAllText(Path.Combine(TestHelpers.ProjectRoot, "Fixtures", name));

    [Fact]
    public void FindCandidates_LocatesElementAfterClassRenameAndWrapperAtHighConfidence() {
        using IDocument before = HtmlParser.ParseWithAngleSharp(Fixture("release-notes-before.html"));
        using IDocument after = HtmlParser.ParseWithAngleSharp(Fixture("release-notes-after.html"));
        HtmlElementFingerprint fingerprint = HtmlElementFingerprint.Capture(before.QuerySelector(SavedSelector)!);
        Assert.Empty(after.QuerySelectorAll(SavedSelector));

        var candidates = HtmlSelectorRelocator.FindCandidates(after, SavedSelector, fingerprint);

        HtmlSelectorRelocationCandidate top = candidates[0];
        Assert.Equal("High", top.Confidence);
        Assert.Equal(3, top.MatchCount);
        Assert.Contains("Version 2.4.0", after.QuerySelector(top.Selector)!.TextContent);
        Assert.Contains("text sample unchanged", top.Reasons);
        Assert.All(candidates.Skip(1), candidate => Assert.NotEqual("High", candidate.Confidence));
    }

    [Fact]
    public void FindCandidates_ReportsNoHighConfidenceCandidateOnUnrelatedPage() {
        using IDocument before = HtmlParser.ParseWithAngleSharp(Fixture("release-notes-before.html"));
        using IDocument unrelated = HtmlParser.ParseWithAngleSharp(Fixture("publisher-linked-picture-article.html"));
        HtmlElementFingerprint fingerprint = HtmlElementFingerprint.Capture(before.QuerySelector(SavedSelector)!);

        var candidates = HtmlSelectorRelocator.FindCandidates(unrelated, SavedSelector, fingerprint);

        Assert.DoesNotContain(candidates, candidate => candidate.Confidence == "High");
    }
}
