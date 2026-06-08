using Acornima.Ast;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HtmlTinkerX;

/// <summary>Reusable helpers for traversing and reading Acornima JavaScript AST nodes.</summary>
public static class HtmlJavaScriptAstUtilities {
    /// <summary>Returns all descendant nodes below the supplied AST root.</summary>
    /// <param name="root">The Acornima AST node to traverse.</param>
    /// <returns>Descendant nodes in source order.</returns>
    public static IEnumerable<Node> DescendantNodes(Node root) {
        if (root == null) {
            throw new ArgumentNullException(nameof(root));
        }

        return EnumerateNodes(root, includeRoot: false);
    }

    /// <summary>Returns the supplied AST root followed by all descendant nodes.</summary>
    /// <param name="root">The Acornima AST node to traverse.</param>
    /// <returns>The root and descendant nodes in source order.</returns>
    public static IEnumerable<Node> DescendantNodesAndSelf(Node root) {
        if (root == null) {
            throw new ArgumentNullException(nameof(root));
        }

        return EnumerateNodes(root, includeRoot: true);
    }

    /// <summary>Evaluates JavaScript literal, array, object, and simple unary expression nodes without executing script.</summary>
    /// <param name="node">The Acornima node to evaluate.</param>
    /// <returns>A .NET literal, array, dictionary, or <see langword="null" /> when the node cannot be statically evaluated.</returns>
    public static object? EvaluateJavaScriptLiteral(Node? node) {
        return TryEvaluateJavaScriptLiteral(node, out object? value) ? value : null;
    }

    private static bool TryEvaluateJavaScriptLiteral(Node? node, out object? value) {
        if (node == null) {
            value = null;
            return false;
        }

        if (node is Literal literal) {
            value = literal.Value;
            return true;
        }

        if (node is ArrayExpression array) {
            List<object?> values = new();
            foreach (Node? element in array.Elements) {
                if (element is SpreadElement) {
                    break;
                }

                values.Add(TryEvaluateJavaScriptLiteral(element, out object? item) ? item : null);
            }

            value = values.ToArray();
            return true;
        }

        if (node is ObjectExpression objectExpression) {
            Dictionary<string, object?> values = new(StringComparer.Ordinal);
            foreach (Node propertyNode in objectExpression.Properties) {
                if (propertyNode is SpreadElement) {
                    values.Clear();
                    continue;
                }

                if (propertyNode is not Property property) {
                    continue;
                }

                string? key = GetObjectPropertyName(property);
                if (!string.IsNullOrEmpty(key)) {
                    values[key!] = TryEvaluateJavaScriptLiteral(property.Value, out object? propertyValue) ? propertyValue : null;
                }
            }

            value = values;
            return true;
        }

        if (node is UnaryExpression unary) {
            string? op = unary.Operator.ToString();
            if (!TryEvaluateJavaScriptLiteral(unary.Argument, out object? operandValue)) {
                value = null;
                return false;
            }

            if ((op == "-" || op == "UnaryNegation") && operandValue is IConvertible convertible) {
                try {
                    value = -convertible.ToDouble(CultureInfo.InvariantCulture);
                    return true;
                } catch (FormatException) {
                    value = null;
                    return true;
                } catch (InvalidCastException) {
                    value = null;
                    return true;
                }
            }

            if (op == "!" || op == "LogicalNot") {
                value = !ToJavaScriptBoolean(operandValue);
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Builds a dotted path for an identifier or member-expression target without executing script.</summary>
    /// <param name="node">The identifier or member-expression node to inspect.</param>
    /// <returns>A dotted member path, or <see langword="null" /> when the path cannot be represented statically.</returns>
    public static string? GetMemberPath(Node node) {
        if (node == null) {
            throw new ArgumentNullException(nameof(node));
        }

        if (node is Identifier identifier) {
            return identifier.Name;
        }

        if (node is MemberExpression member) {
            string? target = GetMemberPath(member.Object);
            string? property = member.Computed
                ? GetComputedPropertyName(member.Property)
                : GetPropertyName(member.Property);
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(property)) {
                return null;
            }

            return $"{target}.{property}";
        }

        return null;
    }

    /// <summary>Reads a dotted property path from a statically evaluated JavaScript literal value.</summary>
    /// <param name="value">The evaluated JavaScript literal value.</param>
    /// <param name="propertyPath">A dotted property path such as <c>settings.auth.token</c>.</param>
    /// <returns>The matched value, or <see langword="null" /> when the path is missing or the current value is <see langword="null" />.</returns>
    public static object? GetPropertyPathValue(object? value, string propertyPath) {
        if (propertyPath == null) {
            throw new ArgumentNullException(nameof(propertyPath));
        }

        if (propertyPath.Length == 0) {
            return value;
        }

        object? current = value;
        foreach (string segment in propertyPath.Split('.')) {
            if (segment.Length == 0 || current == null) {
                return null;
            }

            if (current is IReadOnlyDictionary<string, object?> readOnlyDictionary) {
                current = readOnlyDictionary.TryGetValue(segment, out object? child) ? child : null;
                continue;
            }

            if (current is IDictionary<string, object?> dictionary) {
                current = dictionary.TryGetValue(segment, out object? child) ? child : null;
                continue;
            }

            if (current is IDictionary legacyDictionary) {
                current = legacyDictionary.Contains(segment) ? legacyDictionary[segment] : null;
                continue;
            }

            if (current is IList list && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int index)) {
                current = index >= 0 && index < list.Count ? list[index] : null;
                continue;
            }

            return null;
        }

        return current;
    }

    /// <summary>Applies JavaScript truthiness rules to a statically evaluated literal value.</summary>
    /// <param name="value">The literal value to convert.</param>
    /// <returns><see langword="true" /> when JavaScript would treat the value as truthy; otherwise <see langword="false" />.</returns>
    public static bool ToJavaScriptBoolean(object? value) {
        if (value == null) {
            return false;
        }

        if (value is bool boolean) {
            return boolean;
        }

        if (value is string text) {
            return text.Length > 0;
        }

        if (value is IConvertible convertible) {
            try {
                double number = convertible.ToDouble(CultureInfo.InvariantCulture);
                return number != 0 && !double.IsNaN(number);
            } catch (FormatException) {
                return true;
            } catch (InvalidCastException) {
                return true;
            }
        }

        return true;
    }

    private static string? GetPropertyName(Node node) {
        return node switch {
            Identifier identifier => identifier.Name,
            _ => null
        };
    }

    private static string? GetObjectPropertyName(Property property) {
        if (property.Computed) {
            return GetComputedPropertyName(property.Key);
        }

        return property.Key switch {
            Identifier identifier => identifier.Name,
            Literal literal => literal.Value?.ToString(),
            _ => null
        };
    }

    private static string? GetComputedPropertyName(Node node) {
        return node switch {
            Literal literal => literal.Value?.ToString(),
            _ => null
        };
    }

    private static IEnumerable<Node> EnumerateNodes(Node root, bool includeRoot) {
        Stack<Node> stack = new();
        if (includeRoot) {
            stack.Push(root);
        } else {
            List<Node> rootChildren = root.ChildNodes.ToList();
            for (int index = rootChildren.Count - 1; index >= 0; index--) {
                stack.Push(rootChildren[index]);
            }
        }

        while (stack.Count > 0) {
            Node node = stack.Pop();
            yield return node;

            List<Node> children = node.ChildNodes.ToList();
            for (int index = children.Count - 1; index >= 0; index--) {
                stack.Push(children[index]);
            }
        }
    }
}
