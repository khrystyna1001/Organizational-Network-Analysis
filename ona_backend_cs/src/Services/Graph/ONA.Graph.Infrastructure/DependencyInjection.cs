using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.Services;
using ONA.Graph.Infrastructure.Persistence;
using ONA.Graph.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ONA.Graph.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IGraphService, GraphService>();
        services.AddSingleton<InMemoryGraphStore>();
        services.AddSingleton<IBoardRepository, InMemoryBoardRepository>();
        return services;
    }
}
