using Microsoft.EntityFrameworkCore;
using TreeEditor.Core.Data;
using TreeEditor.Core.Entities;
using TreeEditor.Core.Interfaces;
using TreeEditor.Core.Models;

namespace TreeEditor.Core.Repositories;

public sealed class EfTreeRepository(IDbContextFactory<TreeDbContext> dbFactory) : ITreeRepository
{
    public async Task<IReadOnlyList<NodeDto>> GetRootsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await ToNodeDtos(db, db.Nodes.Where(n => n.ParentId == null)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NodeDto>> GetChildrenAsync(int parentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await ToNodeDtos(db, db.Nodes.Where(n => n.ParentId == parentId)).ToListAsync(ct);
    }

    public async Task<NodeDto?> GetNodeAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await ToNodeDtos(db, db.Nodes.Where(n => n.Id == id)).SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<NodeDto>> GetNodesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await ToNodeDtos(db, db.Nodes.Where(n => ids.Contains(n.Id))).ToListAsync(ct);
    }

    public async Task<ApplyResult> ApplyAsync(ChangeSet changes, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var createdIds = await InsertNodesAsync(db, changes.Added, ct);
        await UpdateValuesAsync(db, changes.Updated, ct);
        await MarkSubtreesDeletedAsync(db, changes.Deleted, ct);

        await transaction.CommitAsync(ct);
        return new ApplyResult(createdIds);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        if (!await db.Nodes.AnyAsync(ct))
        {
            db.Nodes.AddRange(SampleData.Create());
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Database.EnsureCreatedAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Nodes.ExecuteUpdateAsync(s => s.SetProperty(n => n.ParentId, (int?)null), ct);
        await db.Nodes.ExecuteDeleteAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name = 'Nodes'", ct);

        db.Nodes.AddRange(SampleData.Create());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static IQueryable<NodeDto> ToNodeDtos(TreeDbContext db, IQueryable<TreeNode> nodes) =>
        nodes
            .OrderBy(n => n.Id)
            .Select(n => new NodeDto(
                n.Id,
                n.ParentId,
                n.Path,
                n.Value,
                n.IsDeleted,
                db.Nodes.Any(child => child.ParentId == n.Id)));

    private static async Task<Dictionary<Guid, int>> InsertNodesAsync(
        TreeDbContext db, IReadOnlyList<NewNode> newNodes, CancellationToken ct)
    {
        if (newNodes.Count == 0)
            return [];

        var existingParentIds = newNodes.Where(n => n.ParentKey is null).Select(n => n.ParentId).Distinct().ToArray();
        var existingParentsById = await db.Nodes
            .Where(n => existingParentIds.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, ct);

        var createdNodes = new List<TreeNode>(newNodes.Count);
        var createdByKey = new Dictionary<Guid, TreeNode>(newNodes.Count);
        foreach (var newNode in newNodes)
        {
            var parent = FindParent(newNode, existingParentsById, createdByKey);
            var node = new TreeNode
            {
                Parent = parent,
                Value = newNode.Value,
                IsDeleted = parent.IsDeleted,
            };
            createdNodes.Add(node);
            createdByKey[newNode.Key] = node;
        }

        db.Nodes.AddRange(createdNodes);
        await db.SaveChangesAsync(ct);

        foreach (var node in createdNodes)
            node.Path = node.Parent!.ChildPath;
        await db.SaveChangesAsync(ct);

        return createdByKey.ToDictionary(pair => pair.Key, pair => pair.Value.Id);
    }

    private static TreeNode FindParent(
        NewNode newNode, Dictionary<int, TreeNode> existingParentsById, Dictionary<Guid, TreeNode> createdByKey)
    {
        if (newNode.ParentKey is { } parentKey)
        {
            return createdByKey.TryGetValue(parentKey, out var createdParent)
                ? createdParent
                : throw new InvalidOperationException($"Parent of new node '{newNode.Value}' was not created before it.");
        }

        var parentId = newNode.ParentId ?? throw new InvalidOperationException("A new node needs a parent.");
        return existingParentsById.TryGetValue(parentId, out var existingParent)
            ? existingParent
            : throw new InvalidOperationException($"Parent node {parentId} no longer exists.");
    }

    private static async Task UpdateValuesAsync(
        TreeDbContext db, IReadOnlyList<ValueChange> valueChanges, CancellationToken ct)
    {
        if (valueChanges.Count == 0)
            return;

        var ids = valueChanges.Select(change => change.Id).ToArray();
        var nodesById = await db.Nodes.Where(n => ids.Contains(n.Id)).ToDictionaryAsync(n => n.Id, ct);
        foreach (var change in valueChanges)
        {
            if (nodesById.TryGetValue(change.Id, out var node) && !node.IsDeleted)
                node.Value = change.Value;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task MarkSubtreesDeletedAsync(
        TreeDbContext db, IReadOnlyList<int> deletedIds, CancellationToken ct)
    {
        if (deletedIds.Count == 0)
            return;

        var ids = deletedIds.Distinct().ToArray();
        await db.Nodes
            .Where(n => ids.Contains(n.Id)
                        || db.Nodes.Any(root => ids.Contains(root.Id)
                                                && n.Path.StartsWith(root.Path + root.Id + "/")))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsDeleted, true), ct);
    }
}
