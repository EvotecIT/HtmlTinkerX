using System;
using System.Collections.Generic;
using AngleSharp.Dom;

namespace PSParseHTML;

/// <summary>
/// Utility methods for parsing HTML and extracting elements.
/// </summary>
public static class HtmlParserExtensions {
    /// <summary>
    /// Parses the provided HTML and returns elements matching the specified attributes.
    /// </summary>
    /// <param name="html">HTML content to parse.</param>
    /// <param name="tag">Tag name to search for.</param>
    /// <param name="className">Class name to search for.</param>
    /// <param name="id">ID attribute to search for.</param>
    /// <param name="name">Name attribute to search for.</param>
    /// <returns>Enumeration of matching elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> is null.</exception>
    public static IEnumerable<IElement> GetElements(string html, string? tag = null, string? className = null, string? id = null, string? name = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        var document = HtmlParser.ParseWithAngleSharp(html);

        if (!string.IsNullOrEmpty(tag)) {
            return document.GetElementsByTagName(tag!);
        }
        if (!string.IsNullOrEmpty(className)) {
            return document.GetElementsByClassName(className!);
        }
        if (!string.IsNullOrEmpty(id)) {
            var element = document.GetElementById(id!);
            return element != null ? new[] { element } : Array.Empty<IElement>();
        }
        if (!string.IsNullOrEmpty(name)) {
            return document.GetElementsByName(name!);
        }

        return Array.Empty<IElement>();
    }
}
