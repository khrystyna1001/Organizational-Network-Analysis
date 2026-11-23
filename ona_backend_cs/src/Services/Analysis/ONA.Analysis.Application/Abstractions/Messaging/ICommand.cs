namespace ONA.Analysis.Application.Abstractions.Messaging;

/// <summary>Marker for a state-changing use case (CQRS write path).</summary>
public interface ICommand<out TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
