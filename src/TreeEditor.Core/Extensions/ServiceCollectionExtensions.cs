using Microsoft.Extensions.DependencyInjection;
using TreeEditor.Core.Cache;
using TreeEditor.Core.Interfaces;
using TreeEditor.Core.Repositories;
using TreeEditor.Core.Services;

namespace TreeEditor.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTreeEditorCore(this IServiceCollection services)
    {
        services.AddSingleton<ITreeRepository, EfTreeRepository>();
        services.AddSingleton<ITreeService, TreeService>();
        services.AddScoped<TreeCache>();
        return services;
    }
}
