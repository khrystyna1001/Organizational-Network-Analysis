using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.Services;
using ONA.Graph.Infrastructure.Persistence;
using ONA.Graph.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ONA.Graph.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddScoped<IGraphService, GraphService>();
        services.AddDbContext<GraphDbContext>(options => options.UseMySQL(connectionString));
        services.AddScoped<IBoardRepository, MySqlBoardRepository>();
        return services;
    }
}
