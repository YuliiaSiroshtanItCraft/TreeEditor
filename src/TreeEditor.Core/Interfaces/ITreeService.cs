using TreeEditor.Core.Models;

namespace TreeEditor.Core.Interfaces;

public interface ITreeService
{
    Task<IReadOnlyList<NodeDto>> GetRootsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<NodeDto>> GetChildrenAsync(int parentId, CancellationToken ct = default);

    Task<NodeDto?> GetNodeAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<NodeDto>> GetNodesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    Task<ApplyResult> ApplyAsync(ChangeSet changes, CancellationToken ct = default);

    Task InitializeAsync(CancellationToken ct = default);

    Task ResetAsync(CancellationToken ct = default);
}
