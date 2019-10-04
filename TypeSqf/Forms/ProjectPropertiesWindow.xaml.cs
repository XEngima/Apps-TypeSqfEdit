using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TypeSqf.Edit.Highlighting;
using TypeSqf.Model;

namespace TypeSqf.Edit.Forms
{
    /// <summary>
    /// Interaction logic for InputFolderNameWindow.xaml
    /// </summary>
    public partial class ProjectPropertiesWindow : Window
    {
        public ProjectPropertiesWindow(Window owner)
        {
            InitializeComponent();

            var context = (ProjectPropertiesWindowViewModel)DataContext;
            Owner = owner;
        }

        public ProjectPropertiesWindowViewModel MyContext
        {
            get { return (ProjectPropertiesWindowViewModel)DataContext; }
        }

        public bool? Result { get; set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //ProjectNameTextBox.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            MyContext.IsCanceled = true;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            MyContext.IsCanceled = false;
            Close();
        }

        private void EditThemeButton_Click(object sender, RoutedEventArgs e)
        {
            string absoluteThemeDirectoryName = Path.Combine(CurrentApplication.AppDataFolder, "Themes");
            string fileName = Path.Combine(absoluteThemeDirectoryName, "Default Light.xml");

            System.Diagnostics.Process.Start("explorer.exe", "/select, " + fileName);
        }

        private void EditTemplatesButton_Click(object sender, RoutedEventArgs e)
        {
            string absoluteTemplatesDirectoryName = Path.Combine(CurrentApplication.AppDataFolder, "Templates");

            System.Diagnostics.Process.Start("explorer.exe", absoluteTemplatesDirectoryName);
        }

        private void ResetThemesButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Warning!\n\nThe theme files 'Default Light.xml' and 'Default Dark.xml' will be deleted and recreated. If you have edited them, then all changes will be lost. Do you really want to continue?", "Recreate theme files", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            string absoluteThemeDirectoryName = Path.Combine(CurrentApplication.AppDataFolder, "Themes");

            string fileName = Path.Combine(absoluteThemeDirectoryName, "Default Light.xml");

            try
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                    SyntaxHighlightingHandler.WriteThemeToDisc(fileName, Theme.DefaultLight);
                }

                fileName = Path.Combine(absoluteThemeDirectoryName, "Default Dark.xml");
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                    SyntaxHighlightingHandler.WriteThemeToDisc(fileName, Theme.DefaultDark);
                }

                MyContext.SelectedThemeName = "Default Light";
                MessageBox.Show("The files 'Default Light.xml' and 'Default Dark.xml' were reset to defaults.", "Theme Files Reset", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetTemplatesButton_Click(object sender, RoutedEventArgs e)
        {
            var sbTemplateFiles = new StringBuilder();
            string comma = "";

            foreach (var templateFile in FileTemplateHandler.DefaultFileTemplates)
            {
                sbTemplateFiles.Append(comma);
                sbTemplateFiles.Append("'");
                sbTemplateFiles.Append(templateFile.Name);
                sbTemplateFiles.Append(".");
                sbTemplateFiles.Append(templateFile.FileExtension);
                sbTemplateFiles.Append("'");
                comma = ", ";
            }

            var result = MessageBox.Show("Warning!\n\nThe template files " + sbTemplateFiles + " will be deleted and recreated. If you have edited any of them, all changes will be lost. Do you really want to continue?", "Recreate theme files", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            string absoluteThemeDirectoryName = Path.Combine(CurrentApplication.AppDataFolder, "Templates");

            try
            {
                foreach (var templateFile in FileTemplateHandler.DefaultFileTemplates)
                {
                    string fileName = Path.Combine(absoluteThemeDirectoryName, templateFile.Name + "." + templateFile.FileExtension);

                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName);
                    }
                }

				FileTemplateHandler.CreateDefaultFileTemplates();

                MessageBox.Show("The files were reset to defaults.", "Template Files Reset", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		private void VideoThemes_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			ContextMenu contextMenu = button.ContextMenu;
			contextMenu.PlacementTarget = button;
			contextMenu.IsOpen = true;
		}

		private void VideoTypeSqfFeatures2MenuItem_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://www.youtube.com/watch?v=wuAc8I0ok3w");
		}
	}
}
