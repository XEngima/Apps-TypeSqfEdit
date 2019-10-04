using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TypeSqf.Edit.Services
{
    public enum UserMessageType
    {
        Info,
        Question,
        Warning,
        Critical
    }

    public interface IUserMessageService
    {
        MessageBoxResult ShowMessage(string message, string caption, MessageBoxButton messageBoxButton,
            MessageBoxImage messageBoxImage);
    }
}
