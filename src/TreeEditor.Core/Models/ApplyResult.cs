namespace TreeEditor.Core.Models;

public sealed record ApplyResult(IReadOnlyDictionary<Guid, int> CreatedIds);
