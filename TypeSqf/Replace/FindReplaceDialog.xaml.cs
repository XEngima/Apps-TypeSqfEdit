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
        private static string textToFind = "";
        private static bool caseSensitive = true;
        private static bool wholeWord = true;
        private static bool useRegex = false;
        private static bool useWildcards = false;
        private static bool searchUp = false;
        SearchResults searchResults;

        private bool SelectionOnly
        {
            get; set;
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

            KeyDown += FindReplaceDialog_KeyDown;

            searchResults = new SearchResults();
            txtFind.TextChanged += TxtFind_TextChanged;
            txtFind2.TextChanged += TxtFind_TextChanged;
            editor.TextArea.TextView.BackgroundRenderers.Add(searchResults);
            currentDocument = editor.TextArea.Document;
            if (currentDocument != null)
                currentDocument.TextChanged += textArea_Document_TextChanged;
            editor.TextArea.DocumentChanged += textArea_DocumentChanged;
        }

        private void TxtFind_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (cbSelection.IsChecked ?? true)
            {
                SelectionStart = editor.SelectionStart;
                SelectionEnd = editor.SelectionStart + editor.SelectionLength;
            }
            else
            {
                SelectionStart = 0;
                SelectionEnd = editor.Text.Length;
            }
            MarkAllWords(SearchText);
        }

        private void Options_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                MarkAllWords(SearchText);
                var x = sender as CheckBox;
                if( x != null && x.Name == "cbSelection")
                {
                    TxtFind_TextChanged(null, null);
                }
            }
            catch
            {
                return;
            }

        }

        void textArea_DocumentChanged(object sender, EventArgs e)
        {
            if (currentDocument != null)
                currentDocument.TextChanged -= textArea_Document_TextChanged;
            currentDocument = editor.TextArea.Document;
            if (currentDocument != null)
            {
                currentDocument.TextChanged += textArea_Document_TextChanged;
                MarkAllWords(SearchText);
            }
        }

        void textArea_Document_TextChanged(object sender, EventArgs e)
        {
            MarkAllWords(SearchText);
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
            editor.TextArea.TextView.BackgroundRenderers.Remove(searchResults);
            theDialog = null;
        }

        private void Window_Close(object sender, System.EventArgs e)
        {
            theDialog.Close();
        }

        private void FindNextClick(object sender, RoutedEventArgs e)
        {
            if (!FindNext(SearchText))
                System.Media.SystemSounds.Beep.Play();
        }
        private void FindPrevClick(object sender, RoutedEventArgs e)
        {
            cbSearchUp.IsChecked = !cbSearchUp.IsChecked;
            if (!FindNext(SearchText))
                System.Media.SystemSounds.Beep.Play();
            cbSearchUp.IsChecked = !cbSearchUp.IsChecked;

        }

        private void FindNext2Click(object sender, RoutedEventArgs e)
        {
            FindNextClick(sender, e);
            //if (!FindNext(txtFind2.Text))
            //    System.Media.SystemSounds.Beep.Play();
        }

        private void ReplaceClick(object sender, RoutedEventArgs e)
        {
            Regex regex = GetRegEx(txtFind2.Text);
            string input = editor.Text.Substring(editor.SelectionStart, editor.SelectionLength);
            Match match = regex.Match(input);
            bool replaced = false;
            if (match.Success && match.Index == 0 && match.Length == input.Length)
            {
                editor.Document.Replace(editor.SelectionStart, editor.SelectionLength, txtReplace.Text);
                replaced = true;
            }

            if (!FindNext(txtFind2.Text) && !replaced)
                System.Media.SystemSounds.Beep.Play();
        }

        private void ReplaceAllClick(object sender, RoutedEventArgs e)
        {
            Regex regex = GetRegEx(txtFind2.Text, true);
            int offset = 0;
            editor.BeginChange();
            foreach (Match match in regex.Matches(editor.Text, SelectionStart))
            {
                if (match.Index + match.Length + offset <= SelectionEnd + offset)
                {
                    editor.Document.Replace(offset + match.Index, match.Length, txtReplace.Text);
                    offset += txtReplace.Text.Length - match.Length;
                }
            }
            editor.EndChange();
        }

        private bool FindNext(string textToFind)
        {
            Regex regex = GetRegEx(textToFind);
            int start = regex.Options.HasFlag(RegexOptions.RightToLeft) ?
                SelectionEnd : SelectionStart;

            if (SelectionStart != editor.SelectionStart && 
                editor.SelectionStart > SelectionStart && 
                editor.SelectionStart < SelectionEnd
            )
            {
                start = regex.Options.HasFlag(RegexOptions.RightToLeft) ?
                editor.SelectionStart : editor.SelectionStart + editor.SelectionLength;
            }
            

            Match match = regex.Match(editor.Text, start);

            if (match.Success && (match.Index + match.Length > SelectionEnd || match.Index < SelectionStart))
            {
                //Outside given selection
                return false;
            }

            if (!match.Success)
            {
                // start again from beginning or end
                if (regex.Options.HasFlag(RegexOptions.RightToLeft))
                    match = regex.Match(editor.Text, SelectionEnd);
                else
                    match = regex.Match(editor.Text, SelectionStart);
            }

            if (match.Success)
            {
                editor.Select(match.Index, match.Length);
                TextLocation loc = editor.Document.GetLocation(match.Index);
                editor.ScrollTo(loc.Line, loc.Column);
            }

            return match.Success;
        }

        /// <summary>
        /// Collects all words/letters in text and
        /// tells the editor to mark them
        /// </summary>
        /// <param name="textToFind">Text to search for</param>
        private void MarkAllWords(string textToFind)
        {
            searchResults.CurrentResults.Clear();

            if (!string.IsNullOrEmpty(textToFind))
            {
                Regex regex = GetRegEx(textToFind);
                foreach (Match match in regex.Matches(editor.Text, SelectionStart))
                {
                    if (match.Index + match.Length <= SelectionEnd)
                    {
                        SearchResult result = new SearchResult(match);
                        searchResults.CurrentResults.Add(result);
                    }
                    
                }
            }
            editor.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection);
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

            if (cbRegex.IsChecked == true)
            {
                return new Regex(textToFind, options);
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
        }
    }
}