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

namespace HtmlTinkerX.JavaScriptBeautifier {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides functionality for beautifying JavaScript code.
    /// </summary>
    public partial class Beautifier {
        /// <summary>
        /// Initializes a new instance of the <see cref="Beautifier"/> class using default options.
        /// </summary>
        public Beautifier()
            : this(new BeautifierOptions()) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Beautifier"/> class using custom options.
        /// </summary>
        /// <param name="opts">Options controlling the beautification process.</param>
        public Beautifier(BeautifierOptions opts) {
            Opts = opts;
            BlankState();
        }

        /// <summary>
        /// Gets or sets the options used by the beautifier.
        /// </summary>
        public BeautifierOptions Opts { get; set; }

        /// <summary>
        /// Gets or sets the current flag state used during processing.
        /// </summary>
        public BeautifierFlags Flags { get; set; } = new BeautifierFlags(BeautifierMode.Block);

        private List<BeautifierFlags> FlagStore { get; set; } = new();

        private bool WantedNewline { get; set; }

        private bool JustAddedNewline { get; set; }

        private bool DoBlockJustClosed { get; set; }

        private string IndentString { get; set; } = string.Empty;

        private string PreindentString { get; set; } = string.Empty;

        private string LastWord { get; set; } = string.Empty;

        private string LastType { get; set; } = string.Empty;

        private string LastText { get; set; } = string.Empty;

        private string LastLastText { get; set; } = string.Empty;

        private string? Input { get; set; }

        private List<string> Output { get; set; } = new();

        private char[] Whitespace { get; set; } = Array.Empty<char>();

        private string Wordchar { get; set; } = string.Empty;

        private string Digits { get; set; } = string.Empty;

        private string[] Punct { get; set; } = Array.Empty<string>();

        private string[] LineStarters { get; set; } = Array.Empty<string>();

        private int ParserPos { get; set; }

        private int NNewlines { get; set; }

        private void BlankState() {
            // internal flags
            Flags = new BeautifierFlags(BeautifierMode.Block);
            FlagStore = new List<BeautifierFlags>();
            WantedNewline = false;
            JustAddedNewline = false;
            DoBlockJustClosed = false;

            if (Opts.IndentWithTabs) {
                IndentString = "\t";
            } else {
                IndentString = new string(Opts.IndentChar, (int)Opts.IndentSize);
            }

            PreindentString = "";
            LastWord = "";               // last TK_WORD seen
            LastType = "TK_START_EXPR";  // last token type
            LastText = "";               // last token text
            LastLastText = "";           // pre-last token text
            Input = null;
            Output = new List<string>(); // formatted javascript gets built here
            Whitespace = new[] { '\n', '\r', '\t', ' ' };
            Wordchar = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_$";
            Digits = "0123456789";
            Punct = "+ - * / % & ++ -- = += -= *= /= %= == === != !== > < >= <= >> << >>> >>>= >>= <<= && &= | || ! !! , : ? ^ ^= |= :: <?= <? ?> <%= <% %>".Split(' ');

            // Words which always should start on a new line
            LineStarters = "continue,try,throw,return,var,if,switch,case,default,for,while,break,function".Split(',');
            SetMode(BeautifierMode.Block);
            ParserPos = 0;
        }

        private void SetMode(BeautifierMode mode) {
            var prev = new BeautifierFlags(BeautifierMode.Block);

            if (Flags != null) {
                FlagStore.Add(Flags);
                prev = Flags;
            }

            Flags = new BeautifierFlags(mode);

            if (FlagStore.Count == 1) {
                Flags.IndentationLevel = 0;
            } else {
                Flags.IndentationLevel = prev.IndentationLevel;

                if (prev.VarLine && prev.VarLineReindented) {
                    Flags.IndentationLevel = Flags.IndentationLevel + 1;
                }
            }

            Flags.PreviousMode = prev.Mode;
        }

