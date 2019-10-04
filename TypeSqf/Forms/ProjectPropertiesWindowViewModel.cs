using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TypeSqf.Edit.Highlighting;
using TypeSqf.Model;
using TypeSqf.Edit.Services;

namespace TypeSqf.Edit.Forms
{
    public class ProjectPropertiesWindowViewModel : INotifyPropertyChanged
    {
        //----------------------------------------------------------------------------------------------------------------
        #region Construction/Destruction

        public ProjectPropertiesWindowViewModel()
        {
            ThemeNames = new ObservableCollection<string>();

            // Add the template files
            try
            {
                DirectoryInfo d = new DirectoryInfo(Path.Combine(CurrentApplication.AppDataFolder, "Themes"));
                FileInfo[] Files = d.GetFiles();

                foreach (FileInfo file in Files.OrderBy(x => x.Extension).ThenBy(x => x.Name))
                {
                    string fileName = Path.GetFileNameWithoutExtension(file.Name);
                    ThemeNames.Add(fileName);
                }
            }
            catch
            {
                ThemeNames.Add("Default Light");
                ThemeNames.Add("Default Dark");
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Private Variables

        private string _folderName;

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Public Properties

        private bool _enableAutoCompletion { get; set; }
        public bool EnableAutoCompletion
        {
            get { return _enableAutoCompletion; }
            set
            {
                if (_enableAutoCompletion != value)
                {
                    _enableAutoCompletion = value;
                    OnPropertyChanged("EnableAutoCompletion");
                }
            }
        }

        private bool _enableFolding { get; set; }
        public bool EnableFolding
        {
            get { return _enableFolding; }
            set
            {
                if (_enableFolding != value)
                {
                    _enableFolding = value;
                    OnPropertyChanged("EnableFolding");
                }
            }
        }

        private int _selectedThemeIndex;
        public int SelectedThemeIndex
        {
            get { return _selectedThemeIndex; }
            set
            {
                if (_selectedThemeIndex != value)
                {
                    _selectedThemeIndex = value;
                    OnPropertyChanged("SelectedThemeIndex");
                }
            }
        }

        public string SelectedThemeName
        {
            get {
                if (SelectedThemeIndex >= 0)
                {
                    return ThemeNames[SelectedThemeIndex];
                }

                SelectedThemeIndex = 0;
                return ThemeNames[SelectedThemeIndex];
            }
            set
            {
                for (int i = 0; i < ThemeNames.Count; i++)
                {
                    if (ThemeNames[i] == value)
                    {
                        SelectedThemeIndex = i;
                        return;
                    }
                }
            }
        }

        public ObservableCollection<string> ThemeNames { get; private set; }

        private bool _convertTabsToSpaces;
        public bool ConvertTabsToSpaces
        {
            get
            {
                return _convertTabsToSpaces;
            }
            set
            {
                if (_convertTabsToSpaces != value)
                {
                    _convertTabsToSpaces = value;
                    OnPropertyChanged("ConvertTabsToSpaces");
                }
            }
        }

        private bool _addMethodCallLogging;
        public bool AddMethodCallLogging
        {
            get
            {
                return _addMethodCallLogging;
            }
            set
            {
                if (_addMethodCallLogging != value)
                {
                    _addMethodCallLogging = value;
                    OnPropertyChanged("AddMethodCallLogging");
                }
            }
        }

        private int _indentationSize;
        public int IndentationSize
        {
            get
            {
                return _indentationSize;
            }
            set
            {
                if (_indentationSize != value)
                {
                    _indentationSize = value;
                    OnPropertyChanged("IndentationSize");
                    OnPropertyChanged("IndentationSizeString");
                }
            }
        }

        public string IndentationSizeString
        {
            get
            {
                return _indentationSize.ToString();
            }
            set
            {
                int tabSize;
                int.TryParse(value, out tabSize);
                _indentationSize = tabSize;
            }
        }

        public bool IsCanceled { get; set; }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
    }
}
