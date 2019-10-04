using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TypeSqf.Edit.Services
{
    public class UserMessageService : IUserMessageService
    {
        public UserMessageService(Window owner)
        {
            Owner = owner;
        }

        private Window Owner { get; set; }

        public MessageBoxResult ShowMessage(string message, string caption, MessageBoxButton messageBoxButton, MessageBoxImage messageBoxImage)
        {
            return MessageBox.Show(Owner, message, caption, messageBoxButton, messageBoxImage);
        }
    }
}
