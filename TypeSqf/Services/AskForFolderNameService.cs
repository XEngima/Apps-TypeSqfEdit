using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using TypeSqf.Edit.Services;

namespace TypeSqf.Edit.Services
{
    public class AskForFolderNameService : IAskForFolderNameService
    {
        public AskForFolderNameService()
        {
            Cancelled = true;
        }

        public string GetFolderName()
        {
            var folderWindow = new InputFolderNameWindow();
            bool? result = folderWindow.ShowDialog();
            var folderWindowContext = folderWindow.DataContext as InputFolderNameWindowViewModel;

            //Cancelled = (result == null || result == false); 'Kan inte göra...
            Cancelled = (folderWindowContext.Result == null || folderWindowContext.Result == false);
            return folderWindow.FolderNameTextBox.Text;
        }

        public bool Cancelled { get; set; }
    }
}
