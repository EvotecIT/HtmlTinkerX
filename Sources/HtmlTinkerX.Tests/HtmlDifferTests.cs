using HtmlTinkerX;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace HtmlTinkerX.Tests;

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
        var method = typeof(HtmlDiffer).GetMethod(nameof(HtmlDiffer.Compare)) ?? throw new MissingMethodException();
        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object?[] { null, "<p></p>" }));
        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }

    [Fact]
    public void Compare_NullDifference_Throws() {
        var method = typeof(HtmlDiffer).GetMethod(nameof(HtmlDiffer.Compare)) ?? throw new MissingMethodException();
        var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object?[] { "<p></p>", null }));
        Assert.IsType<ArgumentNullException>(ex.InnerException);
    }
}