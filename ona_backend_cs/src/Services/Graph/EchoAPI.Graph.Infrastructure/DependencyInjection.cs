using EchoAPI.Graph.Application.Abstractions;
using EchoAPI.Graph.Application.Services;
using EchoAPI.Graph.Infrastructure.Persistence;
using EchoAPI.Graph.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EchoAPI.Graph.Infrastructure;

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
