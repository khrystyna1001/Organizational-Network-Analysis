using ONA.Graph.Domain.Aggregates;
using System.Collections.Concurrent;

namespace ONA.Graph.Infrastructure.Persistence;

internal sealed class InMemoryGraphStore
{
    internal ConcurrentDictionary<string, Board> Boards { get; } = new(StringComparer.Ordinal);
}
