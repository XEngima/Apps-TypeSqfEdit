using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
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
                SearchResult result = new SearchResult(caretOffset - 1, 1);
                _backgroundMarker.CurrentResults.Add(result);

                char searchFor = _bracketsStarting[Array.IndexOf(_bracketsEnding, textBefore)];
                CommentInfo comment = IsInComment(_textEditor.Text, caretOffset - 1);
                
                // loop document to begining
                for (int i = caretOffset-2; i >= comment.StartOffset; i--)
                {
                    if (MatchBrackets(i, searchFor, textBefore, comment.InComment))
                    {
                        SearchResult result1 = new SearchResult(i, 1);
                        _backgroundMarker.CurrentResults.Add(result1);
                        break;
                    }
                }
            }
            else if (_bracketsStarting.Contains(textAfter))
            {
                SearchResult result = new SearchResult(caretOffset, 1);
                _backgroundMarker.CurrentResults.Add(result);

                char searchFor = _bracketsEnding[Array.IndexOf(_bracketsStarting, textAfter)];
                CommentInfo comment = IsInComment(_textEditor.Text, caretOffset);

                // loop document to begining
                for (int i = caretOffset+1; i <= comment.EndOffset; i++)
                {
                    if (MatchBrackets(i, searchFor, textAfter, comment.InComment))
                    {
                        SearchResult result1 = new SearchResult(i, 1);
                        _backgroundMarker.CurrentResults.Add(result1);
                        break;
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

        private int bracketBalance = 0;
        private bool MatchBrackets(int offset, char searchBracket, char gotBracket, bool inComment=false)
        {

            char charAt = '\0';
            try
            {
                charAt = _textEditor.Text[offset];
            }
            catch
            {

            }

            if (charAt == gotBracket)
            {
                CommentInfo comment = IsInComment(_textEditor.Text, offset);

                if (inComment == comment.InComment)
                {
                    bracketBalance++;
                }
            }
            else if (charAt == searchBracket)
            {
                CommentInfo comment = IsInComment(_textEditor.Text, offset);

                if (inComment == comment.InComment)
                {
                    if (bracketBalance == 0)
                    {
                        return true;
                    }
                    else
                    {
                        bracketBalance--;
                    }
                }
            }
            return false;
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

    }
}
