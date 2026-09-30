namespace TreeEditor.Core.Models;

public sealed record NewNode(Guid Key, int? ParentId, Guid? ParentKey, string Value);
