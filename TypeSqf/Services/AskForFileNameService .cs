using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using TypeSqf.Edit.Services;

namespace TypeSqf.Edit.Services
{
    public class FileTemplate
    {
        public FileTemplate(string name, string fileExtension, string content)
        {
            Name = name;
            FileExtension = fileExtension;
            Content = content;
            ModifiedContent = content;
        }

        public string Name { get; private set; }

        public string FileExtension { get; private set; }

        public string Content { get; private set; }

        public string ModifiedContent { get; set; }
    }

    public class AskForFileNameService : IAskForFileNameService
    {
        private FileTemplate[] _fileTemplates;

        //public AskForFileNameService(FileTemplate[] fileTemplates)
        //{
        //    _fileTemplates = fileTemplates;
        //}

        public AskForFileNameService()
        {
            Cancelled = true;
        }

        public string GetFileName()
        {
            var fileWindow = new InputFileNameWindow();
            bool? result = fileWindow.ShowDialog();
            var fileWindowContext = fileWindow.DataContext as InputFileNameWindowViewModel;

            Cancelled = (fileWindowContext.Result == null || fileWindowContext.Result == false);
            SelectedTemplate = fileWindowContext.SelectedTemplate;
            //return fileWindow.FileNameTextBox.Text;
            return fileWindowContext.FixedFileName;
        }

        public FileTemplate SelectedTemplate
        {
            get; private set;
        }

        public bool Cancelled { get; set; }
    }
}
