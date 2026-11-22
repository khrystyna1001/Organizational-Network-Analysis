using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Features.Boards.Commands;
using ONA.Graph.Application.Features.Boards.Queries;
using ONA.Graph.Application.Ports;
using ONA.Graph.Application.Services;
using ONA.Graph.Infrastructure.Acl;
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

        services.AddGraphApplication();
        return services;
    }

    /// <summary>Registers application services and CQRS handlers explicitly (no mediator library).</summary>
    public static IServiceCollection AddGraphApplication(this IServiceCollection services)
    {
        services.AddScoped<IGraphService, GraphService>();

        services.AddScoped<ICommandHandler<CreateBoardCommand, BoardDto>, CreateBoardCommandHandler>();
        services.AddScoped<ICommandHandler<RenameBoardCommand, BoardDto>, RenameBoardCommandHandler>();
        services.AddScoped<ICommandHandler<BuildBoardGraphCommand, BoardDto>, BuildBoardGraphCommandHandler>();
        services.AddScoped<ICommandHandler<PublishBoardCommand, BoardDto>, PublishBoardCommandHandler>();
        services.AddScoped<ICommandHandler<ArchiveBoardCommand, BoardDto>, ArchiveBoardCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteBoardCommand, bool>, DeleteBoardCommandHandler>();
        services.AddScoped<ICommandHandler<ImportBoardFromHrisCommand, ImportedBoardDto>, ImportBoardFromHrisCommandHandler>();

        services.AddScoped<IQueryHandler<GetBoardByIdQuery, BoardDto?>, GetBoardByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetAllBoardsQuery, IReadOnlyList<BoardDto>>, GetAllBoardsQueryHandler>();

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
