using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TypeSqf.Analyzer;

namespace TypeSqf.Edit
{
    public class BackgroundWorkerCancelChecker : ICancelSignal
    {
        public BackgroundWorkerCancelChecker(BackgroundWorker backgroundWorker)
        {
            Worker = backgroundWorker;
        }

        private BackgroundWorker Worker { get; set; }

        public bool CheckShouldCancel()
        {
            return Worker.CancellationPending;
        }
    }
}
