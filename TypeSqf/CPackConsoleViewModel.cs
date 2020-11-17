using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using TypeSqf.Edit.Service;
using TypeSqf.Model;
using TypeSqf.WebService;

namespace TypeSqf.Edit
{
    public class PackageEventArgs : EventArgs
    {
        public PackageEventArgs(string packageName)
        {
            PackageName = packageName;
        }

        public string PackageName { get; private set; }
    }

    public class CPackConsoleViewModel : INotifyPropertyChanged
    {
        public CPackConsoleViewModel(string projectRootDirectory)
        {
            ConsoleMessages = new ObservableCollection<string>();
            Service = new CPackService();
            ProjectRootDirectory = projectRootDirectory;

            ConsoleMessages.Add("Connecting...");

            TextBoxEnabled = false;

            Task<int> task = CurrentApplication.CheckNewWebServiceVersionAsync();
            task.ContinueWith(version =>
            {
                if (version.Result == 0) {
                    ConsoleMessages.Add("Failed to contact server " + CurrentApplication.TypeSqfDomain + ".");
                    ConsoleMessages.Add(">");
                    TextBoxEnabled = true;
                }
                else if (version.Result == CurrentWebService.Version) {
                    ConsoleMessages.Clear();
                    ConsoleMessages.Add("Please use any of the following commands:");
                    ConsoleMessages.Add(" Install <packagename> [-version <version>] [-beta]");
                    ConsoleMessages.Add(" Update <packagename> [-version <version>] [-beta]");
                    ConsoleMessages.Add(" Remove <packagename>");
                    ConsoleMessages.Add(" List [-beta]");
                    ConsoleMessages.Add(" Exit");
                    TextBoxEnabled = true;
                }
                else {
                    ConsoleMessages.Add("CPack version has changed and need to be updated before use. Pleast update TypeSqf to latest version and try again.");
                    ConsoleMessages.Add(">");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public event EventHandler Exit;

        protected void OnExit()
        {
            Exit?.Invoke(this, new EventArgs());
        }

        public delegate void PackageEventHandler(object sender, PackageEventArgs e);

        public event PackageEventHandler PackageInstalled;

        protected void OnPackageInstalled(string packageName)
        {
            PackageInstalled?.Invoke(this, new PackageEventArgs(packageName));
        }

        public ObservableCollection<string> ConsoleMessages { get; set; }

        private string ProjectRootDirectory { get; set; }

        private CPackService Service { get; set; }

        private bool _textBoxEnabled;

        public bool TextBoxEnabled
        {
            get { return _textBoxEnabled; }
            set
            {
                if (_textBoxEnabled != value) {
                    _textBoxEnabled = value;
                    OnPropertyChanged("TextBoxEnabled");
                }
            }
        }

        private void OnCommandProgress(string message)
        {
            ConsoleMessages.Add(message);
        }

        public async Task ExecuteCommand(string command)
        {
            TextBoxEnabled = false;
            ConsoleMessages.Clear();
            ConsoleMessages.Add(">" + command);

            if (ProjectRootDirectory == null || !File.Exists(Path.Combine(ProjectRootDirectory, "mission.sqm"))) {
                ConsoleMessages.Add("Project must be saved at least once, and the project file (.tproj) must be in the same directory as 'mission.sqm' in order to use the CPack Console.");
                ConsoleMessages.Add(">");
                TextBoxEnabled = true;
                return;
            }

            IProgress<string> progress = new Progress<string>(OnCommandProgress);
            AppVersion targetVersion = null;
            bool updateDependencies = false;
            bool removeDependencies = false;
            bool overwrite = false;
            bool beta = false;

            string[] commandItems = command.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < commandItems.Length; i++)
            {
                if (commandItems[i].ToLower() == "-version") {
                    if (i < commandItems.GetUpperBound(0)) {
                        AppVersion.TryParse(commandItems[i + 1], out targetVersion);
                        i++;
                    }
                }
                else if (commandItems[i].ToLower() == "-updatedep") {
                    updateDependencies = true;
                }
                else if (commandItems[i].ToLower() == "-removedep") {
                    removeDependencies = true;
                }
                else if (commandItems[i].ToLower() == "-overwrite") {
                    overwrite = true;
                }
                else if (commandItems[i].ToLower() == "-beta")
                {
                    beta = true;
                }
            }

            if (commandItems[0].ToLower() == "install")
            {
                if (commandItems.Length < 2)
                {
                    ConsoleMessages.Add("Command Install missing package name");
                    return;
                }

                string packageName = commandItems[1];
                var result = await Service.InstallPackageAsync(packageName, ProjectRootDirectory, targetVersion, updateDependencies, overwrite, beta, progress);
                TextBoxEnabled = true;

                if (result)
                {
                    OnPackageInstalled(packageName);
                }
                //result.ContinueWith((arg) => {});
            }
            else if (commandItems[0].ToLower() == "update") {
                if (commandItems.Length < 2) {
                    ConsoleMessages.Add("Command Update missing package name");
                    return;
                }

                string packageName = commandItems[1];
                var result = await Service.UpdatePackageAsync(packageName, ProjectRootDirectory, targetVersion, updateDependencies, overwrite, beta, progress);
                TextBoxEnabled = true;

                if (result)
                {
                    OnPackageInstalled(packageName);
                }
                //result.ContinueWith((arg) => { TextBoxEnabled = true; });
            }
            else if (commandItems[0].ToLower() == "remove")
            {
                if (commandItems.Length < 2)
                {
                    ConsoleMessages.Add("Command Update missing package name");
                    return;
                }

                string packageName = commandItems[1];
                var result = await Service.RemovePackageAsync(packageName, ProjectRootDirectory, removeDependencies, overwrite, progress);
                TextBoxEnabled = true;

                if (result)
                {
                    OnPackageInstalled(packageName);
                }
                //result.ContinueWith((arg) => {  });
            }
            else if (commandItems[0].ToLower() == "list")
            {
                var result = await Service.ListPackagesAsync(ProjectRootDirectory, beta, progress);
                TextBoxEnabled = true;
                //result.ContinueWith((arg) => {  });
            }
            else if (commandItems[0].ToLower() == "clear") {
                ConsoleMessages.Add(">");
            }
            else if (commandItems[0].ToLower() == "exit")
            {
                OnExit();
            }
            else
            {
                ConsoleMessages.Add("Unknown command: '" + commandItems[0] + "'");
                ConsoleMessages.Add(">");
                TextBoxEnabled = true;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
