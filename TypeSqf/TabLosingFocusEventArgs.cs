using System;

namespace TypeSqf.Edit
{
	public class TabLosingFocusEventArgs : EventArgs
    {
        public TabLosingFocusEventArgs(bool isClosed)
        {
            IsClosed = isClosed;
        }

        public bool IsClosed { get; private set; }
    }
}
