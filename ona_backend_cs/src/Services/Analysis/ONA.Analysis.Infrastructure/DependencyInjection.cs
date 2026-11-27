using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ONA.Analysis.Application.Abstractions;
using ONA.Analysis.Application.DTOs;
using ONA.Analysis.Application.Features.AnalysisRuns.Commands;
using ONA.Analysis.Application.Features.AnalysisRuns.Queries;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Application.Services;
using ONA.Analysis.Infrastructure.Acl;
using ONA.Analysis.Infrastructure.Behaviors;
using ONA.Analysis.Infrastructure.Messaging;
using ONA.Analysis.Infrastructure.Persistence;
using ONA.Analysis.Infrastructure.Persistence.Repositories;

namespace ONA.Analysis.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionString,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        services.AddDbContext<AnalysisDbContext>((provider, options) =>
            options.UseMySQL(connectionString(provider)));
        services.AddScoped<IAnalysisRunRepository, MySqlAnalysisRunRepository>();

        // Anti-Corruption Layer to the Graph context (published snapshot over HTTP).
        services.AddSingleton<GraphSnapshotTranslator>();
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ServiceTokenFactory>();
        services.AddTransient<BearerForwardingHandler>();
        services.AddHttpClient<GraphHttpAdapter>((provider, client) =>
        {
            var baseUrl = provider.GetRequiredService<IConfiguration>()["Services:Graph:BaseUrl"]
                ?? "http://localhost:5056";
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(15);
        }).AddHttpMessageHandler<BearerForwardingHandler>();
        services.AddScoped<IGraphSnapshotPort, GraphSnapshotAcl>();

        AddKafka(services, configuration?["Kafka:BootstrapServers"]);

        return services.AddAnalysisApplication();
    }

    /// <summary>Kafka is optional: without Kafka:BootstrapServers events are dropped and nothing is consumed.</summary>
    private static void AddKafka(IServiceCollection services, string? bootstrapServers)
    {
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            services.AddSingleton<IAnalysisEventPublisher, NoOpEventPublisher>();
            return;
        }

        services.AddSingleton<IAnalysisEventPublisher>(provider =>
            new KafkaEventPublisher(bootstrapServers, provider.GetRequiredService<ILogger<KafkaEventPublisher>>()));
        services.AddSingleton<GraphVersionPublishedProcessor>();
        services.AddSingleton<IHostedService>(provider => new GraphVersionPublishedConsumer(
            bootstrapServers,
            provider.GetRequiredService<GraphVersionPublishedProcessor>(),
            provider.GetRequiredService<ILogger<GraphVersionPublishedConsumer>>()));
    }

    /// <summary>
    /// Registers application services, discovers CQRS handlers and notification handlers of the Application
    /// assembly via MediatR and adds the logging pipeline behavior. The Presentation assembly registers
    /// its own notification handlers (SignalR push) with a second <c>AddMediatR</c> call.
    /// </summary>
    public static IServiceCollection AddAnalysisApplication(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddScoped<AnalysisRunService>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<IAnalysisRunRepository>();
            cfg.AddOpenBehavior(typeof(RequestLoggingBehavior<,>));
        });
        return services;
    }

    public static async Task InitializeAnalysisDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AnalysisDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
