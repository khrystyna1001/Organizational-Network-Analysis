using ONA.Analysis.Application.Abstractions.Messaging;
using ONA.Analysis.Application.DTOs;
using ONA.Analysis.Application.Services;

namespace ONA.Analysis.Application.Features.AnalysisRuns.Commands;

/// <summary>
/// CREATE – RunGraphAnalysis. Reads the published graph of the board through the snapshot port,
/// calculates metrics, stores a run bound to that GraphVersionId and publishes AnalysisCompleted.
/// </summary>
public sealed record StartAnalysisRunCommand(string BoardId) : ICommand<AnalysisRunDto>;

public sealed class StartAnalysisRunCommandHandler(AnalysisRunService service)
    : ICommandHandler<StartAnalysisRunCommand, AnalysisRunDto>
{
    public async Task<AnalysisRunDto> HandleAsync(
        StartAnalysisRunCommand command,
        CancellationToken cancellationToken = default) =>
        AnalysisRunDto.FromDomain(await service.StartAsync(command.BoardId, cancellationToken));
}
