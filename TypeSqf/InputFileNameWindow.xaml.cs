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
    /// Interaction logic for InputFileNameWindow.xaml
    /// </summary>
    public partial class InputFileNameWindow : Window
    {
        public InputFileNameWindow()
        {
            InitializeComponent();

            var context = (InputFileNameWindowViewModel) DataContext;
            context.CloseAction = new Action(() => Close());
        }

        public bool? Result { get; set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Application.Current.MainWindow = this;
            FileNameTextBox.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
