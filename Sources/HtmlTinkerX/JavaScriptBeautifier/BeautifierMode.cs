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
/// Represents the internal parser mode used by the <see cref="Beautifier"/>.
/// </summary>
public enum BeautifierMode {
    /// <summary>
    /// Normal block of statements.
    /// </summary>
    Block,

    /// <summary>
    /// Block opened by a "do" statement.
    /// </summary>
    DoBlock,

    /// <summary>
    /// Array expression enclosed in square brackets.
    /// </summary>
    ArrayExpression,

    /// <summary>
    /// Array expression where indentation has already been increased.
    /// </summary>
    IndentedArrayExpression,

    /// <summary>
    /// Parenthetical expression enclosed in parentheses.
    /// </summary>
    ParentheticalExpression,

    /// <summary>
    /// Parenthetical expression used for a "for" loop.
    /// </summary>
    ForExpression,

    /// <summary>
    /// Parenthetical expression used for a condition.
    /// </summary>
    ConditionalExpression,

    /// <summary>
    /// Object literal expression.
    /// </summary>
    Object
}
