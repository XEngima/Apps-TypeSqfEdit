using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TypeSqf.Edit
{
    /// <summary>
    /// Interaction logic for InputFolderNameWindow.xaml
    /// </summary>
    public partial class InputFolderNameWindow : Window
    {
        public InputFolderNameWindow()
        {
            InitializeComponent();

            var context = (InputFolderNameWindowViewModel) DataContext;
            context.CloseAction = new Action(() => Close());
        }

        public bool? Result { get; set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FolderNameTextBox.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
