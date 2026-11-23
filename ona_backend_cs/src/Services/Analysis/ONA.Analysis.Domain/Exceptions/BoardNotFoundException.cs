namespace ONA.Analysis.Domain.Exceptions;

public sealed class BoardNotFoundException : DomainException
{
    public string BoardId { get; }

    public BoardNotFoundException(string boardId)
        : base("analysis_run.board_not_found", $"Board '{boardId}' was not found.")
    {
        BoardId = boardId;
    }
}
