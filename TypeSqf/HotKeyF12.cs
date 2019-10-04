using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using TypeSqf.Analyzer.Commands;

namespace TypeSqf.Edit.HotKey
{
    class HotKeyF12 : HotKey
    {

        public HotKeyF12(MainWindow sender, TextEditor textEditor, KeyEventArgs e)
            : base(sender, textEditor)
        {
            string selectedWord = textEditor.SelectedText;

            if (string.IsNullOrWhiteSpace(selectedWord) || IsRunning)
            {
                return;
            }
            IsRunning = true;


            string definedVariableFilePath = null;
            string currentFilePath = null;
            int lineNumber = -1;

            string selectedFullname = selectedWord;
            string lineBeforeWord = MainWindow.GetLineReadBackwards(textEditor.Text, textEditor.SelectionStart);
            string namespaceName = MainWindow.GetCurrentNamespaceName(textEditor.Text, textEditor.SelectionStart);
            var usings = MainWindow.GetCurrentUsings(textEditor.Text, textEditor.SelectionStart);

            // Get the full name of this word
            if (lineBeforeWord.EndsWith("."))
            {
                while (lineBeforeWord.EndsWith("."))
                {
                    string beforeDot = MainWindow.GetWordBeforeDotReadBackwards(lineBeforeWord, lineBeforeWord.Length - 1);
                    lineBeforeWord = lineBeforeWord.Remove(lineBeforeWord.IndexOf(beforeDot));
                    selectedFullname = beforeDot + "." + selectedFullname;
                }
            }

            
            TabViewModel currentTab = textEditor.DataContext as TabViewModel;
            if (currentTab != null)
            {
                currentFilePath = currentTab.AbsoluteFilePathName;
            }
            bool isSqx = (currentFilePath.EndsWith(".sqx", true, null)) ? true : false;


            // If this is not a method/property (no '.' before this word)
            // Serach for it in the private/public variable lists.
            if (selectedFullname == selectedWord)
            {

                // Check if the selected word is a declared PRIVATE variable.
                GlobalContextVariable privateVariable = GetDeclaredVariableFromWord(selectedWord, MyContext.DeclaredPrivateVariables);
                if (privateVariable != null)
                {
                    definedVariableFilePath = privateVariable.FileName;
                    lineNumber = privateVariable.DeclaredAtLineNumber;
                    //GoToDefinition();
                    //return;
                    // TODO: Only check private variables if it's:
                    //              _base
                    //              _self
                }

                // Check if the selected word is a declared PUBLIC variable.
                GlobalContextVariable publicVariable = GetDeclaredVariableFromWord(selectedWord, MyContext.DeclaredPublicVariables);
                if (publicVariable != null)
                {
                    var args = new GotoDefinitionArgs(publicVariable.FileName, publicVariable.DeclaredAtLineNumber);
                    args.CurrentOpenFilePath = currentFilePath;
                    GoToDefinition(args);
                    return;
                }
            }

            ObjectInfo objectInfo = null;
            // This is a Custom Type (there's a '.' before this word, or this is the class itself.)
            // If the first word before dot is a private variable
            string firstWord = selectedWord;
            if (selectedFullname.Contains("."))
            {
                firstWord = selectedFullname.Substring(0, selectedFullname.IndexOf("."));
            }

            GlobalContextVariable firstWordPrivateVariable = GetDeclaredVariableFromWord(firstWord, MyContext.DeclaredPrivateVariables);
            if (firstWordPrivateVariable != null && !string.IsNullOrWhiteSpace(firstWordPrivateVariable.TypeName))
            {
                objectInfo = MyContext.DeclaredTypes.FirstOrDefault(c => c.FullName.ToLower() == firstWordPrivateVariable.TypeName.ToLower()) as ObjectInfo;

                if (objectInfo != null)
                {
                    var args = FindChildInClass(objectInfo, selectedWord);
                    if (args != null)
                    {
                        args.CurrentOpenFilePath = currentFilePath;
                        GoToDefinition(args);
                        return;
                    }
                }

            }

            // If this is a static method/property or class
            // Then the first word is a class or enum.
            TypeInfo typeInfo;
            var typeInfos = MainWindow.GetPossibleTypes(namespaceName, MyContext.DeclaredTypes, usings);
            typeInfo = typeInfos.FirstOrDefault(i => i.Name == firstWord);

            if (typeInfo == null)
            {
                typeInfo = typeInfos.FirstOrDefault(i => i.FullName == selectedFullname);
            }

            if (typeInfo != null)
            {
                if (typeInfo.Name == selectedWord)
                {
                    // The selected word is this Class or Enum type.
                    var args = new GotoDefinitionArgs(typeInfo.FileName, typeInfo.LineNumber);
                    args.CurrentOpenFilePath = currentFilePath;
                    GoToDefinition(args);
                    return;
                }
                else
                {
                    // The selected word is a CHILD to this class/enum.
                    var args = FindChildInClass(typeInfo, selectedWord);
                    if (args != null)
                    {
                        args.CurrentOpenFilePath = currentFilePath;
                        GoToDefinition(args);
                        return;
                    }
                }
            }
                
        }

