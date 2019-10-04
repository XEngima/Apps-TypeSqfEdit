using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using TypeSqf.Edit.Services;
using System.IO;

namespace TypeSqf.Edit.Services
{
    public class FileService : IFileService
    {
        public static bool FileOrPathContainsIllegalCharacters(string filePathName)
        {
            bool illegalFile = false;

            if (filePathName.Contains("/"))
            {
                illegalFile = true;
            }
            else
            {
                foreach (char c in Path.GetInvalidPathChars())
                {
                    if (filePathName.Contains(c))
                    {
                        illegalFile = true;
                        break;
                    }
                }
            }

            return illegalFile;
        }

        public string GetOpenFileName(string filter, string title = "", string suggestedName = "")
        {
            OpenFileDialog dlg = new OpenFileDialog()
            {
                CheckFileExists = true,
                Filter = filter,
                InitialDirectory = suggestedName
            };

            if (!string.IsNullOrEmpty(title)) {
                dlg.Title = title;
            }

            dlg.ShowDialog();

            return dlg.FileName;
        }

        public string GetSaveFileName(string filter, string suggestedName = "")
        {
            SaveFileDialog dlg = new SaveFileDialog()
                                 {
                                     Filter = filter,
                                     FileName = suggestedName,
                                 };

            bool? dialogResult = dlg.ShowDialog();

            if (!dialogResult.HasValue || !dialogResult.Value)
            {
                return "";
            }

            return dlg.FileName;
        }
    }
}
