using SwiftPbo;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using TypeSqf.Analyzer;
using TypeSqf.Analyzer.Commands;
using TypeSqf.Edit.Highlighting;
using TypeSqf.Edit.Services;
using TypeSqf.Model;
using TypeSqf.WebService;

namespace TypeSqf.Edit
{
    public partial class MainWindowViewModel : INotifyPropertyChanged
    {
        //----------------------------------------------------------------------------------------------------------------
        #region Private variables

        private bool _windowHasActivatedFirstTime;
        private readonly BackgroundWorker _analyzerWorker;
        private readonly BackgroundWorker _compilerWorker;
        private int _activeTabIndex;
        private bool _startAnalyzerWhenPossible;
        private bool _startCompilerWhenPossible;
        private string _startCompilerWhenPossibleFileName = "";
        private TabViewModel _activeTab;
        private ProjectViewModel _project;
        private List<GlobalContextVariable> _declaredPublicVariables = new List<GlobalContextVariable>();
        private List<GlobalContextVariable> _declaredPrivateVariables = new List<GlobalContextVariable>();
        private List<TypeInfo> _declaredTypes = null;
        private string _newVersionMessage = "";
        private Visibility _newVersionVisibility = Visibility.Hidden;
        private bool _runOnTabGettingFocus = true;
        private bool _runOnTabLosingFocus = true;
        private List<string> _tabOpenedOrder = new List<string>();

        public EventHandler TabGettingFocus;
        public TabLosingFocusEventHandler TabLosingFocus;

        public delegate void TabLosingFocusEventHandler(object sender, TabLosingFocusEventArgs e);

        public enum ResultTabs : int {
            Analyzer = 0,
            Compiler = 1
        }

        protected void OnTabGettingFocus()
        {
            if (TabGettingFocus != null && _runOnTabGettingFocus)
            {
                try {
                    string fileName = Tabs[_activeTabIndex].AbsoluteFilePathName;
                    _tabOpenedOrder.Remove(fileName);
                    _tabOpenedOrder.Add(fileName);
                }
                catch
                {
                }

                TabGettingFocus(this, new EventArgs());
            }
        }

        protected void OnTabLosingFocus(bool isClosed)
        {
            if (TabLosingFocus != null && _runOnTabLosingFocus)
            {
                TabLosingFocus(this, new TabLosingFocusEventArgs(isClosed));
            }
        }

        protected void OnProjectChanged()
        {
            ProjectChanged?.Invoke(this, new EventArgs());
        }

        protected void OnStoppedWriting()
        {
            StoppedWriting?.Invoke(this, new EventArgs());
        }

        public event EventHandler ProjectChanged;
        public event EventHandler FileOpened;
        public event EventHandler StoppedWriting;

        protected void OnFileOpened()
        {
            FileOpened?.Invoke(this, new EventArgs());
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Construction

        public MainWindowViewModel()
        {
            // Check for associated file types sent in as argument.
            string commandLineProject = "";
            string commandLineFile = "";
            FilesToRemoveFromAnalyzer = new List<string>();
            MissionFileHasChanged = false;
            ProjectIsPrepared = false;

            try
            {
                foreach (string arg in Environment.GetCommandLineArgs())
                {
                    if (arg.ToLower().EndsWith(".tproj"))
                    {
                        commandLineProject = arg;
                    }
                    else if (!arg.ToLower().EndsWith(".exe") && !arg.ToLower().EndsWith(".dll") && File.Exists(arg))
                    {
                        commandLineFile = arg;
                    }
                }
            }
            catch
            {
            }

            //commandLineFile = Path.Combine(@"C:\Users\danie\Documents\Arma 3\missions\Intrusion.Stratis\Scripts\Client\Actions", "ActionInvitePlayerToGroup.sqf");

            FileWatcher = null;
            _windowHasActivatedFirstTime = false;
            Tabs = new ObservableCollection<TabViewModel>();
            Tabs.CollectionChanged += TabsOnCollectionChanged;
            ActiveTabIndex = -1;

            _analyzerWorker = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true,
            };

            _analyzerWorker.DoWork += AnalyzerWorkerOnDoWork;
            _analyzerWorker.ProgressChanged += AnalyzerWorkerProgressChanged;
            _analyzerWorker.RunWorkerCompleted += AnalyzerWorkerOnRunWorkerCompleted;

            AnalyzerListFileQueue = new List<string>();
            CompilerProgressBarMax = 100;

            _compilerWorker = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true,
            };

            _compilerWorker.DoWork += CompilerWorkerOnDoWork;
            _compilerWorker.RunWorkerCompleted += CompilerWorkerOnRunWorkerCompleted;
            _compilerWorker.ProgressChanged += CompilerWorkerProgressChanged;

            Project = new ProjectViewModel();

            try
            {
                XmlSerializer reader = new XmlSerializer(typeof(SettingsFile));

                using (StreamReader file = new StreamReader(Path.Combine(CurrentApplication.AppDataFolder, CurrentApplication.SettingsFileName)))
                {
                    Settings = (SettingsFile)reader.Deserialize(file);
                }
            }
            catch
            {
                Settings = new SettingsFile();
            }

            Deployment1MenuCaption = "Export to " + (string.IsNullOrWhiteSpace(Settings.Deployment1Name) ? "deployment folder #1" : Settings.Deployment1Name);
            Deployment2MenuCaption = "Export to " + (string.IsNullOrWhiteSpace(Settings.Deployment2Name) ? "deployment folder #2" : Settings.Deployment2Name);

            Settings.PropertyChanged += Settings_PropertyChanged;

            if (string.IsNullOrWhiteSpace(commandLineProject) && string.IsNullOrWhiteSpace(commandLineFile))
            {
                if (!string.IsNullOrEmpty(Settings.ProjectFileName))
                {
                    OpenProject(Settings.ProjectFileName);
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(commandLineProject))
                {
                    OpenProject(commandLineProject);
                }
                if (!string.IsNullOrEmpty(commandLineFile))
                {
                    OpenFileInTab(commandLineFile);
                    Settings.RemoveOpenFileNames();
                }
            }

            if (CurrentApplication.IsRelease)
            {
                Task<AppVersion> task = CurrentApplication.CheckNewVersionAsync();
                task.ContinueWith(version =>
                {
                    if (version.Result != null && version.Result > CurrentApplication.Version)
                    {
                        NewVersionMessage = "A new version (" + version.Result + ") is available. Click here to get it!";
                        NewVersionMessageVisibility = Visibility.Visible;
                    }
                });
            }

            string absoluteTemplateDirectoryName = Path.Combine(CurrentApplication.AppDataFolder, "Templates");
            if (!Directory.Exists(absoluteTemplateDirectoryName))
            {
                FileTemplateHandler.CreateDefaultFileTemplates();
            }

            // Create theme files
            try
            {
                string absoluteThemeDirectoryName = Path.Combine(CurrentApplication.AppDataFolder, "Themes");
                if (!Directory.Exists(absoluteThemeDirectoryName))
                {
                    // The Themes Directory
                    Directory.CreateDirectory(absoluteThemeDirectoryName);
                }

                // Default Light
                string fileName = Path.Combine(absoluteThemeDirectoryName, "Default Light.xml");
                if (!File.Exists(fileName)) {
                    SyntaxHighlightingHandler.WriteThemeToDisc(fileName, Theme.DefaultLight);
                }

                // Default Dark
                fileName = Path.Combine(absoluteThemeDirectoryName, "Default Dark.xml");
                if (!File.Exists(fileName))
                {
                    SyntaxHighlightingHandler.WriteThemeToDisc(fileName, Theme.DefaultDark);
                }
            }
            catch
            {
            }

