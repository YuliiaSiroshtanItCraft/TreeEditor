namespace TreeEditor.Core.Models;

public sealed class CachedNode
{
    internal CachedNode(Guid key, long order) => (Key, Order) = (key, order);

    public Guid Key { get; }

    public int? Id { get; internal set; }

    public int? ParentId { get; internal set; }

    public Guid? ParentKey { get; internal set; }

    public IReadOnlyList<int> AncestorIds { get; internal set; } = [];

    public string Value { get; internal set; } = "";

    public string OriginalValue { get; internal set; } = "";

    public bool IsDeletedInDb { get; internal set; }

    public bool IsDeletedLocally { get; internal set; }

    internal long Order { get; }

    public bool IsNew => Id is null;
    public bool IsModified => !IsNew && !IsDeleted && Value != OriginalValue;
    public bool IsDeleted => IsDeletedInDb || IsDeletedLocally;
    public bool HasPendingChanges => IsNew ? !IsDeleted : IsModified || IsDeletedLocally && !IsDeletedInDb;
}
