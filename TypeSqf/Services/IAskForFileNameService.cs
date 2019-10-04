using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSqf.Edit.Services
{
    public interface IAskForFolderNameService
    {
        string GetFolderName();

        bool Cancelled { get; }
    }
}
