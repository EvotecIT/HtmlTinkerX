using HtmlTinkerX;
using System;
using System.Linq;
using Xunit;

namespace PSParseHTML.Tests;

public class HtmlDifferTests {
    [Fact]
    public void Compare_Identical_ReturnsEmpty() {
        var diffs = HtmlDiffer.Compare("<p>a</p>", "<p>a</p>");
        Assert.Empty(diffs);
    }

    [Fact]
    public void Compare_Different_ReturnsDiffs() {
        var diffs = HtmlDiffer.Compare("<p>a</p>", "<p>b</p>");
        Assert.NotEmpty(diffs);
    }

    [Fact]
    public void Compare_NullReference_Throws() {
        Assert.Throws<ArgumentNullException>(() => HtmlDiffer.Compare(null!, "<p></p>"));
    }

    [Fact]
    public void Compare_NullDifference_Throws() {
        Assert.Throws<ArgumentNullException>(() => HtmlDiffer.Compare("<p></p>", null!));
    }
}