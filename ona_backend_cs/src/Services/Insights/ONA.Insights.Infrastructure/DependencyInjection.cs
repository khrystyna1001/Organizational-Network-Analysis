using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ONA.Insights.Application.Abstractions;
using ONA.Insights.Application.Features.Recommendations.Events;
using ONA.Insights.Application.Features.Recommendations.Queries;
using ONA.Insights.Application.Ports;
using ONA.Insights.Infrastructure.Acl;
using ONA.Insights.Infrastructure.Auth;
using ONA.Insights.Infrastructure.Behaviors;
using ONA.Insights.Infrastructure.Generation;
using ONA.Insights.Infrastructure.Messaging;
using ONA.Insights.Infrastructure.Persistence;
using ONA.Insights.Infrastructure.Persistence.Repositories;

namespace ONA.Insights.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultAnalysisBaseUrl = "http://localhost:5139";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddInfrastructure(_ => connectionString);
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        services.AddDbContext<InsightsDbContext>((provider, options) =>
            options.UseMySQL(connectionString(provider)));
        services.AddScoped<IRecommendationRepository, MySqlRecommendationRepository>();

        // ACL to the Analysis service (customer-supplier).
        services.AddSingleton<AnalysisRunTranslator>();
        services.AddHttpContextAccessor();
        services.AddSingleton(provider => JwtSettings.From(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<ServiceTokenFactory>(provider => new ServiceTokenFactory(
            provider.GetRequiredService<JwtSettings>(), provider.GetRequiredService<TimeProvider>()));
        services.AddTransient<BearerForwardingHandler>();
        services.AddHttpClient<HttpAnalysisAdapter>((provider, client) =>
        {
            var baseUrl = provider.GetRequiredService<IConfiguration>()["Services:Analysis:BaseUrl"];
            client.BaseAddress = new Uri(
                (string.IsNullOrWhiteSpace(baseUrl) ? DefaultAnalysisBaseUrl : baseUrl).TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(15);
        }).AddHttpMessageHandler<BearerForwardingHandler>();
        services.AddScoped<IAnalysisResultsPort, AnalysisResultsAcl>();

        // Deterministic generator; replace with an LLM adapter behind the same port.
        services.AddSingleton<IInsightGeneratorPort, RuleBasedInsightGenerator>();

        services.AddInsightsApplication();
        return services;
    }

    /// <summary>
    /// Registers application services, discovers CQRS handlers and notification handlers of the Application
    /// assembly via MediatR and adds the logging pipeline behavior. The Presentation assembly registers
    /// its own notification handlers (SignalR push) with a second <c>AddMediatR</c> call.
    /// </summary>
    public static IServiceCollection AddInsightsApplication(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<IRecommendationRepository>();
            cfg.AddOpenBehavior(typeof(RequestLoggingBehavior<,>));
        });

        services.AddScoped<AnalysisCompletedMessageHandler>();

        return services;
    }

    /// <summary>
    /// Registers the Kafka consumer for AnalysisCompleted only when <c>Kafka:BootstrapServers</c> is set.
    /// </summary>
    public static IServiceCollection AddAnalysisCompletedConsumer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var servers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(servers))
        {
            return services;
        }

        return services.AddHostedService(provider => new AnalysisCompletedConsumer(
            servers,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<AnalysisCompletedConsumer>>()));
    }

    public static async Task InitializeInsightsDatabaseAsync(this IServiceProvider services)
    {
        // Tests replace the repository with fakes and switch the database off.
        if (!services.GetRequiredService<IConfiguration>().GetValue("Database:AutoCreate", true))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InsightsDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
