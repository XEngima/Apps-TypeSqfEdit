using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeSqf.Edit.Services
{
    public interface IAskForTextService
    {
        string GetText(string dialogHeader, string textHeader, string suggestedText);

        bool Cancelled { get; }
    }
}
