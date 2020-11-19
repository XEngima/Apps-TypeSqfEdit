using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using TypeSqf.Edit.Replace;

namespace TypeSqf.Edit.Highlighting
{
    class BracketsHighlighter
    {
        private char[] _bracketsEnding;
        private char[] _bracketsStarting;
        private TextEditor _textEditor;
        SearchResultsBackgroundRenderer _backgroundMarker;

        public BracketsHighlighter(TextEditor textEditor)
        {
            _bracketsStarting = new char[] { '{', '(', '[' };
            _bracketsEnding   = new char[] { '}', ')', ']' };
            _textEditor       = textEditor;
            _textEditor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
            _textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

            // Create and register the marker pen that's used to mark text selection
            _backgroundMarker = new SearchResultsBackgroundRenderer();
            Brush markerBrush = new SolidColorBrush(Colors.LightBlue);
            markerBrush.Opacity = 0.3;
            _backgroundMarker.MarkerBrush = markerBrush;
            _textEditor.TextArea.TextView.BackgroundRenderers.Add(_backgroundMarker);
        }

        // Event rased every time caret is changed in document.
        private void Caret_PositionChanged(object sender, EventArgs eventArgs)
        {
            Caret caret = sender as Caret;
            
            _backgroundMarker.CurrentResults.Clear();
            
            int caretOffset = _textEditor.Document.GetOffset(caret.Location);
            char textBefore = '\0';
            char textAfter  = '\0';
            try
            {
                textAfter = _textEditor.Text[caretOffset];
                textBefore = _textEditor.Text[caretOffset-1];
            } catch
            {

            }
            
            if (_bracketsEnding.Contains(textBefore))
            {
                char searchFor = _bracketsStarting[Array.IndexOf(_bracketsEnding, textBefore)];
                CommentInfo comment = IsInComment(_textEditor.Text, caretOffset - 1);

                if (!comment.InComment)
                {
                    BracketsMatch bracketsMatch = FindMatchingBrackets(caretOffset - 1, searchFor, textBefore);

                    if (bracketsMatch != null)
                    {
                        SearchResult result = new SearchResult(bracketsMatch.OpeningOffset, 1);
                        _backgroundMarker.CurrentResults.Add(result);
                        SearchResult result1 = new SearchResult(bracketsMatch.ClosingOffset, 1);
                        _backgroundMarker.CurrentResults.Add(result1);
                    }
                }

                
            }
            else if (_bracketsStarting.Contains(textAfter))
            {
                char searchFor = _bracketsEnding[Array.IndexOf(_bracketsStarting, textAfter)];
                CommentInfo comment = IsInComment(_textEditor.Text, caretOffset);

                if (!comment.InComment)
                {
                    BracketsMatch bracketsMatch = FindMatchingBrackets(caretOffset + 1, textAfter, searchFor);

                    if (bracketsMatch != null)
                    {
                        SearchResult result = new SearchResult(bracketsMatch.OpeningOffset, 1);
                        _backgroundMarker.CurrentResults.Add(result);
                        SearchResult result1 = new SearchResult(bracketsMatch.ClosingOffset, 1);
                        _backgroundMarker.CurrentResults.Add(result1);
                    }
                }
            }

            _textEditor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
        }

        private CommentInfo IsInComment(string text, int start)
        {
            // Line Comment
            string line = "";
            try
            {
                int lineOffset = text.LastIndexOf("\n", start) -1;
                int lineEnd = text.IndexOf("\n", start);
                line = text.Substring(lineOffset, lineEnd - lineOffset);
            
                if (line.LastIndexOf("//", start - lineOffset) > 0)
                {
                    return new CommentInfo(true, lineOffset + line.LastIndexOf("//", start - lineOffset), lineOffset + line.Length);
                }
            }
            catch
            {
                
            }

            // Block Comment
            int blockCommentStart = 0;
            int blockCommentEnd = 0;
            blockCommentStart = text.LastIndexOf("/*", start);
            blockCommentEnd = text.LastIndexOf("*/", start);

            if (blockCommentEnd < blockCommentStart)
            {
                blockCommentEnd = text.IndexOf("*/", start);
                return new CommentInfo(true, blockCommentStart, blockCommentEnd);
            }

            return new CommentInfo(false, 0, text.Length);
        }

