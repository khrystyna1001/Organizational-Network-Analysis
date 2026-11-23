namespace ONA.Analysis.Domain.Exceptions;

public sealed class InvalidAnalysisRunStateTransitionException : DomainException
{
    public InvalidAnalysisRunStateTransitionException(string action, string status)
        : base("analysis_run.invalid_state_transition", $"Cannot {action} an analysis run that is {status}.")
    {
    }
}
