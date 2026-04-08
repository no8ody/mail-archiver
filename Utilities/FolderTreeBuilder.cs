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

            var folderNameSet = new HashSet<string>(
                validFolderData.Select(f => f.FolderName),
                StringComparer.OrdinalIgnoreCase);

            var allNodes = new Dictionary<string, FolderTreeNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var folder in validFolderData)
            {
                allNodes[folder.FolderName] = new FolderTreeNode
                {
                    Name = folder.FolderName,
                    FullPath = folder.FolderName,
                    TotalCount = folder.Count,
                    Level = 0,
                    Children = new List<FolderTreeNode>()
                };
            }

            var rootNodes = new List<FolderTreeNode>();

            foreach (var folder in validFolderData.OrderBy(f => f.FolderName.Length).ThenBy(f => f.FolderName, StringComparer.OrdinalIgnoreCase))
            {
                var node = allNodes[folder.FolderName];
                string? parentPath = null;

                for (int i = folder.FolderName.Length - 1; i >= 0; i--)
                {
                    if (folder.FolderName[i] != '/' && folder.FolderName[i] != '\\' && folder.FolderName[i] != '.')
                    {
                        continue;
                    }

                    var candidate = folder.FolderName.Substring(0, i);
                    if (folderNameSet.Contains(candidate))
                    {
                        parentPath = candidate;
                        break;
                    }
                }

                if (parentPath != null && allNodes.TryGetValue(parentPath, out var parentNode))
                {
                    node.Name = folder.FolderName.Substring(parentPath.Length + 1);
                    node.Level = parentNode.Level + 1;
                    parentNode.Children.Add(node);
                }
                else
                {
                    node.Level = 0;
                    rootNodes.Add(node);
                }
            }

            return Sort(rootNodes);
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

                    var lowerPath = n.FullPath.ToLowerInvariant();
                    foreach (var pf in priorityFolders)
                    {
                        if (lowerPath.StartsWith(pf.Key + "/") ||
                            lowerPath.StartsWith(pf.Key + ".") ||
                            lowerPath.Equals(pf.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            return pf.Value;
                        }
                    }

                    return 100;
                })
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
