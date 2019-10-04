using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TypeSqf.Edit
{
    public class ProjectRootNodeViewModel : ProjectNodeViewModel
    {
        public ProjectRootNodeViewModel()
        {
        }

        public ProjectRootNodeViewModel(ProjectViewModel project, string displayName) : base(project, displayName)
        {
        }

        public override ProjectNodeType NodeType
        {
            get { return ProjectNodeType.Root; }
        }
    }
}
