using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSqf.Model
{
    public static class ProjectFileHandler
    {
        public static void FindProjectFiles(string projecdtRootDir, List<string> allFiles)
        {
            string[] fileEntries = Directory.GetFiles(projecdtRootDir);
            foreach (string fileName in fileEntries)
            {
                allFiles.Add(fileName);
            }

            //Recursion    
            string[] subdirectoryEntries = Directory.GetDirectories(projecdtRootDir);
            foreach (string item in subdirectoryEntries)
            {
                // Avoid "reparse points"
                FileAttributes attributes = File.GetAttributes(item);
                //bool isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);
                bool isHidden = attributes.HasFlag(FileAttributes.Hidden);
                bool isSystem = attributes.HasFlag(FileAttributes.System);

                if (!isHidden && !isSystem)
                //if ((File.GetAttributes(item) & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
                {
                    FindProjectFiles(item, allFiles);
                }
            }
        }
    }
}
