using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using ONA.Graph.Application.Events;
using ONA.Graph.Application.Ports;

namespace ONA.Graph.Infrastructure.Messaging;

/// <summary>Publishes integration events to Kafka (acks=all, idempotent producer).</summary>
internal sealed class KafkaEventPublisher : IGraphEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

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
            MessageTimeoutMs = 3000,
            SocketTimeoutMs = 5000,
            RequestTimeoutMs = 5000
        }).Build();
    }

    public async Task PublishGraphVersionPublishedAsync(
        GraphVersionPublished @event,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _producer.ProduceAsync(
                GraphVersionPublished.Topic,
                new Message<string, string>
                {
                    Key = @event.BoardId,
                    Value = JsonSerializer.Serialize(@event, Json)
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not publish {Topic} for board {BoardId}.",
                GraphVersionPublished.Topic,
                @event.BoardId);
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // Broker down at shutdown: never block or fail the host.
        }

        _producer.Dispose();
    }
}