            // Load theme
            string loadedTheme = SyntaxHighlightingHandler.LoadTheme(Settings.SelectedTheme);
            Settings.SelectedTheme = loadedTheme;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Deployment1Name")
            {
                Deployment1MenuCaption = "Export to " + (string.IsNullOrWhiteSpace(Settings.Deployment1Name) ? "deployment folder #1" : Settings.Deployment1Name);
            }
            else if (e.PropertyName == "Deployment2Name")
            {
                Deployment2MenuCaption = "Export to " + (string.IsNullOrWhiteSpace(Settings.Deployment2Name) ? "deployment folder #2" : Settings.Deployment2Name);
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Analyzer

        public void LoadScriptCommandFile()
        {
            // Load Script Commands
            try
            {
                FileAnalyzer.LoadScriptCommandsFromDisk();
            }
            catch (Exception ex)
            {
                UserMessageService.ShowMessage(ex.Message, "ScriptCommand.xml File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private List<string> AnalyzerListFileQueue { get; set; }

        private List<string> FilesToRemoveFromAnalyzer { get; set; }

        private bool MissionFileHasChanged { get; set; }

        private void StartAnalyzer()
        {
            string explicitFileName = "";
            if (AnalyzerListFileQueue != null && AnalyzerListFileQueue.Count > 0)
            {
                explicitFileName = AnalyzerListFileQueue[0];
            }

            StartAnalyzer(explicitFileName);
        }

        private bool ProjectIsPrepared { get; set; }

        private List<string> CloneList(List<string> items)
        {
            var clonedItems = new List<string>();

            foreach (var item in items)
            {
                clonedItems.Add(item);
            }

            return clonedItems;
        }

        private void StartAnalyzer(string fileName)
        {
            if (_analyzerWorker == null)
            {
                return;
            }

            _startAnalyzerWhenPossible = true;

            bool usingExplicitFileName = false;
            if (!string.IsNullOrEmpty(fileName))
            {
                usingExplicitFileName = true;

                if (!AnalyzerListFileQueue.Contains(fileName))
                {
                    AnalyzerListFileQueue.Add(fileName);
                }
            }

            if (!_analyzerWorker.IsBusy && !_compilerWorker.IsBusy && ((usingExplicitFileName && ActiveTabIndex >= 0) || !usingExplicitFileName))
            {
                bool prepareProject = Project != null && Project.ProjectRootNode != null && !ProjectIsPrepared;

                CompilerProgressBarValue = 0;

                if (prepareProject)
                {
                    var args2 = new AnalyzerWorkerArgs(AnalyzerStartReason.PrepareProject, ProjectRootDirectory, null, Project.FilteredAnalyzerResultItems.ToList(), CloneList(FilesToRemoveFromAnalyzer), MissionFileHasChanged);

                    // Reset files to add and remove.
                    FilesToRemoveFromAnalyzer = new List<string>();
                    MissionFileHasChanged = false;

                    _analyzerWorker.RunWorkerAsync(args2);
                    ProjectIsPrepared = true;
                    return;
                }
                else
                {
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = ActiveTabIndex >= 0 ? Tabs[ActiveTabIndex].AbsoluteFilePathName.ToLower() : "";
                    }

                    _startAnalyzerWhenPossible = prepareProject;

                    CodeFile codeFile = null;

                    if (prepareProject)
                    {
                        codeFile = null;
                    }
                    else if (usingExplicitFileName)
                    {
                        codeFile = new CodeFile(fileName.ToLower(), File.ReadAllText(fileName));
                    }
                    else
                    {
                        if (ActiveTabIndex < 0)
                        {
                            return;
                        }

                        codeFile = new CodeFile(Tabs[ActiveTabIndex].AbsoluteFilePathName, Tabs[ActiveTabIndex].Text);
                    }

                    var filteredAnalyzerResultItems = new List<string>();
                    if (Project != null && Project.FilteredAnalyzerResultItems != null)
                    {
                        filteredAnalyzerResultItems = Project.FilteredAnalyzerResultItems.ToList();
                    }

                    var filteredAnalyzerResultItemsToUse = filteredAnalyzerResultItems;

                    var args = new AnalyzerWorkerArgs(AnalyzerStartReason.AnalyzeFile, ProjectRootDirectory, codeFile, filteredAnalyzerResultItemsToUse, CloneList(FilesToRemoveFromAnalyzer), MissionFileHasChanged);

                    // Reset files to add and remove.
                    FilesToRemoveFromAnalyzer = new List<string>();
                    MissionFileHasChanged = false;

                    // Remove the current enqueued file.

                    string fileToRemove = AnalyzerListFileQueue.FirstOrDefault(x => x.ToLower() == fileName.ToLower());
                    if (!string.IsNullOrWhiteSpace(fileToRemove))
                    {
                        AnalyzerListFileQueue.Remove(fileToRemove);
                        _startAnalyzerWhenPossible = true;
                    }
                    else if (AnalyzerListFileQueue.Count > 0)
                    {
                        _startAnalyzerWhenPossible = true;
                    }

                    // Start the analyzer.

                    _analyzerWorker.RunWorkerAsync(args);
                }
            }
        }

        private ProjectAnalyzer Analyzer { get; set; }

        private void AnalyzerWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            var backgroundWorker = sender as BackgroundWorker;
            var args = e.Argument as AnalyzerWorkerArgs;
            e.Result = null;

            if (Analyzer == null)
            {
                Analyzer = new ProjectAnalyzer(args.ProjectRootDirectory);
            }

            foreach (var file in args.FilesToRemove)
            {
                Analyzer.RemoveFileContentFromAnalyzer(file);
            }

            if (args.MissionFileNeedsUpdate)
            {
                Analyzer.MissionFileHasChanged = true;
            }

            if (args.StartReason == AnalyzerStartReason.PrepareProject)
            {
                Analyzer = new ProjectAnalyzer(args.ProjectRootDirectory);
            }

            if (args.StartReason == AnalyzerStartReason.PrepareProject)
            {
                var cancelChecker = new BackgroundWorkerCancelChecker(backgroundWorker);
                var progressReporter = new BackgroundWorkerProgressReporter(backgroundWorker);

                Analyzer.ResetAndPrepareVariablesAndTypes(cancelChecker, progressReporter);
            }
            else
            {
                e.Result = Analyzer.AnalyzeFile(args.CodeFile);
            }
        }

        private void AnalyzerWorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            CompilerProgressBarValue = CompilerProgressBarMax;

            if (e.Result != null)
            {
                var result = e.Result as AnalyzerResult;

                if (ActiveTabIndex < 0 || result.AbsoluteFileName == Tabs[ActiveTabIndex].AbsoluteFilePathName)
                {
                    StringBuilder sb = new StringBuilder();
                    AnalyzerResultItems.Clear();
                    foreach (CodeError error in result.CodeErrors)
                    {
                        sb.AppendLine(error.ToString());
                        ChangeSelectedResultTab(ResultTabs.Analyzer);
                        AnalyzerResultItems.Add(new AnalyzerResultItem(error.Message.Trim().Replace("\n", " ").Replace("\t", "").Replace("  ", " "), error.LineNumber));
                    }

                    AnalyzerResult = sb.ToString();
                }

                if (result.FileIsInProject)
                {
                    _declaredPublicVariables = result.DeclaredPublicVariables;
                    _declaredPrivateVariables = result.DeclaredPrivateVariables;
                    _declaredTypes = result.DeclaredTypes;
                }
            }

            if (_startAnalyzerWhenPossible)
            {
                StartAnalyzer();
            }
            else if (_startCompilerWhenPossible)
            {
                StartCompiler(false, _startCompilerWhenPossibleFileName);
            }
        }

        private string _analyzerResult;

        public string AnalyzerResult
        {
            get { return _analyzerResult; }
            private set
            {
                if (_analyzerResult != value)
                {
                    _analyzerResult = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("AnalyzerResult"));
                }
            }
        }

        private void StartCompiler(bool fullRebuild, string currentFilePathName = "")
        {
            SaveAllFiles();
            _startCompilerWhenPossible = true;
            _startCompilerWhenPossibleFileName = currentFilePathName;
            //CompilerProgressBarIndeterminate = true;

            if (!_compilerWorker.IsBusy && !_analyzerWorker.IsBusy)
            {
                ChangeSelectedResultTab(ResultTabs.Compiler);
                _startCompilerWhenPossible = false;
                _startCompilerWhenPossibleFileName = "";
                CompilerProgressBarIndeterminate = false;
                CompilerResultItems.Clear();

                var startReason = currentFilePathName == "" ? AnalyzerStartReason.BuildProject : AnalyzerStartReason.BuildFile;
                if (fullRebuild)
                {
                    startReason = AnalyzerStartReason.RebuildProject;
                }

                _compilerWorker.RunWorkerAsync( new CompilerWorkerArgs(startReason, ProjectRootDirectory, currentFilePathName));
            }
            else
            {
                ChangeSelectedResultTab(ResultTabs.Compiler);
                CompilerResultItems.Clear();
                CompilerResultItems.Add(new AnalyzerResultItem("Analyzing project files. Compilation will start soon. Please wait..."));
            }
        }

        private void CompilerWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            CompilerWorkerArgs args = e.Argument as CompilerWorkerArgs;
            e.Result = PerformCompilation(sender as BackgroundWorker, args);
        }

        private void CompilerWorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            int builtFilesCount = (int)e.Result;
            int errorCount = CompilerResultItems.Count(i => i.IsError);

            ChangeSelectedResultTab(ResultTabs.Compiler);
            CompilerProgressBarValue = CompilerProgressBarMax;

            if (errorCount == 0)
            {
                string sFiles = builtFilesCount == 1 ? " file" : " files";
                CompilerResultItems.Add(new AnalyzerResultItem(builtFilesCount.ToString() + sFiles + " successfully built."));
            }
            else
            {
                CompilerResultItems.Add(new AnalyzerResultItem("Build completed with " + errorCount + " error" + (errorCount == 1 ? "" : "s") + "."));
            }

