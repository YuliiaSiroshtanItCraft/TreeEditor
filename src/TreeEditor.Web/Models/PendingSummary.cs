using TreeEditor.Core.Models;

namespace TreeEditor.Web.Models;

public sealed record PendingSummary(int Added, int Updated, int Deleted)
{
    public static readonly PendingSummary None = new(0, 0, 0);

    public static PendingSummary From(ChangeSet c) => new(c.Added.Count, c.Updated.Count, c.Deleted.Count);

    public int Count => Added + Updated + Deleted;

    public string Summary => Count == 0 ? "no changes" : $"{Added} added, {Updated} edited, {Deleted} deleted";
}
