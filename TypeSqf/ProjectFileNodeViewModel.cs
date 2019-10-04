using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TypeSqf.Edit
{
    public class ProjectFileNodeViewModel : ProjectNodeViewModel
    {
        public ProjectFileNodeViewModel()
            :base(null, "")
        {
        }

        [XmlAttribute("Name")]
        public override string DisplayName
        {
            get { return Path.GetFileName(RelativeFileName); }
            set { base.DisplayName = value; }
        }

        public override string ImageSource
        {
            get { return "Images\\file.png"; }
        }

        public override ProjectNodeType NodeType
        {
            get { return ProjectNodeType.File; }
        }
    }
}
