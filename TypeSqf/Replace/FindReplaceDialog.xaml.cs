/*
 * FindReplaceDialog.xaml.cs file is licensed under "The Code Project Open License" (CPOL)
 * 
 * Authors: Bruce Greene, Thomas Willwacher
 * 
 */

using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace TypeSqf.Edit.Replace
{
    /// <summary>
    /// Interaction logic for FindReplaceDialog.xaml
    /// </summary>
    public partial class FindReplaceDialog : Window
    {

        private TextEditor editor;
        TextDocument currentDocument;
        private int documentLength;
        private static string textToFind = "";
        private static bool caseSensitive = false;
        private static bool wholeWord = false;
        private static bool useRegex = false;
        private static bool useWildcards = false;
        private static bool searchUp = false;
        SearchResultsBackgroundRenderer searchResultsBackgroundRenderer;
        SearchResult currentResult = null;
        SearchResultsBackgroundRenderer selectionSearchBackgroundRenderer;

        public static readonly DependencyProperty SelectionOnlyProperty =
            DependencyProperty.Register("SelectionOnly", typeof(bool), typeof(FindReplaceDialog));
        public static readonly DependencyProperty SelectionCheckboxProperty =
            DependencyProperty.Register("SelectionCheckbox", typeof(bool), typeof(FindReplaceDialog));

        public bool SelectionOnly
        {
            get { return (bool)GetValue(SelectionOnlyProperty); }
            set
            {
                SetValue(SelectionOnlyProperty, value);
                if (value)
                {
                    SelectionCheckbox = true;
                }
            }
        }
        private bool SelectionCheckbox
        {
            get { return (bool)GetValue(SelectionCheckboxProperty); }
            set { SetValue(SelectionCheckboxProperty, value); }
        }

        private TabItem ActiveTab
        {
            get { return tabMain.SelectedItem as TabItem; }
        }
        private string SearchText
        {
            get
            {
                var stackPanel = ActiveTab.Content as StackPanel;
                if (stackPanel != null)
                {
                    foreach( object child in LogicalTreeHelper.GetChildren(stackPanel))
                    {
                        var textBox = child as TextBox;
                        if(textBox != null)
                        {
                            if(textBox.Name == "txtFind" || textBox.Name == "txtFind2")
                            {
                                return textBox.Text;
                            }
                        }
                    }
                }
                return null;
            }
        }

        private int _selectionStart;
        private int SelectionStart
        {
            get
            {
                return _selectionStart;
            }
            set
            {
                _selectionStart = value;
            }
        }

        private int _selectionEnd;
        private int SelectionEnd
        {
            get
            {
                return _selectionEnd;
            }
            set
            {
                _selectionEnd = value;
            }
        }

        public FindReplaceDialog(TextEditor editor)
        {
            InitializeComponent();

            this.editor = editor;

            txtFind.Text = txtFind2.Text = textToFind;
            cbCaseSensitive.IsChecked = caseSensitive;
            cbWholeWord.IsChecked = wholeWord;
            cbRegex.IsChecked = useRegex;
            cbWildcards.IsChecked = useWildcards;
            cbSearchUp.IsChecked = searchUp;

            // Create and register the marker pen used later to mark searched words
            searchResultsBackgroundRenderer = new SearchResultsBackgroundRenderer();
            editor.TextArea.TextView.BackgroundRenderers.Add(searchResultsBackgroundRenderer);
            
            // Create and register the marker pen that's used to mark text selection
            selectionSearchBackgroundRenderer = new SearchResultsBackgroundRenderer();
            Brush markerBrush = new SolidColorBrush(Colors.LightGray);
            markerBrush.Opacity = 0.1;
            selectionSearchBackgroundRenderer.MarkerBrush = markerBrush;
            editor.TextArea.TextView.BackgroundRenderers.Add(selectionSearchBackgroundRenderer);

            KeyDown += FindReplaceDialog_KeyDown;
            txtFind.TextChanged += TxtFind_TextChanged;
            txtFind2.TextChanged += TxtFind_TextChanged;
            currentDocument = editor.TextArea.Document;
            if (currentDocument != null)
            {
                currentDocument.TextChanged += textArea_Document_TextChanged;
                documentLength = currentDocument.TextLength;
            }
            editor.TextArea.DocumentChanged += textArea_DocumentChanged;

            editor.TextArea.SelectionChanged += TextArea_SelectionChanged;
            

        }

        private void TextArea_SelectionChanged(object sender, EventArgs e)
        {
            if (editor.SelectionLength > 0 && !SelectionCheckbox)
            {
                SelectionCheckbox = true;
            }
            else if (!SelectionOnly && SelectionCheckbox && editor.SelectionLength <= 0)
            {
                SelectionCheckbox = false;
            }
        }

        private void TxtFind_TextChanged(object sender, TextChangedEventArgs e)
        {
            MarkAllWords(SearchText);
        }

        private void Options_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                MarkAllWords(SearchText);
            }
            catch
            {
                return;
            }
        }

        private void Option_Selection_Changed(object sender, RoutedEventArgs e)
        {
            ClearMarkerSelection();

            if (SelectionOnly)
            {
                MarkSelection();
            }
            TxtFind_TextChanged(null, null);
        }

        void textArea_DocumentChanged(object sender, EventArgs e)
        {
            if (currentDocument != null)
                currentDocument.TextChanged -= textArea_Document_TextChanged;
            currentDocument = editor.TextArea.Document;
            if (currentDocument != null)
            {
                currentDocument.TextChanged += textArea_Document_TextChanged;
                documentLength = currentDocument.TextLength;
                MarkAllWords(SearchText);
            }
        }

        void textArea_Document_TextChanged(object sender, EventArgs e)
        {
            MarkAllWords(SearchText);
            TextDocument document = sender as TextDocument;
            if (document != null && SelectionOnly)
            {
                int newLength = document.TextLength;
                int step = newLength - documentLength;
                documentLength = currentDocument.TextLength;
                MoveMarkerSelection(editor.CaretOffset, step);
            }
        }

        private void FindReplaceDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case System.Windows.Input.Key.Enter:
                    e.Handled = true;
                    if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
                    {
                        FindPrevClick(null, null);
                    } else
                    {
                        FindNextClick(null, null);
                    }
                    break;
                case System.Windows.Input.Key.Escape:
                    e.Handled = true;
                    Window_Closed(null, null);
                    break;
            }
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            textToFind = txtFind2.Text;
            caseSensitive = (cbCaseSensitive.IsChecked == true);
            wholeWord = (cbWholeWord.IsChecked == true);
            useRegex = (cbRegex.IsChecked == true);
            useWildcards = (cbWildcards.IsChecked == true);
            searchUp = (cbSearchUp.IsChecked == true);
            editor.TextArea.TextView.BackgroundRenderers.Remove(searchResultsBackgroundRenderer);
            editor.TextArea.TextView.BackgroundRenderers.Remove(selectionSearchBackgroundRenderer);
            theDialog = null;
        }

        private void Window_Close(object sender, System.EventArgs e)
        {
            theDialog.Close();
        }

        private void FindNextClick(object sender, RoutedEventArgs e)
        {
            if (!FindNext())
                System.Media.SystemSounds.Beep.Play();
        }
        private void FindPrevClick(object sender, RoutedEventArgs e)
        {
            if (!FindPrev())
                System.Media.SystemSounds.Beep.Play();
        }

        private void ReplaceClick(object sender, RoutedEventArgs e)
        {
            if (currentResult != null)
            {
                string replacedText = currentResult.Data.Result(txtReplace.Text);

                editor.Document.Replace(currentResult.StartOffset, currentResult.Length, replacedText);
            }

            // currentResult is updated after editor text is changed, thats why we need to check it again.
            if (currentResult != null)
            {
                editor.Select(currentResult.StartOffset, currentResult.Length);
                TextLocation loc = editor.Document.GetLocation(currentResult.StartOffset);
                editor.ScrollTo(loc.Line, loc.Column);
            }
            else if (!FindNext())
            {
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private void ReplaceAllClick(object sender, RoutedEventArgs e)
        {
            int offset = 0;
            editor.BeginChange();

            foreach (SearchResult result in searchResultsBackgroundRenderer.CurrentResults)
            {
                if (result.Data.Value == editor.Text.Substring(result.StartOffset + offset, result.Length))
                {
                    string replacedText = result.Data.Result(txtReplace.Text);
                    editor.Document.Replace(result.StartOffset + offset, result.Length, replacedText);
                    offset += replacedText.Length - result.Length;
                }
            }
            editor.EndChange();
        }

        private bool FindNext()
        {
            if (currentResult != null)
            {
                if (currentResult.StartOffset == editor.SelectionStart)
                {
                    currentResult = searchResultsBackgroundRenderer.CurrentResults.GetNextSegment(currentResult);
                }
            }
            else
            {
                currentResult = searchResultsBackgroundRenderer.CurrentResults.FirstSegment;
            }

            if (currentResult != null)
            {
                editor.Select(currentResult.StartOffset, currentResult.Length);
                TextLocation loc = editor.Document.GetLocation(currentResult.StartOffset);
                editor.ScrollTo(loc.Line, loc.Column);
                return true;
            }
            else
            {
                return false;
            }

        }

        private bool FindPrev()
        {
            if (currentResult != null)
            {
                currentResult = searchResultsBackgroundRenderer.CurrentResults.GetPreviousSegment(currentResult);
            }
            else
            {
                currentResult = searchResultsBackgroundRenderer.CurrentResults.LastSegment;
            }

            if (currentResult != null)
            {
                editor.Select(currentResult.StartOffset, currentResult.Length);
                TextLocation loc = editor.Document.GetLocation(currentResult.StartOffset);
                editor.ScrollTo(loc.Line, loc.Column);
                return true;
            }
            else
            {
                return false;
            }

        }


        /// <summary>
        /// Collects all search results in text and
        /// tells the editor to mark them
        /// </summary>
        /// <param name="textToFind">Text to search for</param>
        private void MarkAllWords(string textToFind)
        {
            searchResultsBackgroundRenderer.CurrentResults.Clear();

            if (SelectionOnly)
            {
                SearchResult result = selectionSearchBackgroundRenderer.CurrentResults.FirstSegment;
                if (result != null)
                {
                    SelectionStart = result.StartOffset;
                    SelectionEnd = result.EndOffset;
                }

            }
            else
            {
                SelectionStart = 0;
                SelectionEnd = editor.Text.Length;
            }

            if (!string.IsNullOrEmpty(textToFind))
            {
                Regex regex = GetRegEx(textToFind);
                if (regex != null)
                {
                    foreach (Match match in regex.Matches(editor.Text.Substring(SelectionStart, SelectionEnd)))
                    {
                        SearchResult result = new SearchResult(match);
                        searchResultsBackgroundRenderer.CurrentResults.Add(result);
                    }
                }
            }

            // Update current result.
            currentResult = searchResultsBackgroundRenderer.CurrentResults.FindFirstSegmentWithStartAfter(editor.CaretOffset);
            editor.Select(editor.SelectionStart, 0);

            editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
        }

        private void MarkSelection()
        {
            ClearMarkerSelection();

            int start = editor.SelectionStart;
            int length = editor.SelectionLength;

            SearchResult result = new SearchResult(start, length);
            selectionSearchBackgroundRenderer.CurrentResults.Add(result);
            editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
            editor.Select(editor.SelectionStart, 0);
        }

        private void ClearMarkerSelection()
        {
            selectionSearchBackgroundRenderer.CurrentResults.Clear();
            editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
        }

        private void MoveMarkerSelection(int offset, int steps)
        {
            SearchResult result = selectionSearchBackgroundRenderer.CurrentResults.FirstSegment;
            if (result != null)
            {
                if (result.EndOffset <= offset)
                {
                    // Text changed after current selection. No changes required.
                    return;
                }

                int start = result.StartOffset;
                int length = result.Length;
                if (result.StartOffset >= offset)
                {
                    // Text changed before current selection. Move selection start forward/backward.
                    start = result.StartOffset + steps;
                }
                else if (offset > result.StartOffset)
                {
                    // Text changed in current selection. Change selection length to include new changes.
                    length = result.Length + steps;
                }


                selectionSearchBackgroundRenderer.CurrentResults.Clear();
                SearchResult newResult = new SearchResult(start, length);
                selectionSearchBackgroundRenderer.CurrentResults.Add(newResult);
                editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
            }
        }

        /// <summary>
        /// Prepare search options
        /// </summary>
        /// <param name="textToFind">Text to search for</param>
        /// <param name="leftToRight"></param>
        /// <returns></returns>
        private Regex GetRegEx(string textToFind, bool leftToRight = false)
        {
            RegexOptions options = RegexOptions.None;
            if (cbSearchUp.IsChecked == true && !leftToRight)
                options |= RegexOptions.RightToLeft;
            if (cbCaseSensitive.IsChecked == false)
                options |= RegexOptions.IgnoreCase;
            options |= RegexOptions.Multiline;

            if (cbRegex.IsChecked == true)
            {
                try
                {
                    return new Regex(textToFind, options);
                }
                catch (System.ArgumentException ex)
                {
                    return null;
                }
            }
            else
            {
                string pattern = Regex.Escape(textToFind);
                if (cbWildcards.IsChecked == true)
                    pattern = pattern.Replace("\\*", ".*").Replace("\\?", ".");
                if (cbWholeWord.IsChecked == true)
                    pattern = "\\b" + pattern + "\\b";
                return new Regex(pattern, options);
            }
        }

        private static FindReplaceDialog theDialog = null;

        public static void ShowForReplace(TextEditor editor, bool showReplaceTab = false)
        {
            int selectedTab = 0;
            if (showReplaceTab)
            {
                selectedTab = 1;
            }

            if (theDialog == null)
            {
                theDialog = new FindReplaceDialog(editor);
                theDialog.Owner = Application.Current.MainWindow;
                
                var editorWidth = editor.ActualWidth;
                var editorPos = editor.PointToScreen(new Point(editorWidth,0));
                theDialog.Top = editorPos.Y-1;
                theDialog.Left = editorPos.X-12 - theDialog.Width;

                theDialog.tabMain.SelectedIndex = selectedTab;
                theDialog.Show();
                theDialog.Activate();
            }
            else
            {
                theDialog.tabMain.SelectedIndex = selectedTab;
                theDialog.Activate();
            }

            if (!editor.TextArea.Selection.IsMultiline)
            {
                theDialog.txtFind.Text = theDialog.txtFind2.Text = editor.TextArea.Selection.GetText();
                theDialog.txtFind.SelectAll();
                theDialog.txtFind2.SelectAll();
                if (showReplaceTab)
                {
                    if (theDialog.txtFind2.Text.Length > 0)
                    {
                        theDialog.txtReplace.Focus();
                    }
                    else
                    {
                        theDialog.txtFind2.Focus();
                    }
                }
                else
                {
                    theDialog.txtFind.Focus();
                }
            }
            else
            {
                theDialog.SelectionOnly = true;
            }
        }
    }
}