using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TypeSqf.Edit
{
    /// <summary>
    /// Interaction logic for FindInAllFilesWindow.xaml
    /// </summary>
    public partial class FindInAllFilesWindow : Window
    {
        public FindInAllFilesWindow(Window owner)
        {
            InitializeComponent();
            EnableDisableControls();
            MyContext.AfterPerformFind += MyContext_AfterPerformFind;
            Owner = owner;
        }

        private void MyContext_AfterPerformFind(object sender, EventArgs e)
        {
            EnableDisableControls();
            if (MyContext.SearchResultItems.Count > 0)
            {
                //ResultGrid.Focus();
                //ResultGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
                FindButton.Focus();
            }
        }

        public FindInAllFilesViewModel MyContext { get { return DataContext as FindInAllFilesViewModel; } }

        private void HideWindow()
        {
            SearchTextBox.Focus();
            this.Hide();
            SearchTextBox.SelectionStart = 0;
            SearchTextBox.SelectionLength = SearchTextBox.Text.Length;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            UserHasNavigated = false;
            HideWindow();
        }

        public bool UserHasNavigated { get; set; }

        private void NavigateToButton_Click(object sender, RoutedEventArgs e)
        {
            UserHasNavigated = true;
            HideWindow();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            HideWindow();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                //MyContext.SearchText = SearchTextBox.Text;
                MyContext.PerformFind();

                if (MyContext.SearchResultItems.Count > 0)
                {
                    //ResultGrid.Focus();
                    //Send(Key.Down);
                    //Send(Key.Down);
                    //Send(Key.Down);
                    //ResultGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
                    //Keyboard.Focus(ResultGrid);
                    //DataGridCell dgc = ResultGrid.Items[0];
                    //Keyboard.Focus(ResultGrid.SelectedCells[0]);
                    //var selectedRow = (DataGridRow)ResultGrid.ItemContainerGenerator.(ResultGrid.SelectedIndex);
                    //FocusManager.SetIsFocusScope(selectedRow, true);
                    //FocusManager.SetFocusedElement(selectedRow, selectedRow);
                    //Keyboard.Focus(GetDataGridCell(ResultGrid.SelectedCells[0]));
                    //DataGridRow row = (DataGridRow)ResultGrid.ItemContainerGenerator.ContainerFromIndex(0);
                    //row.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    //Keyboard.Focus(row);
                }
            }

            EnableDisableControls();
        }

        ///// <summary>
        /////   Sends the specified key.
        ///// </summary>
        ///// <param name="key">The key.</param>
        //public static void Send(Key key)
        //{
        //    if (Keyboard.PrimaryDevice != null)
        //    {
        //        if (Keyboard.PrimaryDevice.ActiveSource != null)
        //        {
        //            var e = new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, key)
        //            {
        //                RoutedEvent = Keyboard.KeyDownEvent
        //            };
        //            InputManager.Current.ProcessInput(e);

        //            // Note: Based on your requirements you may also need to fire events for:
        //            // RoutedEvent = Keyboard.PreviewKeyDownEvent
        //            // RoutedEvent = Keyboard.KeyUpEvent
        //            // RoutedEvent = Keyboard.PreviewKeyUpEvent
        //        }
        //    }
        //}

        private void HandleCellSelected(object sender, RoutedEventArgs e)
        {
            DataGridCell dataGridCell = sender as DataGridCell;
            Keyboard.Focus(dataGridCell);
        }

        private void EnableDisableControls()
        {
            NavigateToButton.IsEnabled = MyContext.SelectedItem != null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Focus();
        }

        private void ResultGrid_Selected(object sender, RoutedEventArgs e)
        {

        }

        private void ResultGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                UserHasNavigated = true;
                HideWindow();
            }
        }

        private void ResultGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                UserHasNavigated = true;
                HideWindow();
            }
        }

        private void ResultGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MyContext.SelectedItem != null)
            {
                UserHasNavigated = true;
                HideWindow();
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Focus();
        }
    }
}
