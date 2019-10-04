using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSqf.Edit
{
    public class ProjectFolderNodeViewModel : ProjectNodeViewModel
    {
        public ProjectFolderNodeViewModel()
        {
        }

        public ProjectFolderNodeViewModel(ProjectViewModel project, string displayName, string relativeFileName) : base(project, displayName)
        {
            RelativeFileName = relativeFileName;
        }

        //public ProjectFolderNodeViewModel(ProjectViewModel project, string displayName, string absolutePath) : base(project, displayName)
        //{
        //    AbsoluteFileName = absolutePath;
        //}

        public override string ImageSource
        {
            get { return "Images\\file_open.png"; }
        }

        public override ProjectNodeType NodeType
        {
            get { return ProjectNodeType.Folder; }
        }
    }
}
