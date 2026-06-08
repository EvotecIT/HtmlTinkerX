using Acornima;
using Acornima.Ast;
using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using AcornimaNode = Acornima.Ast.Node;

namespace HtmlTinkerX;

/// <summary>Result returned when a JavaScript declaration or assignment target is matched.</summary>
public sealed class HtmlJavaScriptVariableMatch {
    /// <summary>The matched declaration name or final assignment member name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The full declaration or assignment target path, such as <c>window.$Config</c>.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>The declaration kind, or <c>Assignment</c> for loose assignment expressions.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The selected property path when a property path filter was requested.</summary>
    public string? PropertyPath { get; set; }

    /// <summary>The statically evaluated value, or the property-path value when <see cref="PropertyPath" /> is set.</summary>
    public object? Value { get; set; }

    /// <summary>The raw literal text for simple literal values.</summary>
    public string? RawValue { get; set; }

    /// <summary>The source script index when extracted from HTML.</summary>
    public int? ScriptIndex { get; set; }

    /// <summary>The script element type attribute when extracted from HTML.</summary>
    public string? ScriptType { get; set; }

    /// <summary>The source AST node that matched the variable or assignment.</summary>
    public AcornimaNode Node { get; set; } = null!;
}

/// <summary>Selects JavaScript declarations and assignments from source text or inline HTML scripts.</summary>
public static class HtmlJavaScriptVariableSelector {
    /// <summary>Selects variable declarations and assignment expressions from JavaScript source.</summary>
    /// <param name="script">JavaScript source text to inspect.</param>
    /// <param name="names">Optional variable names or assignment paths to match.</param>
    /// <param name="contains">Matches names or paths that contain the requested names.</param>
    /// <param name="startsWith">Matches names or paths that start with the requested names.</param>
    /// <param name="declarationOnly">Returns declarations and skips loose assignments.</param>
    /// <param name="propertyPaths">Optional dotted property paths to read from matched literal values.</param>
    /// <param name="tolerant">Enables Acornima tolerant parsing.</param>
    /// <param name="scriptIndex">Optional HTML script index metadata.</param>
    /// <param name="scriptType">Optional HTML script type metadata.</param>
    /// <param name="module">Parses the source with the ECMAScript module grammar.</param>
    /// <returns>Matched declarations or assignments in source order.</returns>
    public static IReadOnlyList<HtmlJavaScriptVariableMatch> SelectJavaScript(
        string script,
        IReadOnlyList<string>? names = null,
        bool contains = false,
        bool startsWith = false,
        bool declarationOnly = false,
        IReadOnlyList<string>? propertyPaths = null,
        bool tolerant = false,
        int? scriptIndex = null,
        string? scriptType = null,
        bool module = false) {
        if (script == null) {
            throw new ArgumentNullException(nameof(script));
        }

        if (contains && startsWith) {
            throw new ArgumentException("Use either contains or startsWith, not both.");
        }

        Parser parser = new(new ParserOptions { Tolerant = tolerant });
        AcornimaNode root = module
            ? parser.ParseModule(script, sourceFile: null)
            : parser.ParseScript(script, sourceFile: null, strict: false);
        return SelectFromAst(root, names, contains, startsWith, declarationOnly, propertyPaths, scriptIndex, scriptType);
    }

    /// <summary>Selects variable declarations and assignment expressions from an Acornima AST.</summary>
    public static IReadOnlyList<HtmlJavaScriptVariableMatch> SelectFromAst(
        AcornimaNode root,
        IReadOnlyList<string>? names = null,
        bool contains = false,
        bool startsWith = false,
        bool declarationOnly = false,
        IReadOnlyList<string>? propertyPaths = null,
        int? scriptIndex = null,
        string? scriptType = null) {
        if (root == null) {
            throw new ArgumentNullException(nameof(root));
        }

        if (contains && startsWith) {
            throw new ArgumentException("Use either contains or startsWith, not both.");
        }

        List<HtmlJavaScriptVariableMatch> matches = new();
        foreach (AcornimaNode node in HtmlJavaScriptAstUtilities.DescendantNodesAndSelf(root)) {
            if (node is VariableDeclaration declaration) {
                foreach (VariableDeclarator declarator in declaration.Declarations) {
                    if (declarator.Id is not Identifier identifier ||
                        string.IsNullOrEmpty(identifier.Name) ||
                        !IsMatch(identifier.Name, identifier.Name, names, contains, startsWith)) {
                        continue;
                    }

                    AddMatches(matches, identifier.Name, identifier.Name, declaration.Kind.ToString(), declarator.Init, declarator, propertyPaths, scriptIndex, scriptType);
                }

                continue;
            }

            if (declarationOnly || node is not AssignmentExpression assignment || assignment.Operator != Operator.Assignment) {
                continue;
            }

            string? path = HtmlJavaScriptAstUtilities.GetMemberPath(assignment.Left);
            string? name = GetLastPathSegment(path);
            if (name is null || name.Length == 0 || !IsMatch(name, path, names, contains, startsWith)) {
                continue;
            }

            AddMatches(matches, name, path ?? name, "Assignment", assignment.Right, assignment, propertyPaths, scriptIndex, scriptType);
        }

        return matches;
    }