        /// <summary>
        ///  Find current matching brackets. 
        ///  Return null if offset is in comments, strings, pre processor commands
        /// </summary>
        /// <param name="offset">An offset inside brackets to be found.</param>
        /// <param name="openingBracket"></param>
        /// <param name="closingBracket"></param>
        /// <returns></returns>
        private BracketsMatch FindMatchingBrackets(int offset, char openingBracket, char closingBracket)
        {
            Stack<int> brackets = new Stack<int>();
            int firstBracketOffset = 0;
            int bracketBalance = 0;
            var i = 0;
            char currentChar, prevChar, nextChar = '\0';
            while (i < _textEditor.Text.Length)
            {
                currentChar = _textEditor.Text[i];
                int skipTo = -1;

                switch (currentChar)
                {
                    case '#':
                        skipTo = _textEditor.Text.IndexOf('\n', i);
                        break;
                    case '/':
                        if (i < (_textEditor.Text.Length - 1))
                        {
                            nextChar = _textEditor.Text[i+1];
                            if (nextChar == '/')
                            {
                                skipTo = _textEditor.Text.IndexOf('\n', i);
                            }
                            if (nextChar == '*')
                            {
                                skipTo = _textEditor.Text.IndexOf("*/", i);
                            }
                        }
                        break;
                    case '"':
                        while ((skipTo = _textEditor.Text.IndexOf('"', i+1)) > -1 )
                        {
                            prevChar = _textEditor.Text[i - 1];
                            if (prevChar != '\\')
                            {
                                break;
                            }
                        }
                        break;
                    case '\'':
                        while ((skipTo = _textEditor.Text.IndexOf('\'', i+1)) > -1)
                        {
                            prevChar = _textEditor.Text[i - 1];
                            if (prevChar != '\\')
                            {
                                break;
                            }
                        }
                        break;
                }

                // Return null if text cursor was in something that was skipped.
                if (offset > i && offset <= skipTo)
                {
                    return null;
                }
                if (skipTo > i)
                {
                    i = skipTo + 1;
                    continue;
                }

                if (currentChar == openingBracket)
                {
                    brackets.Push(i);
                    if (i >= offset)
                    {
                        bracketBalance++;
                    }
                }
                else if (currentChar == closingBracket)
                {
                    if (brackets.Count > 0)
                    {
                        firstBracketOffset = brackets.Pop();
                        if (i >= offset)
                        {
                            if (bracketBalance == 0)
                            {
                                return new BracketsMatch(openingBracket, firstBracketOffset, closingBracket, i);
                            }
                            else
                            {
                                bracketBalance--;
                            }
                        }
                    }
                }

                i++;
            }

            return null; 
        }
        public class CommentInfo
        {
            public Boolean InComment;
            public int StartOffset;
            public int EndOffset;
            public CommentInfo(Boolean inComment, int start=0, int end=0)
            {
                InComment   = inComment;
                StartOffset = start;
                EndOffset   = end;
            }
        }

        public class BracketsMatch
        {
            public char OpeningBracket { get; private set; }
            public char ClosingBracket { get; private set; }
            public int OpeningOffset { get; private set; }
            public int ClosingOffset { get; private set; }

            public BracketsMatch(char openingBracket, int openingOffset, char closingBracket, int closingOffset)
            {
                OpeningBracket = openingBracket;
                OpeningOffset  = openingOffset;
                ClosingBracket = closingBracket;
                ClosingOffset  = closingOffset;
            }
        }
    }
}
