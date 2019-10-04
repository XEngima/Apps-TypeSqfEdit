using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TypeSqf.Edit
{
    public class FindInAllFilesViewModel : INotifyPropertyChanged
    {
        //----------------------------------------------------------------------------------------------------------------
        #region Construction/Destruction

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Events

        public event EventHandler AfterPerformFind;

        protected void OnAfterPerformFind()
        {
            if (AfterPerformFind != null)
            {
                AfterPerformFind(this, new EventArgs());
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Private Variables

        private ObservableCollection<SearchResultItem> _searchResultItems = new ObservableCollection<SearchResultItem>();

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
        #region Properties

        private bool _caseSensitive = false;
        public bool CaseSensitive
        {
            get { return _caseSensitive; }
            set
            {
                if (_caseSensitive != value)
                {
                    _caseSensitive = value;
                    OnPropertyChanged("CaseSensitive");
                }
            }
        }

        private bool _caseSensitiveEnabled = true;
        public bool CaseSensitiveEnabled
        {
            get { return _caseSensitiveEnabled; }
            set
            {
                if (_caseSensitiveEnabled != value)
                {
                    _caseSensitiveEnabled = value;
                    OnPropertyChanged("CaseSensitiveEnabled");
                }
            }
        }

        private bool _wholeWords = false;
        public bool WholeWords
        {
            get { return _wholeWords; }
            set
            {
                if (_wholeWords != value)
                {
                    _wholeWords = value;
                    OnPropertyChanged("WholeWords");
                }
            }
        }

        private bool _wholeWordsEnabled = true;
        public bool WholeWordsEnabled
        {
            get { return _wholeWordsEnabled; }
            set
            {
                if (_wholeWordsEnabled != value)
                {
                    _wholeWordsEnabled = value;
                    OnPropertyChanged("WholeWordsEnabled");
                }
            }
        }

        private bool _useRegex = false;
        public bool UseRegex
        {
            get { return _useRegex; }
            set
            {
                if (_useRegex != value)
                {
                    _useRegex = value;
                    OnPropertyChanged("UseRegex");

                    if (_useRegex)
                    {
                        WholeWords = false;
                    }

                    WholeWordsEnabled = !_useRegex;
                }
            }
        }

        public string ProjectPath { get; set; }

        public ObservableCollection<SearchResultItem> SearchResultItems
        {
            get
            {
                return _searchResultItems;
            }
        }

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get
            {
                return _selectedIndex;
            }
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    OnPropertyChanged("SelectedIndex");
                }
            }
        }

        public SearchResultItem SelectedItem
        {
            get {
                if (SelectedIndex >= 0)
                {
                    return SearchResultItems[SelectedIndex];
                }

                return null;
            }
        }

        private string _searchText;
        public string SearchText
        {
            get
            {
                return _searchText;
            }
            set
            {
                if (value != _searchText)
                {
                    _searchText = value;
                    OnPropertyChanged("SearchText");
                }
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Methods

        public void AddFileNamesToList(string sourceDir, List<string> allFiles)
        {
            if (string.IsNullOrWhiteSpace(sourceDir))
            {
                return;
            }

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
                bool isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);
                bool isHidden = attributes.HasFlag(FileAttributes.Hidden);
                bool isSystem = attributes.HasFlag(FileAttributes.System);

                if (!isHidden && !isSystem && !isReparsePoint && !item.StartsWith(".") && !item.Contains("\\."))
                {
                    AddFileNamesToList(item, allFiles);
                }
            }
        }

        private int LineFromPos(string s, int pos)
        {
            int Res = 1;
            for (int i = 0; i <= pos - 1; i++)
                if (s[i] == '\n') Res++;
            return Res;
        }

        public void PerformFind()
        {
            SearchResultItems.Clear();

            List<string> allFiles = new List<string>();
            AddFileNamesToList(ProjectPath, allFiles);

            foreach (string fileName in allFiles)
            {
                string contents = File.ReadAllText(fileName);
                var matches = new List<SearchMatch>();
                int count = 0;
                RegexOptions regexOptions = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

                if (UseRegex)
                {
                    string[] lines = contents.Replace("\r\n", "\n").Split("\n".ToCharArray());
                    int lineNo = 0;
                    int caretIndex = 0;

                    foreach (string line in lines)
                    {
                        lineNo++;
                        MatchCollection matchCollection = new Regex(SearchText, regexOptions).Matches(line);
                        if (matchCollection.Count > 0)
                        {
                            count += matchCollection.Count;
                            foreach (var match in matchCollection)
                            {
                                matches.Add(new SearchMatch(caretIndex + ((Match)match).Index, ((Match)match).Length, lineNo));
                            }
                        }

                        caretIndex += line.Length + 2;
                    }
                }
                else
                {
                    //string searchText = CaseSensitive ? SearchText : SearchText.ToLower();
                    //string fixedContent = CaseSensitive ? contents : contents.ToLower();
                    string searchText = SearchText;
                    searchText = Regex.Escape(searchText);

                    if (WholeWords)
                    {
                        MatchCollection matchCollection = new Regex("\\b" + searchText + "\\b", regexOptions).Matches(contents);
                        if (matchCollection.Count > 0)
                        {
                            count = matchCollection.Count;
                            foreach (var match in matchCollection)
                            {
                                matches.Add(new SearchMatch(((Match)match).Index, ((Match)match).Length, LineFromPos(contents, ((Match)match).Index)));
                            }
                        }
                    }
                    else
                    {
                        MatchCollection matchCollection = new Regex(searchText, regexOptions).Matches(contents);
                        if (matchCollection.Count > 0)
                        {
                            count = matchCollection.Count;
                            foreach (var match in matchCollection)
                            {
                                matches.Add(new SearchMatch(((Match)match).Index, ((Match)match).Length, LineFromPos(contents, ((Match)match).Index)));
                            }
                        }
                    }
                }

                if (count > 0)
                {
                    SearchResultItems.Add(new SearchResultItem(fileName, fileName.Substring(ProjectPath.Length + 1), count, contents, matches.ToArray()));
                }
            }

            if (SearchResultItems.Count > 0)
            {
                SelectedIndex = 0;
            }

            OnAfterPerformFind();
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Commands

        private DelegateCommand _performFindCommand;

        public DelegateCommand PerformFindCommand
        {
            get { return (_performFindCommand = _performFindCommand ?? new DelegateCommand(x => true, OnPerformFind)); }
        }

        private void OnPerformFind(object context)
        {
            PerformFind();
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
    }
}
