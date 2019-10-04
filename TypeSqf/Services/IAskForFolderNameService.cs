using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSqf.Edit.Services
{
    public interface IAskForFileNameService
    {
        string GetFileName();

        bool Cancelled { get; }

        FileTemplate SelectedTemplate { get; }
    }
}
