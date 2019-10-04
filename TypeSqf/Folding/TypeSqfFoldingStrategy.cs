using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using System.Text.RegularExpressions;

namespace TypeSqf.Edit.Folding
{
    public class TypeSqfFoldingSection
    {
        public TypeSqfFoldingSection(int startOffset, int endOffset, bool isFolded)
        {
            StartOffset = startOffset;
            EndOffset = endOffset;
            IsFolded = isFolded;
        }

        public int StartOffset { get; private set; }

        public int EndOffset { get; private set; }

        public bool IsFolded { get; private set; }
    }

    public class TypeSqfFoldingStrategy : AbstractFoldingStrategy
    {
        public TypeSqfFoldingStrategy(IEnumerable<TypeSqfFoldingSection> foldingSections)
        {
            FoldingSections = foldingSections;
        }

        public IEnumerable<TypeSqfFoldingSection> FoldingSections
        {
            get; private set;
        }

        private static string GetIndentation(string sLine)
        {
            var sbIndentation = new StringBuilder();

            foreach (char ch in sLine)
            {
                if (!char.IsWhiteSpace(ch))
                {
                    break;
                }

                sbIndentation.Append(ch);
            }

            return sbIndentation.ToString().Replace("\t", "    ");
        }

        /// <summary>
        /// Generates the foldings for our document.
        /// </summary>
        /// <param name="document">The current document.</param>
        /// <param name="fileName">The filename of the document.</param>
        /// <param name="parseInformation">Extra parse information, not used in this sample.</param>
        /// <returns>A list of FoldMarkers.</returns>
        public override IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            List<NewFolding> foldings = new List<NewFolding>();

            if (FoldingSections != null)
            {
                foreach (var section in FoldingSections)
                {
                    foldings.Add(new NewFolding(section.StartOffset, section.EndOffset)
                    {
                        DefaultClosed = section.IsFolded
                    });
                }

                return foldings;
            }

            int nextLineStartOffset = 0;
            int functionStartIndex = 0;
            int regionStartIndex = 0;
            int namespaceStartIndex = 0;
            int classStartIndex = 0;
            string functionStartIndentation = "";
            string namespaceStartIndentation = "";
            string classStartIndentation = "";
            string previousLineText = "";
            string trimmedPreviousLineText = "";

            for (int i = 1; i <= document.LineCount; i++)
            {
                // Get the text of current line.
                string text = document.GetText(document.GetLineByNumber(i));
                nextLineStartOffset += text.Length + 2;
                string trimmedText = text.Trim().ToLower();

                bool isMatch = Regex.IsMatch("dre_function = {", @"^[a-zA-Z_][a-zA-Z0-9_]*\s*=\s*\{$");

                // Look for method starts
				if (functionStartIndex == 0)
				{
					if (Regex.IsMatch(trimmedText, @"^public\s+constructor") || Regex.IsMatch(trimmedText, @"^public.*method") || Regex.IsMatch(trimmedText, @"^protected.*method") || Regex.IsMatch(trimmedText, @"^private.*method") || Regex.IsMatch(trimmedText, @"^[a-zA-Z_][a-zA-Z0-9_]*\s*=\s*\{$"))
					{
						functionStartIndex = nextLineStartOffset - 2;
						functionStartIndentation = GetIndentation(text);
					}
					else if (trimmedText == "{" && Regex.IsMatch(trimmedPreviousLineText, @"^[a-zA-Z_][a-zA-Z0-9_]*\s*=\s*$"))
					{
						functionStartIndex = nextLineStartOffset - 2 - text.Length - 2;
						functionStartIndentation = GetIndentation(text);
					}
				}

				// Check region starts
				if (Regex.IsMatch(trimmedText, @"^(//\s*)?#region"))
                {
                    regionStartIndex = nextLineStartOffset - 2;
                }

                // Check namespace starts
                if (Regex.IsMatch(trimmedText, @"^namespace\s+[A-Za-z0-9_.]"))
                {
                    namespaceStartIndex = nextLineStartOffset - 2;
                    namespaceStartIndentation = GetIndentation(text);
                }

                // Check class starts
                if (Regex.IsMatch(trimmedText, @"^public\s+class"))
                {
                    classStartIndex = nextLineStartOffset - 2;
                    classStartIndentation = GetIndentation(text);
                }

                if (functionStartIndex > 0 && trimmedText == "};" && GetIndentation(text) == functionStartIndentation)
                {
                    // Look for method endings
                    // Add a new FoldMarker to the list.
                    // document = the current document
                    // start = the start line for the FoldMarker
                    // document.GetLineSegment(start).Length = the ending of the current line = the start column of our foldmarker.
                    // i = The current line = end line of the FoldMarker.
                    // 7 = The end column
                    //list.Add(new FoldMarker(document, start, document.GetLineSegment(start).Length, i, 7));
                    foldings.Add(new NewFolding(functionStartIndex, nextLineStartOffset - 2));
                    functionStartIndex = 0;
                }

                if (regionStartIndex > 0 && Regex.IsMatch(trimmedText, @"^(//\s*)?#endregion$"))
                {
                    foldings.Add(new NewFolding(regionStartIndex, nextLineStartOffset - 2));
                    regionStartIndex = 0;
                }

                if (classStartIndex > 0 && trimmedText == "};" && GetIndentation(text) == classStartIndentation)
                {
                    foldings.Add(new NewFolding(classStartIndex, nextLineStartOffset - 2));
                    classStartIndex = 0;
                }

                if (namespaceStartIndex > 0 && trimmedText == "};" && GetIndentation(text) == namespaceStartIndentation)
                {
                    foldings.Add(new NewFolding(namespaceStartIndex, nextLineStartOffset - 2));
                    namespaceStartIndex = 0;
                }

                previousLineText = text;
                trimmedPreviousLineText = trimmedText;
            }

            return foldings.OrderBy(x => x.StartOffset).ToList();
        }

        //public List<FoldMarker> GenerateFoldMarkers(IDocument document, string fileName, object parseInformation)
        //{
        //    List<FoldMarker> list = new List<FoldMarker>();

        //    int start = 0;

        //    // Create foldmarkers for the whole document, enumerate through every line.
        //    for (int i = 0; i < document.TotalNumberOfLines; i++)
        //    {
        //        // Get the text of current line.
        //        string text = document.GetText(document.GetLineSegment(i));

        //        if (text.StartsWith("def")) // Look for method starts
        //            start = i;
        //        if (text.StartsWith("enddef;")) // Look for method endings
        //            // Add a new FoldMarker to the list.
        //            // document = the current document
        //            // start = the start line for the FoldMarker
        //            // document.GetLineSegment(start).Length = the ending of the current line = the start column of our foldmarker.
        //            // i = The current line = end line of the FoldMarker.
        //            // 7 = The end column
        //            list.Add(new FoldMarker(document, start, document.GetLineSegment(start).Length, i, 7));
        //    }

        //    return list;
        //}
    }
}
