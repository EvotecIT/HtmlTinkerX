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
            + "<object>objectPayload</object><template>templatePayload</template><noscript>noscriptPayload</noscript>"
            + "<svg><title>svgPayload</title><text>chartPayload</text></svg><math><mi>mathPayload</mi></math>"
            + "<video>videoPayload</video><audio>audioPayload</audio><canvas>canvasPayload</canvas><applet>appletPayload</applet><p>After</p>";
        Assert.Equal("Before After", HtmlParserToText.ConvertFragmentToText(html));
        Assert.Equal("Save now", HtmlParserToText.ConvertFragmentToText("<p>Save <svg><title>Close</title></svg> now</p>"));
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

    [Fact]
    public void LineBreaksAndBlockElementsBecomeLinesWhileInlineMarkupJoinsWords() {
        const string html = "<h2>Re<b>port</b> status</h2><p>First line<br>Second line</p>"
            + "<ul><li>One</li><li>T<i>wo</i></li></ul>"
            + "<table><tr><th>Ring</th><th>State</th></tr><tr><td>Pilot</td><td>Ready</td></tr></table><div>Done</div>";

        Assert.Equal("Report status\nFirst line\nSecond line\nOne\nTwo\nRing State\nPilot Ready\nDone",
            HtmlParserToText.ConvertFragmentToText(html, preserveLineBreaks: true));
    }

    [Theory]
    [InlineData("Line one\nLine two\r\nLine three", "Line one\nLine two\nLine three")]
    [InlineData("First\n\n\n\nSecond", "First\n\nSecond")]
    [InlineData("A<br><br>B<br><br><br><br>C", "A\n\nB\n\nC")]
    [InlineData("<p>\n  One\n</p>\n\n<p>Two</p>\n", "One\nTwo")]
    [InlineData("A<br>\nB", "A\nB")]
    [InlineData("  Spaced \t  out  \n  text  ", "Spaced out\ntext")]
    public void TextNewlinesKeepTheirMeaningAndBlankLineRunsCollapse(string html, string expected) {
        Assert.Equal(expected, HtmlParserToText.ConvertFragmentToText(html, preserveLineBreaks: true));
    }

    [Fact]
    public void LineModeDropsScriptStyleNoscriptAndTemplateButKeepsFollowingText() {
        const string html = "<script>alert('a')</script>Lead<style>p{color:red}</style> text"
            + "<noscript><p>Enable scripts</p></noscript><template><p>Hidden</p></template>\nNext line<script>x()</script>";

        Assert.Equal("Lead text\nNext line", HtmlParserToText.ConvertFragmentToText(html, preserveLineBreaks: true));
    }

    [Fact]
    public void LineModeDecodesEntitiesKeepsLiteralLessThanAndPreformattedIndentation() {
        const string html = "<p>A &amp; B &lt;tag&gt; if a < b&nbsp;then</p><pre>code\n    indented</pre>";

        Assert.Equal("A & B <tag> if a < b then\ncode\n    indented",
            HtmlParserToText.ConvertFragmentToText(html, preserveLineBreaks: true));
    }

    [Fact]
    public void DefaultFragmentConversionStillJoinsLinesWithSpaces() {
        const string html = "<p>First line<br>Second line</p>Third\nFourth";

        Assert.Equal("First line Second line Third Fourth", HtmlParserToText.ConvertFragmentToText(html));
        Assert.Equal("First line Second line Third Fourth", HtmlParserToText.ConvertFragmentToText(html, preserveLineBreaks: false));
        Assert.Throws<ArgumentNullException>(() => HtmlParserToText.ConvertFragmentToText(null!, preserveLineBreaks: true));
    }
}
