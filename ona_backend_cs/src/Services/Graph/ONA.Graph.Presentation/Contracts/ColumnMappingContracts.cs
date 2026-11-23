using ONA.Graph.Application.Ports;

namespace ONA.Graph.Presentation.Contracts;

/// <summary>JSON sent in the `mapping` form field: CSV header name per role; null or blank = not mapped.</summary>
public sealed record ColumnMappingRequest(
    string? EmployeeId,
    string? FullName,
    string? FirstName,
    string? LastName,
    string? Title,
    string? Department,
    string? Supervisor)
{
    public ColumnMapping ToDomain() => new(
        Clean(EmployeeId), Clean(FullName), Clean(FirstName), Clean(LastName),
        Clean(Title), Clean(Department), Clean(Supervisor));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
