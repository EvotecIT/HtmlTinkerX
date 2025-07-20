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
/// Internal state flags used during JavaScript beautification.
/// </summary>
public class BeautifierFlags {
    /// <summary>
    /// Initializes a new instance of the <see cref="BeautifierFlags"/> class.
    /// </summary>
    /// <param name="mode">The initial mode.</param>
    public BeautifierFlags(BeautifierMode mode) {
        PreviousMode = BeautifierMode.Block;
        Mode = mode;
        VarLine = false;
        VarLineTainted = false;
        VarLineReindented = false;
        InHtmlComment = false;
        IfLine = false;
        ChainExtraIndentation = 0;
        InCase = false;
        InCaseStatement = false;
        CaseBody = false;
        IndentationLevel = 0;
        TernaryDepth = 0;
    }

    /// <summary>
    /// Gets or sets the previous mode.
    /// </summary>
    public BeautifierMode PreviousMode { get; set; }

    /// <summary>
    /// Gets or sets the current mode.
    /// </summary>
    public BeautifierMode Mode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether we're on a variable line.
    /// </summary>
    public bool VarLine { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the variable line is tainted.
    /// </summary>
    public bool VarLineTainted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the variable line is reindented.
    /// </summary>
    public bool VarLineReindented { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether we're in an HTML comment.
    /// </summary>
    public bool InHtmlComment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether we're on an if line.
    /// </summary>
    public bool IfLine { get; set; }

    /// <summary>
    /// Gets or sets the extra indentation for chained methods.
    /// </summary>
    public int ChainExtraIndentation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether we're in a case statement.
    /// </summary>
    public bool InCase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether we're in a case statement block.
    /// </summary>
    public bool InCaseStatement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether we're in a case body.
    /// </summary>
    public bool CaseBody { get; set; }

    /// <summary>
    /// Gets or sets the current indentation level.
    /// </summary>
    public int IndentationLevel { get; set; }

    /// <summary>
    /// Gets or sets the ternary operator depth.
    /// </summary>
    public int TernaryDepth { get; set; }
}