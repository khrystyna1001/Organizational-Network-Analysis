using ONA.Graph.Application.Events;
using ONA.Graph.Application.Ports;

namespace ONA.Graph.Application.Tests.Features;

/// <summary>Hand-written fake: records published events; can be told to fail.</summary>
internal sealed class RecordingEventPublisher : IGraphEventPublisher
{
    private readonly List<GraphVersionPublished> _events = [];

    public bool ThrowOnPublish { get; init; }

    public IReadOnlyList<GraphVersionPublished> Events => _events;

    public Task PublishGraphVersionPublishedAsync(
        GraphVersionPublished @event,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("broker down");
        }

        _events.Add(@event);
        return Task.CompletedTask;
    }
}
