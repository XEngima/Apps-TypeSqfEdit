using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using TypeSqf.Edit.Services;

namespace TypeSqf.Edit.Services
{
    public class AskForTextService : IAskForTextService
    {
        public AskForTextService()
        {
            Cancelled = true;
        }

        public string GetText(string suggestedText)
        {
            var inputTextWindow = new InputTextWindow();
            inputTextWindow.TextTextBox.Text = suggestedText;

            inputTextWindow.TextTextBox.SelectionStart = 0;
            inputTextWindow.TextTextBox.SelectionLength = suggestedText.Length;

            inputTextWindow.ShowDialog();
            var inputTextWindowContext = inputTextWindow.DataContext as InputTextWindowViewModel;

            Cancelled = (inputTextWindowContext.Result == null || inputTextWindowContext.Result == false);
            return inputTextWindow.TextTextBox.Text;
        }

        public bool Cancelled { get; set; }
    }
}
