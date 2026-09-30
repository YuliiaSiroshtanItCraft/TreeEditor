namespace TreeEditor.Core.Models;

public sealed record NodeDto(int Id, int? ParentId, string Path, string Value, bool IsDeleted, bool HasChildren)
{
    public IReadOnlyList<int> AncestorIds { get; } =
        Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
}
