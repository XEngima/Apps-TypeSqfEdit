using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using TypeSqf.Edit.Services;

namespace TypeSqf.Edit.Services
{
    public class AskForProjectNameService : IAskForProjectNameService
    {
        public AskForProjectNameService()
        {
            Cancelled = true;
        }

        public string GetProjectName()
        {
            var newProjectWindow = new NewProjectWindow();
            bool? result = newProjectWindow.ShowDialog();
            var projectWindowContext = newProjectWindow.DataContext as NewProjectWindowViewModel;

            //Cancelled = (result == null || result == false); 'Kan inte göra...
            Cancelled = (projectWindowContext.Result == null || projectWindowContext.Result == false);
            return newProjectWindow.ProjectNameTextBox.Text;
        }

        public bool Cancelled { get; set; }
    }
}
