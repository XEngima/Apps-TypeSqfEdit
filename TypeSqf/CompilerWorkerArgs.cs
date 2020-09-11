namespace TypeSqf.Edit
{
    public partial class MainWindowViewModel
    {
        public class CompilerWorkerArgs
        {
            public CompilerWorkerArgs(AnalyzerStartReason startReason, string projectRootDirectory, string currentFilePathName)
            {
                StartReason = startReason;
                ProjectRootDirectory = projectRootDirectory;
                CurrentFilePathName = currentFilePathName;
            }

            public AnalyzerStartReason StartReason { get; set; }

            public string ProjectRootDirectory { get; set; }

            public string CurrentFilePathName { get; set; }
        }
	}
}
