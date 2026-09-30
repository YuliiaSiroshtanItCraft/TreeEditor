# Tree Editor with a Local Cache

Blazor (.NET 10, Interactive Server) + EF Core 10 + .NET Aspire 13.

The left panel (**Database**) browses the database tree lazily. The right panel (**Cache**) holds elements loaded from it. You can edit, add and delete elements in the cache. Nothing is written to the database until **Apply**.

## Running

Prerequisites: .NET 10 SDK. Docker is **not** required.

```bash
dotnet run --project src/TreeEditor.AppHost
```

Open the Aspire dashboard link printed in the console, then open the `web` endpoint.
The SQLite file is created and seeded at `src/TreeEditor.AppHost/data/tree.db` on first start.

The web project also runs on its own with `dotnet run --project src/TreeEditor.Web`. In that case it uses `tree.db` in its working directory.

## Using it

| Action | How |
|---|---|
| Browse the DB tree | Click ▸ to expand. Children are fetched one level at a time. |
| Load into cache | Select an element and click **Load**, or double-click it. |
| Edit value | Select a cached element and click **Edit** (or double-click it). Press Enter to save, Esc to cancel. |
| Add child | Click **Add child**. A new element is created and opens for editing. |
| Delete | Click **Delete**. The element and its cached descendants are marked *delete pending* and become read-only. |
| Apply | Writes all pending changes in one transaction and refreshes both trees. |
| Reset | Restores the database to the sample data and clears the cache. |

In the cached tree, new elements are shown in green, edited ones in italic amber, and deleted ones struck through.

## Solution layout

```
src/
  TreeEditor.AppHost          Aspire orchestration (SQLite resource + web project)
  TreeEditor.Core             Domain: EF model, repository, service, TreeCache (no UI dependencies)
  TreeEditor.Web              Blazor UI + Aspire service defaults (OpenTelemetry, health checks)
```

Every class, record and interface has its own file, in a folder named after its kind:

```
TreeEditor.Core/
  Interfaces/       ITreeService, ITreeRepository
  Services/         TreeService (business layer)
  Repositories/     EfTreeRepository (data access layer)
  Models/           NodeDto, ChangeSet, NewNode, ValueChange, ApplyResult, CachedNode, SampleItem
  Entities/         TreeNode (EF entity)
  Data/             TreeDbContext, SampleData
  Cache/            TreeCache
  Constants/        TreeConstants
  Extensions/       AddTreeEditorCore (DI registration)
TreeEditor.Web/
  Models/           DbTreeItem, EditState, PendingSummary
  Services/         DbTreeBrowser (state and lazy loading of the database tree)
  Extensions/       ServiceDefaultsExtensions (AddServiceDefaults, MapDefaultEndpoints)
  Components/       each .razor holds markup only; its logic is in the .razor.cs next to it
```

## Layers

Each layer only calls the one below it:

1. **Presentation** (`TreeEditor.Web`, `TreeCache`) calls `ITreeService`.
2. **Business** (`TreeService`) validates values, then calls `ITreeRepository`.
3. **Data access** (`EfTreeRepository`) is the only code that touches `TreeDbContext`.

## Database schema

One table:

```sql
CREATE TABLE Nodes (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    ParentId  INTEGER NULL REFERENCES Nodes(Id),  -- NULL = root; never changes
    Value     TEXT    NOT NULL,                   -- max 200 chars
    Path      TEXT    NOT NULL,                   -- ancestor ids, e.g. '/1/3/7/'
    IsDeleted INTEGER NOT NULL                    -- soft-delete flag
);
CREATE INDEX IX_Nodes_ParentId ON Nodes(ParentId);
CREATE INDEX IX_Nodes_Path     ON Nodes(Path);
```

- **`ParentId`** is the direct parent, and the tree is built from it. The lazy DB tree reads one level at a time (`WHERE ParentId = @id`).
- **`Path`** stores the ids of all ancestors, root first. A root's path is `/`. It exists for the cache:
  - A cached element carries its ancestor ids, so the cache can recognise that it sits under a deleted ancestor, even when the elements in between were never loaded, and without querying the database.
  - The same column lets "delete X and all its descendants" be a single statement in plain EF, whatever the depth: every row whose path starts with X's path plus X's id.
  - The assignment says parent–child relationships can't change, so a path never has to be rewritten.
- **Soft delete (`IsDeleted`)**: the assignment asks for deleted elements to be *marked* as deleted. So rows stay in the table and show struck through in both trees, and they can't be edited.
- **Deterministic sample data**: 21 nodes, 6 levels deep (Root → … → `Node 2.1.1.1.1`). Ids are assigned breadth-first, so the same ids come back after every Reset. Reset also clears `sqlite_sequence`, so new elements get ids that follow straight on from the sample data.

## Implementation decisions

### The cache (`TreeEditor.Core/Cache/TreeCache.cs`)


- **Database access only on Load and Apply.** `TreeCache` depends on `ITreeService`, but only `LoadAsync` and `ApplyAsync` use it. `AddChild`, `SetValue`, `Delete` and the hierarchy queries are synchronous and run purely in memory.
- **Identity.** Every cached element has a `Guid Key`. Elements created in the cache have no database id until Apply, so they are identified by this key and reference their parent by `ParentKey`.
- **Automatic hierarchy.** An element is shown under its parent whenever that parent is in the cache. Otherwise it is shown at the top level. The hierarchy is recomputed on every render, so load order doesn't matter: load a grandchild, then its parent, and the grandchild moves under the parent.
- **Delete.** Deleting an element marks it and every cached descendant as *delete pending*. Descendants are found by the ancestor ids from `Path` and by the in-cache parent chain (for new elements). Pending-deleted elements can't be edited or given children. An element loaded later under a pending-deleted ancestor is marked deleted too.
- **Apply** builds a `ChangeSet`: new elements (parents first), value edits, and deletions. The repository applies it in **one transaction**, in this order:
  1. Inserts. A child of another new element gets its parent's freshly generated id.
  2. Value updates. Updates to rows that are already deleted are skipped.
  3. Subtree soft-deletes.

  New elements that were deleted before Apply are never written. Edits to deleted elements are dropped.

  After Apply, the cache re-reads all of its elements from the database. That makes deletions of never-loaded descendants visible, and so are changes made by other sessions.
- **Reloading** an element that has no pending changes refreshes it from the database. An element with pending changes is left untouched.

### Blazor / hosting

- **Interactive Server rendering.** `TreeCache` is a *scoped* service, so each browser tab (circuit) has its own cache in server memory. The cache is "local" to the user session, and a page refresh starts with an empty cache.
- **`IDbContextFactory`**: each repository call uses a short-lived `DbContext`. That is the recommended pattern for Blazor Server, where a circuit lives much longer than a request.
- **The DB tree reads through the service directly.** It's a view of the database, not part of the cache. After Apply or Reset it reloads and keeps the expanded branches open.
- **SQLite via Aspire.** `CommunityToolkit.Aspire.Hosting.Sqlite` adds SQLite as an Aspire resource and passes the `treedb` connection string to the web project, so the whole solution runs without Docker.
- **Schema creation.** `EnsureCreated` plus seeding at startup keeps the assignment self-contained.
