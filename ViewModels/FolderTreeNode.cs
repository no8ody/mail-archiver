namespace MailArchiver.Models.ViewModels
{
    /// <summary>
    /// Represents a node in the folder tree hierarchy.
    /// </summary>
    public class FolderTreeNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int Level { get; set; }
        public bool HasChildren => Children != null && Children.Any();
        public List<FolderTreeNode> Children { get; set; } = new List<FolderTreeNode>();

        internal Dictionary<string, FolderTreeNode>? ChildrenMap { get; set; }
    }
}
