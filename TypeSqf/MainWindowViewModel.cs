using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;
using TypeSqf.Edit.Highlighting;
using TypeSqf.Model;
using TypeSqf.Edit.Services;
using TypeSqf.Analyzer.Compile;
using TypeSqf.WebService;
using SwiftPbo;
using TypeSqf.Analyzer.Commands;
using TypeSqf.Analyzer;

namespace TypeSqf.Edit
{
	public class MainWindowViewModel : INotifyPropertyChanged
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
		private static List<GlobalContextVariable> _missionCfgPublicVariables = null;
		private volatile List<GlobalContextVariable> _declaredPublicVariables = null;
        private volatile List<GlobalContextVariable> _declaredPrivateVariables = null;
        private volatile List<TypeInfo> _declaredTypes = null;
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

            _analyzerWorker = new BackgroundWorker();
            _analyzerWorker.DoWork += AnalyzerWorkerOnDoWork;
            _analyzerWorker.RunWorkerCompleted += AnalyzerWorkerOnRunWorkerCompleted;
            AnalyzerListFileQueue = new List<string>();

            _compilerWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
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
                CodeAnalyzer.LoadScriptCommandsFromDisk();
            }
            catch (Exception ex)
            {
                UserMessageService.ShowMessage(ex.Message, "ScriptCommand.xml File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private List<string> AnalyzerListFileQueue { get; set; }

        private void StartAnalyzer()
        {
            string explicitFileName = "";
            if (AnalyzerListFileQueue != null && AnalyzerListFileQueue.Count > 0)
            {
                explicitFileName = AnalyzerListFileQueue[0];
            }

            StartAnalyzer(explicitFileName);
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

            if (_analyzerWorker != null && ActiveTabIndex >= 0 && !_analyzerWorker.IsBusy && !_compilerWorker.IsBusy)
            {
                bool performHunt = Project != null && Project.ProjectRootNode != null && (_declaredPublicVariables == null || _declaredTypes == null || _declaredPrivateVariables == null);
                if (performHunt)
                {
                    List<CodeFile> codes2 = new List<CodeFile>();
                    Project.ProjectRootNode.GetAllCodes(ProjectRootDirectory.ToLower(), codes2);
                    _declaredPublicVariables = new List<GlobalContextVariable>();
                    _declaredPrivateVariables = new List<GlobalContextVariable>();
                    _declaredTypes = new List<TypeInfo>();

                    var args2 = new AnalyzerWorkerArgs()
                    {
                        CollectPublicVariablesAndClasses = performHunt,
                        CodeFiles = codes2,
                        DeclaredPublicVariables = _declaredPublicVariables,
                        DeclaredPrivateVariables = _declaredPrivateVariables,
                        DeclaredTypes = _declaredTypes,
                        FilteredOutAnalyzerResults = Project.FilteredAnalyzerResultItems.ToList(),
                    };

                    _analyzerWorker.RunWorkerAsync(args2);
                    return;
                }

                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = ActiveTabIndex >= 0 ? Tabs[ActiveTabIndex].AbsoluteFilePathName.ToLower() : "";
                }

                bool fileBelongsToProject = !string.IsNullOrEmpty(ProjectRootDirectory) 
                    && !string.IsNullOrEmpty(fileName)
                    && !fileName.ToLower().EndsWith(".sqx.sqf")
                    && fileName.ToLower().StartsWith(ProjectRootDirectory.ToLower());

                if (_declaredPublicVariables == null)
                {
                    _declaredPublicVariables = new List<GlobalContextVariable>();
                }
                if (_declaredPrivateVariables == null)
                {
                    _declaredPrivateVariables = new List<GlobalContextVariable>();
                }
                if (_declaredTypes == null)
                {
                    _declaredTypes = new List<TypeInfo>();
                }

                _startAnalyzerWhenPossible = performHunt;

                List<CodeFile> codes = new List<CodeFile>();

                if (performHunt)
                {
                    Project.ProjectRootNode.GetAllCodes(ProjectRootDirectory.ToLower(), codes);
                }
                else if (usingExplicitFileName)
                {
                    codes.Add(new CodeFile(fileName.ToLower(), File.ReadAllText(fileName)));
                }
                else
                {
                    codes.Add(new CodeFile(Tabs[ActiveTabIndex].AbsoluteFilePathName, Tabs[ActiveTabIndex].Text));
                }

                var filteredAnalyzerResultItems = new List<string>();
                if (Project != null && Project.FilteredAnalyzerResultItems != null)
                {
                    filteredAnalyzerResultItems = Project.FilteredAnalyzerResultItems.ToList();
                }

                var declaredPublicVariablesToUse = _declaredPublicVariables;
                var declaredPrivateVariablesToUse = _declaredPrivateVariables;
                var declaredClassesToUse = _declaredTypes;
                var filteredAnalyzerResultItemsToUse = filteredAnalyzerResultItems;

                if (!fileBelongsToProject)
                {
                    declaredPublicVariablesToUse = new List<GlobalContextVariable>();
                    declaredPrivateVariablesToUse = new List<GlobalContextVariable>();
                    declaredClassesToUse = new List<TypeInfo>();
                    filteredAnalyzerResultItemsToUse = new List<string>();
                }

                var args = new AnalyzerWorkerArgs()
                {
                    CollectPublicVariablesAndClasses = performHunt,
                    CodeFiles = codes,
                    DeclaredPublicVariables = declaredPublicVariablesToUse,
                    DeclaredPrivateVariables = declaredPrivateVariablesToUse,
                    DeclaredTypes = declaredClassesToUse,
                    FilteredOutAnalyzerResults = filteredAnalyzerResultItemsToUse,
                };

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

                _analyzerWorker.RunWorkerAsync(args);
            }
        }

        private void AnalyzerWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            var args = e.Argument as AnalyzerWorkerArgs;

            if (args.CollectPublicVariablesAndClasses)
            {
                e.Result = null;

                // Collect types/classes
                foreach (CodeFile code in args.CodeFiles)
                {
                    string extension = Path.GetExtension(code.FileName).ToLower();

                    if (extension == ".sqf" || extension == ".sqx")
                    {
                        var analyzer = new CodeAnalyzer(code.Code, new ScriptCommandCache(), code.FileName, extension == ".sqx", new List<GlobalContextVariable>(), new List<GlobalContextVariable>(), args.DeclaredTypes, ProjectRootDirectory, args.FilteredOutAnalyzerResults);
                        analyzer.AnalyzeLogics();
                    }
                }

				// Collect public variables

				GetMissionCfgPublicVariables(args.DeclaredPublicVariables);

				foreach (CodeFile code in args.CodeFiles)
                {
                    string extension = Path.GetExtension(code.FileName).ToLower();

                    if (extension == ".sqf" || extension == ".sqx")
                    {
                        var analyzer = new CodeAnalyzer(code.Code, new ScriptCommandCache(), code.FileName, extension == ".sqx", args.DeclaredPublicVariables, args.DeclaredPrivateVariables, args.DeclaredTypes, ProjectRootDirectory, args.FilteredOutAnalyzerResults);
                        analyzer.AnalyzeLogics();
                    }
                }
            }
            else
            {
				string extension = Path.GetExtension(args.CodeFiles[0].FileName).ToLower();

                if (extension == ".sqf" || extension == ".sqx")
                {
					GetMissionCfgPublicVariables(args.DeclaredPublicVariables);

					var analyzer = new CodeAnalyzer(args.CodeFiles[0].Code, new ScriptCommandCache(), args.CodeFiles[0].FileName.ToLower(), extension == ".sqx", args.DeclaredPublicVariables, args.DeclaredPrivateVariables, args.DeclaredTypes, ProjectRootDirectory, args.FilteredOutAnalyzerResults);
                    analyzer.AnalyzeLogics();
                    e.Result = new AnalyzerResult
                    {
                        AbsoluteFileName = args.CodeFiles[0].FileName,
                        CodeErrors = analyzer.GetErrors()
                    };
                }
            }
        }

        private void AnalyzerWorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
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
            }

            if (_startAnalyzerWhenPossible)
            {
                StartAnalyzer();
            }
            else if (_startCompilerWhenPossible)
            {
                StartCompiler(_startCompilerWhenPossibleFileName);
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

        private void StartCompiler(string currentFilePathName = "")
        {
            SaveAllFiles();
            _startCompilerWhenPossible = true;
            _startCompilerWhenPossibleFileName = currentFilePathName;
            CompilerProgressBarIndeterminate = true;

            if (!_compilerWorker.IsBusy && !_analyzerWorker.IsBusy)
            {
                ChangeSelectedResultTab(ResultTabs.Compiler);
                _startCompilerWhenPossible = false;
                _startCompilerWhenPossibleFileName = "";
                CompilerProgressBarIndeterminate = false;
                CompilerProgressBarValue = 0;
                CompilerResultItems.Clear();
                CompilerResultItems.Add(new AnalyzerResultItem("Build started at " + DateTime.Now.ToString("HH:mm:ss") + "."));
                _compilerWorker.RunWorkerAsync( new CompilerWorkerArgument { ProjectRootDirectory = ProjectRootDirectory, CurrentFilePathName = currentFilePathName });
            }
            else
            {
                ChangeSelectedResultTab(ResultTabs.Compiler);
                CompilerProgressBarValue = 0;
                CompilerResultItems.Clear();
                CompilerResultItems.Add(new AnalyzerResultItem("Analyzing project files. Compilation will start soon. Please wait..."));
            }
        }

        private void CompilerWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            CompilerWorkerArgument argument = e.Argument as CompilerWorkerArgument;
            PerformCompilation(sender as BackgroundWorker, argument.ProjectRootDirectory, argument.CurrentFilePathName);
        }

        class CompilerWorkerArgument
        {
            public string ProjectRootDirectory { get; set; }

            public string CurrentFilePathName { get; set; }
        }

        private void CompilerWorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            int errorCount = CompilerResultItems.Count(i => i.IsError);

            ChangeSelectedResultTab(ResultTabs.Compiler);
            CompilerProgressBarValue = CompilerProgressBarMax;

            if (errorCount == 0)
            {
                CompilerResultItems.Add(new AnalyzerResultItem("Build completed successfully."));
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
                StartCompiler(_startCompilerWhenPossibleFileName);
            }
        }

        private void CompilerWorkerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var item = e.UserState as AnalyzerResultItem;
            int progress = e.ProgressPercentage;

            CompilerProgressBarValue = progress;
            if (item != null)
            {
                CompilerResultItems.Add(item);
            }
        }

        public void CompileAsync(string currentFilePathName = "")
        {
            StartCompiler(currentFilePathName);
        }

        List<string> _compilerProjectFileNames = null;
        List<TypeInfo> _compilerDeclaredTypes = null;
        List<GlobalContextVariable> _compilerDeclaredPublicVariables = null;
        List<GlobalContextVariable> _compilerDeclaredPrivateVariables = null;
        List<CodeFile> _compilerFiles = null;

        public void PerformCompilation(BackgroundWorker compilerWorker, string projectRootDirectory, string currentFilePathName = "")
        {
            if (string.IsNullOrWhiteSpace(projectRootDirectory))
            {
                compilerWorker.ReportProgress(0, new AnalyzerResultItem("Project has no valid root directory. Aborting."));
                return;
            }

            // If only the current file is being compiled
            if (!string.IsNullOrEmpty(currentFilePathName))
            {
                if (_compilerProjectFileNames != null)
                {
                    // Read the file
                    string currentCode = File.ReadAllText(currentFilePathName);

                    // Update the file in the file collection
                    CodeFile existingCode = _compilerFiles.FirstOrDefault(f => f.FileName == currentFilePathName);
                    if (existingCode != null)
                    {
                        _compilerFiles.Remove(existingCode);
                    }
                    _compilerFiles.Add(new CodeFile(currentFilePathName, currentCode));

                    /*
                    // First, run through all files to collect all types/classes
                    foreach (CodeFile code in _compilerFiles)
                    {
                        if (Path.GetExtension(code.FileName).ToLower() == ".sqx")
                        {
                            var analyzer = new CodeAnalyzer(code.Code, new ScriptCommandCache(), code.FileName, true, new List<GlobalContextVariable>(), new List<GlobalContextVariable>(), _compilerDeclaredTypes, ProjectRootDirectory);
                            analyzer.AnalyzeLogics();
                        }
                    }

                    // Then, run through all files again to collect all public variables
                    foreach (CodeFile file in _compilerFiles)
                    {
                        var analyzer = new CodeAnalyzer(file.Code, new ScriptCommandCache(), file.FileName, true, _compilerDeclaredPublicVariables, _compilerDeclaredPrivateVariables, _compilerDeclaredTypes, ProjectRootDirectory);
                        analyzer.AnalyzeLogics();
                    }
                    */

                    try
                    {
                        string sqxContent = File.ReadAllText(currentFilePathName);
                        CodeError[] analyzerCodeErrors;
                        string sqfContent = SqxCompiler.Compile(sqxContent, currentFilePathName, out analyzerCodeErrors, _compilerDeclaredPublicVariables, _compilerDeclaredTypes, ProjectRootDirectory, Settings.AddMethodCallLogging);

                        foreach (var error in analyzerCodeErrors)
                        {
                            if (error.Priority >= 4)
                            {
                                //AnalyzerResultItems.Add(new AnalyzerResultItem { AbsoluteFilePathName = file.FileName, RelativeFilePathName = AbsoluteToRelativeFilePathName(file.FileName), LineNo = error.LineNumber, Description = error.Message });
                                compilerWorker.ReportProgress(50, new AnalyzerResultItem(error.Message, error.LineNumber, currentFilePathName, AbsoluteToRelativeFilePathName(currentFilePathName)));
                            }
                        }

                        string outputFileName = currentFilePathName + ".sqf";
                        var outputStream = File.CreateText(outputFileName);
                        outputStream.Write(sqfContent);
                        outputStream.Close();
                    }
                    catch (Exception ex)
                    {
                        compilerWorker.ReportProgress(50, new AnalyzerResultItem(ex.Message, 0, currentFilePathName, AbsoluteToRelativeFilePathName(currentFilePathName)));
                    }
                }
            }
            else // Full compile
            {
                _compilerProjectFileNames = new List<string>();
                _compilerDeclaredTypes = new List<TypeInfo>();
                _compilerDeclaredPublicVariables = new List<GlobalContextVariable>();
                _compilerDeclaredPrivateVariables = new List<GlobalContextVariable>();
                _compilerFiles = new List<CodeFile>();

                // Get the files in the project
                FindProjectFiles(projectRootDirectory, _compilerProjectFileNames);

                int _progress = 0;
                CompilerProgressBarMax = (_compilerProjectFileNames.Count(i => i.EndsWith(".sqx", true, null))) * 3;

                // First, run through all files to collect all types/classes
                compilerWorker.ReportProgress(_progress, new AnalyzerResultItem("Collecting types..."));

                foreach (string filePathName in _compilerProjectFileNames)
                {
                    if (FileInProject(filePathName) && Path.GetExtension(filePathName).ToLower() == ".sqx")
                    {
                        string code = File.ReadAllText(filePathName);
                        _compilerFiles.Add(new CodeFile(filePathName, code));

                        var analyzer = new CodeAnalyzer(code, new ScriptCommandCache(), filePathName, true, new List<GlobalContextVariable>(), new List<GlobalContextVariable>(), _compilerDeclaredTypes, ProjectRootDirectory);
                        analyzer.AnalyzeLogics();

                        _progress++;
                        compilerWorker.ReportProgress(_progress);
                    }
                }

                // Then, run through all files again to collect all public variables
                compilerWorker.ReportProgress(_progress, new AnalyzerResultItem("Fetching variables..."));

                foreach (CodeFile file in _compilerFiles)
                {
                    var analyzer = new CodeAnalyzer(file.Code, new ScriptCommandCache(), file.FileName, true, _compilerDeclaredPublicVariables, _compilerDeclaredPrivateVariables, _compilerDeclaredTypes, ProjectRootDirectory);
                    analyzer.AnalyzeLogics();

                    _progress++;
                    compilerWorker.ReportProgress(_progress);
                }


                // Perform the compile of all files
                compilerWorker.ReportProgress(CompilerProgressBarValue, new AnalyzerResultItem("Building..."));

                foreach (CodeFile file in _compilerFiles)
                {
                    try
                    {
                        string sqxContent = File.ReadAllText(file.FileName);
                        CodeError[] analyzerCodeErrors;
                        string sqfContent = SqxCompiler.Compile(sqxContent, file.FileName, out analyzerCodeErrors, _compilerDeclaredPublicVariables, _compilerDeclaredTypes, ProjectRootDirectory, Settings.AddMethodCallLogging);

                        foreach (var error in analyzerCodeErrors)
                        {
                            if (error.Priority >= 4)
                            {
                                compilerWorker.ReportProgress(CompilerProgressBarValue, new AnalyzerResultItem(error.Message, error.LineNumber, file.FileName, AbsoluteToRelativeFilePathName(file.FileName)));
                            }
                        }

                        string outputFileName = file.FileName + ".sqf";
                        var outputStream = File.CreateText(outputFileName);
                        outputStream.Write(sqfContent);
                        outputStream.Close();

                        _progress++;
                        compilerWorker.ReportProgress(_progress);
                    }
                    catch (Exception ex)
                    {
                        compilerWorker.ReportProgress(CompilerProgressBarValue, new AnalyzerResultItem(ex.Message, 0, file.FileName, AbsoluteToRelativeFilePathName(file.FileName)));
                    }
                }
            }
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

                    _declaredPublicVariables = null;
                    _declaredPrivateVariables = null;
                    _declaredTypes = null;

                    // Close all open files
                    CloseAllTabs();

                    OnProjectChanged();
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

            _declaredPublicVariables = null;
            _declaredPrivateVariables = null;
            _declaredTypes = null;

            string rootPath = Path.GetDirectoryName(fileName);
            string missionName = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(fileName));

            CreateProjectOnLocation(rootPath, Path.GetFileNameWithoutExtension(missionName));

            Project.AbsoluteFilePathName = Path.Combine(rootPath, missionName + ".tproj");
            Settings.ProjectFileName = Project.AbsoluteFilePathName;

            CloseAllTabs();

			SaveProjectFile();

            InitFileWatcher(rootPath);

            OnProjectChanged();
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
				_missionCfgPublicVariables = null;
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
                    if (_activeTabIndex >= 0)
                    {
                        OnTabGettingFocus();
                    }
                    StartAnalyzer();
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

            //string lowerFileName = fileName.ToLower();
            //if (!lowerFileName.EndsWith(".sqf") && !lowerFileName.EndsWith(".sqx") && !lowerFileName.EndsWith(".ext") && !lowerFileName.EndsWith(".cpp"))
            //{
            //    fileName = fileName + extension;
            //}

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
                //AbsoluteFileName = absoluteFilePathName,
                DisplayName = displayName,
                IsSelected = true
            };

            SelectedProjectNode.InsertChildNode(fileNode);
            OpenFileInTab(absoluteFilePathName);
            SaveProjectFile();
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
            if (DeclaredPublicVariables != null)
            {
                foreach (var variable in DeclaredPublicVariables.Where(v => v.FileName.ToLower() == fileName.ToLower()).ToList())
                {
                    DeclaredPublicVariables.Remove(variable);
                }
            }

            if (DeclaredTypes != null)
            {
                foreach (var classInfo in DeclaredTypes.Where(v => v.FileName.ToLower() == fileName.ToLower()).ToList())
                {
                    DeclaredTypes.Remove(classInfo);
                }
            }
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
        private DelegateCommand _compileCommand;
        private DelegateCommand _compileCurrentFileCommand;
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

        public DelegateCommand SaveAllFilesCommand
        {
            get { return (_saveAllFilesCommand = _saveAllFilesCommand ?? new DelegateCommand(x => true, OnSaveAllFiles)); }
        }

        public void SaveAllFiles()
        {
            bool projectFileSaved = SaveProjectFile();

            if (FileWatcher != null)
            {
                FileWatcher.EnableRaisingEvents = false;
            }

            //if (projectFileSaved)
            //{
                for (int i = 0; i < Tabs.Count; i++)
                {
                    TabViewModel tab = Tabs[i];

                    if (string.IsNullOrWhiteSpace(tab.AbsoluteFilePathName))
                    {
                        ActiveTabIndex = i;
                    }

                    Tabs[i].Save(FileService);
                }

                SaveFileCommand.RaiseCanExecuteChanged();
            //}

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
                    _tabOpenedOrder.Remove(fileName);

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

        public void FindProjectFiles(string sourceDir, List<string> allFiles)
        {
            string[] fileEntries = Directory.GetFiles(sourceDir);
            foreach (string fileName in fileEntries)
            {
                allFiles.Add(fileName);
            }

            //Recursion    
            string[] subdirectoryEntries = Directory.GetDirectories(sourceDir);
            foreach (string item in subdirectoryEntries)
            {
                // Avoid "reparse points"
                FileAttributes attributes = File.GetAttributes(item);
                //bool isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);
                bool isHidden = attributes.HasFlag(FileAttributes.Hidden);
                bool isSystem = attributes.HasFlag(FileAttributes.System);

                if (!isHidden && !isSystem)
                //if ((File.GetAttributes(item) & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
                {
                    FindProjectFiles(item, allFiles);
                }
            }
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
            FindProjectFiles(ProjectRootDirectory, allFiles);

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

        private void DoCompile(object context)
        {
            CompileAsync();
        }

        private void DoCompileCurrentFile(object context)
        {
            CompileAsync(ActiveTab.AbsoluteFilePathName);
        }

        public DelegateCommand CompileCommand
        {
            get { return (_compileCommand = _compileCommand ?? new DelegateCommand(x => true, DoCompile)); }
        }

        public DelegateCommand CompileCurrentFileCommand
        {
            get { return (_compileCurrentFileCommand = _compileCurrentFileCommand ?? new DelegateCommand(x => true, DoCompileCurrentFile)); }
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
							if (file.Name.ToLower().EndsWith(".sqx") || file.Name.ToLower().EndsWith(".sqf") && !file.Name.ToLower().EndsWith(".sqx.sqf"))
							{
								// Lägg till filen i projektet om det inte redan finns i projektet.
								if (Project.ProjectRootNode.GetNodeByRelativeFileName(file.Name) == null)
								{
									string absoluteFilePathName = Path.Combine(ProjectRootDirectory, file.Name);
									AddExistingFileNode(absoluteFilePathName);
								}
							}
						}
					}
				}

				foreach (CPackFile file in cpack.Files)
                {
                    if (file.Name.ToLower().EndsWith(".sqx") || file.Name.ToLower().EndsWith(".sqf") && !file.Name.ToLower().EndsWith(".sqx.sqf"))
                    {
                        // Lägg till filen i projektet om det inte redan finns i projektet.
                        if (Project.ProjectRootNode.GetNodeByRelativeFileName(file.Name) == null)
                        {
                            string absoluteFilePathName = Path.Combine(ProjectRootDirectory, file.Name);
                            AddExistingFileNode(absoluteFilePathName);
                        }
                    }
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

		public void RefreshPublicVariablesFromMissionCfg()
		{
			_missionCfgPublicVariables = new List<GlobalContextVariable>();

			try
			{
				string fileContent = File.ReadAllText(Path.Combine(ProjectRootDirectory, "mission.sqm"));

				int missionClassIndex = fileContent.IndexOf("class Mission\r\n{");

				if (missionClassIndex >= 0)
				{
					string missionContent = fileContent.Substring(missionClassIndex);

					var matches = Regex.Matches(missionContent, @"name=""[a-zA-Z0-9_]+""");

					foreach (Match match in matches)
					{
						_missionCfgPublicVariables.Add(new GlobalContextVariable(match.Value.Substring(6, match.Value.Length - 7), SqfDataType.Any, SqfDataType.Any.ToString(), false, "mission.sqm", -1));
					}
				}
			}
			catch
			{
			}
		}

		public void GetMissionCfgPublicVariables(List<GlobalContextVariable> declaredPublicVariables)
		{
			if (_missionCfgPublicVariables == null)
			{
				RefreshPublicVariablesFromMissionCfg();
			}

			foreach (var variable in declaredPublicVariables.Where(v => v.FileName.ToLower() == "mission.sqm").ToList())
			{
				declaredPublicVariables.Remove(variable);
			}

			foreach (GlobalContextVariable variable in _missionCfgPublicVariables)
			{
				declaredPublicVariables.Add(variable);
			}
		}
	}
}
