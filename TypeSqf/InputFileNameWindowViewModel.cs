using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using TypeSqf.Model;
using TypeSqf.Edit.Services;

namespace TypeSqf.Edit
{
    public class TemplateFileVariable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Name { get; set; }

        private string _value;
        public string Value {
            get {
                return _value;
            }
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged("Value");
                }
            }
        }
    }

    public class InputFileNameWindowViewModel : INotifyPropertyChanged
    {
        //----------------------------------------------------------------------------------------------------------------
        #region Private Variables

        private string _fileName;

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Construction

        public InputFileNameWindowViewModel()
        {
            FileTemplates = new ObservableCollection<FileTemplate>();
            TemplateVariables = new ObservableCollection<TemplateFileVariable>();

            FileTemplates.Add(new FileTemplate("No Template (Empty File)", "", ""));
            // FileTemplates.Add(new FileTemplate("Empty File (SQX)", "sqx", ""));

            // Add the template files
            try
            {
                DirectoryInfo d = new DirectoryInfo(Path.Combine(CurrentApplication.AppDataFolder, "Templates"));
                FileInfo[] Files = d.GetFiles();

                foreach (FileInfo file in Files.OrderBy(x => x.Extension).ThenBy(x => x.Name))
                {
                    string extension = file.Extension;
                    string fileName = file.Name;

                    if (fileName.Length > extension.Length)
                    {
                        fileName = fileName.Substring(0, fileName.Length - extension.Length);
                    }

                    if (extension.StartsWith("."))
                    {
                        extension = extension.Substring(1);
                    }

                    string content = File.ReadAllText(file.FullName);

                    FileTemplates.Add(new FileTemplate(fileName, extension, content));
                }
            }
            catch
            {
                // Do nothing.
            }

            SelectedTemplateIndex = 0;
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Public Properties

        public Action CloseAction { get; set; }

        public bool? Result { get; set; }

        public int WindowHeight {
            get
            {
                return 217 + TemplateVariablesHeight;
            }
        }

        public int TemplateVariablesHeight { get
            {
                return TemplateVariables.Count() == 0 ? 0 : 50 + TemplateVariables.Count() * 28;
            }
        }

        public string FixedFileName { get
            {
                if (string.IsNullOrWhiteSpace(_fileName))
                {
                    return _fileName;
                }

                if (_fileName.EndsWith("."))
                {
                    return _fileName + SelectedTemplate.FileExtension;
                }
                if (!_fileName.Contains("."))
                {
                    return _fileName + "." + SelectedTemplate.FileExtension;
                }

                return _fileName;
            }
        }

        public string FileName
        {
            get
            {
                return _fileName;
            }
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OkCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged("FileName");
                }
            }
        }

        public ObservableCollection<FileTemplate> FileTemplates { get; private set; }

        private FileTemplate _selectedTemplate;
        public FileTemplate SelectedTemplate
        {
            get {
                return FileTemplates[SelectedTemplateIndex];
            }
        }

        private int _selectedTemplateIndex;
        public int SelectedTemplateIndex
        {
            get { return _selectedTemplateIndex; }
            set
            {
                if (_selectedTemplateIndex != value)
                {
                    _selectedTemplateIndex = value;
                    ParseTemplateVariables(SelectedTemplate.Content);

                    OnPropertyChanged("SelectedTemplateIndex");
                    OnPropertyChanged("TemplateVariablesHeight");
                    OnPropertyChanged("WindowHeight");
                }
            }
        }

        public ObservableCollection<TemplateFileVariable> TemplateVariables { get; set; }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Private Methods

        public static string TemplateFileVarRegexPattern { get { return "%[a-zA-Z0-9_]+%"; } }

        private static string[] ReservedTemplateVariables
        {
            get { return new[] { "FILENAME", "FILENAMEFULL", "DATE", "TIME" }; }
        }

        private void ParseTemplateVariables(string templateText)
        {
            TemplateVariables.Clear();

            var matches = Regex.Matches(templateText, TemplateFileVarRegexPattern);

            foreach (Match match in matches)
            {
                string name = match.Value.Substring(1, match.Value.Length - 2);
                if (!TemplateVariables.Select(v => v.Name).Contains(name) && !ReservedTemplateVariables.Contains(name.ToUpper()))
                {
                    TemplateVariables.Add(new TemplateFileVariable { Name = name });
                }
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Commands

        private DelegateCommand _okCommand;

        public DelegateCommand OkCommand
        {
            get { return (_okCommand = _okCommand ?? new DelegateCommand(OkCommandEnabled, OnOkCommand)); }
        }

        public bool OkCommandEnabled(object context)
        {
            return !string.IsNullOrEmpty(FileName);
        }

        private void OnOkCommand(object context)
        {
            Result = true;

            // Set the ordinary templated variables
            try
            {
                SelectedTemplate.ModifiedContent = SelectedTemplate.ModifiedContent.Replace("%FILENAME%", Path.GetFileNameWithoutExtension(FixedFileName));
                SelectedTemplate.ModifiedContent = SelectedTemplate.ModifiedContent.Replace("%FILENAMEFULL%", Path.GetFileName(FixedFileName));
                SelectedTemplate.ModifiedContent = SelectedTemplate.ModifiedContent.Replace("%DATE%", DateTime.Now.ToShortDateString());
                SelectedTemplate.ModifiedContent = SelectedTemplate.ModifiedContent.Replace("%TIME%", DateTime.Now.ToShortTimeString());
            } catch
            {
                // Do nothing.
            }
        

            foreach (TemplateFileVariable templateVariable in TemplateVariables)
            {
                if (!string.IsNullOrEmpty(templateVariable.Value))
                {
                    SelectedTemplate.ModifiedContent = SelectedTemplate.ModifiedContent.Replace("%" + templateVariable.Name + "%", templateVariable.Value);
                }
            }

            CloseAction();
        }

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
