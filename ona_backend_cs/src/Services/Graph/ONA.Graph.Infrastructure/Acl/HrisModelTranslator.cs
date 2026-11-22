using ONA.Graph.Application.Ports;
using ONA.Graph.Domain.Exceptions;
using ONA.Graph.Domain.ValueObjects;
using ONA.Graph.Infrastructure.Acl.ExternalContracts;

namespace ONA.Graph.Infrastructure.Acl;

/// <summary>
/// Pure translation HRIS -> Graph model (no I/O). Every rule below is one row of the
/// mapping table in docs/architecture/context-map.md and has a unit test in
/// tests/Graph/ONA.Graph.Infrastructure.Tests/Acl/HrisModelTranslatorTests.cs.
/// </summary>
internal sealed class HrisModelTranslator
{
    internal static readonly IReadOnlyList<string> RequiredColumns =
        ["EmpID", "FirstName", "LastName", "Title", "DepartmentType", "Supervisor"];

    /// <summary>The HRIS writes this literal for an empty cell.</summary>
    private const string HrisMissingValue = "NaN";

    /// <summary>Row 1 of the export is the header, so data row i is file row i + 2.</summary>
    private const int FirstDataRowNumber = 2;

    public OrganizationSnapshot ToOrganization(HRIS_EXPORT_DTO export)
    {
        ArgumentNullException.ThrowIfNull(export);

        EnsureRequiredColumns(export.COLUMNS);

        if (export.ROWS.Count == 0)
        {
            throw new InvalidBoardImportException("CSV must contain at least one employee row.");
        }

        var members = new List<OrganizationMember>(export.ROWS.Count);
        var employeeIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < export.ROWS.Count; index++)
        {
            var row = export.ROWS[index];
            var rowNumber = index + FirstDataRowNumber;

            var employeeId = Require(row.EmpID, rowNumber, "EmpID");
            var firstName = Require(row.FirstName, rowNumber, "FirstName");
            var lastName = Require(row.LastName, rowNumber, "LastName");
            var title = Require(row.Title, rowNumber, "Title");
            var department = Require(row.DepartmentType, rowNumber, "DepartmentType");

            if (!employeeIds.Add(employeeId))
            {
                throw new InvalidBoardImportException(
                    rowNumber,
                    "EmpID",
                    $"employee ID '{employeeId}' is duplicated.");
            }

            members.Add(new OrganizationMember(
                employeeId,
                new NodeName($"{firstName} {lastName}"),
                title,
                new DepartmentName(department),
                Clean(row.Supervisor)));
        }

        for (var index = 0; index < members.Count; index++)
        {
            var member = members[index];
            if (member.SupervisorEmployeeId is not { } supervisorId)
            {
                continue;
            }

            var rowNumber = index + FirstDataRowNumber;
            if (string.Equals(supervisorId, member.EmployeeId, StringComparison.Ordinal))
            {
                throw new InvalidBoardImportException(
                    rowNumber,
                    "Supervisor",
                    "an employee cannot supervise themself.");
            }

            if (!employeeIds.Contains(supervisorId))
            {
                throw new InvalidBoardImportException(
                    rowNumber,
                    "Supervisor",
                    $"employee ID '{supervisorId}' does not exist in this CSV.");
            }
        }

        return new OrganizationSnapshot(members);
    }

    private static void EnsureRequiredColumns(IReadOnlyList<string> columns)
    {
        var present = columns
            .Select(column => column.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RequiredColumns.Where(column => !present.Contains(column)).ToArray();

        if (missing.Length > 0)
        {
            throw new MissingImportColumnsException(missing);
        }
    }

    private static string Require(string? value, int rowNumber, string column) =>
        Clean(value) ?? throw new InvalidBoardImportException(rowNumber, column, "value is required.");

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ||
               string.Equals(trimmed, HrisMissingValue, StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmed;
    }
}
