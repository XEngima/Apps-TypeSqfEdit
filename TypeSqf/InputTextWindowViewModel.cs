using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TypeSqf.Edit.Services;

namespace TypeSqf.Edit
{
    public class InputTextWindowViewModel : INotifyPropertyChanged
    {
        //----------------------------------------------------------------------------------------------------------------
        #region Private Variables

        private string _text;

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Public Properties

        public Action CloseAction { get; set; }

        public bool? Result { get; set; }

        public string Text
        {
            get { return _text; }
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OkCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged("Text");
                }
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Commands

        private DelegateCommand _okCommand;

        public DelegateCommand OkCommand
        {
            get { return (_okCommand = _okCommand ?? new DelegateCommand(OkCommandEnabled, OnOkCommand)); }
        }

        public bool OkCommandEnabled(object context)
        {
            return !string.IsNullOrEmpty(Text);
        }

        private void OnOkCommand(object context)
        {
            Result = true;
            CloseAction();
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------------------------
    }
}
