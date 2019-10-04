using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace TypeSqf.Model
{
    [Serializable]
    public class SettingsFile : INotifyPropertyChanged
    {
        public SettingsFile()
        {
            // Default values
            OpenFileNames = new List<string>();
            _enableAutoCompletion = true;
            _showBetaContent = false;
            _enableFolding = false;
            _selectedTheme = "Default Dark";
            _indentationSize = 4;
            _convertTabsToSpaces = false;
            _addMethodCallLogging = false;
            _deployment1Name = "Singleplayer";
            _deployment2Name = "Multiplayer";

            SwiftPbo.ArmaPath.ArmaPathInfo armaPathInfo = SwiftPbo.ArmaPath.DetectArmaPath.ArmaMissionsPath();
            _deployment1Directory = armaPathInfo.Success ? armaPathInfo.Path : "";

            armaPathInfo = SwiftPbo.ArmaPath.DetectArmaPath.ArmaMpMissionsPath();
            _deployment2Directory = armaPathInfo.Success ? armaPathInfo.Path : "";
        }

        //public int Version
        //{
        //    get { return 2; }
        //    set { }
        //}

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RemoveOpenFileNames()
        {
            OpenFileNames = new List<string>();
        }

        public string ProjectFileName { get; set; }

        public List<String> OpenFileNames { get; private set; }

        private bool _showBetaContent;
        public bool ShowBetaContent
        {
            get
            {
                return _showBetaContent;
            }
            set
            {
                if (_showBetaContent != value)
                {
                    _showBetaContent = value;
                    OnPropertyChanged("ShowBetaContent");
                }
            }
        }

        private bool _enableAutoCompletion;
        public bool EnableAutoCompletion
        {
            get
            {
                return _enableAutoCompletion;
            }
            set
            {
                if (_enableAutoCompletion != value)
                {
                    _enableAutoCompletion = value;
                    OnPropertyChanged("EnableAutoCompletion");
                }
            }
        }

        private bool _enableFolding;
        public bool EnableFolding
        {
            get
            {
                return _enableFolding;
            }
            set
            {
                if (_enableFolding != value)
                {
                    _enableFolding = value;
                    OnPropertyChanged("EnableFolding");
                }
            }
        }

        private string _selectedTheme;
        public string SelectedTheme
        {
            get
            {
                return _selectedTheme;
            }
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged("SelectedTheme");
                }
            }
        }

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
                }
            }
        }

        private string _deployment1Name;
        public string Deployment1Name
        {
            get
            {
                return _deployment1Name;
            }
            set
            {
                if (_deployment1Name != value)
                {
                    _deployment1Name = value;
                    OnPropertyChanged("Deployment1Name");
                }
            }
        }

        private string _deployment1Directory;
        public string Deployment1Directory
        {
            get
            {
                return _deployment1Directory;
            }
            set
            {
                if (_deployment1Directory != value)
                {
                    _deployment1Directory = value;
                    OnPropertyChanged("Deployment1Directory");
                }
            }
        }

        private string _deployment2Name;
        public string Deployment2Name
        {
            get
            {
                return _deployment2Name;
            }
            set
            {
                if (_deployment2Name != value)
                {
                    _deployment2Name = value;
                    OnPropertyChanged("Deployment2Name");
                }
            }
        }

        private string _deployment2Directory;
        public string Deployment2Directory
        {
            get
            {
                return _deployment2Directory;
            }
            set
            {
                if (_deployment2Directory != value)
                {
                    _deployment2Directory = value;
                    OnPropertyChanged("Deployment2Directory");
                }
            }
        }
    }
}
