using TreeEditor.Core.Entities;
using TreeEditor.Core.Models;

namespace TreeEditor.Core.Data;

public static class SampleData
{
    private static SampleItem N(string value, params SampleItem[] children) => new(value, children);

    private static readonly SampleItem Tree =
        N("Root",
            N("Node 1",
                N("Node 1.1",
                    N("Node 1.1.1",
                        N("Node 1.1.1.1"),
                        N("Node 1.1.1.2")),
                    N("Node 1.1.2")),
                N("Node 1.2")),
            N("Node 2",
                N("Node 2.1",
                    N("Node 2.1.1",
                        N("Node 2.1.1.1",
                            N("Node 2.1.1.1.1"),
                            N("Node 2.1.1.1.2")))),
                N("Node 2.2",
                    N("Node 2.2.1"))),
            N("Node 3",
                N("Node 3.1"),
                N("Node 3.2",
                    N("Node 3.2.1",
                        N("Node 3.2.1.1")))));

    public static IReadOnlyList<TreeNode> Create()
    {
        var result = new List<TreeNode>();
        var queue = new Queue<(SampleItem Item, TreeNode? Parent)>();
        queue.Enqueue((Tree, null));
        var nextId = 1;

        while (queue.Count > 0)
        {
            var (item, parent) = queue.Dequeue();
            var node = new TreeNode
            {
                Id = nextId++,
                ParentId = parent?.Id,
                Value = item.Value,
                Path = parent?.ChildPath ?? "/",
            };
            result.Add(node);
            foreach (var child in item.Children)
                queue.Enqueue((child, node));
        }

        return result;
    }
}
