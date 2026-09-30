namespace TreeEditor.Core.Entities;

public class TreeNode
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public TreeNode? Parent { get; set; }

    public string Value { get; set; } = "";

    public string Path { get; set; } = "/";

    public bool IsDeleted { get; set; }

    public string ChildPath => $"{Path}{Id}/";
}
