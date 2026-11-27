using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ONA.Insights.Application.Events;
using ONA.Insights.Application.Features.Recommendations.Events;

namespace ONA.Insights.Infrastructure.Messaging;

/// <summary>
/// Kafka consumer for <c>ona.analysis.completed</c> (group <c>ona-insights</c>). Manual commit after handling.
/// Starts fine without a broker: it keeps retrying and never crashes the host.
/// </summary>
internal sealed class AnalysisCompletedConsumer(
    string bootstrapServers,
    IServiceScopeFactory scopes,
    ILogger<AnalysisCompletedConsumer> logger) : BackgroundService
{
    public const string GroupId = "ona-insights";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Kafka consumer stopped; retrying in {Delay}.", RetryDelay);
                await Delay(stoppingToken);
            }
        }
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => logger.LogWarning("Kafka: {Reason}", error.Reason))
            .Build();
        consumer.Subscribe(AnalysisCompletedMessage.Topic);
        logger.LogInformation("Listening to {Topic} on {Servers}.", AnalysisCompletedMessage.Topic, bootstrapServers);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try
            {
                result = consumer.Consume(TimeSpan.FromSeconds(1));
            }
            catch (ConsumeException exception) when (!exception.Error.IsFatal)
            {
                logger.LogWarning("Kafka consume problem: {Reason}", exception.Error.Reason);
                await Delay(stoppingToken);
                continue;
            }

            if (result is null || result.IsPartitionEOF)
            {
                continue;
            }

            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<AnalysisCompletedMessageHandler>();
            var outcome = await handler.Handle(result.Message.Value, stoppingToken);

            switch (outcome.Outcome)
            {
                case MessageOutcome.Handled:
                    consumer.Commit(result);
                    break;
                case MessageOutcome.Skipped:
                    logger.LogWarning("Skipped AnalysisCompleted at {Offset}: {Reason}", result.TopicPartitionOffset, outcome.Reason);
                    consumer.Commit(result);
                    break;
                default:
                    logger.LogWarning("Will retry AnalysisCompleted at {Offset}: {Reason}", result.TopicPartitionOffset, outcome.Reason);
                    consumer.Seek(result.TopicPartitionOffset); // not committed: re-read after back-off
                    await Delay(stoppingToken);
                    break;
            }
        }
    }

    private static async Task Delay(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RetryDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
