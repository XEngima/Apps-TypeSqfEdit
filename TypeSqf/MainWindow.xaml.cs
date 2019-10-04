using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Search;
using TypeSqf.Model;
using TypeSqf.Edit.Services;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using System.Linq;
using System.Text.RegularExpressions;
using TypeSqf.Analyzer.Commands;
using System.Text;
using System.IO;
using ICSharpCode.AvalonEdit.Folding;
using TypeSqf.Edit.Folding;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using TypeSqf.Edit.Highlighting;
using TypeSqf.Edit.Forms;
using TypeSqf.Analyzer;

namespace TypeSqf.Edit
{
    public static class CustomCommands
    {
        public static readonly RoutedUICommand FindInAllFiles = new RoutedUICommand
                (
                        "Find in all files...",
                        "Find in all files...",
                        typeof(CustomCommands),
                        new InputGestureCollection()
                        {
                                        new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Shift)
                        }
                );

        //Define more commands here, just like the one above
    }

    /// Implements AvalonEdit ICompletionData interface to provide the entries in the
    /// completion drop down.
    public class MyCompletionData : ICompletionData, IComparable
    {
        public MyCompletionData(string text, string description)
        {
            Text = text;
            Description = description;

            if (string.IsNullOrEmpty(description))
            {
                Description = null;
            }
        }

        public MyCompletionData(string text)
            : this(text, null)
        {
        }

        public System.Windows.Media.ImageSource Image
        {
            get { return null; }
        }

        public string Text { get; private set; }

        // Use this property if you want to show a fancy UIElement in the list.
        public object Content
        {
            get { return this.Text; }
        }

        public object Description
        {
            get; set;
        }

        public double Priority
        {
            get
            {
                return 1;
            }
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, this.Text);
        }

        public int CompareTo(object obj)
        {
            return string.Compare(Text, ((MyCompletionData)obj).Text);
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _activatingForFirstTime;

        public MainWindow()
        {
            InitializeComponent();

            MyContext.UserMessageService = new UserMessageService(this);
            MyContext.FileService = new FileService();
            MyContext.FileNameService = new AskForFileNameService();
            MyContext.FolderNameService = new AskForFolderNameService();
            MyContext.ProjectNameService = new AskForProjectNameService();

            MyContext.ProjectChanged += MyContext_ProjectChanged;

            Activated += MyContext.OnActivated;

            _activatingForFirstTime = true;
            Activated += MainWindow_Activated;
            Closing += MyContext.OnWindowClosing;

            MyContext.TabGettingFocus += TabItem_TabGettingFocus;
            MyContext.TabLosingFocus += TabItem_TabLosingFocus;

            MyContext.LoadScriptCommandFile();
            MyContext.StoppedWriting += MyContext_StoppedWriting;
            ThemeBackgroundColor = MyContext.ThemeBackgroundColor;

            HotKeyHandler = new HotKey.HotKeyHandler(this);
        }

        private void MyContext_StoppedWriting(object sender, EventArgs e)
        {
            UpdateFoldings();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            if (_activatingForFirstTime)
            {
                _activatingForFirstTime = false;
                ApplySyntaxHighlighting();
            }
        }

        public Brush ThemeBackgroundColor { get; set; }

        private HotKey.HotKeyHandler HotKeyHandler { get; set; }

        private void MyContext_ProjectChanged(object sender, EventArgs e)
        {
            ClearAllResultWindows();

            if (TheFoldingManager != null)
            {
                FoldingManager.Uninstall(TheFoldingManager);
                TheFoldingManager = null;

                var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");

                if (textEditor != null)
                {
                    TheFoldingManager = FoldingManager.Install(textEditor.TextArea);
                }
            }
        }

        private MainWindowViewModel MyContext
        {
            get { return (MainWindowViewModel)DataContext; }
        }

        private void FindExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");
            if (textEditor != null)
            {
                var sp = new SearchPanel();
                sp.Attach(textEditor.TextArea);
                sp.Open();
                sp.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)sp.Reactivate);
            }
        }

        private T FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            T child = default(T);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var ch = VisualTreeHelper.GetChild(parent, i);
                child = ch as T;
                if (child != null && child.Name == name)
                    break;
                else
                    child = FindVisualChildByName<T>(ch, name);

                if (child != null) break;
            }
            return child;
        }

        private bool _gotoCompileErrorTimerActive = false;
        private void GotoCompileError_DispatcherTimer_Tick(object sender, EventArgs e)
        {
            var timer = sender as DispatcherTimer;
            timer.Stop();

            AnalyzerResultItem selectedItem = MyContext.SelectedCompilerResultItem;

            var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");
            if (textEditor != null)
            {
                if (selectedItem != null)
                {
                    int lineNo = selectedItem.LineNo;

                    DocumentLine line = textEditor.Document.GetLineByNumber(lineNo);

                    textEditor.Select(line.Offset, line.Length);
                    textEditor.CaretOffset = line.Offset;
                    textEditor.ScrollToLine(lineNo);
                    textEditor.Focus();
                }
            }

            _gotoCompileErrorTimerActive = false;
        }

        private void AnalyzerResultListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                ListBox resultWindow = sender as ListBox;
                var selectedIndex = resultWindow.SelectedIndex;
                AnalyzerResultItem selectedItem = resultWindow.SelectedItem as AnalyzerResultItem;
                if (selectedIndex >= 0)
                {
                    if (string.IsNullOrWhiteSpace(selectedItem.RelativeFilePathName))
                    {
                        int lineNo = selectedItem.LineNo;

                        if (lineNo > 0)
                        {
                            var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");

                            if (textEditor != null)
                            {
                                DocumentLine line = textEditor.Document.GetLineByNumber(lineNo);

                                textEditor.Select(line.Offset, line.Length);
                                textEditor.CaretOffset = line.Offset;
                                textEditor.ScrollToLine(lineNo);
                                textEditor.Focus();
                            }
                        }
                    }
                    else
                    {
                        MyContext.OpenFileInTab(selectedItem.AbsoluteFilePathName);
                        _gotoCompileErrorTimerActive = true;
                        DispatcherTimer dispatcherTimer = new DispatcherTimer();
                        dispatcherTimer.Tick += new EventHandler(GotoCompileError_DispatcherTimer_Tick);
                        dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
                        dispatcherTimer.Start();
                    }
                }
            }
            catch
            {
                if (!CurrentApplication.IsRelease)
                {
                    throw;
                }
            }
        }

        private void CompilerResultListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                ListBox resultWindow = sender as ListBox;
                var selectedIndex = resultWindow.SelectedIndex;
                AnalyzerResultItem selectedItem = resultWindow.SelectedItem as AnalyzerResultItem;

                if (selectedIndex >= 0 && !string.IsNullOrWhiteSpace(selectedItem.RelativeFilePathName))
                {
                    MyContext.OpenFileInTab(selectedItem.AbsoluteFilePathName);
                    _gotoCompileErrorTimerActive = true;
                    DispatcherTimer dispatcherTimer = new DispatcherTimer();
                    dispatcherTimer.Tick += new EventHandler(GotoCompileError_DispatcherTimer_Tick);
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
                    dispatcherTimer.Start();
                }
            }
            catch
            {
                if (!CurrentApplication.IsRelease)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Clear the result windows from compiler and analyzer results.
        /// </summary>
        public void ClearAllResultWindows()
        {
            MyContext.ChangeSelectedResultTab(MainWindowViewModel.ResultTabs.Analyzer);
            MyContext.AnalyzerResultItems.Clear();
            MyContext.CompilerResultItems.Clear();
            MyContext.CompilerProgressBarIndeterminate = false;
            MyContext.CompilerProgressBarValue = 0;
        }

        private void CPackConsoleMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            CPackConsole console = new CPackConsole(MyContext.ProjectRootDirectory);
            console.MyContext.PackageInstalled += MyContext_PackageInstalled;
            console.Owner = this;
            console.ShowDialog();
        }

        private void MyContext_PackageInstalled(object sender, PackageEventArgs e)
        {
            // Clean project view
            MyContext.RemoveProjectFilesThatDoNotExist();

            // Add package to project view
            MyContext.AddCPackFilesToProject(e.PackageName);

            // Save project
            MyContext.SaveProjectFile();
        }

        private void TreeViewItem_PreviewMouseDoubleClick(object sender, EventArgs e)
        {
            if (MyContext.ActiveTabIndex >= 0)
            {
                var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");
                var activeTab = MyContext.Tabs[MyContext.ActiveTabIndex];
                activeTab.VerticalOffset = textEditor.VerticalOffset;
                activeTab.SelectionStart = textEditor.SelectionStart;
                activeTab.SelectionLength = textEditor.SelectionLength;
                activeTab.CaretOffset = textEditor.CaretOffset;
            }
        }

        private void TabHeader_MouseDown(object sender, RoutedEventArgs e)
        {
            if (MyContext.ActiveTabIndex >= 0)
            {
                var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");
                var activeTab = MyContext.Tabs[MyContext.ActiveTabIndex];
                activeTab.VerticalOffset = textEditor.VerticalOffset;
                activeTab.SelectionStart = textEditor.SelectionStart;
                activeTab.SelectionLength = textEditor.SelectionLength;
                activeTab.CaretOffset = textEditor.CaretOffset;

                ((TabItem)sender).IsSelected = true;
                activeTab = MyContext.Tabs[MyContext.ActiveTabIndex];

                textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");
                //textEditor.Text = activeTab.Text;
                textEditor.ScrollToVerticalOffset(activeTab.VerticalOffset);
                textEditor.CaretOffset = activeTab.CaretOffset;
                textEditor.SelectionLength = 0; // För att det inte ska krascha
                textEditor.SelectionStart = activeTab.SelectionStart;
                textEditor.SelectionLength = activeTab.SelectionLength;

                textEditor.TextArea.Focus();
            }
        }

        bool _tabGettingFocusTimerActive = false;
        private void TabItem_TabGettingFocus(object sender, EventArgs e)
        {
            if (MyContext.ActiveTabIndex >= 0 && !_tabGettingFocusTimerActive)
            {
                _tabGettingFocusTimerActive = true;
                DispatcherTimer dispatcherTimer = new DispatcherTimer();
                dispatcherTimer.Tick += new EventHandler(tabGettingFocusTimer_Tick);
                dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 1);
                dispatcherTimer.Start();
            }
        }

        private void ApplyNewSettings()
        {
            var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");

            if (textEditor != null)
            {
                textEditor.Options.IndentationSize = MyContext.Settings.IndentationSize;
                textEditor.Options.ConvertTabsToSpaces = MyContext.Settings.ConvertTabsToSpaces;
            }
        }

        private void ApplySyntaxHighlighting()
        {
            var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");

            Theme theme = SyntaxHighlightingHandler.LoadedTheme;

            Brush backgroundBrush = (new BrushConverter()).ConvertFromString("#" + theme.BackgroundColor) as Brush;
            Brush foregroundBrush = (new BrushConverter()).ConvertFromString("#" + theme.ForegroundColor) as Brush;

            ThemeBackgroundColor = backgroundBrush;
            MyContext.ThemeBackgroundColor = backgroundBrush;
            MyContext.ThemeForegroundColor = foregroundBrush;

            if (textEditor != null)
            {
                textEditor.Background = backgroundBrush;
                textEditor.Foreground = foregroundBrush;
                textEditor.FontFamily = new FontFamily(theme.FontName);
                textEditor.FontSize = theme.FontSize;

                string extension = MyContext.ActiveTab != null ? Path.GetExtension(MyContext.ActiveTab.Name) : "";

                string fileDef = SyntaxHighlightingHandler.GetDefinition(extension, theme);
                using (XmlTextReader reader = new XmlTextReader(new StringReader(fileDef)))
                {

                    textEditor.SyntaxHighlighting =
                        ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(
                        reader,
                        HighlightingManager.Instance);
                }
            }
        }

        private void tabGettingFocusTimer_Tick(object sender, EventArgs e)
        {
            var timer = sender as DispatcherTimer;
            timer.Stop();

            var activeTab = MyContext.Tabs[MyContext.ActiveTabIndex];
            var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");

            if (textEditor != null)
            {
                textEditor.SelectionStart = 0;
                textEditor.SelectionLength = 0;
                textEditor.ScrollToVerticalOffset(activeTab.VerticalOffset);
                textEditor.CaretOffset = activeTab.CaretOffset;
                textEditor.SelectionStart = activeTab.SelectionStart;
                textEditor.SelectionLength = activeTab.SelectionLength;

                textEditor.TextArea.Focus();

                textEditor.TextArea.TextEntered += TextArea_TextEntered;
                textEditor.TextArea.TextEntering += TextArea_TextEntering;
                textEditor.TextArea.KeyUp += TextArea_KeyUp;
                ApplySyntaxHighlighting();
                ApplyNewSettings();
            }

            _tabGettingFocusTimerActive = false;

            try
            {
                if (TheFoldingManager == null && MyContext.Settings.EnableFolding)
                {
                    TheFoldingManager = FoldingManager.Install(textEditor.TextArea);
                }

                UpdateFoldings(activeTab.FoldingSections);
            }
            catch
            {
                if (TheFoldingManager != null)
                {
                    FoldingManager.Uninstall(TheFoldingManager);
                    TheFoldingManager = null;
                }

                if (!CurrentApplication.IsRelease)
                {
                    throw;
                }
            }
        }

        private FoldingManager TheFoldingManager { get; set; }

        private void UpdateFoldings(IEnumerable<TypeSqfFoldingSection> explicitFoldingSections = null)
        {
            if (!MyContext.Settings.EnableFolding)
            {
                if (TheFoldingManager != null)
                {
                    FoldingManager.Uninstall(TheFoldingManager);
                }

                TheFoldingManager = null;
                return;
            }

            var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");

            if (textEditor != null)
            {
                if (TheFoldingManager == null)
                {
                    TheFoldingManager = FoldingManager.Install(textEditor.TextArea);
                }

                var foldingStrategy = new TypeSqfFoldingStrategy(explicitFoldingSections);
                foldingStrategy.UpdateFoldings(TheFoldingManager, textEditor.Document);
            }
        }

        private void TextArea_KeyUp(object sender, KeyEventArgs e)
        {
            var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");
            _completionHandledIndex = textEditor.SelectionStart;

            HotKeyHandler.KeyUp(textEditor, e);
        }

        private CompletionWindow _completionWindow = null;
        private List<MyCompletionData> _allCompletionWords = null;
        private bool _completionShowingMethods = false;
        private bool _completionShowingNewAlternatives = false;

        private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            try
            {
                if (!MyContext.Settings.EnableAutoCompletion)
                {
                    return;
                }

                if (_completionShowingMethods || _completionShowingNewAlternatives)
                {
                    return;
                }

                if (e.Text.Length > 0 && _completionWindow != null)
                {
                    var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");
                    if (textEditor.SelectionStart >= _completionWindow.StartOffset)
                    {
                        string text = textEditor.Text.Substring(_completionWindow.StartOffset, textEditor.SelectionStart - _completionWindow.StartOffset) + e.Text;

                        if (_allCompletionWords.Count(x => x.Text.StartsWith(text)) == 0)
                        {
                            _completionWindow.Close();
                        }
                    }
                }
            }
            catch
            {
                if (!CurrentApplication.IsRelease)
                {
                    throw;
                }

                if (_completionWindow != null)
                {
                    _completionWindow.Close();
                    _completionWindow = null;
                }
            }
        }

        private static bool InBlockComment(string text, int currentIndex)
        {
            int commentOpenerIndex = text.LastIndexOf("/*", currentIndex);
            int commentClosingIndex = text.LastIndexOf("*/", currentIndex);

            return commentOpenerIndex > commentClosingIndex;
        }

        private static bool InLineComment(string text, int currentIndex)
        {
            string line = GetLineReadBackwards(text, currentIndex - 1).Trim();
            return line.Contains("//");
        }

        private static bool InPreprocessorCommand(string text, int currentIndex)
        {
            string line = GetLineReadBackwards(text, currentIndex - 1).Trim();
            return line.StartsWith("#");
        }

        private static bool InComment(string text, int currentIndex)
        {
            return InBlockComment(text, currentIndex) || InLineComment(text, currentIndex);
        }

        private int _completionHandledIndex = -1;

        /// <summary>
        /// Gets the file extension (in lowers) for the currently selected tab.
        /// </summary>
        private string CurrentFileExtension {
            get
            {
                return MyContext.ActiveTab != null ? Path.GetExtension(MyContext.ActiveTab.Name).ToLower() : "";
            }
        }

        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            try
            {
                if (!MyContext.Settings.EnableAutoCompletion)
                {
                    return;
                }

                if (CurrentFileExtension != ".sqf" && CurrentFileExtension != ".sqx")
                {
                    return;
                }

                var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");

                if (_completionHandledIndex == textEditor.SelectionStart)
                {
                    return;
                }

                _completionHandledIndex = textEditor.SelectionStart;

                string lastWord = char.IsLetterOrDigit(e.Text[0]) ? GetWordReadBackwards(textEditor.Text, textEditor.SelectionStart - 1) + e.Text : "";
                string word = lastWord;
                string line = GetLineReadBackwards(textEditor.Text, textEditor.SelectionStart - 1).Trim();
                string lowerLine = line.ToLower();

                // If in comment, no auto completion
                if (InComment(textEditor.Text, textEditor.SelectionStart) || InPreprocessorCommand(textEditor.Text, textEditor.SelectionStart))
                {
                    return;
                }

                // Hide completion window on semicolon.
                if (_completionWindow != null && (e.Text == ";"))
                {
                    _completionWindow.Hide();
                    _completionWindow = null;
                }

                if (_completionShowingMethods || _completionShowingNewAlternatives)
                {
                    if (e.Text == " ")
                    {
                        if (_completionWindow != null)
                        {
                            _completionWindow.Close();
                        }
                    }
                    return;
                }

                bool isSqx = MyContext.ActiveTabIndex >= 0 && MyContext.Tabs[MyContext.ActiveTabIndex].Name.ToLower().EndsWith(".sqx");

                if (isSqx && e.Text == ".")
                {
                    word = GetWordBeforeDotReadBackwards(textEditor.Text, textEditor.SelectionStart - 1).ToLower();
					string namespaceName = GetCurrentNamespaceName(textEditor.Text, textEditor.SelectionStart);
					var usings = GetCurrentUsings(textEditor.Text, textEditor.SelectionStart);
					string typeName = "";
                    string currentClassName = "";
					bool isSelf = false;

					GlobalContextVariable publicVariable = MyContext.DeclaredPublicVariables.FirstOrDefault(v => v.Name.ToLower() == word);
                    if (publicVariable != null && !string.IsNullOrWhiteSpace(publicVariable.TypeName))
                    {
                        var objectInfo = MyContext.DeclaredTypes.FirstOrDefault(c => c.FullName.ToLower() == publicVariable.TypeName.ToLower()) as ObjectInfo;

                        if (objectInfo != null)
                        {
                            _completionShowingMethods = true;

                            if (_completionWindow != null)
                            {
                                _completionWindow.Hide();
                            }

                            _completionWindow = new CompletionWindow(textEditor.TextArea);
                            _completionWindow.SizeToContent = SizeToContent.WidthAndHeight;
                            _completionWindow.MaxHeight = 200;

                            IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;
                            var completions = new List<MyCompletionData>();

                            foreach (var method in objectInfo.Methods)
                            {
                                if (method.Accessability == Accessability.Public)
                                {
                                    var sbDescription = new StringBuilder();

                                    if (method.ReturnValueTypeName != "Nothing")
                                    {
                                        sbDescription.Append("Method: [");
                                        sbDescription.Append(method.ReturnValueTypeName);
                                        sbDescription.Append("] = ");

                                        sbDescription.Append(method.Name);
                                    }

                                    completions.Add(new MyCompletionData(method.Name, sbDescription.ToString()));
                                }
                            }

                            foreach (var property in objectInfo.Properties)
                            {
                                var sbDescription = new StringBuilder();

                                if (property.ReturnValueTypeName != "Nothing")
                                {
                                    sbDescription.Append("Property: [");
                                    sbDescription.Append(property.ReturnValueTypeName);
                                    sbDescription.Append("] = ");

                                    sbDescription.Append(property.Name);
                                }

                                completions.Add(new MyCompletionData(property.Name, sbDescription.ToString()));
                            }

                            foreach (var completion in completions.OrderBy(c => c.Text))
                            {
                                data.Add(completion);
                            }

                            _completionWindow.Show();
                            _completionWindow.Closed += delegate
                            {
                                _completionShowingMethods = false;
                                _completionWindow = null;
                            };
                        }

						return;
                    }

					typeName = word;
					if (!word.Contains("."))
					{
						if (!string.IsNullOrWhiteSpace(namespaceName))
						{
							typeName = namespaceName + "." + word;
						}
						else
						{
							foreach (var use in usings)
							{
								if (!string.IsNullOrWhiteSpace(use))
								{
									typeName = use + "." + typeName;
									break;
								}
							}
						}
					}

					TypeInfo typeInfo;
					if (word.Contains("."))
					{
						typeInfo = MyContext.DeclaredTypes.FirstOrDefault(t => t.FullName.ToLower() == word);
					}
					else
					{
						var typeInfos = GetPossibleTypes(namespaceName, MyContext.DeclaredTypes, usings);
						typeInfo = typeInfos.FirstOrDefault(i => i.Name.ToLower() == word);
					}

					ObjectInfo typeObj = typeInfo as ObjectInfo;
					EnumInfo enumInfo = typeInfo as EnumInfo;

					if (typeObj != null)
					{
						_completionShowingMethods = true;

						if (_completionWindow != null)
						{
							_completionWindow.Hide();
						}

						_completionWindow = new CompletionWindow(textEditor.TextArea);
						_completionWindow.SizeToContent = SizeToContent.WidthAndHeight;
						_completionWindow.MaxHeight = 200;

						IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;
						var completions = new List<MyCompletionData>();

						foreach (var method in typeObj.Methods)
						{
							if (method.IsStatic)
							{
								var sbDescription = new StringBuilder();

								if (method.ReturnValueTypeName != "Nothing")
								{
									sbDescription.Append("Method: [");
									sbDescription.Append(method.ReturnValueTypeName);
									sbDescription.Append("] = ");

									sbDescription.Append(method.Name);
								}

								completions.Add(new MyCompletionData(method.Name, sbDescription.ToString()));
							}
						}

						foreach (var completion in completions.OrderBy(c => c.Text))
						{
							data.Add(completion);
						}

						_completionWindow.Show();
						_completionWindow.Closed += delegate
						{
							_completionShowingMethods = false;
							_completionWindow = null;
						};

						return;
					}

					if (enumInfo != null)
					{
						_completionShowingMethods = true;

						if (_completionWindow != null)
						{
							_completionWindow.Hide();
						}

						_completionWindow = new CompletionWindow(textEditor.TextArea);
						_completionWindow.SizeToContent = SizeToContent.WidthAndHeight;
						_completionWindow.MaxHeight = 200;

						IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;
						var completions = new List<MyCompletionData>();

						foreach (var enumValue in enumInfo.EnumValues)
						{
							completions.Add(new MyCompletionData(enumValue.Name));
						}

						foreach (var completion in completions.OrderBy(c => c.Text))
						{
							data.Add(completion);
						}

						_completionWindow.Show();
						_completionWindow.Closed += delegate
						{
							_completionShowingMethods = false;
							_completionWindow = null;
						};

						return;
					}

					if (word.ToLower() == "_self")
                    {
						typeName = "";
						string className = GetCurrentClassName(textEditor.Text, textEditor.SelectionStart);
                        isSelf = true;

                        if (!string.IsNullOrEmpty(className))
                        {
                            if (className.Contains("."))
                            {
                                typeName = className;
                            }
                            else if (!string.IsNullOrEmpty(namespaceName))
                            {
                                typeName = namespaceName + "." + className;
                            }
                            else
                            {
                                typeName = className;
                            }
                        }
                    }
                    else if (word.ToLower() == "_base")
                    {
                        typeName = "";
                        string className = GetCurrentClassName(textEditor.Text, textEditor.SelectionStart);
                        isSelf = true;

                        if (!string.IsNullOrEmpty(className))
                        {
                            if (className.Contains("."))
                            {
                                typeName = className;
                            }
                            else if (!string.IsNullOrEmpty(namespaceName))
                            {
                                typeName = namespaceName + "." + className;
                            }
                            else
                            {
                                typeName = className;
                            }
                        }

                        ClassInfo currentClass = MyContext.DeclaredTypes.FirstOrDefault(c => c.FullName.ToLower() == typeName.ToLower()) as ClassInfo;
                        currentClassName = currentClass.FullName;
                        typeName = currentClass.InheritedClass?.FullName;
                        typeName = typeName != null ? typeName : "";
                    }
                    else
                    {
                        typeName = GetDeclaredVariableType(word, textEditor.Text, textEditor.SelectionStart);
                    }

                    ObjectInfo theObject = MyContext.DeclaredTypes.FirstOrDefault(c => c.FullName.ToLower() == typeName.ToLower()) as ObjectInfo;

                    if (theObject != null)
                    {
                        _completionShowingMethods = true;

                        if (_completionWindow != null)
                        {
                            _completionWindow.Hide();
                        }

                        _completionWindow = new CompletionWindow(textEditor.TextArea);
                        _completionWindow.SizeToContent = SizeToContent.WidthAndHeight;
                        _completionWindow.MaxHeight = 200;

                        IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;
                        var completions = new List<MyCompletionData>();

                        if (word.ToLower() == "_base")
                        {
                            string comma = "";
                            var sbDescription = new StringBuilder();

                            sbDescription.Append("(");
                            ClassInfo theClass = theObject as ClassInfo;

                            if (theClass.Constructor?.CodeParameters?.Count() > 0)
                            {
                                foreach (var parameter in theClass.Constructor.CodeParameters)
                                {
                                    sbDescription.Append(comma);
                                    sbDescription.Append(parameter.Name);
                                    sbDescription.Append(" as ");
                                    sbDescription.Append(parameter.TypeName);
                                    comma = ", ";
                                }
                            }

                            sbDescription.Append(")");

                            completions.Add(new MyCompletionData("Constructor", "Constructor " + sbDescription));
                        }

                        foreach (var method in theObject.Methods)
                        {
                            if (method.Accessability == Accessability.Public || isSelf)
                            {
                                var sbDescription = new StringBuilder();

                                if (method.ReturnValueTypeName != "Nothing")
                                {
                                    sbDescription.Append("[");
                                    sbDescription.Append(method.ReturnValueTypeName);
                                    sbDescription.Append("] ");
                                }

                                sbDescription.Append(method.Name);

                                string comma = "";
                                sbDescription.Append("(");

                                if (method.CodeParameters?.Count() > 0)
                                {
                                    foreach (var parameter in method.CodeParameters)
                                    {
                                        sbDescription.Append(comma);
                                        sbDescription.Append(parameter.Name);
                                        sbDescription.Append(" as ");
                                        sbDescription.Append(parameter.TypeName);
                                        comma = ", ";
                                    }
                                }

                                sbDescription.Append(")");

                                completions.Add(new MyCompletionData(method.Name, sbDescription.ToString()));
                            }
                        }

                        foreach (var property in theObject.Properties)
                        {
                            var sbDescription = new StringBuilder();

                            if (property.ReturnValueTypeName != "Nothing")
                            {
                                sbDescription.Append("[");
                                sbDescription.Append(property.ReturnValueTypeName);
                                sbDescription.Append("] ");

                                sbDescription.Append(property.Name);
                            }

                            completions.Add(new MyCompletionData(property.Name, sbDescription.ToString()));
                        }

                        foreach (var completion in completions.OrderBy(c => c.Text))
                        {
                            data.Add(completion);
                        }

                        _completionWindow.Show();
                        _completionWindow.Closed += delegate
                        {
                            _completionShowingMethods = false;
                            _completionWindow = null;
                        };
                    }
                }
                else if (isSqx && e.Text == " ")
                {
                    if (_completionWindow != null)
                    {
                        _completionWindow.Hide();
                    }

                    word = GetWordBeforeDotReadBackwards(textEditor.Text, textEditor.SelectionStart - 1).ToLower();

                    if ((word == "new" || word == "method" || word == "property" || word == "as" || word == "is"))
                    {
                        string inNamespace = GetCurrentNamespaceName(textEditor.Text, textEditor.SelectionStart).ToLower();
                        var usings = GetCurrentUsings(textEditor.Text, textEditor.SelectionStart);

                        _completionShowingNewAlternatives = true;

                        _completionWindow = new CompletionWindow(textEditor.TextArea);
                        _completionWindow.SizeToContent = SizeToContent.WidthAndHeight;
                        _completionWindow.MaxHeight = 200;

                        IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;

                        foreach (var completion in GetPossibleTypeNames(GetCurrentNamespaceName(textEditor.Text, textEditor.SelectionStart), MyContext.DeclaredTypes, usings, word != "new", word != "new"))
                        {
                            data.Add(completion);
                        }

                        _completionWindow.Show();
                        _completionWindow.Closed += delegate
                        {
                            _completionShowingNewAlternatives = false;
                            _completionWindow = null;
                        };
                    }
                    else if (word == "using" && MyContext.DeclaredTypes != null && MyContext.DeclaredTypes.Count() > 0)
                    {
                        var usings = GetCurrentUsings(textEditor.Text, textEditor.SelectionStart);

                        _completionShowingNewAlternatives = true;

                        _completionWindow = new CompletionWindow(textEditor.TextArea);
                        _completionWindow.SizeToContent = SizeToContent.WidthAndHeight;
                        _completionWindow.MaxHeight = 200;

                        IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;

                        foreach (string namespaceName in MyContext.DeclaredTypes.Select(c => c.NamespaceName).Distinct())
                        {
                            if (!usings.Select(u => u.ToLower()).Contains(namespaceName.ToLower()))
                            {
                                data.Add(new MyCompletionData(namespaceName));
                            }
                        }

                        if (_completionWindow.CompletionList.CompletionData.Count() > 0)
                        {
                            _completionWindow.Show();
                            _completionWindow.Closed += delegate
                            {
                                _completionShowingNewAlternatives = false;
                                _completionWindow = null;
                            };
                        }
                        else
                        {
                            _completionShowingNewAlternatives = false;
                            _completionWindow = null;
                        }
                    }
                    else
                    {
                        if (_completionWindow != null)
                        {
                            _completionWindow.Close();
                        }
                    }
                }
                else if (_completionWindow == null && e.Text.Length > 0 && (char.IsLetterOrDigit(e.Text[0]) || e.Text[0] == '_'))
                {
                    CollectCompletionWords(textEditor.Text, textEditor.SelectionStart);

                    // Do not suggest autocompletion in private declarations
                    if (Regex.IsMatch(lowerLine.Trim(), @"private\s+\""") || Regex.IsMatch(lowerLine.Trim(), @"private\s+\[") || lowerLine.StartsWith("params") || InComment(textEditor.Text, textEditor.SelectionStart))
                    {
                        return;
                    }

                    if (word.Length >= 3 && _allCompletionWords.Count(x => x.Text.StartsWith(word)) > 0)
                    {
                        _completionWindow = new CompletionWindow(textEditor.TextArea);
                        _completionWindow.StartOffset = textEditor.SelectionStart - word.Length;
                        _completionWindow.SizeToContent = SizeToContent.WidthAndHeight;
                        _completionWindow.MaxHeight = 200;
                        //_completionWindow
                        IList<ICompletionData> data = _completionWindow.CompletionList.CompletionData;

                        foreach (MyCompletionData completionData in _allCompletionWords.OrderBy(x => x.Text))
                        {
                            data.Add(completionData);
                        }

                        _completionWindow.Show();
                        _completionWindow.CompletionList.SelectItem(word);
                        _completionWindow.Closed += delegate
                        {
                            _completionWindow = null;
                        };
                    }
                }
                else if (e.Text == " ")
                {
                    if (_completionWindow != null)
                    {
                        _completionWindow.Close();
                    }
                }
            }
            catch
            {
                if (!CurrentApplication.IsRelease)
                {
                    throw;
                }

                if (_completionWindow != null)
                {
                    _completionWindow.Close();
                    _completionWindow = null;
                }
            }
        }

        public static IEnumerable<TypeInfo> GetPossibleTypes(string inNamespaceName, List<TypeInfo> declaredTypes, IEnumerable<string> usings)
        {
            var resultList = new List<TypeInfo>();

            if (declaredTypes == null)
            {
                return resultList;
            }

            // Put all namespace names in one distinct list
            var usingsIncludingCurrentNamespace = new List<string>();

            foreach (var sUsing in usings)
            {
                if (!usingsIncludingCurrentNamespace.Contains(sUsing))
                {
                    usingsIncludingCurrentNamespace.Add(sUsing.ToLower());
                }
            }

            if (!usingsIncludingCurrentNamespace.Contains(inNamespaceName))
            {
                usingsIncludingCurrentNamespace.Add(inNamespaceName.ToLower());
            }

            // Gather all valid classas in one distinct list
            var validTypes = new List<TypeInfo>();   // All that are allowed to be written with only the class name
            var invalidTypes = new List<TypeInfo>(); // The ones that need to be specified with the full namespace path

            foreach (var classInfo in declaredTypes)
            {
                if (string.IsNullOrEmpty(classInfo.NamespaceName) || usingsIncludingCurrentNamespace.Contains(classInfo.NamespaceName.ToLower()))
                {
                    validTypes.Add(classInfo);
                }
                else
                {
                    invalidTypes.Add(classInfo);
                }
            }

            foreach (var typeInfo in validTypes)
            {
                var types = validTypes.Where(c => c.Name.ToLower() == typeInfo.Name.ToLower()).ToList();
                //int count = declaredClasses.Count(c => c.Name.ToLower() == classInfo.Name.ToLower());

                // If the class exists in one version
                if (types.Count() == 1)
                {
                    // If the class is in the current namespace or if there is a valid using, just use the simple name
                    if (typeInfo.NamespaceName.ToLower() == inNamespaceName.ToLower() || usings.Select(u => u.ToLower()).Contains(typeInfo.NamespaceName.ToLower()))
                    {
                        resultList.Add(typeInfo);
                    }
                    else // If the class is in another namespace that is not used, use the full name
                    {
                        //resultList.Add(new MyCompletionData(classInfo.FullName, classInfo.NamespaceName));
                    }
                }
                else
                {
                    resultList.Add(typeInfo);
                }
            }

            foreach (var classInfo in invalidTypes)
            {
                //resultList.Add(new MyCompletionData(classInfo.FullName, classInfo.NamespaceName));
            }

            return resultList.ToArray();
        }

		private IEnumerable<MyCompletionData> GetPossibleTypeNames(string inNamespaceName, List<TypeInfo> declaredTypes, IEnumerable<string> usings, bool includeNativeTypes, bool includeInterfaces)
		{
			var resultList = new List<MyCompletionData>();

			// Put all namespace names in one distinct list
			var usingsIncludingCurrentNamespace = new List<string>();

			foreach (var sUsing in usings)
			{
				if (!usingsIncludingCurrentNamespace.Contains(sUsing))
				{
					usingsIncludingCurrentNamespace.Add(sUsing.ToLower());
				}
			}

			if (!usingsIncludingCurrentNamespace.Contains(inNamespaceName))
			{
				usingsIncludingCurrentNamespace.Add(inNamespaceName.ToLower());
			}

			// Gather all valid classas in one distinct list
			var validTypes = new List<TypeInfo>();   // All that are allowed to be written with only the class name
			var invalidTypes = new List<TypeInfo>(); // The ones that need to be specified with the full namespace path

			foreach (var classInfo in declaredTypes)
			{
				if (string.IsNullOrEmpty(classInfo.NamespaceName) || usingsIncludingCurrentNamespace.Contains(classInfo.NamespaceName.ToLower()))
				{
					validTypes.Add(classInfo);
				}
				else
				{
					invalidTypes.Add(classInfo);
				}
			}

			foreach (var classInfo in validTypes)
			{
				if (!includeInterfaces && classInfo is InterfaceInfo)
				{
					continue;
				}

				var classes = validTypes.Where(c => c.Name.ToLower() == classInfo.Name.ToLower()).ToList();
				//int count = declaredClasses.Count(c => c.Name.ToLower() == classInfo.Name.ToLower());

				// If the class exists in one version
				if (classes.Count() == 1)
				{
					// If the class is in the current namespace or if there is a valid using, just use the simple name
					if (classInfo.NamespaceName.ToLower() == inNamespaceName.ToLower() || usings.Select(u => u.ToLower()).Contains(classInfo.NamespaceName.ToLower()))
					{
						resultList.Add(new MyCompletionData(classInfo.Name, classInfo.NamespaceName));
					}
					else // If the class is in another namespace that is not used, use the full name
					{
						//resultList.Add(new MyCompletionData(classInfo.FullName, classInfo.NamespaceName));
					}
				}
				else
				{
					resultList.Add(new MyCompletionData(classInfo.FullName, classInfo.NamespaceName));
				}
			}

			foreach (var classInfo in invalidTypes)
			{
				//resultList.Add(new MyCompletionData(classInfo.FullName, classInfo.NamespaceName));
			}

			if (includeNativeTypes)
			{
				resultList.Add(new MyCompletionData("Array", "Native type"));
				resultList.Add(new MyCompletionData("Boolean", "Native type"));
				resultList.Add(new MyCompletionData("Group", "Native type"));
				resultList.Add(new MyCompletionData("Scalar", "Native type"));
				resultList.Add(new MyCompletionData("Object", "Native type"));
				resultList.Add(new MyCompletionData("Side", "Native type"));
				resultList.Add(new MyCompletionData("String", "Native type"));
				resultList.Add(new MyCompletionData("Code", "Native type"));
				resultList.Add(new MyCompletionData("Config", "Native type"));
				resultList.Add(new MyCompletionData("Control", "Native type"));
				resultList.Add(new MyCompletionData("Display", "Native type"));
				resultList.Add(new MyCompletionData("Script", "Native type"));
				resultList.Add(new MyCompletionData("Task", "Native type"));
				resultList.Add(new MyCompletionData("Any", "Native type"));
			}

			return resultList.OrderBy(rl => rl);
		}

		public static string GetWordReadBackwards(string text, int lastPos)
        {
            int startPos = 0;

            for (int i = lastPos; i >= 0; i--)
            {
                char ch = text[i];
                if (!char.IsLetterOrDigit(text[i]) && text[i] != '_')
                {
                    startPos = i + 1;
                    break;
                }
            }

            return text.Substring(startPos, lastPos - startPos);
        }

        public static string GetWordBeforeDotReadBackwards(string text, int dotPos)
        {
            int startPos = 0;

            for (int i = dotPos - 1; i >= 0; i--)
            {
                char ch = text[i];
                if (!char.IsLetterOrDigit(text[i]) && text[i] != '_' && text[i] != '.')
                {
                    startPos = i + 1;
                    break;
                }
            }

            return text.Substring(startPos, dotPos - startPos);
        }

        public static string GetLineReadBackwards(string text, int curPos)
        {
            int startPos = 0;

            if (text[curPos] == '\n')
            {
                return "";
            }

            for (int i = curPos; i >= 0; i--)
            {
                char ch = text[i];
                if (ch == '\n')
                {
                    startPos = i + 1;
                    break;
                }
            }

            return text.Substring(startPos, curPos - startPos);
        }

        private List<MyCompletionData> _scriptCommandCompletions = null;

        private void CollectScriptCompletions()
        {
            if (_scriptCommandCompletions != null)
            {
                return;
            }

            _scriptCommandCompletions = new List<MyCompletionData>();

            MyCompletionData lastData = null;
            foreach (var scriptCommand in CodeAnalyzer.GetScriptCommandDefinitionCollection().OrderBy(x => x.Name))
            {
                string text = "";
                string description = "";
                string returnName = "result";
                var sbPreArgParamList = new StringBuilder();
                var sbPostArgParamList = new StringBuilder();
                int paramNo = 1;

                if (!string.IsNullOrWhiteSpace(scriptCommand.Description))
                {
                    text += scriptCommand.Description + Environment.NewLine + Environment.NewLine;
                }

                if (scriptCommand.ReturnValueDataType != SqfDataType.Nothing)
                {
                    if (!string.IsNullOrWhiteSpace(scriptCommand.ReturnParamName))
                    {
                        returnName = scriptCommand.ReturnParamName;
                    }

                    text += returnName + " (" + scriptCommand.ReturnValueDataType + ") = ";
                }

                if (scriptCommand.PreArgument.DataType != SqfDataType.Nothing)
                {
                    if (scriptCommand.PreArgument.ParamInfos != null && scriptCommand.PreArgument.ParamInfos.Length > 0)
                    {
                        var sb = new StringBuilder();
                        string comma = "";

                        if (scriptCommand.PreArgument.DataType == SqfDataType.Array)
                        {
                            if (!(scriptCommand.PreArgument.ParamInfos.Length == 1 && scriptCommand.PreArgument.ParamInfos[0].DataType == SqfDataType.Array))
                            {
                                sb.Append("[");
                            }
                        }

                        foreach (var param in scriptCommand.PreArgument.ParamInfos)
                        {
                            sb.Append(comma);
                            sbPreArgParamList.AppendLine();

                            if (string.IsNullOrWhiteSpace(param.Name))
                            {
                                sb.Append("param" + paramNo);
                                sbPreArgParamList.Append("param" + paramNo);
                            }
                            else
                            {
                                sb.Append(param.Name);
                                sbPreArgParamList.Append(param.Name);
                            }

                            sbPreArgParamList.Append(": ");
                            sbPreArgParamList.Append(param.DataType);

                            if (param.IsOptional)
                            {
                                sbPostArgParamList.Append(" - (optional)");
                            }

                            comma = ", ";
                        }

                        if (scriptCommand.PreArgument.DataType == SqfDataType.Array)
                        {
                            if (!(scriptCommand.PreArgument.ParamInfos.Length == 1 && scriptCommand.PreArgument.ParamInfos[0].DataType == SqfDataType.Array))
                            {
                                sb.Append("]");
                            }
                        }

                        text += sb + " ";
                    }
                    else
                    {
                        sbPreArgParamList.AppendLine();
                        if (scriptCommand.PreArgument.DataType == SqfDataType.Array)
                        {
                            text += "[params] ";
                            sbPreArgParamList.Append("[params]");
                        }
                        else if (scriptCommand.PreArgument.DataType == SqfDataType.Any)
                        {
                            text += "param(s) ";
                            sbPreArgParamList.Append("param(s)");
                        }
                        else
                        {
                            text += "param" + paramNo + " ";
                            sbPreArgParamList.Append("param" + paramNo);
                            paramNo++;
                        }

                        sbPreArgParamList.Append(": ");
                        sbPreArgParamList.Append(scriptCommand.PreArgument.DataType);
                    }
                }

                text += scriptCommand.Name;

                if (scriptCommand.PostArgument.DataType != SqfDataType.Nothing)
                {
                    text += " ";
                    //sbPostArgParamList.AppendLine();
                    //sbPostArgParamList.Append("- Post Argument(s):");

                    if (scriptCommand.PostArgument.ParamInfos != null && scriptCommand.PostArgument.ParamInfos.Length > 0)
                    {
                        var sb = new StringBuilder();
                        string comma = "";

                        if (scriptCommand.PostArgument.DataType == SqfDataType.Array)
                        {
                            if (!(scriptCommand.PostArgument.ParamInfos.Length == 1 && scriptCommand.PostArgument.ParamInfos[0].DataType == SqfDataType.Array))
                            {
                                sb.Append("[");
                            }
                        }

                        foreach (var param in scriptCommand.PostArgument.ParamInfos)
                        {
                            sb.Append(comma);
                            sbPostArgParamList.AppendLine();

                            if (string.IsNullOrWhiteSpace(param.Name))
                            {
                                sb.Append("param" + paramNo);
                                sbPostArgParamList.Append("param" + paramNo);
                                paramNo++;
                            }
                            else
                            {
                                sb.Append(param.Name);
                                sbPostArgParamList.Append(param.Name);
                            }

                            sbPostArgParamList.Append(": ");
                            sbPostArgParamList.Append(param.DataType);

                            if (param.IsOptional)
                            {
                                sbPostArgParamList.Append(" - (optional)");
                            }

                            comma = ", ";
                        }

                        if (scriptCommand.PostArgument.DataType == SqfDataType.Array)
                        {
                            if (!(scriptCommand.PostArgument.ParamInfos.Length == 1 && scriptCommand.PostArgument.ParamInfos[0].DataType == SqfDataType.Array))
                            {
                                sb.Append("]");
                            }
                        }

                        text += sb + " ";
                    }
                    else
                    {
                        sbPostArgParamList.AppendLine();
                        if (scriptCommand.PostArgument.DataType == SqfDataType.Array)
                        {
                            text += "[params]";
                            sbPostArgParamList.Append("[params]");
                        }
                        else if (scriptCommand.PostArgument.DataType == SqfDataType.Any)
                        {
                            text += "param(s)";
                            sbPostArgParamList.Append("param(s)");
                        }
                        else
                        {
                            text += "param" + paramNo;
                            sbPostArgParamList.Append("param" + paramNo);
                            paramNo++;
                        }

                        sbPostArgParamList.Append(": ");
                        sbPostArgParamList.Append(scriptCommand.PostArgument.DataType);
                    }
                }

                if (!string.IsNullOrWhiteSpace(scriptCommand.Description))
                {
                    text += Environment.NewLine; // Space after syntax
                }

                //if (scriptCommand.ReturnValueDataType != SqfDataType.Nothing)
                //{
                //    description += Environment.NewLine;
                //    description += "Returns: ";
                //    description += returnName + ": " + scriptCommand.ReturnValueDataType;
                //}

                if (sbPreArgParamList.Length > 0)
                {
                    text += sbPreArgParamList;
                }

                if (sbPostArgParamList.Length > 0)
                {
                    text += sbPostArgParamList;
                }

                lastData = new MyCompletionData(scriptCommand.Name, text.Trim());
                _scriptCommandCompletions.Add(lastData);
            }
        }

        private void CollectCompletionWords(string editorText, int currentIndex)
        {
            try
            {
                _allCompletionWords = new List<MyCompletionData>(1000);

                // Script commands
                CollectScriptCompletions();
                foreach (var scriptCommandCompletion in _scriptCommandCompletions)
                {
                    _allCompletionWords.Add(scriptCommandCompletion);
                }

                // Public variables
                if (MyContext.DeclaredPublicVariables != null)
                {
                    foreach (var publicVariable in MyContext.DeclaredPublicVariables)
                    {
                        _allCompletionWords.Add(new MyCompletionData(publicVariable.Name));
                    }
                }

				// Custom Object Types
				var usings = GetCurrentUsings(editorText, currentIndex);
				var inNamespaceName = GetCurrentNamespaceName(editorText, currentIndex);
				var possibleTypes = GetPossibleTypes(inNamespaceName, MyContext.DeclaredTypes, usings);
				if (MyContext.DeclaredTypes != null)
				{
					foreach (var obj in MyContext.DeclaredTypes)
					{
						_allCompletionWords.Add(new MyCompletionData(obj.FullName));

						if (possibleTypes.Select(x => x.FullName).Contains(obj.FullName))
						{
							_allCompletionWords.Add(new MyCompletionData(obj.Name));
						}
					}
				}

				_allCompletionWords.Add(new MyCompletionData("player"));

                if (MyContext.DeclaredPrivateVariables != null)
                {
                    foreach (var privateVariable in MyContext.DeclaredPrivateVariables)
                    {
                        if (MyContext.ActiveTab != null && MyContext.ActiveTab.AbsoluteFilePathName != null && privateVariable.FileName.ToLower() == MyContext.ActiveTab.AbsoluteFilePathName.ToLower())
                        {
                            _allCompletionWords.Add(new MyCompletionData(privateVariable.Name));
                        }
                    }
                }

                // Convert all to completion data
                bool isSqx = MyContext.ActiveTabIndex >= 0 && MyContext.Tabs[MyContext.ActiveTabIndex].Name.ToLower().EndsWith(".sqx");
                foreach (var keyword in CodeAnalyzer.GetKeywords(isSqx))
                {
                    _allCompletionWords.Add(new MyCompletionData(keyword));
                }
            }
            catch (InvalidOperationException ex)
            {
                // Can happen that the DeclaredPublicVariables array changes in the middle of the operation.
                // If that happens, just continue...
            }
        }

        private string GetDeclaredVariableType(string variableName, string text, int currentIndex)
        {
            var matches = Regex.Matches(text, @"(?<=""" + variableName + @"""\s+as\s+)[A-Za-z0-9.]+", RegexOptions.IgnoreCase);
            Match aboveMatch = null;

            foreach (Match match in matches)
            {
                if (aboveMatch == null)
                {
                    aboveMatch = match;
                }
                else
                {
                    if (match.Index < currentIndex && match.Index > aboveMatch.Index)
                    {
                        aboveMatch = match;
                    }
                }
            }

            string typeName = "";
            if (aboveMatch != null && aboveMatch.Index < currentIndex)
            {
                typeName = aboveMatch.Value;

                if (!typeName.Contains("."))
                {
                    string inNamespaceName = GetCurrentNamespaceName(text, currentIndex);
                    string inNamespaceClassName = string.IsNullOrEmpty(inNamespaceName) ? typeName : inNamespaceName + "." + typeName;
                    if (MyContext.DeclaredTypes.Select(c => c.FullName.ToLower()).Contains(inNamespaceClassName.ToLower()))
                    {
                        return inNamespaceClassName;
                    }

                    var usings = GetCurrentUsings(text, currentIndex);
                    foreach (var sUsing in usings)
                    {
                        string usingClassName = string.IsNullOrEmpty(sUsing) ? typeName : sUsing + "." + typeName;
                        if (MyContext.DeclaredTypes.Select(c => c.FullName.ToLower()).Contains(usingClassName.ToLower()))
                        {
                            return usingClassName;
                        }
                    }
                }
            }

            return typeName;
        }

        public static string GetCurrentNamespaceName(string text, int currentIndex)
        {
            var matches = Regex.Matches(text, @"(?<=namespace\s+)[A-Za-z0-9_.]+(?=\s+{)", RegexOptions.IgnoreCase);
            Match aboveMatch = null;

            foreach (Match match in matches)
            {
                if (aboveMatch == null)
                {
                    aboveMatch = match;
                }
                else
                {
                    if (match.Index < currentIndex && match.Index > aboveMatch.Index)
                    {
                        aboveMatch = match;
                    }
                }
            }

            if (aboveMatch != null && aboveMatch.Index < currentIndex)
            {
                return aboveMatch.Value;
            }

            return "";
        }

        private string GetCurrentClassName(string text, int currentIndex)
        {
            var matches = Regex.Matches(text, @"(?<=public\s+class\s+)[A-Za-z0-9_.]+", RegexOptions.IgnoreCase);
            Match aboveMatch = null;

            foreach (Match match in matches)
            {
                if (aboveMatch == null)
                {
                    aboveMatch = match;
                }
                else
                {
                    if (match.Index < currentIndex && match.Index > aboveMatch.Index)
                    {
                        aboveMatch = match;
                    }
                }
            }

            if (aboveMatch != null && aboveMatch.Index < currentIndex)
            {
                return aboveMatch.Value;
            }

            return "";
        }

        public static IEnumerable<string> GetCurrentUsings(string text, int currentIndex)
        {
            var matches = Regex.Matches(text, @"(?<=using\s+)[A-Za-z0-9.]+(?=\s*;)", RegexOptions.IgnoreCase);
            var usings = new List<string>();

            foreach (Match match in matches)
            {
                if (match.Index < currentIndex && !InComment(text, match.Index))
                {
                    usings.Add(match.Value);
                }
            }

            return usings;
        }

        private void TabItem_TabLosingFocus(object sender, TabLosingFocusEventArgs e)
        {
            if (MyContext.ActiveTabIndex >= 0)
            {
                var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");

                if (TheFoldingManager != null)
                {
                    if (e.IsClosed)
                    {
                        FoldingManager.Uninstall(TheFoldingManager);
                        TheFoldingManager = null;
                    }
                }

                if (textEditor != null)
                {
                    var activeTab = MyContext.Tabs[MyContext.ActiveTabIndex];
                    activeTab.VerticalOffset = textEditor.VerticalOffset;
                    activeTab.SelectionStart = textEditor.SelectionStart;
                    activeTab.SelectionLength = textEditor.SelectionLength;
                    activeTab.CaretOffset = textEditor.CaretOffset;

                    if (TheFoldingManager != null && !e.IsClosed)
                    {
                        var currentFoldingSections = new List<TypeSqfFoldingSection>();

                        foreach (var foldingSection in TheFoldingManager.AllFoldings)
                        {
                            currentFoldingSections.Add(new TypeSqfFoldingSection(foldingSection.StartOffset, foldingSection.EndOffset, foldingSection.IsFolded));
                            activeTab.FoldingSections = currentFoldingSections;
                        }
                    }
                }
            }
        }

        private void AboutMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            //Analyzer.WriteScriptCommandsToDisk();
            //MyContext.RemoveProjectFilesThatDoNotExist();
            MessageBox.Show(this, "TypeSqf Editor " + CurrentApplication.Version + Environment.NewLine + "SQF/SQX Analyzer and Compiler " + CodeAnalyzer.Version + Environment.NewLine + Environment.NewLine +" by Engima", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private FindInAllFilesWindow FindInAllFilesWindow { get; set; }

        private SearchResultItem LastFoundItem { get; set; }

        private void FindInAllFilesExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (FindInAllFilesWindow == null)
            {
                FindInAllFilesWindow = new FindInAllFilesWindow(this);
                FindInAllFilesWindow.Owner = this;
            }

            FindInAllFilesViewModel viewModel = FindInAllFilesWindow.DataContext as FindInAllFilesViewModel;
            viewModel.ProjectPath = MyContext.ProjectRootDirectory;

            FindInAllFilesWindow.ShowDialog();

            SearchResultItem selectedItem = FindInAllFilesWindow.MyContext.SelectedItem;

            if (FindInAllFilesWindow.UserHasNavigated)
            {
                MyContext.OpenFileInTab(selectedItem.FileName);
                LastFoundItem = selectedItem;

                if (MyContext.ActiveTabIndex >= 0 && !_findInAllFilesTimerActive)
                {
                    _findInAllFilesTimerActive = true;
                    DispatcherTimer dispatcherTimer = new DispatcherTimer();
                    dispatcherTimer.Tick += new EventHandler(FindInAllFiles_DispatcherTimer_Tick);
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
                    dispatcherTimer.Start();
                }
            }
        }

        private bool _findInAllFilesTimerActive = false;
        private void FindInAllFiles_DispatcherTimer_Tick(object sender, EventArgs e)
        {
            var timer = sender as DispatcherTimer;
            timer.Stop();

            var textEditor = FindVisualChildByName<TextEditor>(TabControl, "TheTextEditor");
            if (textEditor != null)
            {
                textEditor.Select(LastFoundItem.Matches[0].Index, LastFoundItem.Matches[0].Length);
                textEditor.ScrollToLine(LastFoundItem.Matches[0].LineNo);
                textEditor.Focus();
            }

            _findInAllFilesTimerActive = false;
        }

        private void ExitCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void ExitCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void CPackLibraryMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://typesqf.no-ip.org/cpack");
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(this);

            settingsWindow.MyContext.EnableAutoCompletion = MyContext.Settings.EnableAutoCompletion;
            settingsWindow.MyContext.EnableFolding = MyContext.Settings.EnableFolding;
            settingsWindow.MyContext.SelectedThemeName = MyContext.Settings.SelectedTheme;
            settingsWindow.MyContext.IndentationSize = MyContext.Settings.IndentationSize;
            settingsWindow.MyContext.ConvertTabsToSpaces = MyContext.Settings.ConvertTabsToSpaces;
            settingsWindow.MyContext.AddMethodCallLogging = MyContext.Settings.AddMethodCallLogging;
            settingsWindow.MyContext.Deployment1Name = MyContext.Settings.Deployment1Name;
            settingsWindow.MyContext.Deployment1Directory = MyContext.Settings.Deployment1Directory;
            settingsWindow.MyContext.Deployment2Name = MyContext.Settings.Deployment2Name;
            settingsWindow.MyContext.Deployment2Directory = MyContext.Settings.Deployment2Directory;

            settingsWindow.ShowDialog();

            if (!settingsWindow.MyContext.IsCanceled)
            {
                MyContext.Settings.EnableAutoCompletion = settingsWindow.MyContext.EnableAutoCompletion;
                MyContext.Settings.EnableFolding = settingsWindow.MyContext.EnableFolding;
                MyContext.Settings.SelectedTheme = settingsWindow.MyContext.SelectedThemeName;
                MyContext.Settings.IndentationSize = settingsWindow.MyContext.IndentationSize;
                MyContext.Settings.ConvertTabsToSpaces = settingsWindow.MyContext.ConvertTabsToSpaces;
                MyContext.Settings.AddMethodCallLogging = settingsWindow.MyContext.AddMethodCallLogging;
                MyContext.Settings.Deployment1Name = settingsWindow.MyContext.Deployment1Name;
                MyContext.Settings.Deployment1Directory = settingsWindow.MyContext.Deployment1Directory;
                MyContext.Settings.Deployment2Name = settingsWindow.MyContext.Deployment2Name;
                MyContext.Settings.Deployment2Directory = settingsWindow.MyContext.Deployment2Directory;

                SyntaxHighlightingHandler.LoadTheme(MyContext.Settings.SelectedTheme);
                ApplySyntaxHighlighting();
                ApplyNewSettings();
                MyContext.SaveSettings();

                UpdateFoldings();
            }
        }

        private void SqxReferenceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://typesqf.no-ip.org/sqxreference");
        }

        private void TabControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string fileName in fileNames)
                {
                    MyContext.OpenFileInTab(fileName);
                }
            }
        }

        private void ProjectTreeView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string fileName in fileNames)
                {
                    if (Path.GetExtension(fileName).ToLower() == ".tproj")
                    {
                        MyContext.OpenProject(fileName);
                        return;
                    }

                    MyContext.AddExistingFileNode(fileName);
                }
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var eventArgs = new System.ComponentModel.CancelEventArgs();
            MyContext.OnWindowClosing(this, eventArgs);

            if (!eventArgs.Cancel)
            {
                Environment.Exit(0);
            }
        }

		private void ProjectTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (treeViewItem != null)
			{
				treeViewItem.Focus();
				e.Handled = true;
			}
		}

		static TreeViewItem VisualUpwardSearch(DependencyObject source)
		{
			while (source != null && !(source is TreeViewItem))
			{
				source = VisualTreeHelper.GetParent(source);
			}

			return source as TreeViewItem;
		}

		private void TypeSqfFeatures1MenuItem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=DWPHrDLyiLw");
		}

		private void TypeSqfFeatures2MenuItem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=wuAc8I0ok3w");
		}

		private void TypeSqfFeatures3MenuItem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=D8flQMvHz5Y");
		}

		private void VideoCrowdClassMenuItem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=TGdkcLIPkcA");
		}

		private void VideoEngimaCivilians_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=OVirxev6wjw");
		}

        private void TypeSqfFeatures4MenuItem_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=L6LDWErn7I4");
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ProjectPropertiesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var window = new ProjectPropertiesWindow(this);

            window.MyContext.EnableAutoCompletion = MyContext.Settings.EnableAutoCompletion;
            window.MyContext.EnableFolding = MyContext.Settings.EnableFolding;
            window.MyContext.SelectedThemeName = MyContext.Settings.SelectedTheme;
            window.MyContext.IndentationSize = MyContext.Settings.IndentationSize;
            window.MyContext.ConvertTabsToSpaces = MyContext.Settings.ConvertTabsToSpaces;
            window.MyContext.AddMethodCallLogging = MyContext.Settings.AddMethodCallLogging;

            window.ShowDialog();

            if (!window.MyContext.IsCanceled)
            {
                MyContext.Settings.EnableAutoCompletion = window.MyContext.EnableAutoCompletion;
                MyContext.Settings.EnableFolding = window.MyContext.EnableFolding;
                MyContext.Settings.SelectedTheme = window.MyContext.SelectedThemeName;
                MyContext.Settings.IndentationSize = window.MyContext.IndentationSize;
                MyContext.Settings.ConvertTabsToSpaces = window.MyContext.ConvertTabsToSpaces;
                MyContext.Settings.AddMethodCallLogging = window.MyContext.AddMethodCallLogging;

                SyntaxHighlightingHandler.LoadTheme(MyContext.Settings.SelectedTheme);
                ApplySyntaxHighlighting();
                ApplyNewSettings();
                MyContext.SaveSettings();

                UpdateFoldings();
            }
        }
    }
}
