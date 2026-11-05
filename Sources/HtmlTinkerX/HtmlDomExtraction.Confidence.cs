using AngleSharp.Dom;
using System.Linq;

namespace HtmlTinkerX;

public static partial class HtmlDomExtraction {
    internal static string GetSelectorConfidence(string html, string selector) {
        using IDocument document = HtmlParser.ParseWithAngleSharp(html);
        IElement[] items = document.QuerySelectorAll(selector).ToArray();
        if (items.Length == 0) return "Low";
        HtmlDomSelectorFieldCandidate[] fields = DiscoverFields(items, GetEffectiveBaseUri(document, null), completeStructuralEvidence: true);
        return DescribeCollectionConfidence(ScoreCollection(items, fields, string.Empty, selector, structuralOnly: true));
    }

    internal const int HighConfidenceScore = 120;
    internal const int MediumConfidenceScore = 80;

    internal static string DescribeCollectionConfidence(int score) =>
        score >= HighConfidenceScore ? "High" : score >= MediumConfidenceScore ? "Medium" : "Low";
}
