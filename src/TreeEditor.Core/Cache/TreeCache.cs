using TreeEditor.Core.Constants;
using TreeEditor.Core.Interfaces;
using TreeEditor.Core.Models;

namespace TreeEditor.Core.Cache;

public sealed class TreeCache(ITreeService service)
{
    private readonly Dictionary<Guid, CachedNode> _nodesByKey = [];
    private readonly Dictionary<int, CachedNode> _savedNodesById = [];
    private long _nextOrder;

    public IReadOnlyCollection<CachedNode> Nodes => _nodesByKey.Values;

    public bool HasPendingChanges => Nodes.Any(n => n.HasPendingChanges || n.IsNew);

    public bool Contains(int id) => _savedNodesById.ContainsKey(id);

    public CachedNode? Find(Guid key) => _nodesByKey.GetValueOrDefault(key);

    public CachedNode? GetParent(CachedNode node)
    {
        if (node.ParentKey is { } parentKey)
            return Find(parentKey);
        return node.ParentId is { } parentId ? _savedNodesById.GetValueOrDefault(parentId) : null;
    }

    public IReadOnlyList<CachedNode> GetRoots() => Sort(Nodes.Where(n => GetParent(n) is null));

    public IReadOnlyList<CachedNode> GetChildren(CachedNode parent) => Sort(Nodes.Where(n => GetParent(n) == parent));

    private static List<CachedNode> Sort(IEnumerable<CachedNode> nodes) =>
        nodes.OrderBy(n => n.Id ?? int.MaxValue).ThenBy(n => n.Order).ToList();

    private bool IsDescendantOf(CachedNode node, CachedNode ancestor)
    {
        if (ancestor.Id is { } ancestorId && node.AncestorIds.Contains(ancestorId))
            return true;

        for (var current = GetParent(node); current is not null; current = GetParent(current))
        {
            if (current == ancestor)
                return true;
        }
        return false;
    }

    public async Task<CachedNode?> LoadAsync(int id, CancellationToken ct = default)
    {
        var dto = await service.GetNodeAsync(id, ct);
        if (dto is null)
            return null;

        if (_savedNodesById.TryGetValue(id, out var existing))
        {
            if (!existing.HasPendingChanges && !existing.IsDeletedLocally)
                ApplySnapshot(existing, dto);
            return existing;
        }

        var node = new CachedNode(Guid.NewGuid(), _nextOrder++);
        ApplySnapshot(node, dto);
        _nodesByKey[node.Key] = node;
        _savedNodesById[id] = node;

        if (!node.IsDeleted && Nodes.Any(n => n.IsDeletedLocally && IsDescendantOf(node, n)))
            node.IsDeletedLocally = true;

        return node;
    }

    private static void ApplySnapshot(CachedNode node, NodeDto dto)
    {
        node.Id = dto.Id;
        node.ParentId = dto.ParentId;
        node.ParentKey = null;
        node.AncestorIds = dto.AncestorIds;
        node.Value = dto.Value;
        node.OriginalValue = dto.Value;
        node.IsDeletedInDb = dto.IsDeleted;
        node.IsDeletedLocally = false;
    }

    public CachedNode AddChild(Guid parentKey, string value)
    {
        var parent = GetEditable(parentKey);
        value = Normalize(value);

        var node = new CachedNode(Guid.NewGuid(), _nextOrder++)
        {
            ParentId = parent.Id,
            ParentKey = parent.Key,
            AncestorIds = parent.Id is { } parentId ? [.. parent.AncestorIds, parentId] : parent.AncestorIds,
            Value = value,
        };
        _nodesByKey[node.Key] = node;
        return node;
    }

    public void SetValue(Guid key, string value)
    {
        var node = GetEditable(key);
        node.Value = Normalize(value);
    }

    public void Delete(Guid key)
    {
        var node = GetEditable(key);
        node.IsDeletedLocally = true;
        foreach (var descendant in Nodes.Where(n => IsDescendantOf(n, node)))
            descendant.IsDeletedLocally = true;
    }

    private CachedNode GetEditable(Guid key)
    {
        var node = Find(key) ?? throw new InvalidOperationException("The element is not in the cache.");
        if (node.IsDeleted)
            throw new InvalidOperationException("A deleted element cannot be changed.");
        return node;
    }

    private static string Normalize(string value)
    {
        value = value.Trim();
        if (TreeConstants.ValidateValue(value) is { } error)
            throw new ArgumentException(error, nameof(value));
        return value;
    }

    public ChangeSet BuildChangeSet()
    {
        var added = Nodes
            .Where(n => n.IsNew && !n.IsDeleted)
            .OrderBy(n => n.Order)
            .Select(ToNewNode)
            .ToList();

        var updated = Nodes
            .Where(n => n.IsModified)
            .Select(n => new ValueChange(n.Id!.Value, n.Value))
            .ToList();

        var deleted = Nodes
            .Where(n => !n.IsNew && n.IsDeletedLocally && !n.IsDeletedInDb)
            .Select(n => n.Id!.Value)
            .ToList();

        return new ChangeSet(added, updated, deleted);
    }

    private NewNode ToNewNode(CachedNode node)
    {
        var parent = GetParent(node)!;
        return parent.IsNew
            ? new NewNode(node.Key, null, parent.Key, node.Value)
            : new NewNode(node.Key, parent.Id, null, node.Value);
    }

    public async Task<ChangeSet> ApplyAsync(CancellationToken ct = default)
    {
        var changes = BuildChangeSet();
        if (!changes.IsEmpty)
        {
            var result = await service.ApplyAsync(changes, ct);
            AssignCreatedIds(result.CreatedIds);
        }

        RemoveUnsavedNodes();
        await RefreshSavedNodesAsync(ct);
        return changes;
    }

    private void AssignCreatedIds(IReadOnlyDictionary<Guid, int> createdIds)
    {
        foreach (var (key, id) in createdIds)
        {
            var node = _nodesByKey[key];
            node.Id = id;
            _savedNodesById[id] = node;
        }
    }

    private void RemoveUnsavedNodes()
    {
        foreach (var node in Nodes.Where(n => n.IsNew).ToList())
            _nodesByKey.Remove(node.Key);
    }

    private async Task RefreshSavedNodesAsync(CancellationToken ct)
    {
        var snapshots = (await service.GetNodesAsync(_savedNodesById.Keys.ToList(), ct)).ToDictionary(d => d.Id);
        foreach (var node in _savedNodesById.Values.ToList())
        {
            if (snapshots.TryGetValue(node.Id!.Value, out var dto))
            {
                ApplySnapshot(node, dto);
            }
            else
            {
                _savedNodesById.Remove(node.Id.Value);
                _nodesByKey.Remove(node.Key);
            }
        }
    }

    public void Clear()
    {
        _nodesByKey.Clear();
        _savedNodesById.Clear();
    }
}