        /// <summary>
        /// Beautifies the provided JavaScript code using the configured options.
        /// </summary>
        /// <param name="s">JavaScript source code to beautify.</param>
        /// <param name="opts">Optional overrides for beautifier options.</param>
        /// <returns>The beautified JavaScript code.</returns>
        public string Beautify(string s, BeautifierOptions? opts = null) {
            if (opts != null) {
                Opts = opts;
            }

            BlankState();

            while (s.Length != 0 && (s[0] == ' ' || s[0] == '\t')) {
                PreindentString += s[0];
                s = s.Remove(0, 1);
            }

            Input = s;
            ParserPos = 0;
            while (true) {
                var token = GetNextToken();
                // print (token_text, token_type, self.flags.mode)
                var tokenText = token.Item1;
                var tokenType = token.Item2;

                if (tokenType == "TK_EOF") {
                    break;
                }

                var handlers = new Dictionary<string, Action<string>> {
                    { "TK_START_EXPR", HandleStartExpr },
                    { "TK_END_EXPR", HandleEndExpr },
                    { "TK_START_BLOCK", HandleStartBlock },
                    { "TK_END_BLOCK", HandleEndBlock },
                    { "TK_WORD", HandleWord },
                    { "TK_SEMICOLON", HandleSemicolon },
                    { "TK_STRING", HandleString },
                    { "TK_EQUALS", HandleEquals },
                    { "TK_OPERATOR", HandleOperator },
                    { "TK_COMMA", HandleComma },
                    { "TK_BLOCK_COMMENT", HandleBlockComment },
                    { "TK_INLINE_COMMENT", HandleInlineComment },
                    { "TK_COMMENT", HandleComment },
                    { "TK_DOT", HandleDot },
                    { "TK_UNKNOWN", HandleUnknown }
                };

                handlers[tokenType](tokenText);

                if (tokenType != "TK_INLINE_COMMENT") {
                    LastLastText = LastText;
                    LastType = tokenType;
                    LastText = tokenText;
                }
            }

            var sweetCode = PreindentString + string.Concat(Output).TrimEnd('\n', ' ');
            return sweetCode;
        }

        private void TrimOutput(bool eatNewlines = false) {
            while (Output.Count != 0 &&
                (Output[Output.Count - 1] == " " ||
                Output[Output.Count - 1] == IndentString ||
                Output[Output.Count - 1] == PreindentString ||
                (eatNewlines && (Output[Output.Count - 1] == "\n" || Output[Output.Count - 1] == "\r")))) {
                Output.RemoveAt(Output.Count - 1);
            }
        }

        private bool IsSpecialWord(string s) {
            return s == "case" || s == "return" || s == "do" || s == "if" || s == "throw" || s == "else";
        }

        private bool IsArray(BeautifierMode mode) {
            return mode == BeautifierMode.ArrayExpression || mode == BeautifierMode.IndentedArrayExpression;
        }

        private bool IsExpression(BeautifierMode mode) {
            return mode == BeautifierMode.ArrayExpression ||
                mode == BeautifierMode.IndentedArrayExpression ||
                mode == BeautifierMode.ParentheticalExpression ||
                mode == BeautifierMode.ForExpression ||
                mode == BeautifierMode.ConditionalExpression;
        }

        private void AllowWrapOrPreservedNewline(string tokenText, bool forceLinwrap = false) {
            if (Opts.WrapLineLength > 0 && !forceLinwrap) {
                var startLine = Output.Count - 1;

                while (startLine >= 0) {
                    if (Output[startLine] == "\n") {
                        break;
                    }

                    startLine--;
                }

                startLine++;

                if (startLine < Output.Count) {
                    var slice = new string[Output.Count - startLine];
                    Output.CopyTo(startLine, slice, 0, slice.Length);
                    var currentLine = string.Concat(slice);

                    if (currentLine.Length + tokenText.Length >= Opts.WrapLineLength) {
                        forceLinwrap = true;
                    }
                }
            }

            if (!JustAddedNewline && ((Opts.PreserveNewlines && WantedNewline) || forceLinwrap)) {
                AppendNewline(true, false);
                AppendIndentString();
                WantedNewline = false;
            }
        }

        private void AppendNewline(bool ignoreRepeated = true, bool resetStatementFlags = true) {
            if (Opts.KeepArrayIndentation && IsArray(Flags.Mode)) {
                return;
            }

            if (resetStatementFlags) {
                Flags.IfLine = false;
                Flags.ChainExtraIndentation = 0;
            }

            TrimOutput();

            if (Output.Count == 0) {
                return;
            }

            if (Output[Output.Count - 1] != "\n" || !ignoreRepeated) {
                JustAddedNewline = true;
                Output.Add("\n");
            }

            if (!string.IsNullOrEmpty(PreindentString)) {
                Output.Add(PreindentString);
            }

            foreach (var i in Enumerable.Range(0, Flags.IndentationLevel + Flags.ChainExtraIndentation)) {
                AppendIndentString();
            }

            if (Flags.VarLine && Flags.VarLineReindented) {
                AppendIndentString();
            }
        }

