using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Domain.Exceptions;

public sealed class PublishedGraphMutationNotAllowedException : DomainException
{
    public string BoardId { get; }
    public BoardLifecycle CurrentLifecycle { get; }

    public PublishedGraphMutationNotAllowedException(string boardId, BoardLifecycle currentLifecycle)
        : base(
            "board.published_graph_mutation_not_allowed",
            $"Board '{boardId}' is {currentLifecycle}; its nodes and connections cannot be changed directly.")
    {
        BoardId = boardId;
        CurrentLifecycle = currentLifecycle;
    }
}
