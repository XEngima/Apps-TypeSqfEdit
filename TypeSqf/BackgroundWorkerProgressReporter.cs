using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TypeSqf.Analyzer;

namespace TypeSqf.Edit
{
    public class BackgroundWorkerProgressReporter : IProgressReporter
    {
        private BackgroundWorker _backgroundWorker;

        public BackgroundWorkerProgressReporter(BackgroundWorker backgroundWorker)
        {
            _backgroundWorker = backgroundWorker;
        }

        public void ReportProgress(int percentage, object userState = null)
        {
            _backgroundWorker.ReportProgress(percentage, userState);
        }
    }
}
