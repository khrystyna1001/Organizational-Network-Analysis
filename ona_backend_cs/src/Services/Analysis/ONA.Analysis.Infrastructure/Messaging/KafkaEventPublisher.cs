using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using ONA.Analysis.Application.Events;
using ONA.Analysis.Application.Ports;

namespace ONA.Analysis.Infrastructure.Messaging;

internal static class Topics
{
    public const string GraphVersionPublished = "ona.graph.version-published";
    public const string AnalysisCompleted = "ona.analysis.completed";
}

/// <summary>Publishes Analysis events to Kafka. Failures are logged and swallowed (at-least-once, best effort).</summary>
internal sealed class KafkaEventPublisher : IAnalysisEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(string bootstrapServers, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 5000,
            SocketTimeoutMs = 5000
        }).Build();
    }

    public async Task PublishAnalysisCompletedAsync(AnalysisCompleted @event, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PublishTimeout);
            await _producer.ProduceAsync(
                Topics.AnalysisCompleted,
                new Message<string, string>
                {
                    Key = @event.BoardId,
                    Value = JsonSerializer.Serialize(@event, Json)
                },
                timeout.Token);
        }
        catch (Exception exception) when (exception is KafkaException or OperationCanceledException)
        {
            _logger.LogWarning(exception,
                "Could not publish AnalysisCompleted for run {RunId}; consumers will not be notified.", @event.AnalysisRunId);
        }
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(2));
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Kafka producer flush failed on shutdown.");
        }

        _producer.Dispose();
    }
}

/// <summary>Used when Kafka:BootstrapServers is not configured.</summary>
internal sealed class NoOpEventPublisher : IAnalysisEventPublisher
{
    public Task PublishAnalysisCompletedAsync(AnalysisCompleted @event, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
