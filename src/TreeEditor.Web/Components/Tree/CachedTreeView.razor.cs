using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using TreeEditor.Core.Cache;
using TreeEditor.Core.Models;
using TreeEditor.Web.Models;

namespace TreeEditor.Web.Components.Tree;

public partial class CachedTreeView
{
    private ElementReference _input;

    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Parameter, EditorRequired] public TreeCache Cache { get; set; } = null!;
    [Parameter, EditorRequired] public IReadOnlyList<CachedNode> Nodes { get; set; } = [];
    [Parameter, EditorRequired] public EditState Edit { get; set; } = null!;
    [Parameter] public Guid? SelectedKey { get; set; }
    [Parameter] public bool IsTopLevel { get; set; } = true;
    [Parameter] public EventCallback<CachedNode> OnSelect { get; set; }
    [Parameter] public EventCallback<CachedNode> OnActivate { get; set; }
    [Parameter] public EventCallback OnCommit { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private string TreeRole => IsTopLevel ? "tree" : "group";

    private bool IsSelected(CachedNode node) => node.Key == SelectedKey;

    private bool IsEditing(CachedNode node) => node.Key == Edit.Key;

    private string RowClass(CachedNode node)
    {
        var classes = new List<string> { "tree-row" };
        if (IsSelected(node)) classes.Add("selected");
        if (node.IsDeleted) classes.Add("deleted");
        if (node.IsNew) classes.Add("new");
        if (node.IsModified) classes.Add("modified");
        return string.Join(' ', classes);
    }

    private static string IdLabel(CachedNode node) => node.Id is { } id ? $"#{id}" : "new";

    private static string DeletedLabel(CachedNode node) => node.IsDeletedInDb ? "deleted" : "delete pending";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Edit.NeedsFocus && Nodes.Any(IsEditing))
        {
            Edit.NeedsFocus = false;
            await JS.InvokeVoidAsync("treeEditor.focusAndSelect", _input);
        }
    }

    private Task HandleKeyDown(KeyboardEventArgs e) => e.Key switch
    {
        "Enter" => OnCommit.InvokeAsync(),
        "Escape" => OnCancel.InvokeAsync(),
        _ => Task.CompletedTask,
    };
}
