namespace ONA.Graph.Application.Abstractions.Messaging;

/// <summary>Marker for a read-only use case (CQRS read path). Queries never mutate state.</summary>
public interface IQuery<out TResponse>;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
