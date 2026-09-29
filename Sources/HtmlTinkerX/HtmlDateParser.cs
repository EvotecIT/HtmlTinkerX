using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>
/// Parses the date strings found in feeds, release notes and tables with a fixed, culture-independent set of formats.
/// </summary>
/// <remarks>
/// ISO 8601 and RFC 822/1123 forms are tried first, then these InvariantCulture formats:
/// <c>dd-MMM-yyyy</c>, <c>d-MMM-yyyy</c>, <c>MMM-yyyy</c>, <c>MMMM yyyy</c>, <c>MM/dd/yyyy</c>, <c>MMMM d, yyyy</c> and <c>d MMMM yyyy</c>.
/// Values without an offset are read as UTC. RFC 822 zone names (GMT, UT, UTC, EST, PDT and the other US zones) are understood,
/// and a leading weekday is ignored, so a feed that names the wrong weekday still parses.
/// </remarks>
public static class HtmlDateParser {
    /// <summary>Longest input considered; real date strings are far shorter.</summary>
    private const int MaximumLength = 64;

    private static readonly string[] IsoDayFormats = {
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mmK",
        "yyyy-MM-dd HH:mm:ss.FFFFFFFK",
        "yyyy-MM-dd HH:mm:ssK",
        "yyyy-MM-dd HH:mmK",
        "yyyy-MM-dd"
    };

    private static readonly string[] IsoMonthFormats = { "yyyy-MM" };

    private static readonly string[] RfcFormats = {
        "d MMM yyyy HH:mm:ss zzz",
        "d MMM yyyy HH:mm zzz",
        "d MMM yy HH:mm:ss zzz",
        "d MMM yy HH:mm zzz",
        "d MMM yyyy HH:mm:ss",
        "d MMM yyyy HH:mm"
    };

    private static readonly string[] ExplicitDayFormats = {
        "dd-MMM-yyyy",
        "d-MMM-yyyy",
        "MM/dd/yyyy",
        "MMMM d, yyyy",
        "d MMMM yyyy"
    };

    private static readonly string[] ExplicitMonthFormats = {
        "MMM-yyyy",
        "MMMM yyyy"
    };

    private static readonly Regex LeadingWeekday = new(@"^[A-Za-z]{3,9}\.?,?\s+(?=\d)", RegexOptions.CultureInvariant);
    private static readonly Regex CompactOffset = new(@"\s([+-])(\d{2})(\d{2})$", RegexOptions.CultureInvariant);
    private static readonly Regex ZoneName = new(@"\s([A-Za-z]{1,3})$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses <paramref name="value"/> with the supported formats.
    /// </summary>
    /// <param name="value">The date string.</param>
    /// <param name="result">The parsed value; UTC when the string carried no offset.</param>
    /// <returns>True when the value matched one of the supported formats.</returns>
    public static bool TryParse(string? value, out DateTimeOffset result) => TryParse(value, out result, out _);

    /// <summary>
    /// Parses <paramref name="value"/> with the supported formats and reports whether it named a day or only a month.
    /// </summary>
    /// <param name="value">The date string.</param>
    /// <param name="result">The parsed value; UTC when the string carried no offset, and the first of the month for a month-only value.</param>
    /// <param name="precision"><see cref="HtmlDatePrecision.Day"/> or <see cref="HtmlDatePrecision.Month"/>; <see cref="HtmlDatePrecision.None"/> when parsing failed.</param>
    /// <returns>True when the value matched one of the supported formats.</returns>
    public static bool TryParse(string? value, out DateTimeOffset result, out HtmlDatePrecision precision) {
        result = default;
        precision = HtmlDatePrecision.None;
        string text = (value ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > MaximumLength) {
            return false;
        }

        if (TryExact(text, IsoDayFormats, out result)) {
            precision = HtmlDatePrecision.Day;
            return true;
        }

        if (TryExact(text, IsoMonthFormats, out result)) {
            precision = HtmlDatePrecision.Month;
            return true;
        }

        if (TryExact(NormalizeRfc822(text), RfcFormats, out result)) {
            precision = HtmlDatePrecision.Day;
            return true;
        }

        if (TryExact(text, ExplicitDayFormats, out result)) {
            precision = HtmlDatePrecision.Day;
            return true;
        }

        if (TryExact(text, ExplicitMonthFormats, out result)) {
            precision = HtmlDatePrecision.Month;
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryExact(string text, string[] formats, out DateTimeOffset result) =>
        DateTimeOffset.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);

    /// <summary>Drops a leading weekday and rewrites RFC 822 zones (<c>+0000</c>, <c>GMT</c>, <c>PDT</c>) as <c>+hh:mm</c>.</summary>
    private static string NormalizeRfc822(string text) {
        string normalized = LeadingWeekday.Replace(text, string.Empty);
        normalized = CompactOffset.Replace(normalized, " $1$2:$3");
        Match zone = ZoneName.Match(normalized);
        if (zone.Success) {
            string? offset = ZoneOffset(zone.Groups[1].Value);
            if (offset != null) {
                normalized = normalized.Substring(0, zone.Index) + " " + offset;
            }
        }

        return normalized;
    }

    private static string? ZoneOffset(string zone) {
        switch (zone.ToUpperInvariant()) {
            case "Z":
            case "UT":
            case "UTC":
            case "GMT":
                return "+00:00";
            case "EDT":
                return "-04:00";
            case "EST":
            case "CDT":
                return "-05:00";
            case "CST":
            case "MDT":
                return "-06:00";
            case "MST":
            case "PDT":
                return "-07:00";
            case "PST":
                return "-08:00";
            default:
                return null;
        }
    }
}
