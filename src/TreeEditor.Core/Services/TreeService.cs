using TreeEditor.Core.Constants;
using TreeEditor.Core.Interfaces;
using TreeEditor.Core.Models;

namespace TreeEditor.Core.Services;

public sealed class TreeService(ITreeRepository repository) : ITreeService
{
    public Task<IReadOnlyList<NodeDto>> GetRootsAsync(CancellationToken ct = default) =>
        repository.GetRootsAsync(ct);

    public Task<IReadOnlyList<NodeDto>> GetChildrenAsync(int parentId, CancellationToken ct = default) =>
        repository.GetChildrenAsync(parentId, ct);

    public Task<NodeDto?> GetNodeAsync(int id, CancellationToken ct = default) =>
        repository.GetNodeAsync(id, ct);

    public Task<IReadOnlyList<NodeDto>> GetNodesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        repository.GetNodesAsync(ids, ct);

    public Task<ApplyResult> ApplyAsync(ChangeSet changes, CancellationToken ct = default)
    {
        foreach (var value in changes.Added.Select(a => a.Value).Concat(changes.Updated.Select(u => u.Value)))
        {
            if (TreeConstants.ValidateValue(value) is { } error)
                throw new ArgumentException(error, nameof(changes));
        }

        return repository.ApplyAsync(changes, ct);
    }

    public Task InitializeAsync(CancellationToken ct = default) => repository.InitializeAsync(ct);

    public Task ResetAsync(CancellationToken ct = default) => repository.ResetAsync(ct);
}
