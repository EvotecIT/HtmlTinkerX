using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>
/// Structural identity of an element, used to find it again after a layout change. It stores no visible text,
/// only a hash of a normalized text sample, so it can be saved with recipes and baselines.
/// </summary>
public sealed class HtmlElementFingerprint {
    internal const int MaximumPathLength = 16;
    internal const int MaximumChildTags = 32;
    internal const int MaximumTextSample = 256;
    private static readonly string[] IdentityAttributes = {
        "id", "name", "role", "itemprop", "itemtype", "data-type", "data-testid", "aria-label", "type"
    };
    // Generated ids and counters change between deployments; they would make a fingerprint brittle.
    internal static readonly Regex GeneratedValue = new(@"\d{4,}|^[0-9a-f]{8}-[0-9a-f]{4}-", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Lowercase tag name of the element.</summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>Ancestor tag names from the outermost recorded ancestor to the parent, up to 16 levels.</summary>
    public IReadOnlyList<string> TagPath { get; set; } = Array.Empty<string>();

    /// <summary>Class names, excluding state classes such as active or selected, in ordinal order.</summary>
    public IReadOnlyList<string> Classes { get; set; } = Array.Empty<string>();

    /// <summary>Identity attributes such as id, name, role, itemprop or data-testid, excluding generated-looking values.</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

    /// <summary>SHA-256 prefix of the first 256 characters of normalized text; empty when the element has no text.</summary>
    public string TextHash { get; set; } = string.Empty;

    /// <summary>Direct child tag names in document order, up to 32.</summary>
    public IReadOnlyList<string> ChildTags { get; set; } = Array.Empty<string>();

    /// <summary>Number of same-tag siblings under the parent, including the element itself.</summary>
    public int SameTagSiblingCount { get; set; }

    /// <summary>Records the structural identity of an element.</summary>
    /// <param name="element">Element whose current position should be remembered.</param>
    /// <returns>A detached fingerprint that keeps no reference to the document.</returns>
    public static HtmlElementFingerprint Capture(IElement element) {
        if (element == null) throw new ArgumentNullException(nameof(element));
        List<string> path = new();
        for (IElement? parent = element.ParentElement; parent != null && path.Count < MaximumPathLength; parent = parent.ParentElement) {
            path.Insert(0, parent.LocalName);
        }
        Dictionary<string, string> attributes = new(StringComparer.Ordinal);
        foreach (string name in IdentityAttributes) {
            string? value = element.GetAttribute(name);
            if (!string.IsNullOrWhiteSpace(value) && value!.Length <= 80 && !GeneratedValue.IsMatch(value)) {
                attributes[name] = value;
            }
        }
        return new HtmlElementFingerprint {
            TagName = element.LocalName,
            TagPath = path,
            Classes = element.ClassList.Where(static name => !HtmlDomExtraction.IsUnstableClass(name))
                .Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal).ToArray(),
            Attributes = attributes,
            TextHash = HashText(element),
            ChildTags = element.Children.Take(MaximumChildTags).Select(static child => child.LocalName).ToArray(),
            SameTagSiblingCount = CountSameTagSiblings(element)
        };
    }

    internal static string HashText(IElement element) {
        string text = HtmlDomExtraction.NormalizeWhitespace(element.TextContent);
        if (text.Length == 0) return string.Empty;
        if (text.Length > MaximumTextSample) text = text.Substring(0, MaximumTextSample);
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return BitConverter.ToString(hash, 0, 16).Replace("-", string.Empty).ToLowerInvariant();
    }

    internal static int CountSameTagSiblings(IElement element) =>
        element.ParentElement?.Children.Count(sibling => sibling.LocalName == element.LocalName) ?? 1;
}