    /// <summary>Selects variable declarations and assignment expressions from JavaScript script tags in HTML.</summary>
    public static IReadOnlyList<HtmlJavaScriptVariableMatch> SelectHtml(
        string html,
        IReadOnlyList<string>? names = null,
        bool contains = false,
        bool startsWith = false,
        bool declarationOnly = false,
        IReadOnlyList<string>? propertyPaths = null,
        bool tolerant = false) {
        if (html == null) {
            throw new ArgumentNullException(nameof(html));
        }

        IDocument document = HtmlParser.ParseWithAngleSharp(html);
        List<HtmlJavaScriptVariableMatch> matches = new();
        int scriptIndex = 0;
        foreach (IElement script in document.QuerySelectorAll("script")) {
            string type = script.GetAttribute("type") ?? string.Empty;
            if (!IsJavaScriptScriptType(type)) {
                scriptIndex++;
                continue;
            }

            string content = script.TextContent ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content)) {
                scriptIndex++;
                continue;
            }

            matches.AddRange(SelectJavaScript(content, names, contains, startsWith, declarationOnly, propertyPaths, tolerant, scriptIndex, type, IsJavaScriptModuleType(type)));
            scriptIndex++;
        }

        return matches;
    }

    /// <summary>Returns whether a script type should be treated as JavaScript.</summary>
    public static bool IsJavaScriptScriptType(string? type) {
        string normalized = NormalizeScriptType(type);
        if (normalized.Length == 0) {
            return true;
        }

        return IsJavaScriptModuleType(normalized)
            || normalized.Equals("text/javascript", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("application/ecmascript", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("text/ecmascript", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJavaScriptModuleType(string? type) {
        return NormalizeScriptType(type).Equals("module", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeScriptType(string? type) {
        return (type ?? string.Empty).Split(';')[0].Trim();
    }

    private static void AddMatches(
        List<HtmlJavaScriptVariableMatch> matches,
        string name,
        string path,
        string kind,
        AcornimaNode? valueNode,
        AcornimaNode node,
        IReadOnlyList<string>? propertyPaths,
        int? scriptIndex,
        string? scriptType) {
        object? value = HtmlJavaScriptAstUtilities.EvaluateJavaScriptLiteral(valueNode);
        if (propertyPaths == null || propertyPaths.Count == 0) {
            matches.Add(CreateMatch(name, path, kind, null, value, valueNode, node, scriptIndex, scriptType));
            return;
        }

        foreach (string propertyPath in propertyPaths) {
            matches.Add(CreateMatch(
                name,
                path,
                kind,
                propertyPath,
                HtmlJavaScriptAstUtilities.GetPropertyPathValue(value, propertyPath),
                valueNode,
                node,
                scriptIndex,
                scriptType));
        }
    }

    private static HtmlJavaScriptVariableMatch CreateMatch(
        string name,
        string path,
        string kind,
        string? propertyPath,
        object? value,
        AcornimaNode? valueNode,
        AcornimaNode node,
        int? scriptIndex,
        string? scriptType) {
        return new HtmlJavaScriptVariableMatch {
            Name = name,
            Path = path,
            Kind = kind,
            PropertyPath = propertyPath,
            Value = value,
            RawValue = valueNode is Literal literal ? literal.Raw : null,
            ScriptIndex = scriptIndex,
            ScriptType = scriptType,
            Node = node
        };
    }

    private static bool IsMatch(string variableName, string? variablePath, IReadOnlyList<string>? names, bool contains, bool startsWith) {
        if (names == null || names.Count == 0) {
            return true;
        }

        foreach (string name in names) {
            if (contains && (variableName.Contains(name) || (variablePath?.Contains(name) ?? false))) {
                return true;
            }

            if (startsWith && (variableName.StartsWith(name) || (variablePath?.StartsWith(name) ?? false))) {
                return true;
            }

            if (!contains && !startsWith && (variableName == name || variablePath == name)) {
                return true;
            }
        }

        return false;
    }

    private static string? GetLastPathSegment(string? path) {
        if (string.IsNullOrEmpty(path)) {
            return null;
        }

        int separator = path!.LastIndexOf('.');
        return separator >= 0 ? path.Substring(separator + 1) : path;
    }
}