            if (_startAnalyzerWhenPossible)
            {
                StartAnalyzer();
            }
            else if (_startCompilerWhenPossible)
            {
                StartCompiler(false, _startCompilerWhenPossibleFileName);
            }
        }

        private void AnalyzerWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var item = e.UserState as AnalyzerResultItem;

            CompilerProgressBarValue = e.ProgressPercentage;

            if (item != null)
            {
                CompilerResultItems.Add(item);
            }
        }

        private void CompilerWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var item = e.UserState as AnalyzerResultItem;

            CompilerProgressBarValue = e.ProgressPercentage;

            if (item != null)
            {
                CompilerResultItems.Add(item);
            }
        }

        public void BuildCurrentFileAsync(string currentFilePathName)
        {
            StartCompiler(false, currentFilePathName);
        }

        public void BuildProjectAsync()
        {
            StartCompiler(false, "");
        }

        public void RebuildProjectAsync()
        {
            StartCompiler(true, "");
        }

        public int PerformCompilation(BackgroundWorker compilerWorker, CompilerWorkerArgs args)
        {
            int builtFiles = 0;

            if (Analyzer == null)
            {
                compilerWorker.ReportProgress(0, new AnalyzerResultItem("Analyzer is not yet ready. Aborting."));
                return 0;
            }

            if (string.IsNullOrWhiteSpace(args.ProjectRootDirectory))
            {
                compilerWorker.ReportProgress(0, new AnalyzerResultItem("Project has no valid root directory. Aborting."));
                return 0;
            }

            var cancelChecker = new BackgroundWorkerCancelChecker(compilerWorker);
            var progressReporter = new BackgroundWorkerProgressReporter(compilerWorker);

            // If only the current file is being compiled
            if (args.StartReason == AnalyzerStartReason.BuildFile)
            {
                if (args.CurrentFilePathName.ToLower().EndsWith(".sqx"))
                {
                    Analyzer.BuildFile(cancelChecker, progressReporter, args.CurrentFilePathName, Settings.AddMethodCallLogging);
                    builtFiles = 1;
                }
            }
            else // Full compile
            {
                var compilerProjectFileNames = new List<string>();

                // Get the files in the project
                ProjectFileHandler.FindProjectFiles(args.ProjectRootDirectory, compilerProjectFileNames);

                var compilerFiles = compilerProjectFileNames.Where(i => i.EndsWith(".sqx", true, null));
                //CompilerProgressBarMax = compilerFiles.Count();

                // Perform the compile of all files
                if (args.StartReason == AnalyzerStartReason.RebuildProject)
                {
                    builtFiles = Analyzer.RebuildProject(cancelChecker, progressReporter, Settings.AddMethodCallLogging);
                }
                else
                {
                    builtFiles = Analyzer.BuildProject(cancelChecker, progressReporter, Settings.AddMethodCallLogging);
                }
            }

            return builtFiles;
        }

        private ObservableCollection<AnalyzerResultItem> _analyzerResultItems = new ObservableCollection<AnalyzerResultItem>();
        public ObservableCollection<AnalyzerResultItem> AnalyzerResultItems
        {
            get { return _analyzerResultItems; }
            private set
            {
                _analyzerResultItems = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AnalyzerResultItems"));
            }
        }

        public int SelectedAnalyzerResultIndex { get; set; }

        public AnalyzerResultItem SelectedAnalyzerResultItem
        {
            get { return SelectedAnalyzerResultIndex >= 0 ? AnalyzerResultItems[SelectedAnalyzerResultIndex] : null; }
        }

        private ObservableCollection<AnalyzerResultItem> _compilerResultItems = new ObservableCollection<AnalyzerResultItem>();
        public ObservableCollection<AnalyzerResultItem> CompilerResultItems
        {
            get { return _compilerResultItems; }
            private set
            {
                _compilerResultItems = value;
                OnPropertyChanged(new PropertyChangedEventArgs("CompilerResultItems"));
            }
        }
        public int SelectedCompilerResultIndex { get; set; }

        public AnalyzerResultItem SelectedCompilerResultItem
        {
            get { return SelectedCompilerResultIndex >= 0 ? CompilerResultItems[SelectedCompilerResultIndex] : null; }
        }

        private int _selectedResultTabIndex;
        public int SelectedResultTabIndex
        {
            get { return _selectedResultTabIndex; }
            set
            {
                if (_selectedResultTabIndex != value)
                {
                    _selectedResultTabIndex = (int) value;
                    OnPropertyChanged("SelectedResultTabIndex");
                }
            }
        }
        public void ChangeSelectedResultTab(ResultTabs selectTab)
        {
            SelectedResultTabIndex = (int)selectTab;
        }

        private int _compilerProgressBarValue = 0;
        public int CompilerProgressBarValue
        {
            get { return _compilerProgressBarValue; }
            set
            {
                _compilerProgressBarValue = value;
                OnPropertyChanged("CompilerProgressBarValue");
            }
        }

        private int _compilerProgressBarMax = 100;
        public int CompilerProgressBarMax
        {
            get { return _compilerProgressBarMax; }
            set
            {
                _compilerProgressBarMax = value;
                OnPropertyChanged("CompilerProgressBarMax");
            }
        }

        private bool _compilerProgressBarIndeterminate;
        public bool CompilerProgressBarIndeterminate
        {
            get { return _compilerProgressBarIndeterminate; }
            set
            {
                _compilerProgressBarIndeterminate = value;
                OnPropertyChanged("CompilerProgressBarIndeterminate");
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Public Properties

        private Brush _themeBackgroundColor = null;
        public Brush ThemeBackgroundColor
        {
            get
            {
                return _themeBackgroundColor;
            }
            set
            {
                if (_themeBackgroundColor != value)
                {
                    _themeBackgroundColor = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("ThemeBackgroundColor"));
                }
            }
        }

        private Brush _themeForegroundColor = null;
        public Brush ThemeForegroundColor
        {
            get
            {
                return _themeForegroundColor;
            }
            set
            {
                if (_themeForegroundColor != value)
                {
                    _themeForegroundColor = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("ThemeForegroundColor"));
                }
            }
        }

        public IList<GlobalContextVariable> DeclaredPublicVariables
        {
            get { return _declaredPublicVariables; }
        }

        public IList<GlobalContextVariable> DeclaredPrivateVariables
        {
            get { return _declaredPrivateVariables; }
        }

        public List<TypeInfo> DeclaredTypes
        {
            get { return _declaredTypes; }
        }

        public string NewVersionMessage
        {
            get { return _newVersionMessage; }
            set
            {
                if (_newVersionMessage != value)
                {
                    _newVersionMessage = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("NewVersionMessage"));
                }
            }
        }

        public Visibility NewVersionMessageVisibility
        {
            get { return _newVersionVisibility; }
            set
            {
                if (_newVersionVisibility != value)
                {
                    _newVersionVisibility = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("NewVersionMessageVisibility"));
                }
            }
        }

        public ProjectViewModel Project
        {
            get { return _project; }
            set
            {
                if (_project != value)
                {
                    _project = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Project"));
                }
            }
        }

        public IUserMessageService UserMessageService { get; set; }

        public IFileService FileService
        {
            get; set;
        }

        public IAskForFileNameService FileNameService
        {
            get;
            set;
        }

        public IAskForFolderNameService FolderNameService
        {
            get;
            set;
        }

        public IAskForProjectNameService ProjectNameService
        {
            get;
            set;
        }

        public IAskForTextService TextService { get; set; }

        public ProjectNodeViewModel SelectedProjectNode
        {
            get { return Project.ProjectRootNode.GetSelectedNode(); }
        }

        private string _deployment1MenuCaption;
        public string Deployment1MenuCaption
        {
            get { return _deployment1MenuCaption; }
            set
            {
                if (_deployment1MenuCaption != value)
                {
                    _deployment1MenuCaption = value;
                    OnPropertyChanged("Deployment1MenuCaption");
                    OnPropertyChanged("Deployment1MenuEnabled");
                }
            }
        }

        private string _deployment2MenuCaption;
        public string Deployment2MenuCaption
        {
            get { return _deployment2MenuCaption; }
            set
            {
                if (_deployment2MenuCaption != value)
                {
                    _deployment2MenuCaption = value;
                    OnPropertyChanged("Deployment2MenuCaption");
                    OnPropertyChanged("Deployment2MenuEnabled");
                }
            }
        }

        private bool _deployment1MenuEnabled;
        public bool Deployment1MenuEnabled
        {
            get { return !string.IsNullOrWhiteSpace(Deployment1MenuCaption); }
            set
            {
                if (_deployment1MenuEnabled != value)
                {
                    _deployment1MenuEnabled = value;
                    OnPropertyChanged("Deployment1MenuEnabled");
                }
            }
        }

        private bool _deployment2MenuEnabled;
        public bool Deployment2MenuEnabled
        {
            get { return !string.IsNullOrWhiteSpace(Deployment2MenuCaption); }
            set
            {
                if (_deployment2MenuEnabled != value)
                {
                    _deployment2MenuEnabled = value;
                    OnPropertyChanged("Deployment2MenuEnabled");
                }
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Private Methods

        public void OpenFileInTab(string absoluteFilePathName)
        {
            // Kolla om fönstret redan är öppet
            TabViewModel tab = Tabs.FirstOrDefault(t => t.AbsoluteFilePathName.ToLower() == absoluteFilePathName.ToLower());
            if (tab != null)
            {
                ActiveTab = tab;
                return;
            }

            // Inte öppet, öppna en ny flik
            tab = new TabViewModel(Path.GetFileName(absoluteFilePathName))
            {
                AbsoluteFilePathName = absoluteFilePathName
            };

            if (absoluteFilePathName != "")
            {
                tab.Load(absoluteFilePathName);
                Tabs.Add(tab);
                ActiveTabIndex = Tabs.IndexOf(tab);

                SaveFileCommand.RaiseCanExecuteChanged();
                BuildCurrentFileCommand.RaiseCanExecuteChanged();
            }

            OnFileOpened();
        }

        private FileSystemWatcher FileWatcher { get; set; }

        private SynchronizationContext FileContext { get; set; }

        private void CloseAllTabs()
        {
            var allTabs = Tabs.ToList();

            _runOnTabGettingFocus = false;
            _runOnTabLosingFocus = false;

            foreach (var tab in allTabs)
            {
                Tabs.Remove(tab);
            }

            _runOnTabGettingFocus = true;
            _runOnTabLosingFocus = true;
        }

        public void OpenProject(string absoluteProjectFilePathName)
        {
            if (FileWatcher != null)
            {
                FileWatcher.Dispose();
                FileWatcher = null;
            }

            try
            {
                if (File.Exists(absoluteProjectFilePathName))
                {
                    XmlSerializer reader = new XmlSerializer(typeof(ProjectViewModel));

                    using (StreamReader file = new StreamReader(absoluteProjectFilePathName))
                    {
                        Project = (ProjectViewModel)reader.Deserialize(file);
                    }

                    string rootPath = Path.GetDirectoryName(absoluteProjectFilePathName);

                    Project.ProjectRootNode.SetProjectRecursively(Project);
                    Project.AbsoluteFilePathName = absoluteProjectFilePathName;

                    Settings.ProjectFileName = Project.AbsoluteFilePathName;

                    InitFileWatcher(rootPath);

                    _declaredPublicVariables = new List<GlobalContextVariable>();
                    _declaredPrivateVariables = new List<GlobalContextVariable>();
                    _declaredTypes = new List<TypeInfo>();

                    // Close all open files
                    CloseAllTabs();

                    SaveProjectFile();

                    OnProjectChanged();

                    ProjectIsPrepared = false;

                    if (_analyzerWorker.IsBusy)
                    {
                        _analyzerWorker.CancelAsync();
                    }

                    Project.ProjectRootNode.IsExpanded = true;

                    StartAnalyzer();
                }
            }
            catch (Exception)
            {
                UserMessageService.ShowMessage("Project file has wrong format.", "Open Project File",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void CloseProject()
        {
            Project.AbsoluteFilePathName = null;
            Project.ProjectRootNode = null;
            Project.IsDirty = true;
            OnProjectChanged();
        }

        private void InitFileWatcher(string projectRootPath)
        {
            if (FileWatcher != null)
            {
                FileWatcher.Dispose();
                FileWatcher = null;
            }

            FileWatcher = new FileSystemWatcher(projectRootPath);
            FileWatcher.EnableRaisingEvents = true;
            FileWatcher.IncludeSubdirectories = true;
            FileContext = SynchronizationContext.Current;
            FileWatcher.Changed += FileWatcherOnChanged;
        }

        private void AddProjectNodes(string rootPath, string relativePath, ProjectNodeViewModel projectNode)
        {
            string fullPath = Path.Combine(rootPath, relativePath);
            string[] subDirectoryNames = Directory.GetDirectories(Path.Combine(rootPath, relativePath));

            foreach (string directoryPath in subDirectoryNames)
            {
                string directoryName = Path.GetFileName(directoryPath);
                var subFileNames = new List<string>();
                subFileNames.AddRange(Directory.GetFiles(directoryPath, "*.sqf", SearchOption.AllDirectories).Where(f => !f.ToLower().EndsWith(".sqx.sqf")).ToArray());
                subFileNames.AddRange(Directory.GetFiles(directoryPath, "*.sqx", SearchOption.AllDirectories).ToArray());
                subFileNames.AddRange(Directory.GetFiles(directoryPath, "*.ext", SearchOption.AllDirectories).ToArray());

                if (subFileNames.Count() > 0)
                {
                    var subFolderNode = new ProjectFolderNodeViewModel(Project, directoryName, Path.Combine(relativePath, directoryName));
                    projectNode.InsertChildNode(subFolderNode);
                    AddProjectNodes(rootPath, Path.Combine(relativePath, directoryName), subFolderNode);
                }
            }

            var fileNames = new List<string>();
            fileNames.AddRange(Directory.GetFiles(fullPath, "*.sqf", SearchOption.TopDirectoryOnly).Where(f => !f.ToLower().EndsWith(".sqx.sqf")).ToArray());
            fileNames.AddRange(Directory.GetFiles(fullPath, "*.sqx", SearchOption.TopDirectoryOnly).ToArray());
            fileNames.AddRange(Directory.GetFiles(fullPath, "*.ext", SearchOption.TopDirectoryOnly).ToArray());

            foreach (string filePathName in fileNames)
            {
                var subFileNode = new ProjectFileNodeViewModel()
                {
                    RelativeFileName = Path.Combine(relativePath, Path.GetFileName(filePathName)),
                    //AbsoluteFileName = Path.Combine(fullPath, Path.GetFileName(filePathName)),
                    DisplayName = filePathName
                };
                projectNode.InsertChildNode(subFileNode);
            }

            projectNode.IsExpanded = true;
        }

        private void CreateProjectOnLocation(string rootPath, string projectName)
        {
            Project.Reset(projectName);
            AddProjectNodes(rootPath, "", Project.ProjectRootNode);
            Project.ProjectRootNode.IsExpanded = true;
        }

        private void OpenMission(string fileName)
        {
            if (FileWatcher != null)
            {
                FileWatcher.Dispose();
                FileWatcher = null;
            }

            _declaredPublicVariables = new List<GlobalContextVariable>();
            _declaredPrivateVariables = new List<GlobalContextVariable>();
            _declaredTypes = new List<TypeInfo>();

            string rootPath = Path.GetDirectoryName(fileName);
            string missionName = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(fileName));

            CreateProjectOnLocation(rootPath, Path.GetFileNameWithoutExtension(missionName));

            Project.AbsoluteFilePathName = Path.Combine(rootPath, missionName + ".tproj");
            Settings.ProjectFileName = Project.AbsoluteFilePathName;

            CloseAllTabs();

			SaveProjectFile();

            InitFileWatcher(rootPath);

            OnProjectChanged();

            ProjectIsPrepared = false;
            StartAnalyzer();
        }

        private void FileWatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            // Kolla om filen är öppen
            foreach (TabViewModel tab in Tabs)
            {
                if (tab.AbsoluteFilePathName == e.FullPath)
                {
                    FileContext.Post(val => ReloadFile(e.FullPath, tab), sender);
                }
            }

			if (e.FullPath.EndsWith("mission.sqm"))
			{
                MissionFileHasChanged = true;
			}
        }

        public void ReloadFile(string fileName, TabViewModel tab)
        {
            var result =
                UserMessageService.ShowMessage(
                    "File '" + fileName + "' has changed outside the editor. Do you want to reload it?",
                    "File has changed", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                tab.Load(fileName);
            }
        }

        public event EventHandler NeedToRebindProject;

        private void OnNeedToRebindProject()
        {
            if (NeedToRebindProject != null)
            {
                NeedToRebindProject(this, new EventArgs());
            }
        }

        public bool SaveProjectFile()
        {
            if (Project == null || Project.ProjectRootNode == null)
            {
                return false;
            }

            if (FileWatcher != null)
            {
                FileWatcher.EnableRaisingEvents = false;
            }

            string projectAbsoluteFilePathName = Project.AbsoluteFilePathName;

            if (string.IsNullOrEmpty(projectAbsoluteFilePathName))
            {
                projectAbsoluteFilePathName = FileService.GetSaveFileName(CurrentApplication.SaveProjectFilter);
            }

            if (!string.IsNullOrEmpty(projectAbsoluteFilePathName))
            {

                string missionFilePathName = Path.Combine(Path.GetDirectoryName(projectAbsoluteFilePathName), "mission.sqm");

                if (!File.Exists(missionFilePathName))
                {
                    UserMessageService.ShowMessage("The mission.sqm file was not found in the selected directory. The project file (.tproj) must be saved in the root of your Arma mission. That is in the same directory as mission.sqm.", "Invalid project directory.", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return false;
                }

                Project.ProjectRootNode.DisplayName = Path.GetFileNameWithoutExtension(projectAbsoluteFilePathName);

                using (var writer = new StreamWriter(projectAbsoluteFilePathName))
                {
                    var serializer = new XmlSerializer(typeof(ProjectViewModel));
                    serializer.Serialize(writer, Project);
                    writer.Flush();
                }

                Project.AbsoluteFilePathName = projectAbsoluteFilePathName;
                Project.ProjectRootNode.RelativeFileName = "";

                if (FileWatcher == null)
                {
                    string projectRootPath = Path.GetDirectoryName(projectAbsoluteFilePathName);
                    InitFileWatcher(projectRootPath);
                }
                else
                {
                    FileWatcher.EnableRaisingEvents = true;
                }

                return true;
            }

            return false;
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Tabs

        public int ActiveTabIndex
        {
            get { return _activeTabIndex; }
            set
            {
                if (_activeTabIndex != value)
                {
                    OnTabLosingFocus(false);
                    _activeTabIndex = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("ActiveTabIndex"));

                    if (_activeTabIndex >= 0)
                    {
                        ActiveTab = Tabs[_activeTabIndex];
                    }

                    AnalyzerResult = "";
                    SaveFileCommand.RaiseCanExecuteChanged();
                    BuildCurrentFileCommand.RaiseCanExecuteChanged();
                    if (_activeTabIndex >= 0)
                    {
                        OnTabGettingFocus();
                    }

                    if (_activeTabIndex >= 0)
                    {
                        StartAnalyzer();
                    }
                }
            }
        }

        public TabViewModel ActiveTab
        {
            get { return _activeTab; }
            set
            {
                if (_activeTab != value)
                {
                    _activeTab = value;
                    ActiveTabIndex = Tabs.IndexOf(value);
                    OnPropertyChanged(new PropertyChangedEventArgs("ActiveTab"));
                }
            }
        }

        private void TabsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    ((TabViewModel)item).PropertyChanged += OnTabPropertyChanged;
                }
            }
        }

        private static int _analyzerChangeNo = 0;
        private void OnTabPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            TabViewModel tab = sender as TabViewModel;

            if (e.PropertyName == "Text")
            {
                _analyzerChangeNo++;
                SaveFileCommand.RaiseCanExecuteChanged();
                BuildCurrentFileCommand.RaiseCanExecuteChanged();

                if (tab == Tabs[ActiveTabIndex])
                {
                    DispatcherTimer dispatcherTimer = new DispatcherTimer();
                    dispatcherTimer.Tick += new EventHandler(AnalyzerTimer_Elapsed);
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 700);
                    dispatcherTimer.Tag = _analyzerChangeNo;
                    dispatcherTimer.Start();
                }
            }
        }

        private void AnalyzerTimer_Elapsed(object sender, EventArgs e)
        {
            var timer = sender as DispatcherTimer;

            if ((int)timer.Tag == _analyzerChangeNo)
            {
                StartAnalyzer();
                OnStoppedWriting();
            }

            timer.Stop();
        }

        public ObservableCollection<TabViewModel> Tabs { get; set; }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Project Nodes

        private void AddProjectFolderNode()
        {
            string folderName = FolderNameService.GetFolderName();

            if (!FolderNameService.Cancelled)
            {
                //var folderNode = new ProjectFolderNodeViewModel(Project, folderName, Path.GetFullPath(SelectedProjectNode.AbsoluteFileName)); // Is this correct?
                var folderNode = new ProjectFolderNodeViewModel(Project, folderName, Path.Combine(SelectedProjectNode.RelativeFileName, folderName));
                SelectedProjectNode.InsertChildNode(folderNode);
            }
        }

        private ProjectNodeViewModel AddFolderPathToNode(ProjectNodeViewModel node, string restPath)
        {
            string[] folderNames = restPath.Split("\\".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (folderNames.Length == 0)
            {
                return node;
            }

            string nextFolderName = folderNames[0];
            string relativePath = Path.Combine(node.RelativeFileName, nextFolderName);

            string backSlash = "";
            var sbRestPath = new StringBuilder();
            for (int i = 1; i < folderNames.Length; i++)
            {
                sbRestPath.Append(backSlash);
                sbRestPath.Append(folderNames[i]);
                backSlash = "\\";
            }

            foreach (ProjectFolderNodeViewModel folderNode in node.Children.Where(n => n is ProjectFolderNodeViewModel))
            {
                if (folderNode.DisplayName == nextFolderName)
                {
                    folderNode.IsExpanded = true;
                    return AddFolderPathToNode(folderNode, sbRestPath.ToString());
                }
            }

            //var nextFolder = new ProjectFolderNodeViewModel(Project, nextFolderName, absolutePath);
            var nextFolder = new ProjectFolderNodeViewModel(Project, nextFolderName, relativePath);
            nextFolder.IsExpanded = true;
            node.InsertChildNode(nextFolder);
            return AddFolderPathToNode(nextFolder, sbRestPath.ToString());
        }

        private void AddNewFileNode()
        {
            if (SelectedProjectNode is ProjectFileNodeViewModel)
            {
                UserMessageService.ShowMessage(
                    "A file cannot be added to a file. Please select root node or a folder.", "Selected file",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string relativePath = SelectedProjectNode.RelativeFileName;

            string fileName = FileNameService.GetFileName();

            if (FileNameService.Cancelled)
            {
                return;
            }

            string extension = ".sqf";
            if (FileNameService.SelectedTemplate != null)
            {
                extension = "." + FileNameService.SelectedTemplate.FileExtension;
            }

            if (!fileName.Contains("."))
            {
                fileName = fileName + "." + extension;
            }

            string displayName = Path.GetFileNameWithoutExtension(fileName);
            string relativeFilePathName = Path.Combine(relativePath, fileName);
            string absoluteFilePathName = Path.Combine(ProjectRootDirectory, relativeFilePathName);

            if (File.Exists(absoluteFilePathName))
            {
                UserMessageService.ShowMessage("File that you are trying to create already exists.", "File exists",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (FileNameService.SelectedTemplate != null)
            {
                if (!fileName.ToLower().EndsWith(extension) && !string.IsNullOrEmpty(FileNameService.SelectedTemplate.Content))
                {
                    UserMessageService.ShowMessage("The entered filename's extension (" + Path.GetExtension(fileName) + ") does not match the template's extension (" + extension + "). Functionality may be lost.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                File.WriteAllText(absoluteFilePathName, FileNameService.SelectedTemplate.ModifiedContent);
            }
            else
            {
                File.Create(absoluteFilePathName).Close();
            }

            ProjectFileNodeViewModel fileNode = new ProjectFileNodeViewModel()
            {
                RelativeFileName = relativeFilePathName,
                DisplayName = displayName,
                IsSelected = true
            };

            SelectedProjectNode.InsertChildNode(fileNode);
            OpenFileInTab(absoluteFilePathName);
            SaveProjectFile();
        }

        private void RenameFileNode()
        {
            try
            {
                if (!(SelectedProjectNode is ProjectFileNodeViewModel))
                {
                    UserMessageService.ShowMessage(
                        "A folder or the project root node cannot be renamed.", "Rename",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string oldRelativePathName = SelectedProjectNode.RelativeFileName;
                string oldRelativePath = Path.GetDirectoryName(oldRelativePathName);
                string oldAbsolutePathName = Path.Combine(ProjectRootDirectory, oldRelativePathName);
                string oldAbsolutePath = Path.GetDirectoryName(oldAbsolutePathName);
                string oldSuffix = Path.GetExtension(oldAbsolutePathName);

                string newFileName = TextService.GetText(SelectedProjectNode.DisplayName)?.Trim();
                //string newSuffix = Path.GetExtension(newFileName);

                //if (string.IsNullOrEmpty(newSuffix) && !string.IsNullOrEmpty(oldSuffix))
                //{
                //    newFileName += oldSuffix;
                //}

                if (TextService.Cancelled || string.IsNullOrEmpty(newFileName))
                {
                    return;
                }

                string newAbsolutePathName = Path.Combine(oldAbsolutePath, newFileName);

                if (File.Exists(newAbsolutePathName))
                {
                    UserMessageService.ShowMessage("A file named '" + newFileName + "' already exists.", "File exists",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Directory.Move(oldAbsolutePathName, newAbsolutePathName);

                _runOnTabLosingFocus = false;
                _runOnTabGettingFocus = false;

                for (int i = Tabs.Count() - 1; i >= 0; i--)
                {
                    if (Tabs[i].AbsoluteFilePathName.ToLower() == oldAbsolutePathName.ToLower())
                    {
                        ActiveTabIndex--;
                        Tabs.RemoveAt(i);
                        break;
                    }
                }

                _runOnTabLosingFocus = true;
                _runOnTabGettingFocus = true;

                string newRelativePathName = Path.Combine(oldRelativePath, newFileName);

                //ProjectFileNodeViewModel fileNode = new ProjectFileNodeViewModel()
                //{
                //    RelativeFileName = newRelativePathName,
                //    DisplayName = newFileName,
                //    IsSelected = true
                //};

                //SelectedProjectNode.InsertChildNode(fileNode);
                SelectedProjectNode.RelativeFileName = newRelativePathName;
                SelectedProjectNode.DisplayName = newFileName;

                //OpenFileInTab(oldAbsoluteFilePathName);

                SaveProjectFile();
            }
            catch
            {
                UserMessageService.ShowMessage("The file or folder could not be renamed.", "File error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        private void AddNewFolderNode()
        {
            if (SelectedProjectNode is ProjectFileNodeViewModel)
            {
                UserMessageService.ShowMessage(
                    "A folder cannot be added to a file. Please select root node or a folder.", "Selected file",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string relativePath = SelectedProjectNode.RelativeFileName;

            string folderName = FolderNameService.GetFolderName();

            if (FolderNameService.Cancelled)
            {
                return;
            }

            string displayName = folderName;
            string relativeFilePathName = Path.Combine(relativePath, folderName);
            string absoluteFilePathName = Path.Combine(ProjectRootDirectory, relativeFilePathName);

            if (Directory.Exists(absoluteFilePathName))
            {
                UserMessageService.ShowMessage("Folder that you are trying to create already exists.", "Folder exists",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Directory.CreateDirectory(absoluteFilePathName);

            var folderNode = new ProjectFolderNodeViewModel()
            {
                RelativeFileName = relativeFilePathName,
                DisplayName = displayName,
                IsSelected = true,
                IsExpanded = true,
            };

            SelectedProjectNode.InsertChildNode(folderNode);
            SaveProjectFile();
        }

        protected void OpenSelectedFileInFileExplorer()
        {
            if (SelectedProjectNode is ProjectRootNodeViewModel)
            {
                System.Diagnostics.Process.Start(ProjectRootDirectory);
                return;
            }

            string absoluteFilePathName = Path.Combine(ProjectRootDirectory, SelectedProjectNode.RelativeFileName);

            if (SelectedProjectNode is ProjectFolderNodeViewModel)
            {
                System.Diagnostics.Process.Start(absoluteFilePathName);
            }
            else
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select, " + absoluteFilePathName);
            }
        }

        protected void CollapseAllSubNodes()
        {
            SelectedProjectNode.CollapseAllSubNodes();
        }

        protected void ExpandAllSubNodes()
        {
            SelectedProjectNode.ExpandAllSubNodes();
        }

        public void AddExistingFileNode(string absoluteFilePathName = "")
        {
            if (string.IsNullOrEmpty(ProjectRootDirectory))
            {
                UserMessageService.ShowMessage("The project file must be saved before you add files to the project.",
                    "Project file not saved", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            string filePathName = "";

            if (string.IsNullOrEmpty(absoluteFilePathName))
            {
                if (SelectedProjectNode != null && !string.IsNullOrWhiteSpace(ProjectRootDirectory))
                {
                    filePathName = Path.Combine(ProjectRootDirectory, SelectedProjectNode.RelativeFileName);
                }

                filePathName = FileService.GetOpenFileName(CurrentApplication.OpenFileFilter, "Add File", filePathName);
            }
            else
            {
                filePathName = absoluteFilePathName;
            }

            if (!string.IsNullOrEmpty(filePathName))
            {
                if (!filePathName.StartsWith(ProjectRootDirectory))
                {
                    UserMessageService.ShowMessage(
                        "The file \"" + Path.GetFileName(filePathName) +
                        "\" is not in your mission folder. Project files that you add to your project must be in mission folder or subfolder.",
                        "File not in mission folder", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }

                string restPath = filePathName.Substring(ProjectRootDirectory.Length + 1);
                restPath = Path.GetDirectoryName(restPath);

                // Add file to a node reflecting its file system directory
                var node = AddFolderPathToNode(Project.ProjectRootNode, restPath);


                var existingNode = node.Children.FirstOrDefault(n => n.DisplayName == Path.GetFileName(filePathName));

                if (existingNode != null)
                {
                    existingNode.IsSelected = true;
                    return;
                }

                var fileNode = new ProjectFileNodeViewModel()
                {
                    RelativeFileName = Path.Combine(node.RelativeFileName, Path.GetFileName(filePathName)),
                };

                node.InsertChildNode(fileNode);

                Project.ProjectRootNode.IsSelected = false;
                Project.ProjectRootNode.IsExpanded = true;
                fileNode.IsSelected = true;

                SaveProjectFile();

                //if (filePathName.ToLower().EndsWith(".sqf") || filePathName.ToLower().EndsWith(".sqx"))
                //{
                //    FilesToAddToAnalyzer.Add(filePathName);
                //}

                StartAnalyzer(filePathName);
                StartAnalyzer();
            }
        }

        private void RemoveFromDisc()
        {
            string msg = "";

            if (SelectedProjectNode is ProjectFileNodeViewModel)
            {
                msg = "Do you really want to remove file \"" + SelectedProjectNode.DisplayName + "\" from disc? This action cannot be undone.";
            }
            else if (SelectedProjectNode is ProjectFolderNodeViewModel)
            {
                msg = "Do you really want to remove folder \"" + SelectedProjectNode.DisplayName +
                      "\" and all its contents from disc? This action cannot be undone.";
            }
            else
            {
                return;
            }

            MessageBoxResult dialogResult = UserMessageService.ShowMessage(msg, "Remove from disc",
                MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

            string absoluteFileName = Path.Combine(ProjectRootDirectory, SelectedProjectNode.RelativeFileName);

            try
            {
                if (dialogResult == MessageBoxResult.Yes)
                {
                    _runOnTabLosingFocus = false;
                    _runOnTabGettingFocus = false;

                    for (int i = Tabs.Count() - 1; i >= 0; i--)
                    {
                        if (Tabs[i].AbsoluteFilePathName.ToLower() == absoluteFileName.ToLower())
                        {
                            Tabs.RemoveAt(i);
                            break;
                        }
                    }

                    _runOnTabLosingFocus = true;
                    _runOnTabGettingFocus = true;

                    if (SelectedProjectNode is ProjectFileNodeViewModel)
                    {
                        File.Delete(absoluteFileName);
                    }
                    else if (SelectedProjectNode is ProjectFolderNodeViewModel)
                    {
                        Directory.Delete(absoluteFileName, true);
                    }

                    RemoveActiveProjectNode();
                    SaveProjectFile();
                    ClearDeclaredVariableAndClasses(absoluteFileName);
                    StartAnalyzer();
                }
            }
            catch (Exception ex)
            {
                UserMessageService.ShowMessage(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearDeclaredVariableAndClasses(string fileName)
        {
            FilesToRemoveFromAnalyzer.Add(fileName);
        }

        private void RemoveActiveProjectNode()
        {
            string fileName = Path.Combine(ProjectRootDirectory, SelectedProjectNode.RelativeFileName);
            Project.ProjectRootNode.FindAndRemoveNode(SelectedProjectNode);
            ClearDeclaredVariableAndClasses(fileName);
            SaveProjectFile();
            StartAnalyzer();
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Commands

        private DelegateCommand _newProjectCommand;
        private DelegateCommand _openProjectCommand;
        private DelegateCommand _closeProjectCommand;
        private DelegateCommand _openMissionCommand;
        private DelegateCommand _newFileCommand;
        private DelegateCommand _openFileCommand;
        private DelegateCommand _saveProjectFileCommand;
        private DelegateCommand _saveFileCommand;
        private DelegateCommand _saveAllFilesCommand;
        private DelegateCommand _closeTabCommand;
        private DelegateCommand _closeAllButThisTabCommand;
        private DelegateCommand _openProjectNodeCommand;
        private DelegateCommand _addProjectFolderNodeCommand;
        private DelegateCommand _addNewFileNodeCommand;
        private DelegateCommand _renameFileNodeCommand;
        private DelegateCommand _addNewFolderNodeCommand;
        private DelegateCommand _addExistingFileNodeCommand;
        private DelegateCommand _openInFileExplorerCommand;
        private DelegateCommand _collapseAllSubNodesCommand;
        private DelegateCommand _expandAllSubNodesCommand;
        private DelegateCommand _removeProjectNodeCommand;
        private DelegateCommand _removeFromDiscCommand;
        private DelegateCommand _filterOutForCurrentFileCommand;
        private DelegateCommand _filterOutForCurrentProjectCommand;
        private DelegateCommand _installCPackCommand;
        private DelegateCommand _getNewVersionCommand;
        private DelegateCommand _findInAllfilesCommand;
        private DelegateCommand _buildCurrentFileCommand;
        private DelegateCommand _buildProjectCommand;
        private DelegateCommand _rebuildProjectCommand;
        private DelegateCommand _deploy1Command;
        private DelegateCommand _deploy2Command;
        private DelegateCommand _cleanCommand;

        public DelegateCommand NewProjectCommand
        {
            get { return (_newProjectCommand = _newProjectCommand ?? new DelegateCommand(x => true, OnNewProject)); }
        }

        private void OnNewProject(object context)
        {
            string fileName = FileService.GetOpenFileName(CurrentApplication.OpenMissionFilter, "Locate the Arma mission file ('mission.sqm').");

            if (!string.IsNullOrEmpty(fileName))
            {
                OpenMission(fileName);
                Project.ProjectRootNode.IsSelected = true;
            }
        }

        public DelegateCommand OpenProjectCommand
        {
            get { return (_openProjectCommand = _openProjectCommand ?? new DelegateCommand(x => true, OnOpenProject)); }
        }

        private void OnOpenProject(object context)
        {
            string fileName = FileService.GetOpenFileName(CurrentApplication.OpenProjectFilter);

            if (!string.IsNullOrEmpty(fileName))
            {
                OpenProject(fileName);
            }
        }

        public DelegateCommand OpenMissionCommand
        {
            get { return (_openMissionCommand = _openMissionCommand ?? new DelegateCommand(x => true, OnOpenMission)); }
        }

        private void OnOpenMission(object context)
        {
            string fileName = FileService.GetOpenFileName(CurrentApplication.OpenMissionFilter);

            if (!string.IsNullOrEmpty(fileName))
            {
                OpenMission(fileName);
            }
        }

        public DelegateCommand CloseProjectCommand
        {
            get { return (_closeProjectCommand = _closeProjectCommand ?? new DelegateCommand(x => true, OnCloseProject)); }
        }

        private void OnCloseProject(object context)
        {
            CloseProject();
        }

        public DelegateCommand NewFileCommand
        {
            get { return (_newFileCommand = _newFileCommand ?? new DelegateCommand(x => true, OnNewFile)); }
        }

        private void CreateNewFile()
        {
            string fileName = FileNameService.GetFileName();

            if (FileNameService.Cancelled)
            {
                return;
            }

            string extension = ".sqf";
            if (FileNameService.SelectedTemplate != null)
            {
                extension = "." + FileNameService.SelectedTemplate.FileExtension;
            }

            if (!fileName.ToLower().EndsWith(".sqf") && !fileName.ToLower().EndsWith(".sqx"))
            {
                fileName = fileName + extension;
            }

            string displayName = Path.GetFileNameWithoutExtension(fileName);

            string content = "";

            if (FileNameService.SelectedTemplate != null)
            {
                if (!fileName.ToLower().EndsWith(extension) && !string.IsNullOrEmpty(FileNameService.SelectedTemplate.Content))
                {
                    UserMessageService.ShowMessage("The entered filename's extension (" + Path.GetExtension(fileName) + ") does not match the template's extension (" + extension + "). Functionality may be lost.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                content = FileNameService.SelectedTemplate.ModifiedContent;
            }

            var newTab = new TabViewModel(displayName);
            Tabs.Add(newTab);
            ActiveTabIndex = Tabs.IndexOf(newTab);
            SaveFileCommand.RaiseCanExecuteChanged();
            BuildCurrentFileCommand.RaiseCanExecuteChanged();
            newTab.Name = fileName;
            newTab.Header = fileName;
            newTab.Text = content;
        }

        private void OnNewFile(object context)
        {
            CreateNewFile();
        }

        public DelegateCommand OpenFileCommand
        {
            get { return (_openFileCommand = _openFileCommand ?? new DelegateCommand(x => true, OnOpenFile)); }
        }

        private void OnOpenFile(object context)
        {
            string filePathName = FileService.GetOpenFileName(CurrentApplication.OpenFileFilter);

            OpenFileInTab(filePathName);
        }

        public DelegateCommand SaveFileCommand
        {
            get { return (_saveFileCommand = _saveFileCommand ?? new DelegateCommand(CanSave, OnSaveFile)); }
        }

        private void OnSaveFile(object context)
        {
            if (FileWatcher != null)
            {
                FileWatcher.EnableRaisingEvents = false;
            }

            if (ActiveTabIndex >= 0)
            {
                bool saved = Tabs[ActiveTabIndex].Save(FileService);
                if (saved)
                {
                    string fileName = Path.GetFileName(Tabs[ActiveTabIndex].AbsoluteFilePathName);
                    Tabs[ActiveTabIndex].Name = fileName;
                    Tabs[ActiveTabIndex].Header = fileName;
                }
            }

            SaveFileCommand.RaiseCanExecuteChanged();

            if (FileWatcher != null)
            {
                FileWatcher.EnableRaisingEvents = true;
            }
        }

        public DelegateCommand SaveProjectFileCommand
        {
            get { return (_saveProjectFileCommand = _saveProjectFileCommand ?? new DelegateCommand(x => Project.ProjectRootNode != null, OnSaveProjectFile)); }
        }

        private void OnSaveProjectFile(object context)
        {
            if (FileWatcher != null)
            {
                FileWatcher.EnableRaisingEvents = false;
            }

            SaveProjectFile();

            if (FileWatcher != null)
            {
                FileWatcher.EnableRaisingEvents = true;
            }
        }

        public bool CanSave(object context)
        {
            if (ActiveTabIndex >= 0)
            {
                return Tabs[ActiveTabIndex].IsDirty;
            }

            return false;
        }

        public bool CanCompile(object context)
        {
            if (ActiveTabIndex >= 0)
            {
                return Tabs[ActiveTabIndex].AbsoluteFilePathName.ToLower().EndsWith(".sqx");
            }

            return false;
        }

        public DelegateCommand SaveAllFilesCommand
        {
            get { return (_saveAllFilesCommand = _saveAllFilesCommand ?? new DelegateCommand(x => true, OnSaveAllFiles)); }
        }

        public void SaveAllFiles()
        {
            SaveProjectFile();

            if (FileWatcher != null)
            {
                FileWatcher.EnableRaisingEvents = false;
            }

            for (int i = 0; i < Tabs.Count; i++)
            {
                TabViewModel tab = Tabs[i];

                if (string.IsNullOrWhiteSpace(tab.AbsoluteFilePathName))
                {
                    ActiveTabIndex = i;
                }

                if (Tabs[i].IsDirty)
                {
                    Tabs[i].Save(FileService);
                }
            }

            SaveFileCommand.RaiseCanExecuteChanged();

            if (FileWatcher != null)
            {
                FileWatcher.EnableRaisingEvents = true;
            }
        }

        private void OnSaveAllFiles(object context)
        {
            SaveAllFiles();
        }

        public DelegateCommand CloseTabCommand
        {
            get { return (_closeTabCommand = _closeTabCommand ?? new DelegateCommand(x => true, CloseTab)); }
        }

        private void CloseTab(object context)
        {
            var tab = Tabs[_activeTabIndex];
            bool userCancelled = false;

            if (tab.IsDirty)
            {
                var result = MessageBox.Show("The file '" + tab.Name + "' has unsaved changes. Do you want to save it now?", "Save unsaved changes?", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
                else if (result == MessageBoxResult.Yes)
                {
                    if (FileWatcher != null)
                    {
                        FileWatcher.EnableRaisingEvents = false;
                    }

                    userCancelled = !tab.Save(new FileService());

                    if (FileWatcher != null)
                    {
                        FileWatcher.EnableRaisingEvents = true;
                    }
                }
                else if (result == MessageBoxResult.No)
                {
                    // Do nothing.
                }
                else
                {
                    throw new InvalidOperationException("Erroneous path in CloseTab(object).");
                }
            }

            if (!userCancelled)
            {
                OnTabLosingFocus(true);

                int tabIndexToRemove = ActiveTabIndex;

                try
                {
                    string fileName = Tabs[_activeTabIndex].AbsoluteFilePathName;
                    _tabOpenedOrder.Remove(fileName); TODO

                    if (_tabOpenedOrder.Count() > 0)
                    {
                        string lastFileName = _tabOpenedOrder[_tabOpenedOrder.Count() - 1];
                        
                        if (FileIsOpenInTab(lastFileName))
                        {
                            OpenFileInTab(lastFileName);
                        }
                    }
                }
                catch
                {
                }

                _runOnTabLosingFocus = false;
                Tabs.RemoveAt(tabIndexToRemove);
                _runOnTabLosingFocus = true;
            }
        }

        public bool FileIsOpenInTab(string absoluteFilePathName)
        {
            foreach (var tab in Tabs)
            {
                if (!string.IsNullOrWhiteSpace(tab.AbsoluteFilePathName)) {
                    if (tab.AbsoluteFilePathName.ToLower() == absoluteFilePathName.ToLower())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public DelegateCommand CloseAllButThisTabCommand
        {
            get { return (_closeAllButThisTabCommand = _closeAllButThisTabCommand ?? new DelegateCommand(x => true, CloseAllButThisTab)); }
        }

        private void CloseAllButThisTab(object context)
        {
            TabViewModel tabToKeep = Tabs[ActiveTabIndex];

            _runOnTabLosingFocus = false;
            _runOnTabGettingFocus = false;

            for (int i = Tabs.Count() - 1; i >= 0; i--)
            {
                if (Tabs[i] != tabToKeep)
                {
                    Tabs.RemoveAt(i);
                }
            }

            _runOnTabLosingFocus = true;
            _runOnTabGettingFocus = true;
        }

        public DelegateCommand OpenProjectNodeCommand
        {
            get { return (_openProjectNodeCommand = _openProjectNodeCommand ?? new DelegateCommand(x => true, OpenProjectNode)); }
        }

        private void OpenProjectNode(object context)
        {
            try
            {
                if (SelectedProjectNode is ProjectFileNodeViewModel)
                {
                    OpenFileInTab(Path.Combine(ProjectRootDirectory, SelectedProjectNode.RelativeFileName));
                }
                else
                {
                    SelectedProjectNode.IsExpanded = !SelectedProjectNode.IsExpanded;
                }
            }
            catch (Exception ex)
            {
                UserMessageService.ShowMessage(ex.Message, "Error", MessageBoxButton.OK,
                    MessageBoxImage.Stop);
            }
        }

        public DelegateCommand AddNewFileNodeCommand
        {
            get { return (_addNewFileNodeCommand = _addNewFileNodeCommand ?? new DelegateCommand(x => true, DoAddNewFileNodeCommand)); }
        }

        private void DoAddNewFileNodeCommand(object context)
        {
            AddNewFileNode();
        }

        public DelegateCommand RenameFileNodeCommand
        {
            get { return (_renameFileNodeCommand = _renameFileNodeCommand ?? new DelegateCommand(x => true, DoRenameFileNodeCommand)); }
        }

        private void DoRenameFileNodeCommand(object context)
        {
            RenameFileNode();
        }

        public DelegateCommand AddNewFolderNodeCommand
        {
            get { return (_addNewFolderNodeCommand = _addNewFolderNodeCommand ?? new DelegateCommand(x => true, DoAddNewFolderNodeCommand)); }
        }

        private void DoAddNewFolderNodeCommand(object context)
        {
            AddNewFolderNode();
        }

        public DelegateCommand OpenInFileExplorerCommand
        {
            get { return (_openInFileExplorerCommand = _openInFileExplorerCommand ?? new DelegateCommand(x => true, DoOpenInFileExplorerCommand)); }
        }

        private void DoOpenInFileExplorerCommand(object context)
        {
            OpenSelectedFileInFileExplorer();
        }

        public DelegateCommand CollapseAllSubNodesCommand
        {
            get { return (_collapseAllSubNodesCommand = _collapseAllSubNodesCommand ?? new DelegateCommand(x => true, DoCollapseAllSubNodesCommand)); }
        }

        private void DoCollapseAllSubNodesCommand(object context)
        {
            CollapseAllSubNodes();
        }

        public DelegateCommand ExpandAllSubNodesCommand
        {
            get { return (_expandAllSubNodesCommand = _expandAllSubNodesCommand ?? new DelegateCommand(x => true, DoExpandAllSubNodesCommand)); }
        }

        private void DoExpandAllSubNodesCommand(object context)
        {
            ExpandAllSubNodes();
        }

        public DelegateCommand AddExistingFileNodeCommand
        {
            get { return (_addExistingFileNodeCommand = _addExistingFileNodeCommand ?? new DelegateCommand(x => true, DoAddExistingFileNodeCommand)); }
        }

        private void DoAddExistingFileNodeCommand(object context)
        {
            AddExistingFileNode();
        }

        public DelegateCommand AddProjectFolderNodeCommand
        {
            get { return (_addProjectFolderNodeCommand = _addProjectFolderNodeCommand ?? new DelegateCommand(x => true, DoAddProjectFolderNodeCommand)); }
        }

        private void DoAddProjectFolderNodeCommand(object context)
        {
            AddProjectFolderNode();
        }


        public DelegateCommand RemoveFromDiscCommand
        {
            get { return (_removeFromDiscCommand = _removeFromDiscCommand ?? new DelegateCommand(x => true, DoRemoveFromDiscCommand)); }
        }

        private void DoRemoveFromDiscCommand(object context)
        {
            RemoveFromDisc();
        }

        public DelegateCommand RemoveProjectNodeCommand
        {
            get { return (_removeProjectNodeCommand = _removeProjectNodeCommand ?? new DelegateCommand(x => true, DoRemoveProjectNodeCommand)); }
        }

        private void DoRemoveProjectNodeCommand(object context)
        {
            RemoveActiveProjectNode();
        }

        public DelegateCommand FilterOutForCurrentFileCommand
        {
            get { return (_filterOutForCurrentFileCommand = _filterOutForCurrentFileCommand ?? new DelegateCommand(x => true, FilterOutForCurrentFile)); }
        }

        private void FilterOutForCurrentFile(object context)
        {
        }

        public DelegateCommand FilterOutForCurrentProjectCommand
        {
            get { return (_filterOutForCurrentProjectCommand = _filterOutForCurrentProjectCommand ?? new DelegateCommand(x => true, FilterOutForCurrentProject)); }
        }

        private void FilterOutForCurrentProject(object context)
        {
            Project.AddFilteredAnalyzerResultItems(SelectedAnalyzerResultItem.Description);
            AnalyzerResultItems.Remove(SelectedAnalyzerResultItem);
        }

        public DelegateCommand InstallCPackCommand
        {
            get { return (_installCPackCommand = _installCPackCommand ?? new DelegateCommand(x => true, InstallCPack)); }
        }

        private void InstallCPack(object context)
        {
            string projectRootPath = Path.GetDirectoryName(Settings.ProjectFileName);
            CPackSettings localCPackSettings = null;
            string cPackSettingsFileName = Path.Combine(projectRootPath, "CPack.Config");

            if (File.Exists(cPackSettingsFileName))
            {
                XmlSerializer reader = new XmlSerializer(typeof(CPackSettings));
                using (StringReader sXml = new StringReader(File.ReadAllText(cPackSettingsFileName)))
                {
                    localCPackSettings = (CPackSettings)reader.Deserialize(sXml);
                }
            }
            else
            {
                localCPackSettings = new CPackSettings();
            }

            var messages = new List<string>();
            //InstallPackageOld("Engima.SearchGroup", localCPackSettings, messages);
        }

        public DelegateCommand GetNewVersionCommand
        {
            get { return (_getNewVersionCommand = _getNewVersionCommand ?? new DelegateCommand(x => true, GetNewVersion)); }
        }

        private void GetNewVersion(object context)
        {
            System.Diagnostics.Process.Start(CurrentApplication.TypeSqfDomain);
        }

        private string _syntaxHighlighing;
        public string SyntaxHighlighting
        {
            get { return _syntaxHighlighing; }
            set
            {
                if (_syntaxHighlighing != value)
                {
                    _syntaxHighlighing = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("SyntaxHighlighting"));
                }
            }
        }

        public string AbsoluteToRelativeFilePathName(string absoluteFilePathName)
        {
            if (absoluteFilePathName.ToLower().StartsWith(ProjectRootDirectory.ToLower()))
            {
                return absoluteFilePathName.Substring(ProjectRootDirectory.Length + 1);
            }

            return null;
        }

        public bool FileInProject(string absoluteFilePathName)
        {
            if (absoluteFilePathName.ToLower().StartsWith(ProjectRootDirectory.ToLower()))
            {
                string relativeFilePathName = absoluteFilePathName.Substring(ProjectRootDirectory.Length + 1);
                return Project.FileInProject(relativeFilePathName);
            }

            return false;
        }

        public void Clean()
        {
            if (string.IsNullOrEmpty(ProjectRootDirectory))
            {
                return;
            }

            List<string> allFiles = new List<string>();
            ProjectFileHandler.FindProjectFiles(ProjectRootDirectory, allFiles);

            // First, run through all files to collect classes and global functions/variables
            _runOnTabLosingFocus = false;
            _runOnTabGettingFocus = false;

            foreach (string filePathName in allFiles)
            {
                if (filePathName.ToLower().EndsWith(".sqx.sqf"))
                {
                    // Remove eventual tabs
                    for (int i = Tabs.Count() - 1; i >= 0; i--)
                    {
                        if (Tabs[i].AbsoluteFilePathName.ToLower() == filePathName.ToLower())
                        {
                            Tabs.RemoveAt(i);
                            break;
                        }
                    }

                    // Remove eventual tree nodes
                    string relativeFilePathName = filePathName.Substring(ProjectRootDirectory.Length + 1);
                    var node = Project.ProjectRootNode.GetNodeByRelativeFileName(relativeFilePathName);
                    if (node != null)
                    {
                        Project.ProjectRootNode.FindAndRemoveNode(node);
                    }

                    // Remove the file
                    File.Delete(filePathName);
                }
            }

            _runOnTabGettingFocus = true;
            _runOnTabLosingFocus = true;

            RemoveProjectFilesThatDoNotExist();
            SaveProjectFile();
        }

        private void DoBuildCurrentFile(object context)
        {
            if (ActiveTab != null)
            {
                BuildCurrentFileAsync(ActiveTab.AbsoluteFilePathName);
            }
        }

        private void DoBuildProject(object context)
        {
            BuildProjectAsync();
        }

        private void DoRebuildProject(object context)
        {
            RebuildProjectAsync();
        }

        public DelegateCommand BuildCurrentFileCommand
        {
            get { return (_buildCurrentFileCommand = _buildCurrentFileCommand ?? new DelegateCommand(CanCompile, DoBuildCurrentFile)); }
        }

        public DelegateCommand BuildProjectCommand
        {
            get { return (_buildProjectCommand = _buildProjectCommand ?? new DelegateCommand(x => true, DoBuildProject)); }
        }

        public DelegateCommand RebuildProjectCommand
        {
            get { return (_rebuildProjectCommand = _rebuildProjectCommand ?? new DelegateCommand(x => true, DoRebuildProject)); }
        }

        private void DoClean(object context)
        {
            Clean();
        }

        public DelegateCommand CleanCommand
        {
            get { return (_cleanCommand = _cleanCommand ?? new DelegateCommand(x => true, DoClean)); }
        }

        private void DoDeploy1(object context)
        {
            Deploy(Settings.Deployment1Directory);
        }

        private void DoDeploy2(object context)
        {
            Deploy(Settings.Deployment2Directory);
        }

        private void Deploy(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                UserMessageService.ShowMessage("There's no output folder specified. Please specify the PBO export folders in Tools->Options and try again.", "Export PBO", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (!Directory.Exists(directoryPath))
            {
                UserMessageService.ShowMessage("The output folder '" + directoryPath + "' does not exist.", "Export PBO", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            string pboFileName = new DirectoryInfo(ProjectRootDirectory).Name + ".pbo";
            string pboFilePathName = Path.Combine(directoryPath, pboFileName);
            bool success = PboArchive.Create(ProjectRootDirectory, pboFilePathName);

            if (success)
            {
                UserMessageService.ShowMessage("Project was successfully exported to file '" + pboFilePathName + "'.", "Export PBO", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                UserMessageService.ShowMessage("Export to '" + pboFilePathName + "' failed.", "Export PBO", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public DelegateCommand Deploy1Command
        {
            get { return (_deploy1Command = _deploy1Command ?? new DelegateCommand(x => true, DoDeploy1)); }
        }

        public DelegateCommand Deploy2Command
        {
            get { return (_deploy2Command = _deploy2Command ?? new DelegateCommand(x => true, DoDeploy2)); }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, e);
            }
        }

        //----------------------------------------------------------------------------------------------------------------
        #endregion
        //----------------------------------------------------------------------------------------------------------------

        public void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void OnActivated(object sender, EventArgs eventArgs)
        {
            if (!_windowHasActivatedFirstTime)
            {
                _windowHasActivatedFirstTime = true;

                foreach (var fileName in Settings.OpenFileNames)
                {
                    try
                    {
                        if (File.Exists(fileName))
                        {
                            OpenFileInTab(fileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        UserMessageService.ShowMessage(ex.Message, "Error", MessageBoxButton.OK,
                            MessageBoxImage.Stop);
                    }
                }
            }
        }

        private SettingsFile _settingsFile;
        public SettingsFile Settings
        {
            get { return _settingsFile; }
            private set
            {
                _settingsFile = value;
            }
        }

        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            bool userCancelled = false;

            if (Project.ProjectRootNode != null)
            {
                Project.ProjectRootNode.CollapseAllSubNodes();
                Project.ProjectRootNode.IsSelected = true;
            }

            SaveProjectFile();

            foreach (TabViewModel tab in Tabs)
            {
                if (tab.IsDirty)
                {
                    ActiveTab = tab;
                    var result = MessageBox.Show("The file '" + tab.Name + "' has unsaved changes. Do you want to save it now?", "Save unsaved changes?", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                    else if (result == MessageBoxResult.Yes)
                    {
                        if (FileWatcher != null)
                        {
                            FileWatcher.EnableRaisingEvents = false;
                        }

                        userCancelled = !tab.Save(new FileService());
                        if (userCancelled)
                        {
                            e.Cancel = true;
                            return;
                        }

                        if (FileWatcher != null)
                        {
                            FileWatcher.EnableRaisingEvents = true;
                        }
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        // Do nothing.
                    }
                    else
                    {
                        throw new InvalidOperationException("Erroneous path in OnWindowClosing().");
                    }
                }
            }

            Settings.OpenFileNames.Clear();
            foreach (TabViewModel tab in Tabs)
            {
                if (!string.IsNullOrWhiteSpace(tab.AbsoluteFilePathName))
                {
                    Settings.OpenFileNames.Add(tab.AbsoluteFilePathName);
                }
            }

            SaveSettings();
        }

        public void SaveSettings()
        {
            try
            {
                using (var writer = new StreamWriter(Path.Combine(CurrentApplication.AppDataFolder, CurrentApplication.SettingsFileName)))
                {
                    Settings.ProjectFileName = "";
                    if (Project != null && !string.IsNullOrWhiteSpace(Project.AbsoluteFilePathName))
                    {
                        Settings.ProjectFileName = Project.AbsoluteFilePathName;
                    }

                    var serializer = new XmlSerializer(typeof(SettingsFile));
                    serializer.Serialize(writer, Settings);
                    writer.Flush();
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

        public void AddCPackFilesToProject(string packageName)
        {
            CPackSettings settings = Service.CPackService.GetLocalCPackSettings(ProjectRootDirectory);

            CPack cpack = settings.CPacks.FirstOrDefault(p => p.Name.ToLower() == packageName.ToLower());

            if (cpack != null)
            {
                // Börja med att lägga till alla dependencies
                foreach (var dependency in cpack.Dependencies)
                {
                    var dependencyPack = settings.CPacks.FirstOrDefault(p => p.Name.ToLower() == dependency.Name.ToLower());

                    if (dependencyPack != null)
                    {
                        foreach (CPackFile file in dependencyPack.Files)
                        {
                            string fileName = file.Name.ToLower();

                            if (fileName.EndsWith(".sqx") || (fileName.EndsWith(".sqf") && !fileName.EndsWith(".sqx.sqf")) || fileName.EndsWith(".ext") || fileName.EndsWith(".txt") || fileName == "mission.sqm")
                            {
                                // Lägg till filen i projektet om det inte redan finns i projektet.
                                if (Project.ProjectRootNode.GetNodeByRelativeFileName(file.Name) == null)
                                {
                                    string absoluteFilePathName = Path.Combine(ProjectRootDirectory, file.Name);
                                    AddExistingFileNode(absoluteFilePathName);
                                }
                            }
                        }

                        AddInitFileNodes(cpack);
                    }
                }

                foreach (CPackFile file in cpack.Files)
                {
                    string fileName = file.Name.ToLower();

                    if (fileName.EndsWith(".sqx") || (fileName.EndsWith(".sqf") && !fileName.EndsWith(".sqx.sqf")) || fileName.EndsWith(".ext") || fileName.EndsWith(".txt") || fileName == "mission.sqm")
                    {
                        // Lägg till filen i projektet om det inte redan finns i projektet.
                        if (Project.ProjectRootNode.GetNodeByRelativeFileName(file.Name) == null)
                        {
                            string absoluteFilePathName = Path.Combine(ProjectRootDirectory, file.Name);
                            AddExistingFileNode(absoluteFilePathName);
                        }
                    }
                }

                AddInitFileNodes(cpack);
            }
        }

        private void AddInitFileNodes(CPack cpack)
        {
            var initFiles = new List<string>();

            if (!string.IsNullOrWhiteSpace(cpack.InitLine))
            {
                initFiles.Add("init.sqf");
            }

            if (!string.IsNullOrWhiteSpace(cpack.InitPlayerLocalLine))
            {
                initFiles.Add("initplayerlocal.sqf");
            }

            if (!string.IsNullOrWhiteSpace(cpack.InitPlayerServerLine))
            {
                initFiles.Add("initplayerserver.sqf");
            }

            if (!string.IsNullOrWhiteSpace(cpack.InitServerLine))
            {
                initFiles.Add("initserver.sqf");
            }

            foreach (string initFileName in initFiles)
            {
                // Lägg till filen i projektet om det inte redan finns i projektet.
                if (Project.ProjectRootNode.GetNodeByRelativeFileName(initFileName) == null)
                {
                    string absoluteFilePathName = Path.Combine(ProjectRootDirectory, initFileName);
                    AddExistingFileNode(absoluteFilePathName);
                }
            }
        }

        public void RemoveProjectFilesThatDoNotExist()
        {
            Project.ProjectRootNode.RemoveChildrenThatDoNotHaveAFile(ProjectRootDirectory);
        }

        public string ProjectRootDirectory
        {
            get
            {
                if (Project != null)
                {
                    return Path.GetDirectoryName(Project.AbsoluteFilePathName);
                }

                return null;
            }
        }
	}
}
