using Microsoft.AspNetCore.Components;
using TreeEditor.Core.Cache;
using TreeEditor.Core.Interfaces;
using TreeEditor.Core.Models;
using TreeEditor.Web.Models;
using TreeEditor.Web.Services;

namespace TreeEditor.Web.Components.Pages;

public partial class Home
{
    private const string NewElementValue = "New element";

    private readonly EditState _edit = new();
    private Guid? _selectedCacheKey;
    private PendingSummary _pending = PendingSummary.None;
    private string? _status;
    private bool _statusIsError;
    private bool _busy;

    [Inject] private ITreeService TreeService { get; set; } = null!;
    [Inject] private TreeCache Cache { get; set; } = null!;
    [Inject] private DbTreeBrowser DbTree { get; set; } = null!;

    private CachedNode? SelectedCached => _selectedCacheKey is { } key ? Cache.Find(key) : null;
    private bool CanEditSelected => !_busy && SelectedCached is { IsDeleted: false };
    private bool CanLoad => !_busy && DbTree.SelectedId is not null;
    private bool CanApply => !_busy && Cache.HasPendingChanges;
    private string ApplyLabel => _pending.Count > 0 ? $"Apply ({_pending.Count})" : "Apply";

    protected override Task OnInitializedAsync() => RunAsync(DbTree.RefreshAsync);

    private Task ToggleDbAsync(DbTreeItem item) => RunAsync(() => DbTree.ToggleAsync(item));

    private Task LoadAsync(DbTreeItem item)
    {
        DbTree.Select(item);
        return LoadSelectedAsync();
    }

    private Task LoadSelectedAsync() => RunAsync(async () =>
    {
        if (DbTree.SelectedId is not { } id) return;

        var node = await Cache.LoadAsync(id);
        if (node is null)
        {
            SetStatus($"Element #{id} no longer exists.", isError: true);
            return;
        }

        _selectedCacheKey = node.Key;
        SetStatus($"Loaded “{node.Value}” into the cache.");
    });

    private void SelectCached(CachedNode node)
    {
        if (_edit.Key is { } editing && editing != node.Key)
            CancelEdit();
        _selectedCacheKey = node.Key;
    }

    private void ActivateCached(CachedNode node)
    {
        SelectCached(node);
        if (!node.IsDeleted) StartEdit();
    }

    private void StartEdit()
    {
        if (SelectedCached is not { IsDeleted: false } node) return;
        _edit.Key = node.Key;
        _edit.Value = node.Value;
        _edit.NeedsFocus = true;
    }

    private void CommitEdit()
    {
        if (_edit.Key is not { } key) return;
        if (TryChangeCache(() => Cache.SetValue(key, _edit.Value)))
            CancelEdit();
    }

    private void CancelEdit() => _edit.Key = null;

    private void AddChild()
    {
        if (SelectedCached is not { } parent) return;
        TryChangeCache(() =>
        {
            var child = Cache.AddChild(parent.Key, NewElementValue);
            _selectedCacheKey = child.Key;
            StartEdit();
        });
    }

    private void Delete()
    {
        if (SelectedCached is not { } node) return;
        TryChangeCache(() => Cache.Delete(node.Key));
        CancelEdit();
    }

    private Task ApplyAsync() => RunAsync(async () =>
    {
        CancelEdit();
        var applied = await Cache.ApplyAsync();
        await DbTree.RefreshAsync();
        if (SelectedCached is null)
            _selectedCacheKey = null;
        SetStatus($"Applied: {PendingSummary.From(applied).Summary}.");
    });

    private Task ResetAsync() => RunAsync(async () =>
    {
        await TreeService.ResetAsync();
        Cache.Clear();
        CancelEdit();
        _selectedCacheKey = null;
        DbTree.ClearSelection();
        await DbTree.RefreshAsync();
        SetStatus("Database and cache were reset to the sample data.");
    });

    private bool TryChangeCache(Action change)
    {
        try
        {
            change();
            ClearStatus();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            SetStatus(ex.Message, isError: true);
            return false;
        }
        finally
        {
            UpdatePending();
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        _busy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", isError: true);
        }
        finally
        {
            _busy = false;
            UpdatePending();
        }
    }

    private void SetStatus(string message, bool isError = false) => (_status, _statusIsError) = (message, isError);

    private void ClearStatus() => (_status, _statusIsError) = (null, false);

    private void UpdatePending() => _pending = PendingSummary.From(Cache.BuildChangeSet());
}
