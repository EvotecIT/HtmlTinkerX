using HtmlTinkerX;
using Xunit;

namespace HtmlTinkerX.Tests;

public class PreMailerWarningTests {
    [Fact]
    public void Constructor_DefaultSeverity_IsWarning() {
        var warning = new PreMailerWarning("msg");
        Assert.Equal(PreMailerSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void Constructor_AssignsProvidedSeverity() {
        var warning = new PreMailerWarning("info", PreMailerSeverity.Info);
        Assert.Equal(PreMailerSeverity.Info, warning.Severity);
    }
}