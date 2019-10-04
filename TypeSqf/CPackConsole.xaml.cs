using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TypeSqf.Edit.Service;

namespace TypeSqf.Edit
{
    /// <summary>
    /// Interaction logic for ManageCPackWindow.xaml
    /// </summary>
    public partial class CPackConsole : Window
    {
        public CPackConsole(string projectRootDirectory)
        {
            DataContext = new CPackConsoleViewModel(projectRootDirectory);
            Service = new CPackService();
            PreviousCommands = new List<string>();
            InitializeComponent();
            CommandTextBox.Focus();
            DialogContext = SynchronizationContext.Current;

            MyContext.PropertyChanged += (sender, args) =>{
                                             if (args.PropertyName == "TextBoxEnabled") {
                                                 Thread.Sleep(200);
                                                 DialogContext.Post(val =>{
                                                                        if (CommandTextBox.IsEnabled)
                                                                            CommandTextBox.Focus();
                                                                    }, sender);
                                             }
                                         };

            MyContext.Exit += MyContext_Exit;
        }

        private void MyContext_Exit(object sender, EventArgs e)
        {
            Close();
        }

        public SynchronizationContext DialogContext { get; set; }

        public CPackConsoleViewModel MyContext { get { return DataContext as CPackConsoleViewModel; } }

        public CPackService Service { get; set; }

        private List<string> PreviousCommands { get; set; }

        private int PrevCommandIndex { get; set; }

        private void CommandTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MyContext.ExecuteCommand(CommandTextBox.Text.Trim());
                PreviousCommands.Add(CommandTextBox.Text.Trim());
                CommandTextBox.Text = "";
                PrevCommandIndex = PreviousCommands.Count;
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (PrevCommandIndex > 0)
                {
                    PrevCommandIndex--;
                    CommandTextBox.Text = PreviousCommands[PrevCommandIndex];
                    CommandTextBox.SelectionStart = CommandTextBox.Text.Length;
                }
            }
            else if (e.Key == Key.Down)
            {
                if (PrevCommandIndex < PreviousCommands.Count - 1)
                {
                    PrevCommandIndex++;
                    CommandTextBox.Text = PreviousCommands[PrevCommandIndex];
                    CommandTextBox.SelectionStart = CommandTextBox.Text.Length;
                }
                else
                {
                    CommandTextBox.Text = "";
                    PrevCommandIndex = PreviousCommands.Count;
                }
            }
        }

		private void VideoTypeSqfFeatures3MenuItem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=D8flQMvHz5Y");
		}

		private void VideoButton_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			ContextMenu contextMenu = button.ContextMenu;
			contextMenu.PlacementTarget = button;
			contextMenu.IsOpen = true;
		}

        private void VideoTypeSqfFeatures4MenuItem_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=L6LDWErn7I4");
        }
    }
}
