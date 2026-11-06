namespace ONA.Graph.Domain.Exceptions;

public sealed class InvalidConnectionWeightException : DomainException
{
    public double AttemptedWeight { get; }

    public InvalidConnectionWeightException(double attemptedWeight)
        : base(
            "connection.invalid_weight",
            $"Connection weight must be a positive finite number; received {attemptedWeight}.")
    {
        AttemptedWeight = attemptedWeight;
    }
}
