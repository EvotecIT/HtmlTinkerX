using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

/// <summary>
/// Suggests where content moved when a saved CSS selector stops matching after a layout change.
/// Candidates are ranked suggestions with reasons; nothing is changed automatically.
/// </summary>
public static class HtmlSelectorRelocator {
    private const int MinimumScore = 40;
    private const int MaximumAncestorScope = 3;
    private static readonly HashSet<string> SkippedTags = new(StringComparer.Ordinal) {
        "html", "head", "body", "script", "style", "template", "noscript", "meta", "link", "title"
    };
    private static readonly Regex ClassTokenSplit = new(@"[^a-zA-Z0-9]+", RegexOptions.Compiled);

    /// <summary>Ranks elements in the current document that resemble a fingerprinted element.</summary>
    /// <param name="document">Current document, for example after the page layout changed.</param>
    /// <param name="selector">Saved selector that no longer finds the expected content. A candidate with the same selector is omitted.</param>
    /// <param name="fingerprint">Fingerprint captured when the selector still matched.</param>
    /// <param name="limit">Maximum number of candidates to return.</param>
    /// <returns>Candidates ordered by score. One selector appears once, with its best-scoring element.</returns>
    /// <remarks>High and Medium use the collection-confidence thresholds (120 and 80). An unchanged text sample
    /// carries a large share of the score, so content that changed together with the layout ranks lower.</remarks>
    public static IReadOnlyList<HtmlSelectorRelocationCandidate> FindCandidates(
        IDocument document, string selector, HtmlElementFingerprint fingerprint, int limit = 5) {
        if (document == null) throw new ArgumentNullException(nameof(document));
        if (selector == null) throw new ArgumentNullException(nameof(selector));
        if (fingerprint == null) throw new ArgumentNullException(nameof(fingerprint));
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        string original = selector.Trim();
        Dictionary<string, HtmlSelectorRelocationCandidate> bySelector = new(StringComparer.Ordinal);
        foreach (IElement element in document.All) {
            if (SkippedTags.Contains(element.LocalName) || element.Closest("head") != null) continue;
            List<string> reasons = new();
            int score = Score(fingerprint, HtmlElementFingerprint.Capture(element), reasons);
            if (score < MinimumScore) continue;
            string candidateSelector = BuildSelector(document, element, out IElement[] matches);
            if (candidateSelector.Length == 0 || string.Equals(candidateSelector, original, StringComparison.Ordinal)) continue;
            if (bySelector.TryGetValue(candidateSelector, out HtmlSelectorRelocationCandidate? existing) && existing.Score >= score) continue;
            // The selector returns every match, so confidence cannot exceed what its least similar matches support.
            int unlike = matches.Count(match => match != element
                && Score(fingerprint, HtmlElementFingerprint.Capture(match), new List<string>()) < HtmlDomExtraction.MediumConfidenceScore);
            int confidenceScore = score;
            if (unlike > 0) {
                reasons.Add($"{unlike} of {matches.Length} matched element(s) do not resemble the saved element");
                confidenceScore = Math.Min(score, unlike * 2 >= matches.Length
                    ? HtmlDomExtraction.MediumConfidenceScore - 1 : HtmlDomExtraction.HighConfidenceScore - 1);
            }
            string sample = HtmlDomExtraction.NormalizeWhitespace(element.TextContent);
            bySelector[candidateSelector] = new HtmlSelectorRelocationCandidate {
                Selector = candidateSelector, Score = score, MatchCount = matches.Length, Reasons = reasons,
                Confidence = HtmlDomExtraction.DescribeCollectionConfidence(confidenceScore),
                SampleText = sample.Length > 240 ? sample.Substring(0, 237) + "..." : sample
            };
        }
        return bySelector.Values
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(static candidate => candidate.MatchCount)
            .ThenBy(static candidate => candidate.Selector, StringComparer.Ordinal)
            .Take(limit)
            .Select((candidate, index) => {
                candidate.Index = index;
                return candidate;
            })
            .ToArray();
    }

