using System;
using Xunit;

namespace HtmlTinkerX.Tests;

public class HtmlParserFragmentTextTests {
    [Theory]
    [InlineData("<p>Updated <code>report</code>Status: enabled.</p><ul><li>List calendars.</li><li>Get events.</li></ul><p>Review tokens.</p>")]
    [InlineData("<p>Updated <strong>report</strong>Status: enabled.</p><ol><li>List calendars.<ul><li>Get events.</li></ul></li></ol><p>Review tokens.</p>")]
    [InlineData("<p>Updated <i>report</i>Status: enabled.</p>List calendars.<br>Get events.<p>Review tokens.</p>")]
    public void GraphFragmentsKeepListAndParagraphBoundariesWithoutSplittingApiNames(string html) {
        Assert.Equal("Updated reportStatus: enabled. List calendars. Get events. Review tokens.",
            HtmlParserToText.ConvertFragmentToText(html));
    }

    [Fact]
    public void SectionsTablesAndLineBreaksKeepTheirReadingOrder() {
        const string html = "<h2>Availability</h2><table><caption>Deployment rings</caption><tr><th>Ring</th><th>State</th></tr>"
            + "<tr><td>Pilot</td><td>Ready</td></tr></table><dl><dt>Next</dt><dd>Review<br>Deploy</dd></dl>";
        Assert.Equal("Availability Deployment rings Ring State Pilot Ready Next Review Deploy",
            HtmlParserToText.ConvertFragmentToText(html));
    }

    [Theory]
    [InlineData("<search>First</search><search>Second</search>")]
    [InlineData("<center>First</center><center>Second</center>")]
    [InlineData("<select><option>First</option><option>Second</option></select>")]
    public void OtherStandardAndLegacyBlockElementsKeepWordBoundaries(string html) {
        Assert.Equal("First Second", HtmlParserToText.ConvertFragmentToText(html));
    }

    [Fact]
    public void ActiveAndEmbeddedContentDoesNotBecomeSourceText() {
        const string html = "<p>Before</p><script>scriptPayload()</script><style>.stylePayload{display:block}</style>"
            + "<iframe src='https://example.org/frame'>iframePayload</iframe><embed src='https://example.org/embed' title='embedPayload'>"
            + "<object>objectPayload</object><template>templatePayload</template><noscript>noscriptPayload</noscript><p>After</p>";
        Assert.Equal("Before After", HtmlParserToText.ConvertFragmentToText(html));
    }

    [Fact]
    public void InlineSpacingPunctuationAndEncodedTextStayLiteral() {
        const string html = "Use <a href='https://example.org/docs'><b>trace</b>Status</a>: <em>ready</em>, then <span>deploy</span>."
            + " &lt;script&gt; is text &amp; code; keep&nbsp;words together.";
        Assert.Equal("Use traceStatus: ready, then deploy. <script> is text & code; keep words together.",
            HtmlParserToText.ConvertFragmentToText(html));
    }

    [Fact]
    public void EmptyFragmentsHaveNoTextAndNullIsRejected() {
        Assert.Equal(string.Empty, HtmlParserToText.ConvertFragmentToText(" \n\t"));
        Assert.Equal(string.Empty, HtmlParserToText.ConvertFragmentToText("<div></div>"));
        Assert.Throws<ArgumentNullException>(() => HtmlParserToText.ConvertFragmentToText(null!));
    }
}
