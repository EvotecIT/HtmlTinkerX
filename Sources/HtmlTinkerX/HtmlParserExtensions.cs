using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.IO;

namespace HtmlTinkerX;

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
    /// <example>
    /// <code>
    /// var divs = HtmlParserExtensions.GetElements(html, tag: "div");
    /// </code>
    /// </example>
    public static IEnumerable<IElement> GetElements(string? html, string? tag = null, string? className = null, string? id = null, string? name = null) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        var document = HtmlParser.ParseWithAngleSharp(html);

        if (!(tag == null || tag.Length == 0)) {
            string tagValue = tag;
            return document.GetElementsByTagName(tagValue);
        }
        if (!(className == null || className.Length == 0)) {
            string classValue = className;
            return document.GetElementsByClassName(classValue);
        }
        if (!(id == null || id.Length == 0)) {
            string idValue = id;
            var element = document.GetElementById(idValue);
            return element != null ? new[] { element } : Array.Empty<IElement>();
        }
        if (!(name == null || name.Length == 0)) {
            string nameValue = name;
            return document.GetElementsByName(nameValue);
        }

        return Array.Empty<IElement>();
    }

    /// <summary>
    /// Parses the provided HTML file and returns elements matching the specified attributes.
    /// </summary>
    /// <param name="filePath">Path to the HTML file.</param>
    /// <param name="tag">Tag name to search for.</param>
    /// <param name="className">Class name to search for.</param>
    /// <param name="id">ID attribute to search for.</param>
    /// <param name="name">Name attribute to search for.</param>
    /// <returns>Enumeration of matching elements.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static IEnumerable<IElement> GetElementsFromFile(string filePath, string? tag = null, string? className = null, string? id = null, string? name = null) {
        string html = HtmlUtilities.ReadFileChecked(filePath);
        return GetElements(html, tag, className, id, name);
    }
}