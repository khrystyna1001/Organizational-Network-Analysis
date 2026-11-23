using ONA.Analysis.Application.Events;

namespace ONA.Analysis.Application.Ports;

/// <summary>
/// Outgoing events of the Analysis context. Called AFTER the run is saved (at-least-once, no outbox).
/// Implementations log and swallow transport failures; callers additionally never let a failure fail the run.
/// </summary>
public interface IAnalysisEventPublisher
{
    Task PublishAnalysisCompletedAsync(AnalysisCompleted @event, CancellationToken cancellationToken = default);
}
