using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using TypeSqf.Model;

namespace TypeSqf.Edit
{
    [Serializable]
    [XmlInclude(typeof(ProjectRootNodeViewModel))]
    [XmlInclude(typeof(ProjectFileNodeViewModel))]
    [XmlInclude(typeof(ProjectFolderNodeViewModel))]
    public abstract class ProjectNodeViewModel : INotifyPropertyChanged
    {
        private string _displayName;
        private string _relativeFileName = "";

        public event EventHandler NodeTreeChanged;

        protected ProjectNodeViewModel()
        {
            Children = new ObservableCollection<ProjectNodeViewModel>();
            Children.CollectionChanged += ChildrenOnCollectionChanged;
            Project = null;
            DisplayName = "";
        }

        protected ProjectNodeViewModel(ProjectViewModel project, string displayName)
        {
            Children = new ObservableCollection<ProjectNodeViewModel>();
            Children.CollectionChanged += ChildrenOnCollectionChanged;
            Project = project;
            DisplayName = displayName;
        }

        private void ChildrenOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            if (Project != null)
            {
                Project.IsDirty = true;
            }
        }

        public void GetAllRelativeFileNames(List<string> relativeFilePathNames)
        {
            relativeFilePathNames.Add(RelativeFileName);

            foreach (var child in Children)
            {
                child.GetAllRelativeFileNames(relativeFilePathNames);
            }
        }

        public void GetAllAbsoluteFileNames(string projectRootPath, List<string> absoluteFilePathNames)
        {
            absoluteFilePathNames.Add(Path.Combine(projectRootPath, RelativeFileName));

            foreach (var child in Children)
            {
                child.GetAllAbsoluteFileNames(projectRootPath, absoluteFilePathNames);
            }
        }

        [XmlIgnore]
        private ProjectViewModel Project { get; set; }

        public void SetProjectRecursively(ProjectViewModel project)
        {
            Project = project;

            foreach (var child in Children)
            {
                child.SetProjectRecursively(project);
            }
        }

