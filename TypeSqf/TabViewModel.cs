using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using TypeSqf.Edit.Folding;
using TypeSqf.Model;
using TypeSqf.Edit.Services;

namespace TypeSqf.Edit
{
    public class TabViewModel : INotifyPropertyChanged
    {
        private bool _isDirty;
        private string _name;
        private string _header;
        private string _text;
        private string _absoluteFilePathName;
        
        public TabViewModel(string name)
        {
            _name = "";
            _text = "";
            _name = name;
            _header = name;
            _absoluteFilePathName = "";
            _isDirty = false;
        }

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Name"));
                }
            }
        }

        public string AbsoluteFilePathName
        {
            get { return _absoluteFilePathName; }
            set
            {
                if (_absoluteFilePathName != value)
                {
                    _absoluteFilePathName = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("AbsoluteFilePathName"));
                }
            }
        }

        public string Header
        {
            get { return _header; }
            set
            {
                if (_header != value)
                {
                    _header = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Header"));
                }
            }
        }

        public string Text
        {
            get { return _text; }
            set
            {
                if (_text != value)
                {
                    _text = value;
                    IsDirty = true;
                    OnPropertyChanged(new PropertyChangedEventArgs("Text"));
                }
            }
        }

        private double _verticalOffset;
        public double VerticalOffset
        {
            get { return _verticalOffset; }
            set
            {
                if (_verticalOffset != value)
                {
                    _verticalOffset = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("VerticalOffset"));
                }
            }
        }

        private int _caretOffset;
        public int CaretOffset
        {
            get { return _caretOffset; }
            set
            {
                if (_caretOffset != value)
                {
                    _caretOffset = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("CaretOffset"));
                }
            }
        }

        private int _selectionStart;
        public int SelectionStart
        {
            get { return _selectionStart; }
            set
            {
                if (_selectionStart != value)
                {
                    _selectionStart = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("SelectionStart"));
                }
            }
        }

        private int _selectionLength;
        public int SelectionLength
        {
            get { return _selectionLength; }
            set
            {
                if (_selectionLength != value)
                {
                    _selectionLength = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("SelectionLength"));
                }
            }
        }

        public bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("IsDirty"));

                    if (_isDirty)
                    {
                        Header = Name + "*";
                    }
                    else
                    {
                        Header = Name;
                    }
                }
            }
        }

        public IEnumerable<TypeSqfFoldingSection> FoldingSections { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Save()
        {
            IsDirty = false;
        }

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, e);
            }
        }

        public void Load(string filePath)
        {
            Text = File.ReadAllText(filePath);
            IsDirty = false;
        }

        public bool Save(IFileService fileService)
        {
            if (AbsoluteFilePathName == "")
            {
                AbsoluteFilePathName = fileService.GetSaveFileName(CurrentApplication.SaveFileFilter, Name);
            }

            if (!string.IsNullOrEmpty(AbsoluteFilePathName))
            {
                // TODO: Processen kan inte komma åt filen description.ext eftersom den används i en annan process.
                File.WriteAllText(AbsoluteFilePathName, Text);
                IsDirty = false;
                return true;
            }

            return false;
        }
    }
}
