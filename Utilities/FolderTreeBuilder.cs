using MailArchiver.Models.ViewModels;

namespace MailArchiver.Utilities
{
    public static class FolderTreeBuilder
    {
        public static List<FolderTreeNode> Build(IEnumerable<(string FolderName, int Count)> folderData)
        {
            const int maxFolderNameLength = 500;
            const int maxTotalFolders = 10000;

            var validFolderData = folderData
                .Where(f =>
                    !string.IsNullOrWhiteSpace(f.FolderName) &&
                    f.FolderName.Length <= maxFolderNameLength &&
                    !f.FolderName.Contains("..") &&
                    !f.FolderName.Contains("<") &&
                    !f.FolderName.Contains(">") &&
                    !f.FolderName.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
                .Take(maxTotalFolders)
                .ToList();

            var root = new Dictionary<string, FolderTreeNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in validFolderData)
            {
                var parts = folder.FolderName
                    .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();

                if (parts.Length == 0)
                    continue;

                var currentLevel = root;
                FolderTreeNode? currentNode = null;

                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var fullPath = string.Join("/", parts.Take(i + 1));

                    if (!currentLevel.TryGetValue(part, out var node))
                    {
                        node = new FolderTreeNode
                        {
                            Name = part,
                            FullPath = fullPath,
                            Level = i,
                            TotalCount = 0,
                            Children = new List<FolderTreeNode>()
                        };
                        currentLevel[part] = node;
                    }

                    currentNode = node;

                    if (i < parts.Length - 1)
                    {
                        node.ChildrenMap ??= new Dictionary<string, FolderTreeNode>(StringComparer.OrdinalIgnoreCase);
                        currentLevel = node.ChildrenMap;
                    }
                }

                if (currentNode != null)
                {
                    currentNode.TotalCount = folder.Count;
                }
            }

            return Sort(BuildList(root));
        }

        private static List<FolderTreeNode> BuildList(Dictionary<string, FolderTreeNode> nodes)
        {
            var result = new List<FolderTreeNode>();

            foreach (var node in nodes.Values)
            {
                if (node.ChildrenMap != null && node.ChildrenMap.Any())
                {
                    node.Children = BuildList(node.ChildrenMap);
                    node.ChildrenMap = null;
                }
                else
                {
                    node.Children = new List<FolderTreeNode>();
                }

                result.Add(node);
            }

            return result;
        }

        private static List<FolderTreeNode> Sort(List<FolderTreeNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return nodes;

            var priorityFolders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "inbox", 1 },
                { "drafts", 2 },
                { "sent", 3 },
                { "junk", 4 },
                { "spam", 5 },
                { "trash", 6 },
                { "deleted", 7 },
                { "archive", 8 }
            };

            foreach (var node in nodes)
            {
                if (node.Children != null && node.Children.Any())
                {
                    node.Children = Sort(node.Children.ToList());
                }
            }

            return nodes
                .OrderBy(n =>
                {
                    var lowerName = n.Name.ToLowerInvariant();
                    if (priorityFolders.TryGetValue(lowerName, out var priority))
                        return priority;

                    foreach (var pf in priorityFolders)
                    {
                        if (n.FullPath.ToLowerInvariant().StartsWith(pf.Key + "/") ||
                            n.FullPath.Equals(pf.Key, StringComparison.OrdinalIgnoreCase))
                            return pf.Value;
                    }

                    return 100;
                })
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
