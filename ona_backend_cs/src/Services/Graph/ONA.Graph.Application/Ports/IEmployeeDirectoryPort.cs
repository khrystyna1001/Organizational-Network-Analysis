using ONA.Graph.Domain.ValueObjects;

namespace ONA.Graph.Application.Ports;

/// <summary>
/// Outbound port to the external HR information system (HRIS) employee export.
/// Specified in docs/architecture/context-map.md. The signature uses only our own model:
/// foreign HRIS structures never leave ONA.Graph.Infrastructure/Acl.
/// </summary>
public interface IEmployeeDirectoryPort
{
    /// <param name="mapping">Which CSV column plays which role; null = the HRIS column names.</param>
    Task<OrganizationSnapshot> ReadOrganizationAsync(
        Stream employeeExport,
        ColumnMapping? mapping = null,
        CancellationToken cancellationToken = default);

    /// <summary>Header, first rows and a suggested mapping, so the user can confirm or fix it before importing.</summary>
    Task<CsvPreview> PreviewAsync(
        Stream employeeExport,
        CancellationToken cancellationToken = default);
}

/// <summary>CSV header names for each role (null = not mapped). See "Any CSV: column mapping" in context-map.md.</summary>
public sealed record ColumnMapping(
    string? EmployeeId = null,
    string? FullName = null,
    string? FirstName = null,
    string? LastName = null,
    string? Title = null,
    string? Department = null,
    string? Supervisor = null)
{
    /// <summary>The built-in mapping of the HRIS employee export.</summary>
    public static ColumnMapping Hris { get; } = new("EmpID", null, "FirstName", "LastName", "Title", "DepartmentType", "Supervisor");
}

public sealed record CsvPreview(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> SampleRows,
    int RowCount,
    ColumnMapping SuggestedMapping);

/// <summary>An accepted organization: every supervisor reference points to a member of the snapshot.</summary>
public sealed record OrganizationSnapshot(IReadOnlyList<OrganizationMember> Members);

public sealed record OrganizationMember(
    string EmployeeId,
    NodeName Name,
    string Title,
    DepartmentName Department,
    string? SupervisorEmployeeId);

/// <summary>
/// Technical failure while talking to the HRIS (unreadable/malformed export).
/// Not a business rule, therefore not a DomainException.
/// </summary>
public sealed class HrisIntegrationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
