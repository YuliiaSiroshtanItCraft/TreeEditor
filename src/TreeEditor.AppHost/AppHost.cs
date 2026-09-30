var builder = DistributedApplication.CreateBuilder(args);

var treeDb = builder.AddSqlite(
    "treedb",
    databasePath: Path.Combine(builder.AppHostDirectory, "data"),
    databaseFileName: "tree.db");

builder.AddProject<Projects.TreeEditor_Web>("web")
    .WithReference(treeDb)
    .WaitFor(treeDb);

builder.Build().Run();
