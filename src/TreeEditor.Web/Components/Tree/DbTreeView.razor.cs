using Microsoft.AspNetCore.Components;
using TreeEditor.Web.Models;

namespace TreeEditor.Web.Components.Tree;

public partial class DbTreeView
{
    [Parameter, EditorRequired] public IReadOnlyList<DbTreeItem> Items { get; set; } = [];
    [Parameter] public int? SelectedId { get; set; }
    [Parameter] public Func<int, bool> IsCached { get; set; } = _ => false;
    [Parameter] public bool IsTopLevel { get; set; } = true;
    [Parameter] public EventCallback<DbTreeItem> OnSelect { get; set; }
    [Parameter] public EventCallback<DbTreeItem> OnToggle { get; set; }
    [Parameter] public EventCallback<DbTreeItem> OnActivate { get; set; }

    private string TreeRole => IsTopLevel ? "tree" : "group";

    private bool IsSelected(DbTreeItem item) => item.Node.Id == SelectedId;

    private string RowClass(DbTreeItem item)
    {
        var classes = new List<string> { "tree-row" };
        if (IsSelected(item)) classes.Add("selected");
        if (item.Node.IsDeleted) classes.Add("deleted");
        return string.Join(' ', classes);
    }

    private static string? AriaExpanded(DbTreeItem item) =>
        item.Node.HasChildren ? (item.IsExpanded ? "true" : "false") : null;
}
