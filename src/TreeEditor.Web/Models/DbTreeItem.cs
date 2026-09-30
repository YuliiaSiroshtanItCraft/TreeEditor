using TreeEditor.Core.Models;

namespace TreeEditor.Web.Models;

public sealed class DbTreeItem(NodeDto node)
{
    public NodeDto Node { get; } = node;
    public List<DbTreeItem>? Children { get; set; }
    public bool IsExpanded { get; set; }
}