    internal static int Score(HtmlElementFingerprint expected, HtmlElementFingerprint current, List<string> reasons) {
        int score = 0;
        if (string.Equals(expected.TagName, current.TagName, StringComparison.Ordinal)) {
            score += 20;
            reasons.Add($"same tag <{current.TagName}>");
        } else {
            reasons.Add($"tag changed from <{expected.TagName}> to <{current.TagName}>");
        }
        int attributeScore = 0;
        foreach (KeyValuePair<string, string> attribute in expected.Attributes) {
            if (current.Attributes.TryGetValue(attribute.Key, out string? value) && string.Equals(value, attribute.Value, StringComparison.Ordinal)) {
                attributeScore += attribute.Key == "id" ? 40 : 15;
                reasons.Add($"{attribute.Key} '{attribute.Value}' unchanged");
            }
        }
        score += Math.Min(attributeScore, 70);
        string[] expectedTokens = ClassTokens(expected.Classes);
        string[] currentTokens = ClassTokens(current.Classes);
        int sharedTokens = expectedTokens.Intersect(currentTokens, StringComparer.Ordinal).Count();
        if (sharedTokens > 0) {
            int union = expectedTokens.Union(currentTokens, StringComparer.Ordinal).Count();
            score += 40 * sharedTokens / union;
            reasons.Add(expected.Classes.SequenceEqual(current.Classes, StringComparer.Ordinal)
                ? "classes unchanged"
                : $"class names share {sharedTokens} of {union} word(s)");
        }
        if (expected.TextHash.Length > 0 && string.Equals(expected.TextHash, current.TextHash, StringComparison.Ordinal)) {
            score += 40;
            reasons.Add("text sample unchanged");
        }
        if (expected.ChildTags.Count == 0 && current.ChildTags.Count == 0) {
            score += 15;
        } else {
            int shared = LongestCommonSubsequence(expected.ChildTags, current.ChildTags);
            int total = Math.Max(expected.ChildTags.Count, current.ChildTags.Count);
            score += 30 * shared / total;
            if (shared > 0) reasons.Add($"child structure {shared} of {total} element(s) in order");
        }
        if (expected.TagPath.Count > 0) {
            int shared = LongestCommonSubsequence(expected.TagPath, current.TagPath);
            int total = Math.Max(expected.TagPath.Count, current.TagPath.Count);
            score += 20 * shared / total;
            if (shared < expected.TagPath.Count || current.TagPath.Count != expected.TagPath.Count) {
                reasons.Add($"ancestor path changed ({shared} of {total} levels in common)");
            }
        }
        if (expected.SameTagSiblingCount == current.SameTagSiblingCount) {
            score += 10;
            if (current.SameTagSiblingCount > 1) reasons.Add($"same number of repeated siblings ({current.SameTagSiblingCount})");
        } else if (expected.SameTagSiblingCount > 1 && current.SameTagSiblingCount > 1) {
            score += 5;
            reasons.Add($"repeated siblings changed from {expected.SameTagSiblingCount} to {current.SameTagSiblingCount}");
        }
        return score;
    }

    private static string BuildSelector(IDocument document, IElement element, out IElement[] matches) {
        // Widen to ancestors only while the selector also matches elements outside the element's sibling group.
        IElement? parent = element.ParentElement;
        string selector = BuildElementSelector(element);
        IElement? ancestor = parent;
        for (int depth = 0; ; depth++) {
            try {
                matches = document.QuerySelectorAll(selector).ToArray();
            } catch {
                matches = Array.Empty<IElement>();
                return string.Empty;
            }
            if (matches.All(match => match.ParentElement == parent) || ancestor == null || depth == MaximumAncestorScope || SkippedTags.Contains(ancestor.LocalName)) {
                return selector;
            }
            selector = BuildElementSelector(ancestor) + " > " + selector;
            ancestor = ancestor.ParentElement;
        }
    }

    private static string BuildElementSelector(IElement element) {
        string? id = element.Id;
        if (!string.IsNullOrEmpty(id) && HtmlDomExtraction.SafeCssIdentifier.IsMatch(id!) && !HtmlElementFingerprint.GeneratedValue.IsMatch(id!)) {
            return element.LocalName + "#" + id;
        }
        return HtmlDomExtraction.BuildLocalSelector(element, includeSemanticAttribute: true);
    }

    private static string[] ClassTokens(IReadOnlyList<string> classes) =>
        classes.SelectMany(static name => ClassTokenSplit.Split(name.ToLowerInvariant()))
            .Where(static token => token.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static int LongestCommonSubsequence(IReadOnlyList<string> left, IReadOnlyList<string> right) {
        int[] previous = new int[right.Count + 1];
        int[] current = new int[right.Count + 1];
        for (int i = 1; i <= left.Count; i++) {
            for (int j = 1; j <= right.Count; j++) {
                current[j] = string.Equals(left[i - 1], right[j - 1], StringComparison.Ordinal)
                    ? previous[j - 1] + 1
                    : Math.Max(previous[j], current[j - 1]);
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Count];
    }
}
