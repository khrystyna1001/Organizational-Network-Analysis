using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Features.Boards.Commands;

/// <summary>
/// UPDATE (domain command) – BuildNetworkGraph from docs/architecture/stateDiagram-analysis.md.
/// Draft -> Built. Throws GraphNotReadyForPublicationException for an empty graph.
/// Forbidden transitions throw InvalidGraphStateTransitionException and nothing is saved.
/// </summary>
public sealed record BuildBoardGraphCommand(string BoardId) : ICommand<BoardDto>;

public sealed class BuildBoardGraphCommandHandler(IBoardRepository repository)
    : ICommandHandler<BuildBoardGraphCommand, BoardDto>
{
    public async Task<BoardDto> HandleAsync(
        BuildBoardGraphCommand command,
        CancellationToken cancellationToken = default)
    {
        // Tracked load: the change made by the aggregate is persisted by SaveAsync.
        var board = await repository.GetByIdAsync(command.BoardId, cancellationToken)
            ?? throw new BoardNotFoundException(command.BoardId);

        board.BuildNetworkGraph();

        await repository.SaveAsync(board, cancellationToken);
        return BoardDto.FromDomain(board);
    }
}
