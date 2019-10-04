using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TypeSqf.Model;

namespace TypeSqf.Edit.Highlighting
{
    [XmlRoot]
    [XmlInclude(typeof(ColorDefinition))]
    public class Theme
    {
        public static Theme DefaultLight
        {
            get
            {
                var theme = new Theme
                {
                    BackgroundColor = "FFFFFF",
                    ForegroundColor = "000000",
                    FontName = "Courier New",
                    FontSize = 14,
                };

                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Comments", ForeColor = "008000", Example = "/* Commented out */" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "StringLiterals", ForeColor = "646464", Example = "'A text as a string literal.'" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "NumberLiterals", ForeColor = "FF0000", Example = "42" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "ConstantLiterals", ForeColor = "FF0000", Bold = true, Example = "true, false, east, west, objNull" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "PrivateVariables", ForeColor = "0058B0", Example = "_variable" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Brackets", ForeColor = "0000FF", Bold = true, Example = "() [] {}" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Keywords", ForeColor = "FF0000", Bold = true, Example = "if, for, do, while, new, as" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Operators", ForeColor = "0000FF", Bold = true, Example = "+, -, ==, !=" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "ScopeAndBreakCommands", ForeColor = "000080", Bold = true, Example = "scopeName, breakTo, breakOut" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "GlobalFunctions", ForeColor = "191970", Bold = true, Example = "globFnc = {};" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "PreprocessorKeywords", ForeColor = "0000FF", Bold = true, Example = "#define, #ifndef, #else, #endif" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Types", ForeColor = "2b91af", Bold = true, Example = "String, Number, Object, Group, MyCustomClass" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "ClassMemberNames", ForeColor = "333333", Bold = true, Example = "public method MyClassMember {};" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "ClassKeywords", ForeColor = "0000CC", Bold = true, Example = "public class, private method, namespace, using" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "PropertyGetSet", ForeColor = "777777", Bold = false, Example = "get; private set;" });

                return theme;
            }
        }

        public static Theme DefaultDark
        {
            get
            {
                var theme = new Theme
                {
                    BackgroundColor = "222222",
                    ForegroundColor = "F5F5F5",
                    FontName = "Courier New",
                    FontSize = 14,
                };

                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Comments", ForeColor = "90EE90", Italic = true, Example = "/* Commented out */" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "StringLiterals", ForeColor = "DB7093", Example = "'A text as a string literal.'" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "NumberLiterals", ForeColor = "DB7093", Example = "42" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "ConstantLiterals", ForeColor = "DB7093", Bold = true, Example = "true, false, east, west, objNull" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "PrivateVariables", ForeColor = "FFDEAD", Example = "_variable" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Brackets", ForeColor = "F0FFFF", Bold = true, Example = "() [] {}" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Keywords", ForeColor = "09CFFF", Example = "if, for, do, while, new, as" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Operators", ForeColor = "09CFFF", Example = "+, -, ==, !=" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "ScopeAndBreakCommands", ForeColor = "09CFFF", Bold = true, Example = "scopeName, breakTo, breakOut" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "GlobalFunctions", ForeColor = "F5FFFA", Bold = true, Example = "globFnc = {};" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "PreprocessorKeywords", ForeColor = "999999", Bold = true, Example = "#define, #ifndef, #else, #endif" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "Types", ForeColor = "00FFFF", Bold = true, Example = "String, Number, Object, Group, MyCustomClass" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "ClassMemberNames", ForeColor = "F0FFFF", Bold = true, Example = "public method MyClassMember {};" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "ClassKeywords", ForeColor = "09CFFF", Bold = true, Example = "public class, private method, namespace, using" });
                theme.ColorDefinitionList.Add(new ColorDefinition { Name = "PropertyGetSet", ForeColor = "BBBBBB", Bold = false, Example = "get; private set;" });

                return theme;
            }
        }

        public Theme()
        {
            ColorDefinitionList = new List<ColorDefinition>();
        }

        public virtual string BackgroundColor { get; set; }

        public virtual string ForegroundColor { get; set; }

        public virtual string FontName { get; set; }

        public virtual double FontSize { get; set; }

        /// <summary>
        /// Gets a color definition.
        /// </summary>
        /// <param name="name">The name of the color definition.</param>
        /// <returns>A color definition. A default color definition if the definition was not found.</returns>
        public ColorDefinition GetColorDefinition(string name)
        {
            ColorDefinition colorDef = ColorDefinitions.FirstOrDefault(x => x.Name == name);

            if (colorDef == null)
            {
                if (!CurrentApplication.IsRelease)
                {
                    //throw new InvalidOperationException("Saknar färgdefinition i temat.");
                }

                if (ForegroundColor != null)
                {
                    colorDef = new ColorDefinition(name, ForegroundColor);
                }
                else
                {
                    colorDef = new ColorDefinition(name, "000000");
                }
            }

            return colorDef;
        }

        [XmlIgnore]
        public List<ColorDefinition> ColorDefinitionList { get; protected set; }

        public ColorDefinition[] ColorDefinitions
        {
            get
            {
                return ColorDefinitionList.ToArray();
            }
            set
            {
                ColorDefinitionList = new List<ColorDefinition>();
                ColorDefinitionList.AddRange(value);
            }
        }
    }

    //public class DefaultLightTheme : Theme
    //{
    //    public override string BackgroundColor { get { return "FFFFFF"; } }
    //    public override string ForegroundColor { get { return "000000"; } }
    //    public override string FontName { get { return "Courier New"; } }
    //    public override double FontSize { get { return 14; } }
    //}
}
