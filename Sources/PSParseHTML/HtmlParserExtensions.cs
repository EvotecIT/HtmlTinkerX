using System;
using System.Collections.Generic;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace PSParseHTML;

/// <summary>
/// Extension methods for <see cref="HtmlParser"/>.
/// </summary>
public static class HtmlParserExtensions {
    /// <summary>
    /// Parses the provided HTML and returns elements matching the specified attributes.
    /// </summary>
    /// <param name="parser">The HTML parser instance.</param>
    /// <param name="html">HTML content to parse.</param>
    /// <param name="tag">Tag name to search for.</param>
    /// <param name="className">Class name to search for.</param>
    /// <param name="id">ID attribute to search for.</param>
    /// <param name="name">Name attribute to search for.</param>
    /// <returns>Enumeration of matching elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parser"/> or <paramref name="html"/> is null.</exception>
    public static IEnumerable<IElement> GetElements(this HtmlParser parser, string html, string? tag = null, string? className = null, string? id = null, string? name = null) {
        if (parser == null) {
            throw new ArgumentNullException(nameof(parser));
        }
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        var document = parser.ParseDocument(html);

        if (!string.IsNullOrEmpty(tag)) {
            return document.GetElementsByTagName(tag);
        }
        if (!string.IsNullOrEmpty(className)) {
            return document.GetElementsByClassName(className);
        }
        if (!string.IsNullOrEmpty(id)) {
            var element = document.GetElementById(id);
            return element != null ? new[] { element } : Array.Empty<IElement>();
        }
        if (!string.IsNullOrEmpty(name)) {
            return document.GetElementsByName(name);
        }

        return Array.Empty<IElement>();
    }
}
