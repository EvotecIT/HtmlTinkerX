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
        private void HandleStartExpr(string tokenText) {
            if (tokenText == "[") {
                if (LastType == "TK_WORD" || LastText == ")") {
                    if (LineStarters.Contains(LastText)) {
                        Append(" ");
                    }

                    SetMode(BeautifierMode.ParentheticalExpression);
                    Append(tokenText);
                    return;
                }

                if (Flags.Mode == BeautifierMode.ArrayExpression || Flags.Mode == BeautifierMode.IndentedArrayExpression) {
                    if (LastLastText == "]" && LastText == ",") {
                        // # ], [ goes to a new line
                        if (Flags.Mode == BeautifierMode.ArrayExpression) {
                            Flags.Mode = BeautifierMode.IndentedArrayExpression;
                            if (!Opts.KeepArrayIndentation) {
                                Indent();
                            }
                        }

                        SetMode(BeautifierMode.ArrayExpression);

                        if (!Opts.KeepArrayIndentation) {
                            AppendNewline();
                        }
                    } else if (LastText == "[") {
                        if (Flags.Mode == BeautifierMode.ArrayExpression) {
                            Flags.Mode = BeautifierMode.IndentedArrayExpression;

                            if (!Opts.KeepArrayIndentation) {
                                Indent();
                            }
                        }
                        SetMode(BeautifierMode.ArrayExpression);

                        if (!Opts.KeepArrayIndentation) {
                            AppendNewline();
                        }
                    } else {
                        SetMode(BeautifierMode.ArrayExpression);
                    }
                } else {
                    SetMode(BeautifierMode.ArrayExpression);
                }
            } else {
                if (LastText == "for") {
                    SetMode(BeautifierMode.ForExpression);
                } else if (LastText == "if" || LastText == "while") {
                    SetMode(BeautifierMode.ConditionalExpression);
                } else {
                    SetMode(BeautifierMode.ParentheticalExpression);
                }
            }

            if (LastText == ";" || LastType == "TK_START_BLOCK") {
                AppendNewline();
            } else if (LastType == "TK_END_EXPR" || LastType == "TK_START_EXPR" || LastType == "TK_END_BLOCK" || LastText == ".") {
                // do nothing on (( and )( and ][ and ]( and .(
                if (WantedNewline) {
                    AppendNewline();
                }
            } else if (LastType != "TK_WORD" && LastType != "TK_OPERATOR") {
                Append(" ");
            } else if (LastWord == "function" || LastWord == "typeof") {
                // function() vs function (), typeof() vs typeof ()
                if (Opts.JslintHappy) {
                    Append(" ");
                }
            } else if (LineStarters.Contains(LastText) || LastText == "catch") {
                Append(" ");
            }

            if (LastType == "TK_EQUALS" ||
                LastType == "TK_OPERATOR") {
                if (Flags.Mode != BeautifierMode.Object) {
                    AllowWrapOrPreservedNewline(tokenText);
                }
            }

            Append(tokenText);
        }

        private void HandleEndExpr(string tokenText) {
            if (tokenText == "]") {
                if (Opts.KeepArrayIndentation) {
                    if (LastText == "}") {
                        RemoveIndent();
                        Append(tokenText);
                        RestoreMode();
                        return;
                    }
                } else if (Flags.Mode == BeautifierMode.IndentedArrayExpression) {
                    if (LastText == "]") {
                        RestoreMode();
                        AppendNewline();
                        Append(tokenText);
                        return;
                    }
                }
            }
            RestoreMode();
            Append(tokenText);
        }

        private void HandleStartBlock(string tokenText) {
            TrackStringLiteralBlockStart();

            if (LastWord == "do") {
                SetMode(BeautifierMode.DoBlock);
            } else {
                SetMode(BeautifierMode.Block);
            }

            if (Opts.BraceStyle == BraceStyle.Expand) {
                if (LastType != "TK_OPERATOR") {
                    if (LastType == "TK_EQUALS" ||
                        (IsSpecialWord(LastText) && LastText != "else")) {
                        Append(" ");
                    } else {
                        AppendNewline();
                    }
                }
                Append(tokenText);
                Indent();
            } else {
                if (LastType != "TK_OPERATOR" && LastType != "TK_START_EXPR") {
                    if (LastType == "TK_START_BLOCK") {
                        AppendNewline();
                    } else {
                        Append(" ");
                    }
                } else {
                    // if TK_OPERATOR or TK_START_EXPR
                    if (IsArray(Flags.PreviousMode) && LastText == ",") {
                        if (LastLastText == "}") {
                            Append(" ");
                        } else {
                            AppendNewline();
                        }
                    }
                }
                Indent();
                Append(tokenText);
            }
        }

        private void HandleEndBlock(string tokenText) {
            TrackStringLiteralBlockEnd();

            RestoreMode();
            if (Opts.BraceStyle == BraceStyle.Expand) {
                if (LastText != "{") {
                    AppendNewline();
                }
            } else {
                if (LastType == "TK_START_BLOCK") {
                    if (JustAddedNewline) {
                        RemoveIndent();
                    } else {
                        TrimOutput();
                    }
                } else {
                    if (IsArray(Flags.Mode) && Opts.KeepArrayIndentation) {
                        Opts.KeepArrayIndentation = false;
                        AppendNewline();
                        Opts.KeepArrayIndentation = true;
                    } else {
                        AppendNewline();
                    }
                }
            }
            Append(tokenText);
        }

        private void HandleWord(string tokenText) {
            if (DoBlockJustClosed) {
                Append(" ");
                Append(tokenText);
                Append(" ");
                DoBlockJustClosed = false;
                return;
            }
            if (tokenText == "function") {
                if (Flags.VarLine && LastText != "=") {
                    Flags.VarLineReindented = !Opts.KeepFunctionIndentation;
                }

                if ((JustAddedNewline || LastText == ";") && LastText != "{") {
                    // make sure there is a nice clean space of at least one blank line
                    // before a new function definition
                    var haveNewlines = NNewlines;
                    if (!JustAddedNewline) {
                        haveNewlines = 0;
                    }

                    if (!Opts.PreserveNewlines) {
                        haveNewlines = 1;
                    }

                    for (var i = 0; i < (2 - haveNewlines); ++i) {
                        AppendNewline(false);
                    }
                }

                if ((LastText == "get" || LastText == "set" || LastText == "new") || LastType == "TK_WORD") {
                    Append(" ");
                }

                if (LastType == "TK_WORD") {
                    if (LastText == "get" || LastText == "set" || LastText == "new" || LastText == "return") {
                        Append(" ");
                    } else {
                        AppendNewline();
                    }
                } else if (LastType == "TK_OPERATOR" || LastText == "=") {
                    // foo = function
                    Append(" ");
                } else if (IsExpression(Flags.Mode)) {
                    // (function
                } else {
                    AppendNewline();
                }

                Append("function");
                LastWord = "function";
                return;
            }

            if (tokenText == "case" || (tokenText == "default" && Flags.InCaseStatement)) {
                AppendNewline();
                if (Flags.CaseBody) {
                    RemoveIndent();
                    Flags.CaseBody = false;
                    Flags.IndentationLevel -= 1;
                }
                Append(tokenText);
                Flags.InCase = true;
                Flags.InCaseStatement = true;
                return;
            }

            var prefix = "NONE";

            if (LastType == "TK_END_BLOCK") {
                if (tokenText != "else" && tokenText != "catch" && tokenText != "finally") {
                    prefix = "NEWLINE";
                } else {
                    if (Opts.BraceStyle == BraceStyle.Expand || Opts.BraceStyle == BraceStyle.EndExpand) {
                        prefix = "NEWLINE";
                    } else {
                        prefix = "SPACE";
                        Append(" ");
                    }
                }
            } else if (LastType == "TK_SEMICOLON" && (Flags.Mode == BeautifierMode.Block || Flags.Mode == BeautifierMode.DoBlock)) {
                prefix = "NEWLINE";
            } else if (LastType == "TK_SEMICOLON" && IsExpression(Flags.Mode)) {
                prefix = "SPACE";
            } else if (LastType == "TK_STRING") {
                prefix = "NEWLINE";
            } else if (LastType == "TK_WORD") {
                if (LastText == "else") {
                    // eat newlines between ...else *** some_op...
                    // won't preserve extra newlines in this place (if any), but don't care that much
                    TrimOutput(true);
                }
                prefix = "SPACE";
            } else if (LastType == "TK_START_BLOCK") {
                prefix = "NEWLINE";
            } else if (LastType == "TK_END_EXPR") {
                Append(" ");
                prefix = "NEWLINE";
            }

            if (Flags.IfLine && LastType == "TK_END_EXPR") {
                Flags.IfLine = false;
            }

            if (LastType == "TK_COMMA" ||
                LastType == "TK_START_EXPR" ||
                LastType == "TK_EQUALS" ||
                LastType == "TK_OPERATOR") {
                if (Flags.Mode != BeautifierMode.Object) {
                    AllowWrapOrPreservedNewline(tokenText);
                }
            }

            if (LineStarters.Contains(tokenText)) {
                if (LastText == "else") {
                    prefix = "SPACE";
                } else {
                    prefix = "NEWLINE";
                }
            }

            if (tokenText == "else" || tokenText == "catch" || tokenText == "finally") {
                if (LastType != "TK_END_BLOCK" || Opts.BraceStyle == BraceStyle.Expand ||
                    Opts.BraceStyle == BraceStyle.EndExpand) {
                    AppendNewline();
                } else {
                    TrimOutput(true);
                    Append(" ");
                }
            } else if (prefix == "NEWLINE") {
                if (IsSpecialWord(LastText)) {
                    // no newline between return nnn
                    Append(" ");
                } else if (LastType != "TK_END_EXPR") {
                    if ((LastType != "TK_START_EXPR" || tokenText != "var") && LastText != ":") {
                        // no need to force newline on VAR -
                        // for (var x = 0...
                        if (tokenText == "if" && LastWord == "else" && LastText != "{") {
                            Append(" ");
                        } else {
                            Flags.VarLine = false;
                            Flags.VarLineReindented = false;
                            AppendNewline();
                        }
                    }
                } else if (LineStarters.Contains(tokenText) && LastText != ")") {
                    Flags.VarLine = false;
                    Flags.VarLineReindented = false;
                    AppendNewline();
                }
            } else if (IsArray(Flags.Mode) && LastText == "," && LastLastText == "}") {
                AppendNewline(); //}, in lists get a newline
            } else if (prefix == "SPACE") {
                Append(" ");
            }

            Append(tokenText);
            LastWord = tokenText;

            if (tokenText == "var") {
                Flags.VarLine = true;
                Flags.VarLineReindented = false;
                Flags.VarLineTainted = false;
            }

            if (tokenText == "if") {
                Flags.IfLine = true;
            }

            if (tokenText == "else") {
                Flags.IfLine = false;
            }
        }

        private void HandleSemicolon(string tokenText) {
            Append(tokenText);
            Flags.VarLine = false;
            Flags.VarLineReindented = false;
            if (Flags.Mode == BeautifierMode.Object) {
                // OBJECT mode is weird and doesn't get reset too well.
                Flags.Mode = BeautifierMode.Block;
            }
        }

        private void HandleString(string tokenText) {
            if (LastType == "TK_END_EXPR" &&
                (Flags.PreviousMode == BeautifierMode.ConditionalExpression || Flags.PreviousMode == BeautifierMode.ForExpression)) {
                Append(" ");
            } else if (LastType == "TK_WORD") {
                Append(" ");
            } else if (LastType == "TK_COMMA" ||
                       LastType == "TK_START_EXPR" ||
                       LastType == "TK_EQUALS" ||
                       LastType == "TK_OPERATOR") {
                if (Flags.Mode != BeautifierMode.Object) {
                    AllowWrapOrPreservedNewline(tokenText, LastType == "TK_COMMA" && ShouldSplitStringLiteral(tokenText));
                }
            } else {
                AppendNewline();
            }

            AppendStringToken(tokenText);
        }

        private void HandleEquals(string tokenText) {
            if (Flags.VarLine) {
                // just got an '=' in a var-line, different line breaking rules will apply
                Flags.VarLineTainted = true;
            }

            Append(" ");
            Append(tokenText);
            Append(" ");
        }

        private void HandleComma(string tokenText) {
            if (LastType == "TK_COMMENT") {
                AppendNewline();
            }

            if (Flags.VarLine) {
                if (IsExpression(Flags.Mode) || LastType == "TK_END_BLOCK") {
                    // do not break on comma, for ( var a = 1, b = 2
                    Flags.VarLineTainted = false;
                }
                if (Flags.VarLineTainted) {
                    Append(tokenText);
                    Flags.VarLineReindented = true;
                    Flags.VarLineTainted = false;
                    AppendNewline();
                    return;
                } else
                    Flags.VarLineTainted = false;
                Append(tokenText);
                Append(" ");
                return;
            }

            if (LastType == "TK_END_BLOCK" && Flags.Mode != BeautifierMode.ParentheticalExpression) {
                Append(tokenText);
                if (Flags.Mode == BeautifierMode.Object && LastText == "}") {
                    AppendNewline();
                } else {
                    Append(" ");
                }
            } else if (ShouldBreakStatementComma()) {
                Append(tokenText);
                AppendNewline();
            } else {
                if (Flags.Mode == BeautifierMode.Object) {
                    Append(tokenText);
                    AppendNewline();
                } else {
                    // EXPR or DO_BLOCK
                    Append(tokenText);
                    Append(" ");
                }
            }
        }

        private void HandleOperator(string tokenText) {
            var spaceBefore = true;
            var spaceAfter = true;

            if (IsSpecialWord(LastText)) {
                // return had a special handling in TK_WORD
                Append(" ");
                Append(tokenText);
                return;
            }

            // hack for actionscript's import .*;
            if (tokenText == "*" && LastType == "TK_DOT" && !LastLastText.All(char.IsDigit)) {
                Append(tokenText);
                return;
            }

            if (tokenText == ":" && Flags.InCase) {
                Flags.CaseBody = true;
                Indent();
                Append(tokenText);
                AppendNewline();
                Flags.InCase = true;
                return;
            }

            if (tokenText == "::") {
                // no spaces around the exotic namespacing syntax operator
                Append(tokenText);
                return;
            }

            if ((tokenText == "++" || tokenText == "--" || tokenText == "!") || (tokenText == "+" || tokenText == "-") &&
                ((LastType == "TK_START_BLOCK" || LastType == "TK_START_EXPR" || LastType == "TK_EQUALS" || LastType == "TK_OPERATOR") ||
                (LineStarters.Contains(LastText) || LastText == ","))) {
                spaceBefore = false;
                spaceAfter = false;

                if (LastText == ";" && IsExpression(Flags.Mode)) {
                    // for (;; ++i)
                    // ^^
                    spaceBefore = true;
                }

                if (LastText == "TK_WORD" && LineStarters.Contains(LastText)) {
                    spaceBefore = true;
                }

                if (Flags.Mode == BeautifierMode.Block && (LastText == ";" || LastText == "{")) {
                    // { foo: --i }
                    // foo(): --bar
                    AppendNewline();
                }
            } else if (tokenText == ":") {
                if (Flags.TernaryDepth == 0) {
                    if (Flags.Mode == BeautifierMode.Block) {
                        Flags.Mode = BeautifierMode.Object;
                    }
                    spaceBefore = false;
                } else {
                    Flags.TernaryDepth -= 1;
                }
            } else if (tokenText == "?") {
                Flags.TernaryDepth += 1;
            }

            if (spaceBefore) {
                Append(" ");
            }

            Append(tokenText);

            if (spaceAfter) {
                Append(" ");
            }
        }

        private void HandleBlockComment(string tokenText) {
            var lines = tokenText.Replace("\r", "").Split('\n');
            // all lines start with an asterisk? that's a proper box comment

            if (lines.Skip(1).Where(x => x.Trim() == "" || x.TrimStart()[0] != '*').All(string.IsNullOrEmpty)) {
                AppendNewline();
                Append(lines[0]);
                foreach (var line in lines.Skip(1)) {
                    AppendNewline();
                    Append(" " + line.Trim());
                }
            } else {
                // simple block comment: leave intact
                if (lines.Length > 1) {
                    // multiline comment starts on a new line
                    AppendNewline();
                } else {
                    // single line /* ... */ comment stays on the same line
                    Append(" ");
                }
                foreach (var line in lines) {
                    Append(line);
                    Append("\n");
                }
            }
            AppendNewline();
        }

        private void HandleInlineComment(string tokenText) {
            Append(" ");
            Append(tokenText);
            Append(" ");
        }

        private void HandleComment(string tokenText) {
            if (LastText == "," && !WantedNewline) {
                TrimOutput(true);
            }

            if (LastType != "TK_COMMENT") {
                if (WantedNewline) {
                    AppendNewline();
                } else {
                    Append(" ");
                }
            }

            Append(tokenText);
            AppendNewline();
        }

        private void HandleDot(string tokenText) {
            if (IsSpecialWord(LastText)) {
                Append(" ");
            } else {
                AllowWrapOrPreservedNewline(tokenText, LastText == ")" && Opts.BreakChainedMethods);
            }

            Append(tokenText);
        }

        private void HandleUnknown(string tokenText) {
            Append(tokenText);
        }
    }
}
