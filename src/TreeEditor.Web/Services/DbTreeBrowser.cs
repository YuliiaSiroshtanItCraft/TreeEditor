using TreeEditor.Core.Interfaces;
using TreeEditor.Core.Models;
using TreeEditor.Web.Models;

namespace TreeEditor.Web.Services;

public sealed class DbTreeBrowser(ITreeService treeService)
{
    public List<DbTreeItem>? Roots { get; private set; }

    public int? SelectedId { get; private set; }

    public void Select(DbTreeItem item) => SelectedId = item.Node.Id;

    public void ClearSelection() => SelectedId = null;

    public async Task ToggleAsync(DbTreeItem item)
    {
        if (item.IsExpanded)
        {
            item.IsExpanded = false;
            return;
        }

        item.Children ??= ToItems(await treeService.GetChildrenAsync(item.Node.Id));
        item.IsExpanded = true;
    }

    public async Task RefreshAsync()
    {
        var expandedIds = new HashSet<int>();
        CollectExpandedIds(Roots, expandedIds);
        Roots = await LoadLevelAsync(await treeService.GetRootsAsync(), expandedIds);
    }

    private static void CollectExpandedIds(List<DbTreeItem>? items, HashSet<int> expandedIds)
    {
        foreach (var item in items ?? [])
        {
            if (!item.IsExpanded) continue;
            expandedIds.Add(item.Node.Id);
            CollectExpandedIds(item.Children, expandedIds);
        }
    }

    private async Task<List<DbTreeItem>> LoadLevelAsync(IReadOnlyList<NodeDto> nodes, HashSet<int> expandedIds)
    {
        var items = ToItems(nodes);
        foreach (var item in items)
        {
            if (!item.Node.HasChildren || !expandedIds.Contains(item.Node.Id)) continue;
            item.Children = await LoadLevelAsync(await treeService.GetChildrenAsync(item.Node.Id), expandedIds);
            item.IsExpanded = true;
        }
        return items;
    }

    private static List<DbTreeItem> ToItems(IReadOnlyList<NodeDto> nodes) =>
        nodes.Select(n => new DbTreeItem(n)).ToList();
}
