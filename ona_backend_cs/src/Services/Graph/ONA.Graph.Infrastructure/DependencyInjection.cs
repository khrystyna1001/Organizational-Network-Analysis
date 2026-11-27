using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Features.Boards.Commands;
using ONA.Graph.Application.Features.Boards.Queries;
using ONA.Graph.Application.Ports;
using ONA.Graph.Application.Services;
using ONA.Graph.Infrastructure.Acl;
using ONA.Graph.Infrastructure.Behaviors;
using ONA.Graph.Infrastructure.Messaging;
using ONA.Graph.Infrastructure.Persistence;
using ONA.Graph.Infrastructure.Persistence.Repositories;
using ONA.Graph.Infrastructure.Persistence.Seeders;

namespace ONA.Graph.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddInfrastructure(_ => connectionString);
    }

    /// <summary>
    /// Resolves the connection string when the DbContext is created, so configuration
    /// overrides applied at host build time (e.g. WebApplicationFactory in tests) are respected.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        services.AddDbContext<GraphDbContext>((provider, options) =>
            options.UseMySQL(connectionString(provider)));
        services.AddScoped<IBoardRepository, MySqlBoardRepository>();
        services.AddScoped<GraphDbContextSeeder>();

        // Anti-Corruption Layer to the external HRIS employee export.
        services.AddSingleton<HrisModelTranslator>();
        services.AddSingleton<CsvHrisAdapter>();
        services.AddScoped<IEmployeeDirectoryPort, HrisEmployeeDirectoryAcl>();

        // Kafka is optional: without Kafka:BootstrapServers events are dropped (no-op).
        services.AddSingleton<IGraphEventPublisher>(provider =>
        {
            var servers = provider.GetService<IConfiguration>()?["Kafka:BootstrapServers"];
            return string.IsNullOrWhiteSpace(servers)
                ? new NoOpEventPublisher()
                : new KafkaEventPublisher(servers, provider.GetRequiredService<ILogger<KafkaEventPublisher>>());
        });

        services.AddGraphApplication();
        return services;
    }

    /// <summary>
    /// Registers <see cref="IGraphService"/> (shared node/connection logic used by the node and connection
    /// handlers), discovers all CQRS handlers via MediatR and adds the logging pipeline behavior.
    /// </summary>
    public static IServiceCollection AddGraphApplication(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddScoped<IGraphService, GraphService>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<IBoardRepository>();
            cfg.AddOpenBehavior(typeof(RequestLoggingBehavior<,>));
        });

        return services;
    }

    /// <summary>Applies migrations and seeds starter data. Keeps DbContext/seeder internal.</summary>
    public static async Task InitializeGraphDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<GraphDbContextSeeder>();
        await seeder.SeedAsync();
    }
}
