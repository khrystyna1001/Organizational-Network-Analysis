using ONA.Graph.Application.Events;

namespace ONA.Graph.Application.Ports;

/// <summary>Outgoing port for integration events (Kafka adapter in Infrastructure).</summary>
public interface IGraphEventPublisher
{
    Task PublishGraphVersionPublishedAsync(
        GraphVersionPublished @event,
        CancellationToken cancellationToken = default);
}
