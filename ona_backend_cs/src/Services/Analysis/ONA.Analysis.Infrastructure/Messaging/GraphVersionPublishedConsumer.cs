using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ONA.Analysis.Infrastructure.Messaging;

/// <summary>
/// Consumes ona.graph.version-published (group ona-analysis). Never crashes the host: if the broker
/// or topic is not available yet it logs and retries. Offsets are committed only after the message
/// was handled; technical failures seek back and retry after a pause.
/// </summary>
internal sealed class GraphVersionPublishedConsumer(
    string bootstrapServers,
    GraphVersionPublishedProcessor processor,
    ILogger<GraphVersionPublishedConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // never block host startup
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Kafka consumer stopped unexpectedly; restarting in {Delay}.", RetryDelay);
                await DelayAsync(stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = "ona-analysis",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = true,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
                logger.LogWarning("Kafka: {Reason} (fatal: {Fatal})", error.Reason, error.IsFatal))
            .Build();
        consumer.Subscribe(Topics.GraphVersionPublished);
        logger.LogInformation("Kafka consumer subscribed to {Topic} at {Servers}.", Topics.GraphVersionPublished, bootstrapServers);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try
            {
                result = consumer.Consume(TimeSpan.FromSeconds(1));
            }
            catch (ConsumeException exception) when (!exception.Error.IsFatal)
            {
                // e.g. topic does not exist yet – keep polling
                logger.LogWarning("Kafka consume problem: {Reason}", exception.Error.Reason);
                await DelayAsync(stoppingToken);
                continue;
            }

            if (result is null || result.IsPartitionEOF)
            {
                continue;
            }

            if (await processor.ProcessAsync(result.Message?.Value, stoppingToken))
            {
                consumer.Commit(result);
            }
            else
            {
                consumer.Seek(result.TopicPartitionOffset);
                await DelayAsync(stoppingToken);
            }
        }

        consumer.Close();
    }

    private static async Task DelayAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(RetryDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
