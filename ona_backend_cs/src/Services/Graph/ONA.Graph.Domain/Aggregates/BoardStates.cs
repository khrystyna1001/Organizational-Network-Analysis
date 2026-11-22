using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Domain.Aggregates;

/// <summary>
/// GoF State pattern for the Board lifecycle. Each state permits only the transitions
/// drawn in docs/architecture/stateDiagram-analysis.md and rejects everything else
/// with <see cref="InvalidGraphStateTransitionException"/>.
/// </summary>
internal interface IBoardState
{
    BoardLifecycle Lifecycle { get; }

    bool AllowsGraphMutation { get; }

    bool AllowsDeletion { get; }

    BoardLifecycle Build(Board board);

    BoardLifecycle Publish(Board board);

    BoardLifecycle Archive(Board board);
}

internal static class BoardStates
{
    private static readonly IBoardState Draft = new DraftState();
    private static readonly IBoardState Built = new BuiltState();
    private static readonly IBoardState Published = new PublishedState();
    private static readonly IBoardState Archived = new ArchivedState();

    public static IBoardState For(BoardLifecycle lifecycle) =>
        lifecycle switch
        {
            BoardLifecycle.Draft => Draft,
            BoardLifecycle.Built => Built,
            BoardLifecycle.Published => Published,
            BoardLifecycle.Archived => Archived,
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycle), lifecycle, "Unknown board lifecycle.")
        };

    internal static InvalidGraphStateTransitionException Reject(Board board, string command) =>
        new(board.Id, board.Lifecycle, command);
}

internal sealed class DraftState : IBoardState
{
    public BoardLifecycle Lifecycle => BoardLifecycle.Draft;
    public bool AllowsGraphMutation => true;
    public bool AllowsDeletion => true;

    public BoardLifecycle Build(Board board)
    {
        if (board.Nodes.Count == 0)
        {
            throw new GraphNotReadyForPublicationException(board.Id, "the graph must contain at least one node.");
        }

        return BoardLifecycle.Built;
    }

    public BoardLifecycle Publish(Board board) => throw BoardStates.Reject(board, "PublishGraphVersion");

    public BoardLifecycle Archive(Board board) => throw BoardStates.Reject(board, "ArchiveBoard");
}

internal sealed class BuiltState : IBoardState
{
    public BoardLifecycle Lifecycle => BoardLifecycle.Built;
    public bool AllowsGraphMutation => true;
    public bool AllowsDeletion => true;

    public BoardLifecycle Build(Board board) => throw BoardStates.Reject(board, "BuildNetworkGraph");

    public BoardLifecycle Publish(Board board)
    {
        if (board.Nodes.Count == 0)
        {
            throw new GraphNotReadyForPublicationException(board.Id, "the graph must contain at least one node.");
        }

        return BoardLifecycle.Published;
    }

    public BoardLifecycle Archive(Board board) => throw BoardStates.Reject(board, "ArchiveBoard");
}

internal sealed class PublishedState : IBoardState
{
    public BoardLifecycle Lifecycle => BoardLifecycle.Published;
    public bool AllowsGraphMutation => false;
    public bool AllowsDeletion => false;

    public BoardLifecycle Build(Board board) => throw BoardStates.Reject(board, "BuildNetworkGraph");

    public BoardLifecycle Publish(Board board) => throw BoardStates.Reject(board, "PublishGraphVersion");

    public BoardLifecycle Archive(Board board) => BoardLifecycle.Archived;
}

internal sealed class ArchivedState : IBoardState
{
    public BoardLifecycle Lifecycle => BoardLifecycle.Archived;
    public bool AllowsGraphMutation => false;
    public bool AllowsDeletion => true;

    public BoardLifecycle Build(Board board) => throw BoardStates.Reject(board, "BuildNetworkGraph");

    public BoardLifecycle Publish(Board board) => throw BoardStates.Reject(board, "PublishGraphVersion");

    public BoardLifecycle Archive(Board board) => throw BoardStates.Reject(board, "ArchiveBoard");
}