        private void AppendIndentString() {
            if (LastText != "") {
                Output.Add(IndentString);
            }
        }

        private void Append(string s) {
            if (s == " ") {
                // do not add just a single space after the // comment, ever
                if (LastType == "TK_COMMENT") {
                    AppendNewline();
                    return;
                }

                // make sure only single space gets drawn
                if (Output.Count != 0 &&
                    Output[Output.Count - 1] != " " &&
                    Output[Output.Count - 1] != "\n" &&
                    Output[Output.Count - 1] != IndentString) {
                    Output.Add(" ");
                }
            } else {
                JustAddedNewline = false;
                Output.Add(s);
            }
        }

        private void Indent() {
            Flags.IndentationLevel = Flags.IndentationLevel + 1;
        }

        private void RemoveIndent() {
            if (Output.Count != 0 &&
                (Output[Output.Count - 1] == IndentString ||
                 Output[Output.Count - 1] == PreindentString)) {
                Output.RemoveAt(Output.Count - 1);
            }
        }

        private void RestoreMode() {
            DoBlockJustClosed = Flags.Mode == BeautifierMode.DoBlock;

            if (FlagStore.Count > 0) {
                var mode = Flags.Mode;
                Flags = FlagStore[FlagStore.Count - 1];
                FlagStore.RemoveAt(FlagStore.Count - 1);
                Flags.PreviousMode = mode;
            }
        }

