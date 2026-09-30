using Microsoft.EntityFrameworkCore;
using TreeEditor.Core.Data;
using TreeEditor.Core.Extensions;
using TreeEditor.Core.Interfaces;
using TreeEditor.Web.Extensions;
using TreeEditor.Web.Components;
using TreeEditor.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("treedb") ?? "Data Source=tree.db";
builder.Services.AddDbContextFactory<TreeDbContext>(o => o.UseSqlite(connectionString));

builder.Services.AddTreeEditorCore();
builder.Services.AddScoped<DbTreeBrowser>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

await app.Services.GetRequiredService<ITreeService>().InitializeAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
