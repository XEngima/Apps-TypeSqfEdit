using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace TypeSqf.Edit.Highlighting
{
    class BracketsHighlighter
    {
        private char[] _bracketsEnding;
        private char[] _bracketsStarting;
        private TextEditor _textEditor;
        BracketsMatch _bracketsMatch = null;
        BracketsColorizingTransformer _bracketsColorizingTransformer;

        public BracketsHighlighter(TextEditor textEditor)
        {
            _bracketsStarting = new char[] { '{', '(', '[' };
            _bracketsEnding   = new char[] { '}', ')', ']' };
            _textEditor       = textEditor;
            _bracketsMatch    = null;
            _textEditor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
            _textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

            _bracketsColorizingTransformer = new BracketsColorizingTransformer();
            _textEditor.TextArea.TextView.LineTransformers.Add(_bracketsColorizingTransformer);
        }

        // Event rased every time caret is changed in document.
        private void Caret_PositionChanged(object sender, EventArgs eventArgs)
        {
            Caret caret = sender as Caret;
            if (_bracketsMatch != null)
            {
                _bracketsColorizingTransformer.Locations.Clear();
                _textEditor.TextArea.TextView.Redraw();
                _bracketsMatch = null;
            }
            
            int caretOffset = _textEditor.Document.GetOffset(caret.Location);
            char textBefore = '\0';
            char textAfter  = '\0';
            try
            {
                textAfter = _textEditor.Text[caretOffset];
                textBefore = _textEditor.Text[caretOffset-1];
            } catch
            {
                return;
            }
            
            if (_bracketsEnding.Contains(textBefore))
            {
                char searchFor = _bracketsStarting[Array.IndexOf(_bracketsEnding, textBefore)];
                _bracketsMatch = FindMatchingBrackets(caretOffset - 1, searchFor, textBefore);
            }
            else if (_bracketsStarting.Contains(textAfter))
            {
                char searchFor = _bracketsEnding[Array.IndexOf(_bracketsStarting, textAfter)];
                _bracketsMatch = FindMatchingBrackets(caretOffset + 1, textAfter, searchFor);
            }


            if (_bracketsMatch != null)
            {
                TextLocation openingLine = _textEditor.Document.GetLocation(_bracketsMatch.OpeningOffset);
                TextLocation closingLine = _textEditor.Document.GetLocation(_bracketsMatch.ClosingOffset);

                _bracketsColorizingTransformer.Locations.Add(openingLine);
                _bracketsColorizingTransformer.Locations.Add(closingLine);

                _textEditor.TextArea.TextView.Redraw(_bracketsMatch.OpeningOffset, _bracketsMatch.ClosingOffset - _bracketsMatch.OpeningOffset);

            }
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
                            else if (nextChar == '*')
                            {
                                skipTo = _textEditor.Text.IndexOf("*/", i);
                                if (skipTo == -1)
                                {
                                    skipTo = _textEditor.Text.Length;
                                }
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

    public class BracketsColorizingTransformer : DocumentColorizingTransformer
    {
        public List<TextLocation> Locations = new List<TextLocation>();

        protected override void ColorizeLine(DocumentLine line)
        {
            List<TextLocation> rowLocations = Locations.FindAll(x => x.Line == line.LineNumber);
            
            if (rowLocations.Count > 0)
            {
                foreach (TextLocation textLocation in rowLocations)
                {
                    int lineStartOffset = line.Offset;

                    base.ChangeLinePart(
                        lineStartOffset + textLocation.Column - 1,
                        lineStartOffset + textLocation.Column,
                        (VisualLineElement element) =>
                        {
                            Typeface tf = element.TextRunProperties.Typeface;

                            element.TextRunProperties.SetTypeface(new Typeface(
                                tf.FontFamily,
                                System.Windows.FontStyles.Normal,
                                System.Windows.FontWeights.Bold,
                                tf.Stretch
                            ));
                            element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(Colors.DarkRed));
                            Brush markerBrush = new SolidColorBrush(Colors.WhiteSmoke);
                            //markerBrush.Opacity = 0.8;
                            element.TextRunProperties.SetBackgroundBrush(markerBrush);
                        });
                }
            }
        }
    }
}