        /// <summary>
        /// Search in Class or Enum for a child.
        /// </summary>
        /// <param name="typeInfo"></param>
        /// <param name="childName"></param>
        private GotoDefinitionArgs FindChildInClass(TypeInfo typeInfo, string childName)
        {
            ObjectInfo objectInfo = typeInfo as ObjectInfo;
            if (objectInfo != null)
            {
                foreach (var method in objectInfo.Methods)
                {
                    if (method.Name == childName)
                    {
                        return new GotoDefinitionArgs(objectInfo.FileName, method.LineNumber, childName);
                    }
                }
                foreach (var property in objectInfo.Properties)
                {
                    if (property.Name == childName)
                    {
                        return new GotoDefinitionArgs(objectInfo.FileName, property.LineNumber);
                    }
                }
            }

            EnumInfo enumInfo = typeInfo as EnumInfo;
            if (enumInfo != null)
            {
                foreach (var enumValue in enumInfo.EnumValues)
                {
                    if (enumValue.Name == childName)
                    {
                        return new GotoDefinitionArgs(enumInfo.FileName, enumInfo.LineNumber);
                    }
                }
            }
            return null;
        }

        public GlobalContextVariable GetDeclaredVariableFromWord(string word, IList<GlobalContextVariable> variables)
        {
            try
            {
                return variables.FirstOrDefault(v => v.Name.ToLower() == word);
            }
            catch
            {
                return null;
            }
        }

        private GotoDefinitionArgs _gotoDefinitionArgs = null;
        public void GoToDefinition(GotoDefinitionArgs args)
        {
            
            if (args.AbsoluteFilePath.ToLower() == args.CurrentOpenFilePath.ToLower())
            {
                _gotoDefinitionArgs = args;
                GotoDefinition_ScrollToLine();
            }
            else
            {
                // Open the correct file in the text editor and starting a timer
                // to let the file be loaded before scrolling to line number.
                MyContext.OpenFileInTab(args.AbsoluteFilePath);
                _gotoDefinitionArgs = args;
                DispatcherTimer dispatcherTimer = new DispatcherTimer();
                dispatcherTimer.Tick += new EventHandler(GotoDefinition_ScrollToLine);
                dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
                dispatcherTimer.Start();
            }
        }

        /// <summary>
        /// Scroll to correct line in the editor. This could be called from a DispatcherTimer.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GotoDefinition_ScrollToLine(object sender=null, EventArgs e=null)
        {
            var timer = sender as DispatcherTimer;
            if (timer != null)
            {
                timer.Stop();
            }

            var args = _gotoDefinitionArgs;

            if (CodeTextEditor != null)
            {
                if (args.LineNumber != -1)
                {
                    ICSharpCode.AvalonEdit.Document.DocumentLine line = CodeTextEditor.Document.GetLineByNumber(args.LineNumber);

                    CodeTextEditor.Select(line.Offset, line.Length);
                    CodeTextEditor.CaretOffset = line.Offset;
                    CodeTextEditor.ScrollToLine(args.LineNumber);
                    CodeTextEditor.Focus();
                }
            }
        }
    }

    public class GotoDefinitionArgs
    {
        public GotoDefinitionArgs()
        {

        }
        public GotoDefinitionArgs(string absoluteFilePath, int lineNumber, string word = null)
        {
            AbsoluteFilePath = absoluteFilePath;
            LineNumber = lineNumber;
            Word = word;
        }
        public GotoDefinitionArgs(string absoluteFilePath, string currentOpenFilePath, int lineNumber, string word = null)
        {
            AbsoluteFilePath = absoluteFilePath;
            CurrentOpenFilePath = currentOpenFilePath;
            LineNumber = lineNumber;
            Word = word;
        }
        public string AbsoluteFilePath { get; set; }
        public string CurrentOpenFilePath { get; set; }
        public int LineNumber { get; set; }
        public string Word { get; set; }
    }
}
