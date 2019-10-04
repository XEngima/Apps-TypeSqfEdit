using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace TypeSqf.Edit.HotKey
{
    public class HotKeyHandler
    {

        public HotKeyHandler(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
        }

        private MainWindow MainWindow { get; set; }

        public void KeyUp(TextEditor textEditor, KeyEventArgs e)
        {

            /// HotKey F12 Event
            if (e.Key == Key.F12)
            {
                new HotKeyF12(MainWindow, textEditor, e);
            }

        }

    }
}