        private Tuple<string, string> GetNextToken() {
            NNewlines = 0;

            if (ParserPos >= Input!.Length) {
                return new Tuple<string, string>("", "TK_EOF");
            }

            WantedNewline = false;
            var c = Input[ParserPos];
            ParserPos += 1;
            var keepWhitespace = Opts.KeepArrayIndentation && IsArray(Flags.Mode);

            if (keepWhitespace) {
                var whitespaceCount = 0;

                while (Whitespace.Contains(c)) {
                    if (c == '\n') {
                        TrimOutput();
                        Output.Add("\n");
                        JustAddedNewline = true;
                        whitespaceCount = 0;
                    } else if (c == '\t') {
                        whitespaceCount += 4;
                    } else if (c == '\r') {
                    } else {
                        whitespaceCount += 1;
                    }

                    if (ParserPos >= Input.Length) {
                        return new Tuple<string, string>("", "TK_EOF");
                    }

                    c = Input[ParserPos];
                    ParserPos += 1;
                }

                if (JustAddedNewline) {
                    foreach (var i in Enumerable.Range(0, whitespaceCount)) {
                        Output.Add(" ");
                    }
                }
            } else //  not keep_whitespace
              {
                while (Whitespace.Contains(c)) {
                    if (c == '\n') {
                        if (Opts.MaxPreserveNewlines == 0 || Opts.MaxPreserveNewlines > NNewlines) {
                            NNewlines += 1;
                        }
                    }

                    if (ParserPos >= Input.Length) {
                        return new Tuple<string, string>("", "TK_EOF");
                    }

                    c = Input[ParserPos];
                    ParserPos += 1;
                }

                if (Opts.PreserveNewlines && NNewlines > 1) {
                    foreach (var i in Enumerable.Range(0, NNewlines)) {
                        AppendNewline(i == 0);
                        JustAddedNewline = true;
                    }
                }

                WantedNewline = NNewlines > 0;
            }

            var cc = c.ToString();

            if (Wordchar.Contains(c)) {
                if (ParserPos < Input.Length) {
                    cc = c.ToString();

                    while (Wordchar.Contains(Input[ParserPos])) {
                        cc += Input[ParserPos];
                        ParserPos += 1;
                        if (ParserPos == Input.Length)
                            break;
                    }
                }

                // small and surprisingly unugly hack for 1E-10 representation
                if (ParserPos != Input.Length && "+-".Contains(Input[ParserPos]) && Regex.IsMatch(cc, "^[0-9]+[Ee]$")) {
                    var sign = Input[ParserPos];
                    ParserPos++;
                    var t = GetNextToken();
                    cc += sign + t.Item1;
                    return new Tuple<string, string>(cc, "TK_WORD");
                }

                if (cc == "in") // in is an operator, need to hack
                {
                    return new Tuple<string, string>(cc, "TK_OPERATOR");
                }

                if (WantedNewline
                    && LastType != "TK_OPERATOR"
                    && LastType != "TK_EQUALS"
                    && !Flags.IfLine
                    && (Opts.PreserveNewlines || LastText != "var")) {
                    AppendNewline();
                }

                return new Tuple<string, string>(cc, "TK_WORD");
            }

            if ("([".Contains(c)) {
                return new Tuple<string, string>(c.ToString(), "TK_START_EXPR");
            }

            if (")]".Contains(c)) {
                return new Tuple<string, string>(c.ToString(), "TK_END_EXPR");
            }

            if (c == '{') {
                return new Tuple<string, string>(c.ToString(), "TK_START_BLOCK");
            }

            if (c == '}') {
                return new Tuple<string, string>(c.ToString(), "TK_END_BLOCK");
            }

            if (c == ';') {
                return new Tuple<string, string>(c.ToString(), "TK_SEMICOLON");
            }

            if (c == '/') {
                var comment = "";
                var inlineComment = true;

                if (Input[ParserPos] == '*') // peek /* .. */ comment
                {
                    ParserPos += 1;
                    if (ParserPos < Input.Length) {
                        while (!(Input[ParserPos] == '*' && ParserPos + 1 < Input.Length && Input[ParserPos + 1] == '/') &&
                            ParserPos < Input.Length) {
                            c = Input[ParserPos];
                            comment += c;

                            if ("\r\n".Contains(c)) {
                                inlineComment = false;
                            }

                            ParserPos += 1;

                            if (ParserPos >= Input.Length) {
                                break;
                            }
                        }
                    }

                    ParserPos += 2;

                    if (inlineComment && NNewlines == 0) {
                        return new Tuple<string, string>("/*" + comment + "*/", "TK_INLINE_COMMENT");
                    }

                    return new Tuple<string, string>("/*" + comment + "*/", "TK_BLOCK_COMMENT");
                }

                if (Input[ParserPos] == '/') // peek // comment
                {
                    comment = c.ToString();
                    while (!("\r\n").Contains(Input[ParserPos])) {
                        comment += Input[ParserPos];
                        ParserPos += 1;

                        if (ParserPos >= Input.Length) {
                            break;
                        }
                    }

                    if (WantedNewline) {
                        AppendNewline();
                    }

                    return new Tuple<string, string>(comment, "TK_COMMENT");
                }
            }

            if (c == '\'' || c == '"' ||
                (c == '/' &&
                ((LastType == "TK_WORD" && IsSpecialWord(LastText)) ||
                (LastType == "TK_END_EXPR" && (Flags.PreviousMode == BeautifierMode.ForExpression || Flags.PreviousMode == BeautifierMode.ConditionalExpression)) ||
                ((new[] { "TK_COMMENT", "TK_START_EXPR", "TK_START_BLOCK", "TK_END_BLOCK", "TK_OPERATOR", "TK_EQUALS", "TK_EOF", "TK_SEMICOLON", "TK_COMMA" }).Contains(LastType))))) {
                var sep = c;
                var esc = false;
                var esc1 = 0;
                var esc2 = 0;
                var resultingString = c.ToString();

                if (ParserPos < Input.Length) {
                    if (sep == '/') {
                        // handle regexp
                        var inCharClass = false;
                        while (esc || inCharClass || Input[ParserPos] != sep) {
                            resultingString += Input[ParserPos];
                            if (!esc) {
                                esc = Input[ParserPos] == '\\';
                                if (Input[ParserPos] == '[') {
                                    inCharClass = true;
                                } else if (Input[ParserPos] == ']') {
                                    inCharClass = false;
                                }
                            } else {
                                esc = false;
                            }

                            ParserPos += 1;
                            if (ParserPos >= Input.Length) {
                                // ncomplete regex when end-of-file reached
                                // bail out with what has received so far
                                return new Tuple<string, string>(resultingString, "TK_STRING");
                            }
                        }
                    } else {
                        // handle string
                        while (esc || Input[ParserPos] != sep) {
                            resultingString += Input[ParserPos];
                            if (esc1 != 0 && esc1 >= esc2) {
                                var hex = resultingString.Substring(resultingString.Length - esc2, esc2);
                                if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out esc1)) {
                                    esc1 = 0;
                                }

                                if (esc1 != 0 && esc1 >= 0x20 && esc1 <= 0x7e) {
                                    resultingString = resultingString.Substring(0, resultingString.Length - (esc2 + 2));

                                    if ((char)esc1 == sep || (char)esc1 == '\\') {
                                        resultingString += '\\';
                                    }

                                    resultingString += (char)esc1;
                                }

                                esc1 = 0;
                            }

                            if (esc1 != 0) {
                                ++esc1;
                            } else if (!esc) {
                                esc = Input[ParserPos] == '\\';
                            } else {
                                esc = false;
                                if (Opts.UnescapeStrings) {
                                    if (Input[ParserPos] == 'x') {
                                        esc1 = 1;
                                        esc2 = 2;
                                    } else if (Input[ParserPos] == 'u') {
                                        esc1 = 1;
                                        esc2 = 4;
                                    }
                                }
                            }

                            ParserPos += 1;
                            if (ParserPos >= Input.Length) {
                                // incomplete string when end-of-file reached
                                // bail out with what has received so far
                                return new Tuple<string, string>(resultingString, "TK_STRING");
                            }
                        }
                    }
                }

