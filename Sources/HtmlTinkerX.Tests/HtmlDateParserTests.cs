using HtmlTinkerX;

namespace HtmlTinkerX.Tests;

public class HtmlDateParserTests {
    [Theory]
    [InlineData("2026-09-29T14:02:11Z", 2026, 9, 29, 14, 2, 11, 0)]
    [InlineData("2026-09-29T14:02:11.1234567+02:00", 2026, 9, 29, 14, 2, 11, 120)]
    [InlineData("2026-09-29T14:02", 2026, 9, 29, 14, 2, 0, 0)]
    [InlineData("2026-09-29 14:02:11", 2026, 9, 29, 14, 2, 11, 0)]
    [InlineData("2026-09-29", 2026, 9, 29, 0, 0, 0, 0)]
    [InlineData("Tue, 29 Sep 2026 14:02:11 GMT", 2026, 9, 29, 14, 2, 11, 0)]
    [InlineData("Tue, 29 Sep 2026 14:02:11 +0000", 2026, 9, 29, 14, 2, 11, 0)]
    [InlineData("Tue, 29 Sep 2026 14:02:11 -0700", 2026, 9, 29, 14, 2, 11, -420)]
    [InlineData("Tue, 29 Sep 2026 14:02:11 UTC", 2026, 9, 29, 14, 2, 11, 0)]
    [InlineData("Tue, 29 Sep 2026 14:02:11 EST", 2026, 9, 29, 14, 2, 11, -300)]
    [InlineData("Tue, 29 Sep 2026 14:02:11 PDT", 2026, 9, 29, 14, 2, 11, -420)]
    [InlineData("Sun, 29 Sep 2026 14:02 GMT", 2026, 9, 29, 14, 2, 0, 0)]
    [InlineData("29 Sep 26 14:02:11 +0100", 2026, 9, 29, 14, 2, 11, 60)]
    [InlineData("29-Sep-2026", 2026, 9, 29, 0, 0, 0, 0)]
    [InlineData("9-Sep-2026", 2026, 9, 9, 0, 0, 0, 0)]
    [InlineData("09/29/2026", 2026, 9, 29, 0, 0, 0, 0)]
    [InlineData("September 29, 2026", 2026, 9, 29, 0, 0, 0, 0)]
    [InlineData("September 9, 2026", 2026, 9, 9, 0, 0, 0, 0)]
    [InlineData("29 September 2026", 2026, 9, 29, 0, 0, 0, 0)]
    [InlineData("  29 September 2026  ", 2026, 9, 29, 0, 0, 0, 0)]
    public void TryParse_ReadsDayPrecisionValues(string value, int year, int month, int day, int hour, int minute, int second, int offsetMinutes) {
        Assert.True(HtmlDateParser.TryParse(value, out DateTimeOffset parsed, out HtmlDatePrecision precision));

        Assert.Equal(HtmlDatePrecision.Day, precision);
        Assert.Equal(new DateTime(year, month, day, hour, minute, second), new DateTime(parsed.Year, parsed.Month, parsed.Day, parsed.Hour, parsed.Minute, parsed.Second));
        Assert.Equal(TimeSpan.FromMinutes(offsetMinutes), parsed.Offset);
    }

    [Theory]
    [InlineData("Sep-2026", 2026, 9)]
    [InlineData("sep-2026", 2026, 9)]
    [InlineData("September 2026", 2026, 9)]
    [InlineData("2026-09", 2026, 9)]
    public void TryParse_ReportsMonthPrecisionForMonthOnlyValues(string value, int year, int month) {
        Assert.True(HtmlDateParser.TryParse(value, out DateTimeOffset parsed, out HtmlDatePrecision precision));

        Assert.Equal(HtmlDatePrecision.Month, precision);
        Assert.Equal(new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero), parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("soon")]
    [InlineData("29/09/2026")]
    [InlineData("2026-13-01")]
    [InlineData("Tue, 29 Sep 2026 14:02:11 XYZ")]
    [InlineData("2026-09-29T14:02:11Z plus a very long trailing text that no real date string would ever carry")]
    public void TryParse_RejectsUnsupportedValues(string? value) {
        Assert.False(HtmlDateParser.TryParse(value, out DateTimeOffset parsed, out HtmlDatePrecision precision));

        Assert.Equal(default, parsed);
        Assert.Equal(HtmlDatePrecision.None, precision);
    }

    [Fact]
    public void TryParse_WithoutPrecisionMatchesThePrecisionOverload() {
        Assert.True(HtmlDateParser.TryParse("Sep-2026", out DateTimeOffset parsed));
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), parsed);
    }
}
