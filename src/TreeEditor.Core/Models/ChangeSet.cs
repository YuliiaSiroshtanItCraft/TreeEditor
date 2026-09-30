namespace TreeEditor.Core.Models;

public sealed record ChangeSet(
    IReadOnlyList<NewNode> Added,
    IReadOnlyList<ValueChange> Updated,
    IReadOnlyList<int> Deleted)
{
    public bool IsEmpty => Added.Count == 0 && Updated.Count == 0 && Deleted.Count == 0;
}
