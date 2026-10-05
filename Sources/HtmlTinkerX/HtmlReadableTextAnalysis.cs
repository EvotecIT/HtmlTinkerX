using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HtmlTinkerX;

// One document-local pass supplies subtree metrics without rescanning descendants for every container.
internal sealed class HtmlReadableTextAnalysis {
    internal static readonly Regex AttachmentPattern = new(@"\b(attachment|attachments|download|downloads|file|files|pdf|docx|xlsx|pptx|zip|zalacznik|zalaczniki|załącznik|załączniki)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static readonly Regex BoilerplatePattern = new(@"\b(nav|navbar|menu|breadcrumb|breadcrumbs|footer|header|sidebar|search|cookie|cookies|social|share|pagination|strona główna|wyszukaj|hamburger|drukuj|metryczka)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static readonly Regex CookiePattern = new(@"\b(cookie|cookies|consent|privacy|gdpr|rodo|plików cookies|pliki cookies)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly Dictionary<IElement, Metrics> _elements = new();
    private readonly string _text;
    private readonly KeywordIndex _attachments;
    private readonly KeywordIndex _boilerplate;
    private readonly KeywordIndex _cookies;

    internal HtmlReadableTextAnalysis(IDocument document) {
        var text = new StringBuilder();
        var stack = new Stack<(INode Node, bool Exit)>();
        stack.Push((document, false));
        while (stack.Count > 0) {
            var visit = stack.Pop();
            if (visit.Node is IText nodeText) {
                text.Append(nodeText.Data);
                continue;
            }
            if (!visit.Exit) {
                if (visit.Node is IElement element) _elements.Add(element, new Metrics { Start = text.Length });
                stack.Push((visit.Node, true));
                for (int index = visit.Node.ChildNodes.Length - 1; index >= 0; index--) stack.Push((visit.Node.ChildNodes[index], false));
            } else if (visit.Node is IElement element) {
                Metrics metrics = _elements[element];
                metrics.End = text.Length;
                foreach (INode child in element.ChildNodes) {
                    if (child is IText childText) {
                        metrics.Words = WordSummary.Join(metrics.Words, WordSummary.Read(childText.Data));
                    } else if (child is IElement childElement) {
                        Metrics subtree = _elements[childElement];
                        metrics.Words = WordSummary.Join(metrics.Words, subtree.Words);
                        metrics.Links += subtree.Links;
                        metrics.LinkWords += subtree.LinkWords;
                        metrics.LinkAttachments += subtree.LinkAttachments;
                        metrics.Paragraphs += subtree.Paragraphs;
                        metrics.Headings += subtree.Headings;
                        metrics.Tables += subtree.Tables;
                    }
                }
                metrics.OwnLink = element.LocalName == "a" && element.HasAttribute("href");
                if (metrics.OwnLink) {
                    metrics.Links++;
                    metrics.LinkWords += metrics.Words.Count;
                    metrics.LinkAttachments += AttachmentPattern.Matches(element.GetAttribute("href") ?? string.Empty).Count;
                }
                metrics.Paragraphs += element.LocalName == "p" ? 1 : 0;
                metrics.Headings += element.LocalName is "h1" or "h2" or "h3" ? 1 : 0;
                metrics.Tables += element.LocalName == "table" ? 1 : 0;
            }
        }
        _text = text.ToString();
        _attachments = new KeywordIndex(_text, AttachmentPattern);
        _boilerplate = new KeywordIndex(_text, BoilerplatePattern);
        _cookies = new KeywordIndex(_text, CookiePattern);
    }

    internal Metrics Get(IElement element) => _elements[element];

    internal int AttachmentCount(IElement element) {
        Metrics metrics = Get(element);
        int ownHref = metrics.OwnLink ? AttachmentPattern.Matches(element.GetAttribute("href") ?? string.Empty).Count : 0;
        return _attachments.Count(metrics.Start, metrics.End) + metrics.LinkAttachments - ownHref;
    }

    internal int BoilerplateCount(IElement element) {
        Metrics metrics = Get(element);
        string metadata = string.Join(" ", element.Id, element.ClassName, element.GetAttribute("role"), element.GetAttribute("aria-label"));
        int count = BoilerplatePattern.Matches(metadata).Count + _boilerplate.Count(metrics.Start, metrics.End);
        // Preserve a phrase spanning the metadata/text separator, such as aria-label="strona" followed by "główna".
        int metadataStart = Math.Max(0, metadata.Length - 64);
        string boundary = metadata.Substring(metadataStart) + " " + _text.Substring(metrics.Start, Math.Min(64, metrics.End - metrics.Start));
        int separator = metadata.Length - metadataStart;
        foreach (Match match in BoilerplatePattern.Matches(boundary)) {
            if (match.Index < separator && match.Index + match.Length > separator + 1) count++;
        }
        return count;
    }

    internal bool HasCookieSignal(IElement element) {
        Metrics metrics = Get(element);
        string metadata = string.Join(" ", element.Id, element.ClassName, element.GetAttribute("role"), element.GetAttribute("aria-label"));
        return CookiePattern.IsMatch(Normalize(metadata)) || _cookies.Count(metrics.Start, metrics.End) > 0;
    }

    internal int CookieWordCount(IElement element) => Get(element).Words.Count + WordSummary.Read(string.Join(" ",
        element.Id, element.ClassName, element.GetAttribute("role"), element.GetAttribute("aria-label"))).Count;

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    internal sealed class Metrics {
        internal int Start, End, Links, LinkWords, LinkAttachments, Paragraphs, Headings, Tables;
        internal bool OwnLink;
        internal WordSummary Words = WordSummary.Empty;
    }

    // This summary preserves the existing word regex when inline elements split a word or its apostrophe/hyphen run.
    internal struct WordSummary {
        internal int Count;
        private bool _endsInWord, _prefixContinues, _allWordCharacters;
        internal static WordSummary Empty => new() { _prefixContinues = true, _allWordCharacters = true };

        internal static WordSummary Read(string text) {
            WordSummary result = Empty;
            foreach (char character in text) {
                if (char.IsLetter(character) || char.IsNumber(character)) {
                    if (!result._endsInWord) result.Count++;
                    result._endsInWord = true;
                } else if (character is not '\'' and not '’' and not '-') {
                    result._endsInWord = false;
                    result._allWordCharacters = false;
                    if (result.Count == 0) result._prefixContinues = false;
                }
            }
            return result;
        }

        internal static WordSummary Join(WordSummary left, WordSummary right) => new() {
            Count = left.Count + right.Count - (left._endsInWord && right._prefixContinues && right.Count > 0 ? 1 : 0),
            _endsInWord = right._allWordCharacters ? left._endsInWord || right._endsInWord : right._endsInWord,
            _prefixContinues = left.Count > 0 ? left._prefixContinues : left._allWordCharacters && right._prefixContinues,
            _allWordCharacters = left._allWordCharacters && right._allWordCharacters
        };
    }

    private sealed class KeywordIndex {
        private static readonly Regex BoundaryWord = new(@"[\w\u200c\u200d]", RegexOptions.CultureInvariant);
        private readonly string _text;
        private readonly Regex _pattern;
        private readonly Match[] _matches;
        private readonly int[] _starts;

        internal KeywordIndex(string text, Regex pattern) {
            _text = text;
            _pattern = pattern;
            _matches = pattern.Matches(text).Cast<Match>().ToArray();
            _starts = _matches.Select(match => match.Index).ToArray();
        }

        internal int Count(int start, int end) {
            if (start == end) return 0;
            int first = LowerBound(start), after = LowerBound(end);
            int count = after - first;
            if (after > first && _matches[after - 1].Index + _matches[after - 1].Length > end) count--;
            // Flattened neighboring elements may remove a word boundary that exists at the subtree edge.
            // Check only those two edges, retaining one character of context beyond the longest keyword.
            bool prefixAdded = false;
            if (start > 0 && BoundaryWord.IsMatch(_text[start - 1].ToString())) {
                foreach (Match match in _pattern.Matches(_text.Substring(start, Math.Min(64, end - start)))) {
                    if (match.Index == 0) { count++; prefixAdded = true; }
                }
            }
            if (end < _text.Length && BoundaryWord.IsMatch(_text[end].ToString())) {
                int suffixStart = Math.Max(start, end - 64);
                foreach (Match match in _pattern.Matches(_text.Substring(suffixStart, end - suffixStart))) {
                    if (match.Index + match.Length == end - suffixStart && !(prefixAdded && suffixStart + match.Index == start)) count++;
                }
            }
            return count;
        }

        private int LowerBound(int position) {
            int low = 0, high = _starts.Length;
            while (low < high) {
                int middle = low + (high - low) / 2;
                if (_starts[middle] < position) low = middle + 1; else high = middle;
            }
            return low;
        }
    }
}