        [XmlAttribute("Name")]
        public virtual string DisplayName
        {
            get { return _displayName; }
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged("DisplayName");
                }
            }
        }

        private bool _isSelected = false;

        [XmlIgnore]
        public bool IsSelected {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged("IsSelected");
                }
            }
        }

        private bool _isExpanded;

        [XmlIgnore]
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded != value) {
                    _isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                }
            }
        }

        public void GetAllCodes(string absoluteRootPath, List<CodeFile> codeFiles)
        {
            if (this is ProjectFileNodeViewModel)
            {
                var fileNode = this as ProjectFileNodeViewModel;
                string absoluteFilePathName = Path.Combine(absoluteRootPath, fileNode.RelativeFileName);

                if ((absoluteFilePathName.ToLower().EndsWith(".sqf") || absoluteFilePathName.ToLower().EndsWith(".sqx")) && File.Exists(absoluteFilePathName)) {
                    codeFiles.Add(new CodeFile(absoluteFilePathName.ToLower(), File.ReadAllText(absoluteFilePathName)));
                }
            }

            foreach (var child in Children)
            {
                child.GetAllCodes(absoluteRootPath, codeFiles);
            }
        }

        [XmlAttribute("FileName")]
        public string RelativeFileName { get
            {
                return _relativeFileName != null ? _relativeFileName : "";
            }
            set
            {
                _relativeFileName = value;
            }
        }

        //public void UpdateRelativeFileNames(string projectFileName)
        //{
        //    if (!string.IsNullOrEmpty(AbsoluteFileName)) {
        //        if (this is ProjectFileNodeViewModel)
        //        {
        //            RelativeFileName = Common.MakeRelativePath(projectFileName, AbsoluteFileName);
        //        }
        //        else if (this is ProjectFolderNodeViewModel)
        //        {
        //            RelativeFileName = Common.MakeRelativePath(projectFileName, AbsoluteFileName);
        //        }
        //    }

        //    foreach (var child in Children)
        //    {
        //        child.UpdateRelativeFileNames(projectFileName);
        //    }
        //}

        //public void UpdateAbsoluteFileNames(string rootFolder)
        //{
        //    if (this is ProjectFileNodeViewModel)
        //    {
        //        AbsoluteFileName = Path.Combine(rootFolder, RelativeFileName);
        //    }
        //    if (this is ProjectFolderNodeViewModel)
        //    {
        //        AbsoluteFileName = Path.Combine(rootFolder, RelativeFileName);
        //    }
        //    else
        //    {
        //        //AbsoluteFileName = RelativeFileName;
        //    }

        //    foreach (var child in Children)
        //    {
        //        child.UpdateAbsoluteFileNames(rootFolder);
        //    }
        //}

        [XmlIgnore]
        public ObservableCollection<ProjectNodeViewModel> Children { get; private set; }

        public void InsertChildNode(ProjectNodeViewModel node)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];

                if (node is ProjectFileNodeViewModel && child is ProjectFolderNodeViewModel)
                {
                    continue;
                }
                else if (node is ProjectFolderNodeViewModel && child is ProjectFileNodeViewModel)
                {
                    Children.Insert(i, node);
                    return;
                }
                else if (string.Compare(child.DisplayName, node.DisplayName, true) > 0)
                {
                    Children.Insert(i, node);
                    return;
                }
            }

            Children.Add(node);
        }

        [XmlArray("SubNodes")]
        public ProjectNodeViewModel[] XmlChildren
        {
            get { return Children.ToArray(); }
            set
            {
                foreach (ProjectNodeViewModel node in value)
                {
                    InsertChildNode(node);
                }
            }
        }

        [XmlIgnore]
        public virtual string ImageSource
        {
            get { return "Images\\project.png"; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        [XmlAttribute("Type")]
        public abstract ProjectNodeType NodeType
        {
            get;
        }

        public ProjectNodeViewModel GetSelectedNode()
        {
            if (IsSelected)
            {
                return this;
            }

            foreach (ProjectNodeViewModel node in Children)
            {
                ProjectNodeViewModel selectedNode = node.GetSelectedNode();
                if (selectedNode != null)
                {
                    return selectedNode;
                }
            }

            return null;
        }

        public ProjectNodeViewModel GetNodeByRelativeFileName(string relativeFilePathName)
        {
            if (RelativeFileName.ToLower() == relativeFilePathName.ToLower())
            {
                return this;
            }

            foreach (ProjectNodeViewModel childNode in Children)
            {
                var foundNode = childNode.GetNodeByRelativeFileName(relativeFilePathName);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            return null;
        }

        public bool FindAndRemoveNode(ProjectNodeViewModel node)
        {
            if (Children.Contains(node))
            {
                Children.Remove(node);
                return true;
            }

            foreach (ProjectNodeViewModel child in Children)
            {
                if (child.FindAndRemoveNode(node))
                {
                    return true;
                }
            }

            return false;
        }

        protected void OnNodeTreeChanged()
        {
            if (NodeTreeChanged != null)
            {
                NodeTreeChanged(this, new EventArgs());
            }
        }

        public override string ToString()
        {
            return NodeType + " - " + DisplayName;
        }

        public void RemoveChildrenThatDoNotHaveAFile(string projectRootNode)
        {
            var childrenToRemove = new List<ProjectNodeViewModel>();

            foreach (var child in Children)
            {
                string absoluteFilePathName = Path.Combine(projectRootNode, child.RelativeFileName);
                if (child is ProjectFolderNodeViewModel && Directory.Exists(absoluteFilePathName) || child is ProjectFileNodeViewModel && File.Exists(absoluteFilePathName))
                {
                    child.RemoveChildrenThatDoNotHaveAFile(projectRootNode);
                }
                else
                {
                    childrenToRemove.Add(child);
                }
            }

            foreach (var child in childrenToRemove)
            {
                Children.Remove(child);
            }
        }

        public void CollapseAllSubNodes()
        {
            foreach (var child in Children)
            {
                child.CollapseAllSubNodes();
                child.IsExpanded = false;
            }
        }

        public void ExpandAllSubNodes()
        {
            foreach (var child in Children)
            {
                child.ExpandAllSubNodes();
                child.IsExpanded = true;
            }
        }
    }
}
