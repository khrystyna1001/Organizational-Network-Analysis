using ONA.Graph.Domain.ValueObjects;

namespace ONA.Graph.Application.Ports;

/// <summary>
/// Outbound port to the external HR information system (HRIS) employee export.
/// Specified in docs/architecture/context-map.md. The signature uses only our own model:
/// foreign HRIS structures never leave ONA.Graph.Infrastructure/Acl.
/// </summary>
public interface IEmployeeDirectoryPort
{
    Task<OrganizationSnapshot> ReadOrganizationAsync(
        Stream employeeExport,
        CancellationToken cancellationToken = default);
}

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
