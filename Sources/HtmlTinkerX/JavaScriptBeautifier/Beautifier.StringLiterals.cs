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
    using System.Collections.Generic;
    using System.Text;

    public partial class Beautifier {
        private const int DefaultMaxStringLiteralLength = 2400;

        private int ImportAttributeBlockDepth { get; set; }

        private int ModuleSpecifierBlockDepth { get; set; }

        private bool ShouldBreakStatementComma()
            => (Flags.Mode == BeautifierMode.Block || Flags.Mode == BeautifierMode.DoBlock) &&
               (LastType == "TK_STRING" || LastType == "TK_WORD");

        private bool ShouldSplitStringLiteral(string tokenText)
            => Opts.SplitLongStringLiterals &&
               IsStringLiteralSplitContext() &&
               TryGetQuotedStringContent(tokenText, out _, out string content) &&
               content.Length > GetStringLiteralChunkLength();

        private void AppendStringToken(string tokenText) {
            if (!TrySplitStringLiteral(tokenText, out List<string>? chunks)) {
                Append(tokenText);
                return;
            }

            List<string> literalChunks = chunks!;
            Append("(");
            Append(literalChunks[0]);
            for (int i = 1; i < literalChunks.Count; i++) {
                Append(" ");
                Append("+");
                AppendSplitStringNewline();
                Append(literalChunks[i]);
            }

            Append(")");
        }

        private bool TrySplitStringLiteral(string tokenText, out List<string>? chunks) {
            chunks = null;

            if (!Opts.SplitLongStringLiterals ||
                !IsStringLiteralSplitContext() ||
                !TryGetQuotedStringContent(tokenText, out char quote, out string content)) {
                return false;
            }

            int chunkLength = GetStringLiteralChunkLength();
            if (content.Length <= chunkLength) {
                return false;
            }

            chunks = new List<string>();
            var current = new StringBuilder();
            foreach (string unit in EnumerateStringLiteralUnits(content)) {
                if (current.Length > 0 && current.Length + unit.Length > chunkLength) {
                    chunks.Add(QuoteStringChunk(quote, current.ToString()));
                    current.Clear();
                }

                current.Append(unit);
            }

            if (current.Length > 0) {
                chunks.Add(QuoteStringChunk(quote, current.ToString()));
            }

            return chunks.Count > 1;
        }

        private int GetStringLiteralChunkLength() {
            if (Opts.MaxStringLiteralLength > 0) {
                return Opts.MaxStringLiteralLength;
            }

            if (Opts.WrapLineLength > 0) {
                return Opts.WrapLineLength;
            }

            return DefaultMaxStringLiteralLength;
        }

        private bool IsStringLiteralSplitContext() {
            if (ImportAttributeBlockDepth > 0 || ModuleSpecifierBlockDepth > 0) {
                return false;
            }

            if (Flags.Mode == BeautifierMode.Object) {
                return LastText == ":";
            }

            if (LastType == "TK_EQUALS" ||
                LastType == "TK_COMMA" ||
                LastType == "TK_OPERATOR") {
                return true;
            }

            if (LastType == "TK_START_EXPR" && IsExpression(Flags.Mode)) {
                return true;
            }

            return LastType == "TK_WORD" && (LastText == "return" || LastText == "throw");
        }

        private void TrackStringLiteralBlockStart() {
            if (ImportAttributeBlockDepth > 0) {
                ImportAttributeBlockDepth++;
            } else if (LastWord == "with" || LastWord == "assert") {
                ImportAttributeBlockDepth = 1;
            }

            if (ModuleSpecifierBlockDepth > 0) {
                ModuleSpecifierBlockDepth++;
            } else if (LastWord == "import" || LastWord == "export") {
                ModuleSpecifierBlockDepth = 1;
            }
        }

        private void TrackStringLiteralBlockEnd() {
            if (ImportAttributeBlockDepth > 0) {
                ImportAttributeBlockDepth--;
            }

            if (ModuleSpecifierBlockDepth > 0) {
                ModuleSpecifierBlockDepth--;
            }
        }

        private void AppendSplitStringNewline() {
            TrimOutput();

            if (Output.Count == 0) {
                return;
            }

            if (Output[Output.Count - 1] != "\n") {
                JustAddedNewline = true;
                Output.Add("\n");
            }

            if (!string.IsNullOrEmpty(PreindentString)) {
                Output.Add(PreindentString);
            }

            for (int i = 0; i < Flags.IndentationLevel + Flags.ChainExtraIndentation; i++) {
                AppendIndentString();
            }

            AppendIndentString();
        }

        private static bool TryGetQuotedStringContent(string tokenText, out char quote, out string content) {
            quote = '\0';
            content = string.Empty;

            if (tokenText.Length < 2) {
                return false;
            }

            quote = tokenText[0];
            if ((quote != '\'' && quote != '"') || tokenText[tokenText.Length - 1] != quote) {
                quote = '\0';
                return false;
            }

            content = tokenText.Substring(1, tokenText.Length - 2);
            return true;
        }

        private static IEnumerable<string> EnumerateStringLiteralUnits(string content) {
            for (int i = 0; i < content.Length; i++) {
                if (content[i] != '\\' || i + 1 >= content.Length) {
                    yield return content[i].ToString();
                    continue;
                }

                int escapeEnd = GetEscapeEnd(content, i + 1);
                yield return content.Substring(i, escapeEnd - i + 1);
                i = escapeEnd;
            }
        }

        private static int GetEscapeEnd(string content, int escapedIndex) {
            char escaped = content[escapedIndex];

            if (escaped == 'x' && HasHexDigits(content, escapedIndex + 1, 2)) {
                return escapedIndex + 2;
            }

            if (escaped == 'u') {
                if (escapedIndex + 1 < content.Length && content[escapedIndex + 1] == '{') {
                    int closingBrace = content.IndexOf('}', escapedIndex + 2);
                    if (closingBrace > escapedIndex + 2) {
                        return closingBrace;
                    }
                }

                if (HasHexDigits(content, escapedIndex + 1, 4)) {
                    return escapedIndex + 4;
                }
            }

            if (escaped == '\r' && escapedIndex + 1 < content.Length && content[escapedIndex + 1] == '\n') {
                return escapedIndex + 1;
            }

            if (escaped >= '0' && escaped <= '7') {
                int end = escapedIndex;
                while (end + 1 < content.Length &&
                       end - escapedIndex < 2 &&
                       content[end + 1] >= '0' &&
                       content[end + 1] <= '7') {
                    end++;
                }

                return end;
            }

            return escapedIndex;
        }

        private static bool HasHexDigits(string content, int start, int count) {
            if (start + count > content.Length) {
                return false;
            }

            for (int i = start; i < start + count; i++) {
                char c = content[i];
                bool isHex =
                    c >= '0' && c <= '9' ||
                    c >= 'a' && c <= 'f' ||
                    c >= 'A' && c <= 'F';
                if (!isHex) {
                    return false;
                }
            }

            return true;
        }

        private static string QuoteStringChunk(char quote, string content)
            => string.Concat(quote, content, quote);
    }
}
