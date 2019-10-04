using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit;

namespace TypeSqf.Edit.HotKey
{
    public class HotKey
    {
        public HotKey(MainWindow sender, TextEditor textEditor)
        {
            MyContext = sender.DataContext as MainWindowViewModel;
            CodeTextEditor = textEditor;
        }
        ~HotKey()
        {
            IsRunning = false;
        }

        protected MainWindowViewModel MyContext { get; set; }

        protected TextEditor CodeTextEditor { get; set; }

        protected static bool IsRunning { get; set; }

    }
}
