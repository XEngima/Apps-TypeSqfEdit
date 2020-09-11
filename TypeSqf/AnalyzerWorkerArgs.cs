using System.Collections.Generic;
using TypeSqf.Analyzer.Commands;

namespace TypeSqf.Edit
{
    public enum AnalyzerStartReason
    {
        PrepareProject,
        AnalyzeFile,
        BuildFile,
        BuildProject,
        RebuildProject,
    }

	public class AnalyzerWorkerArgs
    {
        public AnalyzerWorkerArgs(AnalyzerStartReason startReason, string projectRootDirectory, CodeFile codeFile, List<string> filteredOutAnalyzerResults, List<string> filesToRemove, bool missionFileNeedsUpdate, bool fileIsInProject)
        {
            StartReason = startReason;
            ProjectRootDirectory = projectRootDirectory;
            CodeFile = codeFile;
            FilteredOutAnalyzerResults = filteredOutAnalyzerResults;
            FilesToRemove = filesToRemove;
            MissionFileNeedsUpdate = missionFileNeedsUpdate;
            FileIsInProject = fileIsInProject;
        }

        public AnalyzerStartReason StartReason { get; private set; }

        public string ProjectRootDirectory { get; private set; }

        public CodeFile CodeFile { get; private set; }

        public List<string> FilteredOutAnalyzerResults { get; private set; }

        public List<string> FilesToRemove { get; private set; }

        public bool MissionFileNeedsUpdate { get; private set; }

        public bool FileIsInProject { get; private set; }
    }
}
