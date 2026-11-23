namespace ONA.Identity.Application.Abstractions.Messaging;

/// <summary>Marker for a read-only use case (CQRS read path).</summary>
public interface IQuery<out TResponse>;
