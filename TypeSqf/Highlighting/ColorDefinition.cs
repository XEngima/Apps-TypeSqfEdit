using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TypeSqf.Edit.Highlighting
{
    [XmlType("ColorDef")]
    public class ColorDefinition
    {
        public ColorDefinition()
        {
            Name = "";
            ForeColor = "000000";
            Bold = false;
            Italic = false;
        }

        public ColorDefinition(string name, string foreColor, bool bold, bool italic)
        {
            Name = name;
            ForeColor = foreColor;
            Bold = bold;
            Italic = italic;
        }

        public ColorDefinition(string name, string foreColor)
            :this(name, foreColor, false, false)
        {
        }

        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string ForeColor { get; set; }

        [XmlAttribute]
        public bool Bold { get; set; }

        [XmlAttribute]
        public bool Italic { get; set; }

        [XmlAttribute]
        public string Example { get; set; }
    }
}
