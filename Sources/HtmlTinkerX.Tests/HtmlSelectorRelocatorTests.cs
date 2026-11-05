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
    public void FindCandidates_WidensSelectorThatAlsoMatchesUnrelatedElements() {
        using IDocument before = HtmlParser.ParseWithAngleSharp(
            "<main><ul class='releases'><li class='release'>Version 2.4.0</li><li class='release'>Version 2.3.1</li></ul></main>");
        // The renamed class also appears outside the moved list, and a sibling with another class keeps the
        // same-tag sibling count above the number of renamed items.
        using IDocument after = HtmlParser.ParseWithAngleSharp(
            "<main><section class='notes'><ul><li class='entry'>Version 2.4.0</li><li class='entry'>Version 2.3.1</li>"
            + "<li class='promo'>Subscribe</li></ul></section><aside><ul><li class='entry'>Unrelated</li></ul></aside></main>");
        HtmlElementFingerprint fingerprint = HtmlElementFingerprint.Capture(before.QuerySelector("li.release")!);

        HtmlSelectorRelocationCandidate top = HtmlSelectorRelocator.FindCandidates(after, "ul.releases > li.release", fingerprint)[0];

        Assert.Equal(2, top.MatchCount);
        Assert.All(after.QuerySelectorAll(top.Selector), match => Assert.NotNull(match.Closest("section.notes")));
    }

    [Fact]
    public void FindCandidates_DoesNotRateHighWhenSelectorAlsoMatchesUnlikeSibling() {
        const string Release = "<li class='release' role='listitem'><h3>Version {0}</h3><p>Notes</p></li>";
        using IDocument before = HtmlParser.ParseWithAngleSharp("<main><ul>"
            + string.Format(Release, "2.4.0") + string.Format(Release, "2.3.1") + string.Format(Release, "2.3.0") + "</ul></main>");
        // The class was dropped, so the role attribute is the only hook, and a promotion item shares it.
        using IDocument after = HtmlParser.ParseWithAngleSharp("<main><ul>"
            + "<li role='listitem'><h3>Version 2.4.0</h3><p>Notes</p></li><li role='listitem'><h3>Version 2.3.1</h3><p>Notes</p></li>"
            + "<li role='listitem'><a href='/subscribe'>Subscribe</a></li></ul></main>");
        HtmlElementFingerprint fingerprint = HtmlElementFingerprint.Capture(before.QuerySelector("li")!);

        HtmlSelectorRelocationCandidate top = HtmlSelectorRelocator.FindCandidates(after, "li.release", fingerprint)[0];

        Assert.Equal("li[role='listitem']", top.Selector);
        Assert.NotEqual("High", top.Confidence);
        Assert.Contains("1 of 3 matched element(s) do not resemble the saved element", top.Reasons);
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
