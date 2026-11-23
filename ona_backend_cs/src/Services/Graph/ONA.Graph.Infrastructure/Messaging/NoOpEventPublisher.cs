using ONA.Graph.Application.Events;
using ONA.Graph.Application.Ports;

namespace ONA.Graph.Infrastructure.Messaging;

/// <summary>Used when no Kafka broker is configured (Kafka:BootstrapServers is empty).</summary>
internal sealed class NoOpEventPublisher : IGraphEventPublisher
{
    public Task PublishGraphVersionPublishedAsync(
        GraphVersionPublished @event,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
