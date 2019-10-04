using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace TypeSqf.Edit
{
    [Serializable]
    [XmlRoot(ElementName = "Project")]
    public class ProjectViewModel : INotifyPropertyChanged
    {
        private ProjectRootNodeViewModel _projectRootNode;

        public ProjectViewModel()
        {
            ProjectRootNode = null; // new ProjectRootNodeViewModel(this, "My project");
            //IsDirty = false;
            //ProjectRootNode.NodeTreeChanged += (sender, args) => IsDirty = true;
        }

        public ProjectViewModel(string name)
            :this()
        {
            ProjectRootNode = new ProjectRootNodeViewModel(this, name);
            IsDirty = false;
            ProjectRootNode.NodeTreeChanged += (sender, args) => IsDirty = true;
        }

        public void Reset(string name)
        {
            ProjectRootNode = new ProjectRootNodeViewModel();

            ProjectRootNode.DisplayName = name;
            ProjectRootNode.IsExpanded = false;
            ProjectRootNode.IsSelected = false;
            ProjectRootNode.RelativeFileName = "";
            ProjectRootNode.Children.Clear();
            IsDirty = false;
            AbsoluteFilePathName = null;
            _filteredAnalyzerResultItems.Clear();
        }

        [XmlAttribute]
        public int Version
        {
            get { return 1; }
        }

        public bool FileInProject(string relativeFilePathName)
        {
            var relativeFileNames = new List<string>();
            ProjectRootNode.GetAllRelativeFileNames(relativeFileNames);

            return relativeFileNames.Contains(relativeFilePathName);
        }

        [XmlIgnore]
        public string AbsoluteFilePathName { get; set; }

        [XmlIgnore]
        public bool IsDirty { get; set; }

        public ProjectRootNodeViewModel ProjectRootNode
        {
            get { return _projectRootNode; }
            set
            {
                if (_projectRootNode != value)
                {
                    _projectRootNode = value;
                    OnPropertyChanged("ProjectRootNode");
                    OnPropertyChanged("ProjectRootNodes");
                }
            }
        }

        [XmlIgnore]
        public List<ProjectRootNodeViewModel> ProjectRootNodes
        {
            get
            {
                return new List<ProjectRootNodeViewModel>() { ProjectRootNode };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }


        private List<string> _filteredAnalyzerResultItems = new List<string>();

        [XmlArray]
        public string[] FilteredAnalyzerResultItems
        {
            get { return _filteredAnalyzerResultItems.ToArray(); }
            set
            {
                foreach (string analyzerResult in value)
                {
                    _filteredAnalyzerResultItems.Add(analyzerResult);
                }
            }
        }
        
        public void AddFilteredAnalyzerResultItems(string analyzerResult)
        {
            _filteredAnalyzerResultItems.Add(analyzerResult);
            IsDirty = true;
        }

        public void AddFilteredAnalyzerResultItems(string analyzerResult, string fileName)
        {

        }
    }
}