                ParserPos += 1;
                resultingString += sep;
                if (sep == '/') {
                    // regexps may have modifiers /regexp/MOD, so fetch those too
                    while (ParserPos < Input.Length && Wordchar.Contains(Input[ParserPos])) {
                        resultingString += Input[ParserPos];
                        ParserPos += 1;
                    }
                }
                return new Tuple<string, string>(resultingString, "TK_STRING");
            }

            if (c == '#') {
                var resultString = "";
                // she-bang
                if (Output.Count == 0 && Input.Length > 1 && Input[ParserPos] == '!') {
                    resultString = c.ToString();
                    while (ParserPos < Input.Length && c != '\n') {
                        c = Input[ParserPos];
                        resultString += c;
                        ParserPos += 1;
                    }
                    Output.Add(resultString.Trim() + '\n');
                    AppendNewline();
                    return GetNextToken();
                }

                //  Spidermonkey-specific sharp variables for circular references
                // https://developer.mozilla.org/En/Sharp_variables_in_JavaScript
                // http://mxr.mozilla.org/mozilla-central/source/js/src/jsscan.cpp around line 1935
                var sharp = "#";

                if (ParserPos < Input.Length && Digits.Contains(Input[ParserPos])) {
                    while (true) {
                        c = Input[ParserPos];
                        sharp += c;
                        ParserPos += 1;

                        if (ParserPos >= Input.Length || c == '#' || c == '=') {
                            break;
                        }
                    }
                }

                if (c == '#' || ParserPos >= Input.Length) {
                    // pass
                } else if (Input[ParserPos] == '[' && Input[ParserPos + 1] == ']') {
                    sharp += "[]";
                    ParserPos += 2;
                } else if (Input[ParserPos] == '{' && Input[ParserPos + 1] == '}') {
                    sharp += "{}";
                    ParserPos += 2;
                }

                return new Tuple<string, string>(sharp, "TK_WORD");
            }

            if (c == '<' && Input.Substring(ParserPos - 1, Math.Min(4, Input.Length - ParserPos + 1)) == "<!--") {
                ParserPos += 3;
                var ss = "<!--";

                while (ParserPos < Input.Length && Input[ParserPos] != '\n') {
                    ss += Input[ParserPos];
                    ParserPos += 1;
                }

                Flags.InHtmlComment = true;
                return new Tuple<string, string>(ss, "TK_COMMENT");
            }

            if (c == '-' && Flags.InHtmlComment && Input.Substring(ParserPos - 1, 3) == "-->") {
                Flags.InHtmlComment = false;
                ParserPos += 2;

                if (WantedNewline) {
                    AppendNewline();
                }

                return new Tuple<string, string>("-->", "TK_COMMENT");
            }

            if (c == '.') {
                return new Tuple<string, string>(".", "TK_DOT");
            }

            if (Punct.Contains(c.ToString())) {
                var ss = c.ToString();
                while (ParserPos < Input.Length && Punct.Contains(ss + Input[ParserPos])) {
                    ss += Input[ParserPos];
                    ParserPos += 1;

                    if (ParserPos >= Input.Length) {
                        break;
                    }
                }

                if (ss == "=") {
                    return new Tuple<string, string>("=", "TK_EQUALS");
                }

                if (ss == ",") {
                    return new Tuple<string, string>(",", "TK_COMMA");
                }

                return new Tuple<string, string>(ss, "TK_OPERATOR");
            }

            return new Tuple<string, string>(c.ToString(), "TK_UNKNOWN");
        }

    }
}
