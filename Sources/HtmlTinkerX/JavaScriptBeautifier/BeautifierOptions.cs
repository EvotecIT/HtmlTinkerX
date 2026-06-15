#region License
// MIT License
//
// Copyright (c) 2018 Denis Ivanov
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion

namespace HtmlTinkerX.JavaScriptBeautifier;

/// <summary>
/// Configuration options for JavaScript beautification.
/// </summary>
public class BeautifierOptions {
    /// <summary>
    /// Initializes a new instance of the <see cref="BeautifierOptions"/> class with default values.
    /// </summary>
    public BeautifierOptions() {
        IndentSize = 4;
        IndentChar = ' ';
        IndentWithTabs = false;
        PreserveNewlines = true;
        MaxPreserveNewlines = 10.0f;
        JslintHappy = false;
        BraceStyle = BraceStyle.Collapse;
        KeepArrayIndentation = false;
        KeepFunctionIndentation = false;
        EvalCode = false;
        WrapLineLength = 0;
        BreakChainedMethods = false;
        UnescapeStrings = false;
        SplitLongStringLiterals = false;
        MaxStringLiteralLength = 0;
    }

    /// <summary>
    /// Gets or sets the number of spaces to use for indentation.
    /// </summary>
    public uint IndentSize { get; set; }

    /// <summary>
    /// Gets or sets the character to use for indentation.
    /// </summary>
    public char IndentChar { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use tabs for indentation.
    /// </summary>
    public bool IndentWithTabs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to preserve existing newlines.
    /// </summary>
    public bool PreserveNewlines { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of newlines to preserve.
    /// </summary>
    public float MaxPreserveNewlines { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to format for JSLint compatibility.
    /// </summary>
    public bool JslintHappy { get; set; }

    /// <summary>
    /// Gets or sets the brace style to use.
    /// </summary>
    public BraceStyle BraceStyle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to keep array indentation.
    /// </summary>
    public bool KeepArrayIndentation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to keep function indentation.
    /// </summary>
    public bool KeepFunctionIndentation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to evaluate code.
    /// </summary>
    public bool EvalCode { get; set; }

    /// <summary>
    /// Gets or sets the line length at which to wrap.
    /// </summary>
    public int WrapLineLength { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to break chained methods.
    /// </summary>
    public bool BreakChainedMethods { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether hexadecimal escape sequences in strings should be converted to characters.
    /// </summary>
    public bool UnescapeStrings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether long quoted string literals should be split into concatenated chunks.
    /// </summary>
    public bool SplitLongStringLiterals { get; set; }

    /// <summary>
    /// Gets or sets the maximum raw content length for a quoted string chunk when splitting long literals.
    /// A value of zero uses <see cref="WrapLineLength"/> when set, otherwise a conservative editor-friendly default.
    /// </summary>
    public int MaxStringLiteralLength { get; set; }
}
