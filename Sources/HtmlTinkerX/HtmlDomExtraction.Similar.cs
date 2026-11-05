using AngleSharp.Dom;
using System;
using System.Collections.Generic;

namespace HtmlTinkerX;

public static partial class HtmlDomExtraction {
    /// <summary>
    /// Finds elements in the same document that have the same shape as <paramref name="element"/>, for example the other cards in a product list.
    /// </summary>
    /// <param name="element">Element to compare with, typically one item of a repeated list.</param>
    /// <returns>Similar elements in document order, without <paramref name="element"/> itself.</returns>
    /// <remarks>Candidates must have the same tag and the same ancestor tag path. They are then scored with the
    /// <see cref="HtmlSelectorRelocator"/> fingerprint rules (classes, identity attributes, child structure and sibling count)
    /// and kept at Medium confidence or better.</remarks>
    public static IReadOnlyList<IElement> FindSimilar(IElement element) {
        if (element == null) {
            throw new ArgumentNullException(nameof(element));
        }

        IDocument document = element.Owner
            ?? throw new ArgumentException("The element must belong to a document.", nameof(element));
        // Same tag and ancestor path alone score 40, so Medium confidence also needs matching classes or children.
        HtmlElementFingerprint expected = HtmlElementFingerprint.Capture(element);
        List<IElement> similar = new();
        foreach (IElement candidate in document.GetElementsByTagName(element.LocalName)) {
            if (candidate == element || !HasSameTagPath(element, candidate)) {
                continue;
            }

            if (HtmlSelectorRelocator.Score(expected, HtmlElementFingerprint.Capture(candidate), new List<string>()) >= MediumConfidenceScore) {
                similar.Add(candidate);
            }
        }

        return similar;
    }

    private static bool HasSameTagPath(IElement expected, IElement candidate) {
        IElement? left = expected.ParentElement;
        IElement? right = candidate.ParentElement;
        while (left != null && right != null) {
            if (!string.Equals(left.LocalName, right.LocalName, StringComparison.Ordinal)) {
                return false;
            }

            left = left.ParentElement;
            right = right.ParentElement;
        }

        return left == null && right == null;
    }
}
