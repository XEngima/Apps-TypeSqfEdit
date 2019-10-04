using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSqf.Edit
{
    public class SearchMatch
    {
        public SearchMatch(int index, int length, int lineNo)
        {
            Index = index;
            Length = length;
            LineNo = lineNo;
        }

        public int LineNo { get; set; }

        public int Index { get; set; }

        public int Length { get; set; }
    }

    public class SearchResultItem
    {
        public SearchResultItem()
        {
        }

        public SearchResultItem(string fileName, string relativeFileName, int occurrences, string fileContent, SearchMatch[] matches)
        {
            FileName = fileName;
            RelativeFileName = relativeFileName;
            Occurrences = occurrences;
            FileContent = fileContent;
            Matches = matches;
        }

        public string FileName { get; set; }

        public string RelativeFileName { get; set; }

        public int Occurrences { get; set; }

        public string FileContent { get; set; }

        public SearchMatch[] Matches { get; set; }
    }
}

