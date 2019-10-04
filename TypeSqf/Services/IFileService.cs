using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSqf.Edit.Services
{
    public interface IFileService
    {
        string GetOpenFileName(string filter, string title = "", string suggestedName = "");

        string GetSaveFileName(string filter, string suggestedName = "");
    }
}
