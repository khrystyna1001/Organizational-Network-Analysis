namespace ONA.Identity.Application.Abstractions.Messaging;

/// <summary>Marker for a state-changing use case (CQRS write path).</summary>
public interface ICommand<out TResponse>;
